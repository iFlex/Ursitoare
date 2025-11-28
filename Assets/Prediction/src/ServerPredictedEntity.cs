using Assets.Scripts.Systems.Events;
using Prediction.data;
using Prediction.utils;
using UnityEngine;

namespace Prediction
{
    //TODO: unit test
    //TODO: document in readme
    public class ServerPredictedEntity : AbstractPredictedEntity
    {
        public GameObject gameObject;
        private PhysicsStateRecord serverStateBfr = new PhysicsStateRecord();
        private uint tickId;
        
        private uint _waitTicksBeforeSimStart;
        private uint waitTicksBeforeSimStart;
        
        TickIndexedBuffer<PredictionInputRecord> inputQueue;
        
        public ServerPredictedEntity(int bufferSize, Rigidbody rb, GameObject visuals, PredictableControllableComponent[] controllablePredictionContributors, PredictableComponent[] predictionContributors) : base(rb, visuals, controllablePredictionContributors, predictionContributors)
        {
            //TODO: configurable how much to wait before sim start...
            _waitTicksBeforeSimStart = 0;
            waitTicksBeforeSimStart = _waitTicksBeforeSimStart;
            
            inputQueue = new TickIndexedBuffer<PredictionInputRecord>(bufferSize);
            inputQueue.emptyValue = null;
        }

        public PhysicsStateRecord ServerSimulationTick()
        {
            PredictionInputRecord nextInput = TakeNextInput();
            if (nextInput != null)
            {
                //TODO: validate input, should happen in LoadInput
                LoadInput(nextInput);
            }
            ApplyForces();
            Tick();
            PopulatePhysicsStateRecord(GetTickId(), serverStateBfr);
            return serverStateBfr;
        }

        public void BufferClientTick(uint clientTickId, PredictionInputRecord inputRecord)
        {
            if (inputQueue.GetFill() == 0)
            {
                firstTickArrived.Dispatch(true);
            }

            if (clientTickId > tickId)
            {
                inputQueue.Add(clientTickId, inputRecord);
            }
            else
            {
                //TODO: notify late update dropped
            }
        }
        
        public bool ValidateState(uint tickId, PredictionInputRecord input)
        {
            throw new System.NotImplementedException();
        }
        
        public void ResetClientState()
        {
            //NOTE: use this when changing the controller of the plane.
            tickId = 0;
            waitTicksBeforeSimStart = _waitTicksBeforeSimStart;
            inputQueue.Clear();
        }

        public void Tick()
        {
            if (waitTicksBeforeSimStart > 0)
            {
                waitTicksBeforeSimStart--;
                if (waitTicksBeforeSimStart == 0)
                {
                    simulationStarted.Dispatch(true);
                }
            }
        }
        
        public uint GetTickId()
        {
            return tickId;
        }
        
        public PredictionInputRecord TakeNextInput()
        {
            uint newTick = inputQueue.GetNextTick(tickId);
            if (newTick > 0)
            {
                tickId = newTick;
                return inputQueue.Remove(tickId);
            }
            return inputQueue.emptyValue;
        }

        public SafeEventDispatcher<bool> simulationStarted = new();
        public SafeEventDispatcher<bool> firstTickArrived = new();
    }
}