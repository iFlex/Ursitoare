using System;
using Assets.Scripts.Systems.Events;
using Prediction.data;
using Prediction.Simulation;
using Prediction.utils;
using UnityEngine;

namespace Prediction
{
    //TODO: document in readme
    //TODO: visuals interpolation
    public class ClientPredictedEntity : AbstractPredictedEntity
    {
        //STATE TRACKING
        public uint maxAllowedAvgResimPerTick = 1;
        public GameObject gameObject;
        internal RingBuffer<PredictionInputRecord> localInputBuffer;
        internal RingBuffer<PhysicsStateRecord> localStateBuffer;
        
        //This is used exclusively in follower mode (predicted entity not controlled by user).
        private uint lastAppliedFollowerTick = 0;
        
        Func<uint, RingBuffer<PhysicsStateRecord>, TickIndexedBuffer<PhysicsStateRecord>, bool>
            resimulationEligibilityCheckHook;
        Func<PhysicsStateRecord, PhysicsStateRecord, bool> singleStateResimulationEligibilityHook;
        public PhysicsController physicsController;
        internal TickIndexedBuffer<PhysicsStateRecord> serverStateBuffer;

        internal uint totalTicks = 0;
        internal uint totalResimulationSteps = 0;
        internal uint totalResimulationStepsOverbudget = 0;
        internal uint totalResimulations = 0;
        public ClientPredictedEntity(int bufferSize, Rigidbody rb, GameObject visuals, PredictableControllableComponent[] controllablePredictionContributors, PredictableComponent[] predictionContributors) : base(rb, visuals, controllablePredictionContributors, predictionContributors)
        {
            rigidbody = rb;
            detachedVisualsIdentity = visuals;
            
            localInputBuffer = new RingBuffer<PredictionInputRecord>(bufferSize);
            localStateBuffer = new RingBuffer<PhysicsStateRecord>(bufferSize);
            this.controllablePredictionContributors = controllablePredictionContributors ?? Array.Empty<PredictableControllableComponent>();
            this.predictionContributors = predictionContributors ?? Array.Empty<PredictableComponent>();
            
            serverStateBuffer = new TickIndexedBuffer<PhysicsStateRecord>(bufferSize);
            
            for (int i = 0; i < controllablePredictionContributors.Length; i++)
            {
                totalFloatInputs += controllablePredictionContributors[i].GetFloatInputCount();
                totalBinaryInputs += controllablePredictionContributors[i].GetBinaryInputCount();
            }
            
            for (int i = 0; i < bufferSize; i++)
            {
                localInputBuffer.Add(new PredictionInputRecord(totalFloatInputs, totalBinaryInputs));
                localStateBuffer.Add(new PhysicsStateRecord());
            }
        }
        
        public void SetCustomEligibilityCheckHandler(Func<uint, RingBuffer<PhysicsStateRecord>, TickIndexedBuffer<PhysicsStateRecord>, bool> handler)
        {
            resimulationEligibilityCheckHook = handler;
            singleStateResimulationEligibilityHook = null;
        }

        public void SetSingleStateEligibilityCheckHandler(Func<PhysicsStateRecord, PhysicsStateRecord, bool> handler)
        {
            resimulationEligibilityCheckHook = _defaultResimulationEligibilityCheck;
            singleStateResimulationEligibilityHook = handler;
        }
        
        public PredictionInputRecord ClientSimulationTick(uint tickId)
        {
            if (tickId > 0)
            {
                //NOTE: no need to sample initial state, it's irellevant.
                //This samples the state AFTER the tick has run, hence the - 1.
                SamplePhysicsState(tickId - 1);
            }
            PredictionInputRecord inputRecord = SampleInput(tickId);
            LoadInput(inputRecord);
            ApplyForces();
            totalTicks++;
            if (totalResimulationStepsOverbudget > 0)
            {
                totalResimulationStepsOverbudget--;
            }
            return inputRecord;
        }

        PredictionInputRecord SampleInput(uint tickId)
        {
            //TODO: correctly convert tick to index!
            PredictionInputRecord inputData = localInputBuffer.Get((int)tickId);
            inputData.WriteReset();
            SampleInput(inputData);
            return inputData;
        }
        
        void SampleInput(PredictionInputRecord inputRecord)
        {
            for (int i = 0; i < controllablePredictionContributors.Length; ++i)
            {
                //NOTE: this samples the input of each component and stores it in the inputRecord as a side effect.
                controllablePredictionContributors[i].SampleInput(inputRecord);
            }
        }
        
        void SamplePhysicsState(uint tickId)
        {
            //TODO: correctly convert tick to index!
            PhysicsStateRecord stateData = localStateBuffer.Get((int)tickId);
            //NOTE: this samples the physics state of the predicted entity and stores it in the localStateBuffer as a side effect.
            PopulatePhysicsStateRecord(tickId, stateData);
        }

        bool AddServerState(uint lastAppliedTick, PhysicsStateRecord serverRecord)
        {
            //TODO: use lastAppliedTick to determine how old the update is and do stuff about it
            serverStateBuffer.Add(serverRecord.tickId, serverRecord);
            return serverRecord.tickId == serverStateBuffer.GetEndTick();
        }

        public void BufferFollowerServerTick(PhysicsStateRecord lastArrivedServerState)
        {
            //TODO: debug gate
            Debug.Log($"[ClientPredictedEntity][BufferFollowerServerTick] state:{lastArrivedServerState}");
            AddServerState(lastAppliedFollowerTick, lastArrivedServerState);
            SnapTo(serverStateBuffer.GetEnd());
            lastAppliedFollowerTick = serverStateBuffer.GetEndTick();
        }
        
        public void BufferServerTick(uint lastAppliedTick, PhysicsStateRecord latestServerState)
        {
            //Debug.Log($"[Prediction][BufferServerTick] tickId:{latestServerState.tickId}");
            if (AddServerState(lastAppliedTick, latestServerState))
            {
                //NOTE: somehow the server reports are in the future. Don't resimulate until we get there too
                if (lastAppliedTick < latestServerState.tickId)
                    return;
                
                if (resimulationEligibilityCheckHook(latestServerState.tickId, localStateBuffer, serverStateBuffer))
                {
                    if (CanResiumlate())
                    {
                        ResimulateFrom(latestServerState.tickId, lastAppliedTick, latestServerState);
                    }
                    else
                    {
                        SnapTo(latestServerState, true);
                    }
                }
                else
                {
                    predictionAcceptable.Dispatch(true);
                }
                //TODO: consider a decision where we need to pause simulation on client to let server catch up...
            }
        }

        bool _defaultResimulationEligibilityCheck(uint tickId, RingBuffer<PhysicsStateRecord> clientStates,
            TickIndexedBuffer<PhysicsStateRecord> serverStates)
        {
            //TODO: ensure correct conversion from uint tick to index
            PhysicsStateRecord localState = clientStates.Get((int)tickId);
            PhysicsStateRecord serverState = serverStates.Get(tickId);
            return singleStateResimulationEligibilityHook.Invoke(localState, serverState);
        }

        void SnapTo(PhysicsStateRecord serverState, bool fireSnapEvent = false)
        {
            rigidbody.position = serverState.position;
            rigidbody.rotation = serverState.rotation;
            rigidbody.linearVelocity = serverState.velocity;
            rigidbody.angularVelocity = serverState.angularVelocity;
            if (fireSnapEvent)
            {
                resimulationSkipped?.Dispatch(true);
            }
        }
        
        void ResimulateFrom(uint startTick, uint lastAppliedTick, PhysicsStateRecord startState)
        {
            physicsController.BeforeResimulate(this);
            resimulation.Dispatch(true);
            
            //Apply Server State
            SnapTo(startState);
            //TODO: check conversion to int
            PhysicsStateRecord record = localStateBuffer.Get((int) startTick);
            PopulatePhysicsStateRecord(startTick, record);
            
            uint index = startTick + 1;
            while (index <= lastAppliedTick)
            {
                resimulationStep.Dispatch(true);
                
                //TODO: correct conversion of tickId to index plz
                PredictionInputRecord inputData = localInputBuffer.Get((int) index);
                LoadInput(inputData);
                ApplyForces();
                physicsController.Resimulate(this);
                //TODO: check conversion to int
                record = localStateBuffer.Get((int) index);
                PopulatePhysicsStateRecord(index, record);

                index++;
                totalResimulationSteps++;
                totalResimulationStepsOverbudget++;
                resimulationStep.Dispatch(false);
            }
            
            totalResimulations++;
            resimulation.Dispatch(false);
            physicsController.AfterResimulate(this);
        }
        
        public uint GetTotalTicks()
        {
            return totalTicks;
        }
        
        public uint GetAverageResimPerTick()
        {
            return totalResimulationSteps / totalTicks;
        }
        
        bool CanResiumlate()
        {
            //return () < maxAllowedAvgResimPerTick;
            return totalResimulationStepsOverbudget == 0;
        }

        public uint GetResimulationOverbudget()
        {
            return totalResimulationStepsOverbudget;
        }

        public SafeEventDispatcher<bool> predictionAcceptable = new();
        public SafeEventDispatcher<bool> resimulation = new();
        public SafeEventDispatcher<bool> resimulationStep = new();
        public SafeEventDispatcher<bool> resimulationSkipped = new();
    }
}