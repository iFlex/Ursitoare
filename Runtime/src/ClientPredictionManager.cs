using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sector0.Events;
using Sector0.Ursitoare.Components;
using Sector0.Ursitoare.Data;
using Sector0.Ursitoare.Utils;
using UnityEngine;

namespace Sector0.Ursitoare
{
    public class ClientPredictionManager : PredictionManager
    {
        public static bool DO_SNAP = true;
        public static bool PREDICTION_ENABLED = true;
        public static bool PREDICT_FOLLOWERS = true;
        
        //FUDO: we might not need the RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD, it gives some good flexibilty for now.
        public static float RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = 0;
        public static float RESIMULATE_PRECISE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = 0;
        public static int MISSING_PACKETS_BUFFER_SIZE = 10;
        public static int RESIM_TICK_COUNT_BUFFER_SIZE = 30;
        public static bool TRACK_PACKET_LOSS = true;
        public static int CLIENT_RTT_MEASUREMENTS_BUFFER_SIZE = 20;
        
        public static ClientPredictionManager Instance;
        
        //NOTE: heartbeats are only sent when no predicted entity is controlled locally
        //         tickId
        protected Action<uint>                       unreliableClientHeartbeadSender;
        //         tickId, inputData
        protected Action<uint, uint, PredictionInputRecord>       unreliableClientStateSender;

        protected Dictionary<uint, ClientPredictedEntity> _clientEntities = new Dictionary<uint, ClientPredictedEntity>();
        protected HashSet<ClientPredictedEntity> localEntities = new HashSet<ClientPredictedEntity>();
        protected HashSet<uint> localEntityIds = new();
        
        private TickIndexedBuffer<bool> missedTicksBuffer = new TickIndexedBuffer<bool>(MISSING_PACKETS_BUFFER_SIZE);
        protected TickIndexedBuffer<TickRttRecord> clientTickRTTBuffer;
        //TODO: is this overkill? should we drop it?
        private TickIndexedBuffer<uint> tickResimCounter = new TickIndexedBuffer<uint>(RESIM_TICK_COUNT_BUFFER_SIZE);
        
        public bool resimulating { get; private set; } = false;

        public ClientPredictionManager(Action<uint> unreliableClientHeartbeadSender, Action<uint, uint, PredictionInputRecord> unreliableClientStateSender)
        {
            this.unreliableClientHeartbeadSender = unreliableClientHeartbeadSender;
            this.unreliableClientStateSender = unreliableClientStateSender;
            
            Validate();
            PhysicsController.Setup(false);
            Instance = this;
            
            clientTickRTTBuffer = new TickIndexedBuffer<TickRttRecord>(CLIENT_RTT_MEASUREMENTS_BUFFER_SIZE);
            clientTickRTTBuffer.emptyValue = new TickRttRecord();
            tickResimCounter.emptyValue = 0;
        }
        
        protected override void Validate()
		{
			if (unreliableClientHeartbeadSender == null)
            {
                throw new Exception(
                    "INVALID_CONFIG: isClient = true but no clientHeartbeatSender provided");
            }
            if (unreliableClientStateSender == null)
            {
                throw new Exception(
                    "INVALID_CONFIG: isClient = true but no clientStateSender provided");
            }
            if (INTERPOLATION_PROVIDER == null)
            {
                throw new Exception(
                    "INVALID_CONFIG: isClient = true but no Interpolation provider present");
            }

            if (SNAPSHOT_INSTANCE_RESIM_CHECKER == null)
            {
                throw new Exception(
                    "INVALID_CONFIG: isClient = true but no Snapshot Resimulation Checker provided");
            }	
            
            if (PhysicsController == null)
            {
                throw new Exception(
                    "INVALID_CONFIG: No Physics Controller provided.");
            }
		}

        protected override void PreSimTick()
        {
            ClientPreSimTick();
        }

        protected override void PostSimTick()
        {
            ClientPostSimTick();
        }

        public override void Tick()
        {
            //Uses latest update for each follower
            if (PREDICTION_ENABLED)
            {
                ClientResimulationCheckPass();
            }
            
            base.Tick();
            
            if (clientTickRTTBuffer.GetCapacity() > 0)
            {
                TickRttRecord tickRttRecord = new TickRttRecord();
                tickRttRecord.tickId = tickId;
                tickRttRecord.sentTime = Time.realtimeSinceStartupAsDouble;
                clientTickRTTBuffer.Add(tickId, tickRttRecord);

                if (TRACK_PACKET_LOSS && missedTicksBuffer.GetFill() > 0)
                {
                    onPacketLoss.Dispatch(missedTicksBuffer.GetFill());
                    //TODO: might be expensive
                    missedTicksBuffer.Clear();
                }
            }
        }

        public void AddPredictedEntity(ClientPredictedEntity entity)
        {
            if (entity == null)
                return;

            uint id = entity.id;
            if (DEBUG || LOG_EVENTS)
                Debug.Log($"[PredictionManager][AddPredictedEntity] CLIENT ({id})=>({entity})");
            
            _clientEntities[id] = entity;
            AddPredictedEntity(entity.gameObject);
            entity.SetSingleStateEligibilityCheckHandler(SNAPSHOT_INSTANCE_RESIM_CHECKER.Check);
            entity.SetFollowerSingleStateEligibilityCheckHandler(FOLLOWER_INSTANCE_RESIM_CHECKER.Check);
            
            bool alreadyLocallyControlled = IsControlledLocally(id);
            entity.SetControlledLocally(alreadyLocallyControlled);
            if (alreadyLocallyControlled)
            {
                localEntities.Add(entity);
            }
            
            if (autoTrackRigidbodies)
            {
                PhysicsController.Track(entity.rigidbody);
            }
        }

        public void RemovePredictedEntity(ClientPredictedEntity entity)
        {
            if (entity != null)
            {
                if (IsControlledLocally(entity.id))
                {
                    localEntities.Remove(entity);
                    entity.SetControlledLocally(false);
                }
                
                _clientEntities.Remove(entity.id);
                if (autoTrackRigidbodies)
                {
                    PhysicsController.Untrack(entity.rigidbody);
                }
                RemovePredictedEntity(entity.gameObject);
            }
        }
        
        //TODO: rename and consolidate with server SetLocalEntity -> SetOwnedLocally
        //TODO: unit test
        void SetLocalEntity(uint id)
        {
            if (DEBUG || DEBUG_OWNERSHIP || LOG_EVENTS)
                Debug.Log($"[PredictionManager][Ownership][SetLocalEntity] entityId:{id} controlledLocally:{IsControlledLocally(id)}");

            if (IsControlledLocally(id))
                return;
            localEntityIds.Add(id);
            
            var newLocalEntity = _clientEntities.GetValueOrDefault(id, null);
            if (DEBUG || DEBUG_OWNERSHIP || LOG_EVENTS)
                Debug.Log($"[PredictionManager][Ownership][SetLocalEntity] entityId:{id} entityInstance:{newLocalEntity}|");
            
            if (newLocalEntity != null)
            {
                //FUDO: consider moving the id fetching mechanic inside entity
                localEntities.Add(newLocalEntity);
                newLocalEntity.SetControlledLocally(true);
            }
        }
        
        //TODO: unit test
        void UnsetLocalEntity(uint id)
        {
            if (DEBUG || DEBUG_OWNERSHIP || LOG_EVENTS)
                Debug.Log($"[PredictionManager][Ownership][UnsetLocalEntity] entityId:{id} controlledLocally:{IsControlledLocally(id)}");
            
            if (IsControlledLocally(id))
            {
                localEntityIds.Remove(id);
                
                var remEnt = _clientEntities.GetValueOrDefault(id, null);
                if (DEBUG || DEBUG_OWNERSHIP || LOG_EVENTS)
                    Debug.Log($"[PredictionManager][Ownership][UnsetLocalEntity] entityId:{id} entityInstance:{remEnt}|");
                
                if (remEnt != null)
                {
                    localEntities.Remove(remEnt);
                    remEnt.SetControlledLocally(false);
                }
            }
        }

        public HashSet<ClientPredictedEntity> GetLocalEntities()
        {
            return localEntities;
        }
        
        public bool IsControlledLocally(ClientPredictedEntity entity)
        {
            return localEntities.Contains(entity);
        }
        
        public bool IsControlledLocally(uint id)
        {
            return localEntityIds.Contains(id);
        }

        public bool HasLocallyControlledEntities()
        {
            return localEntities.Count > 0;
        }

        public override uint GetServerTickId()
        {
            return reportedServerTickId;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsFollower(ClientPredictedEntity entity)
        {
            return !IsControlledLocally(entity);
        }
        
        int PredictionDecisionToInt(PredictionDecision decision)
        {
            switch (decision)
            {
                case PredictionDecision.NOOP: return 0;
                case PredictionDecision.SNAP: return 1;
                case PredictionDecision.RESIMULATE: return 2;
                case PredictionDecision.SIMULATION_FREEZE: return 3;
            }
            return 0;
        }

        PredictionDecision IntToPredictionDecision(int code)
        {
            switch (code)
            {
                case 1: return PredictionDecision.SNAP;
                case 2: return PredictionDecision.RESIMULATE;
                case 3: return PredictionDecision.SIMULATION_FREEZE;
            }
            return PredictionDecision.NOOP;
        }
        
        bool ShouldIgnoreResimulationDecision(ClientPredictedEntity entity)
        {
            return IsFollower(entity) && !entity.predictAsFollower;
        }

        float GetMinSqrDistToAllLocalEnts(ClientPredictedEntity ent)
        {
            float result = float.MaxValue;
            foreach (var localEnt in localEntities)
            {
                if (!localEnt.gameObject)
                {
                    if (LOG_ERRORS)
                        Debug.LogError($"[ClientPredictionManager][GetMinSqrDistToAllLocalEnts] NULL_PREDICTED_GAME_OBJECT. id:{localEnt.id}");
                    continue;
                }
                
                float intermediary = (ent.gameObject.transform.position - localEnt.gameObject.transform.position).sqrMagnitude;
                if (intermediary < result)
                {
                    result = intermediary;
                }
            }
            return result;
        }
        
        void ConfigureFollowerResimulation(ClientPredictedEntity ent)
        {
            if (!HasLocallyControlledEntities())
            {
                //No local entity - means we can just have everyone follow the server.
                ent.predictAsFollower = false;
                ent.usePreciseResimChecker = false;
                return;
            }
			
			if (RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD == 0)
            {
				ent.usePreciseResimChecker = false;
                ent.predictAsFollower = PREDICT_FOLLOWERS;
                return;
            }

            float sqrDistance = GetMinSqrDistToAllLocalEnts(ent);
			ent.usePreciseResimChecker = RESIMULATE_PRECISE_FOLLOWERS_SQR_DISTANCE_THRESHOLD > 0 && sqrDistance < RESIMULATE_PRECISE_FOLLOWERS_SQR_DISTANCE_THRESHOLD;
            ent.predictAsFollower = PREDICT_FOLLOWERS && sqrDistance < RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD;
        }
        
        //TODO: package private
        //TODO: ability to only resimulate for locally controlled object
        public PredictionDecision ComputePredictionDecision(out uint resimFromTickId)
        {
            int decisionCode = 0;
            resimFromTickId = uint.MaxValue;
            
            bool localAsksResimulation = false;
            int totalResimulationDecisions = 0;
            
            foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
            {
                if (!pair.Value.gameObject)
                {
                    if (LOG_ERRORS)
                        Debug.LogError($"[ClientPredictionManager][ComputePredictionDecision] NULL_PREDICTED_GAME_OBJECT. id:{pair.Key}");
                    continue;
                }
                
				ConfigureFollowerResimulation(pair.Value);
                PredictionDecision decision =
                    pair.Value.GetPredictionDecision(tickId, out uint localFromTick);
                if (ShouldIgnoreResimulationDecision(pair.Value))
                {
                    localFromTick = resimFromTickId;
                    decision = PredictionDecision.NOOP;
                }
                
                int crnt = PredictionDecisionToInt(decision);
                if (crnt > decisionCode)
                {
                    decisionCode = crnt;
                }
                if (decision == PredictionDecision.RESIMULATE)
                {
                    if (IsControlledLocally(pair.Value))
                    {
                        localAsksResimulation = true;
                    } 
                    resimFromTickId = Math.Min(resimFromTickId, localFromTick);
                }
            }

            PredictionDecision finalDecision = IntToPredictionDecision(decisionCode);
            if (finalDecision == PredictionDecision.SIMULATION_FREEZE)
            {
                totalTickFreezes++;
            }
            if (finalDecision == PredictionDecision.RESIMULATE)
            {
                totalResimulationDecisions++;
            }
            if (totalResimulationDecisions == 1 && localAsksResimulation)
            {
                totalResimulationsDueToAuthority++;
            }
            if (totalResimulationDecisions > 1 && localAsksResimulation)
            {
                totalResimulationsDueToBoth++;
            }
            if (!localAsksResimulation && totalResimulationDecisions > 0)
            {
                totalResimulationsDueToFollowers++;
            }
            return finalDecision;
        }
        
        
        //NOTE: returns if the simulation should be skipped for this tick.
        bool ClientResimulationCheckPass()
        {
            PredictionDecision decision = ComputePredictionDecision(out uint fromTick);
            if (decision == PredictionDecision.RESIMULATE)
            {
                shouldResimThisTick = true;
                if (!CanResiumlate(fromTick))
                {
                    decision = PredictionDecision.NOOP;
                    totalResimulationsSkipped++;
                }
            }

            switch (decision)
            {
                case PredictionDecision.RESIMULATE:
                    Resimulate(fromTick);
                    break;

                case PredictionDecision.SNAP:
                    Snap();
                    break;

                case PredictionDecision.SIMULATION_FREEZE:
                    SnapAllToServerAndReset();
                    return true;
            }
            return false;
        }
        
        void Resimulate(uint startTick)
        {
            if (tickId <= startTick)
            {
                //NOTE: this shouldn't be possible
                if (DEBUG || LOG_EVENTS)
                    Debug.Log($"[PredictionManager][Resimulate] tickId:{tickId} startTick:{startTick}. Are you misusing the system? startTick cannot be larger or equal than the current tickId");
                resimSkipNotEnoughHistory++;
                return;
            }
 
            resimulating = true;
            uint rewind = tickId - startTick;
            if (!PhysicsController.Rewind(rewind))
            {
                resimSkipNotEnoughHistory++;
                resimulating = false;
                return;
            }

            ticksSinceResim = 0;
            resimulatedThisTick = true;
            _resimTimer.Start();
            lastResimmedTicks = rewind;
            
            //TODO: decide what to do with these hooks...
            PhysicsController.BeforeResimulate(null);
            if (maxRewindDistance < rewind)
            {
                maxRewindDistance = rewind;
            }
            totalRewindDistance += rewind;
            resimulation.Dispatch(true);
            
            //Snap to correct state reported by server for all relevant objects
            foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
            {
                if (!pair.Value.gameObject)
                {
                    if (LOG_ERRORS)
                        Debug.LogError($"[ClientPredictionManager][Resimulate] NULL_PREDICTED_GAME_OBJECT. id:{pair.Key}");
                    continue;
                }
                
                if (correctWholeWorldWhenResimulating || pair.Value.GetPredictionDecision(tickId, out uint localFromTick) == PredictionDecision.RESIMULATE)
                {
                    pair.Value.SnapToServer(startTick);
                    pair.Value.PostResimulationStep(startTick);
                }
            }
            //All relevant bodies are now at the end of startTick
            MarkResimulatedTick(startTick);
            onPostResimTick.Dispatch(startTick);
            
            uint index = startTick + 1;
            while (index < tickId)
            {
                onPreResimTick.Dispatch(index);
                
                foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
                {
                    if (!pair.Value.gameObject)
                    {
                        if (LOG_ERRORS)
                            Debug.LogError($"[ClientPredictionManager][Resimulate] NULL_PREDICTED_GAME_OBJECT. id:{pair.Key}");
                        continue;
                    }
                    
                    //Note: this will run logic on local authority: fetchInput, loadInput, applyForces
                    pair.Value.PreResimulationStep(index);
                }
                PhysicsController.Resimulate(null);
                foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
                {
                    if (!pair.Value.gameObject)
                    {
                        if (LOG_ERRORS)
                            Debug.LogError($"[ClientPredictionManager][Resimulate] NULL_PREDICTED_GAME_OBJECT. id:{pair.Key}");
                        continue;
                    }
                    
                    if (resimUseAvailableServerTicks)
                    {
                        //NOTE: the resimulation step may have caused slightly different position and rotation, and also may have caused triggering of OnCollision events for those positions
                        pair.Value.SnapToServerIfExists(index);
                    }
                    pair.Value.PostResimulationStep(index);
                }
                
                MarkResimulatedTick(index);
                onPostResimTick.Dispatch(index);
                
                index++;
                totalResimulationSteps++;
            }
            
            totalResimulations++;
            resimulation.Dispatch(false);
            PhysicsController.AfterResimulate(null);
            resimulating = false;
            lastResimDuration = _resimTimer.Stop();
        }
        
        void MarkResimulatedTick(uint tid)
        {
            if (protectFromOversimulation)
            {
                tickResimCounter.Add(tid, tickResimCounter.Get(tid) + 1);
            }
        }
        
        //TODO: unit test this
        void Snap()
        {
            if (!DO_SNAP)
                return;
            
            foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
            {
                if (!pair.Value.gameObject)
                {
                    if (LOG_ERRORS)
                        Debug.LogError($"[ClientPredictionManager][Snap] NULL_PREDICTED_GAME_OBJECT. id:{pair.Key}");
                    continue;
                }
                
                if (pair.Value.GetPredictionDecision(tickId, out uint localFromTick) == PredictionDecision.SNAP)
                {
                    pair.Value.SnapToServer(localFromTick);
                }
            }
        }

        void SnapAllToServerAndReset()
        {
            foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
            {
                if (!pair.Value.gameObject)
                {
                    if (LOG_ERRORS)
                        Debug.LogError($"[ClientPredictionManager][SnapAllToServerAndReset] NULL_PREDICTED_GAME_OBJECT. id:{pair.Key}");
                    continue;
                }
                
                pair.Value.SnapToLatestServerAndReset();
            }
            
            if (DEBUG || LOG_EVENTS)
                Debug.Log($"[PredictionManager][SnapAllToServerAndReset] tickId:{tickId}");
            onSnapToServer.Dispatch(tickId);
        }
        
        bool CanResiumlate(uint tid)
        {
            return !protectFromOversimulation || (
                ( oversimProtectWithTickInterval && ticksSinceResim >= minTicksBetweenResims) || 
                (!oversimProtectWithTickInterval && tickResimCounter.Get(tid) < maxTickResimulationCount));
        }
        
        void ClientPreSimTick()
        {
            resimulating = false;
            //Uses latest update for each follower
            foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
            {
                if (!pair.Value.gameObject)
                {
                    if (LOG_ERRORS)
                        Debug.LogError($"[ClientPredictionManager][ClientPreSimTick] NULL_PREDICTED_GAME_OBJECT. id:{pair.Key}");
                    continue;
                }
                
                if (IsControlledLocally(pair.Key) && PREDICTION_ENABLED)
                {
                    if (DEBUG)
                        Debug.Log($"[PredictionManager][ClientPreSimTick] Client:{pair.Value} tick:{tickId}");
                    
                    PredictionInputRecord tickInputRecord = pair.Value.ClientSimulationTick(tickId);
                    try
                    {
                        if (DEBUG)
                            Debug.Log($"[PredictionManager][ClientPreSimTick][SEND] (id:{pair.Value.id}) tick:{tickId} data:{tickInputRecord} sndr:{unreliableClientStateSender}");
                        unreliableClientStateSender?.Invoke(tickId, pair.Value.id, tickInputRecord);
                    }
                    catch (Exception e)
                    {
                        clientSendErrors++;
                        EntityProcessingError err;
                        err.exception = e;
                        err.entityId = pair.Value.id;
                        onClientStateSendError.Dispatch(err);
                    }
                }
                else
                {
                    //Only run this on the pure client
                    if (!PREDICTION_ENABLED)
                    {
                        pair.Value.predictAsFollower = false;
                    }
                    pair.Value.ClientFollowerSimulationTick(tickId);
                }

                if (LOG_PRE_SIM_STATE)
                {
                    Debug.Log($"[CL][PRESIMULATION][DATA] i:{pair.Key} t:{tickId} p:{pair.Value.rigidbody.position.ToString("F10")} r:{pair.Value.rigidbody.rotation.ToString("F10")}");
                }
            }

            if (!HasLocallyControlledEntities())
            {
                SendSpectatorHeartbeat(tickId);
            }
        }
        
        void SendSpectatorHeartbeat(uint tid)
        {
            try
            {
                unreliableClientHeartbeadSender?.Invoke(tid);
            }
            catch (Exception e)
            {
                clientSendErrors++;
                //TODO: event?
            }   
        }

        void ClientPostSimTick()
        {
            resimulating = false;
            foreach (KeyValuePair<uint, ClientPredictedEntity> pair in _clientEntities)
            {
                if (!pair.Value.gameObject)
                {
                    if (LOG_ERRORS)
                        Debug.LogError($"[ClientPredictionManager][ClientPostSimTick] NULL_PREDICTED_GAME_OBJECT. id:{pair.Key}");
                    continue;
                }
                
                pair.Value.SamplePhysicsState(tickId);
            }
        }
        
        public void OnServerWorldStateReceived(WorldStateRecord wsr)
        {
            if (DEBUG)
                Debug.Log($"[PredictionManager][OnServerWorldStateReceived] WorldState:{wsr}]");
            
            reportedServerTickId = wsr.serverTickId;
            for (int i = 0; i < wsr.fill; i++)
            {
                wsr.states[i].tickId = wsr.tickId;
                OnServerStateReceived(wsr.entityIDs[i], wsr.states[i]);
            }
        }
        
        public void OnServerStateReceived(uint entityId, PhysicsStateRecord stateRecord)
        {
            if (DEBUG)
                Debug.Log($"[PredictionManager][OnServerStateReceived] entityId:{entityId} stateRecord:{stateRecord}");
            
            //TODO: ignore server updates from the "future". Why? because the server always uses client tickIds 
            //      corresponding to each connection. Which means it can never send a tickId that's larger than something the client's already ticked.
            ClientPredictedEntity entity = _clientEntities.GetValueOrDefault(entityId, null);
            if (entity != null)
            {
                entity.BufferServerTick(tickId, stateRecord);
                if (TRACK_PACKET_LOSS)
                {
                    if (lastAckTickId < stateRecord.tickId)
                    {
                        for (uint i = lastAckTickId + 1; i < stateRecord.tickId; i++)
                        {
                            missedTicksBuffer.Add(i, true);       
                        }
                        lastAckTickId = stateRecord.tickId;
                    }
                    missedTicksBuffer.Remove(stateRecord.tickId);
                }
            }
            
            if (clientLastReceivedTickId < stateRecord.tickId)
            {
                MeasureServerRecvIntervalDuration();
                clientLastReceivedTickId = stateRecord.tickId;
                
                //TODO: measure smallest RTT from batch of client tickIds instead of firing events for every RTT computed
                if (clientTickRTTBuffer.GetCapacity() > 0)
                {
                    TickRttRecord tickRttRecord = clientTickRTTBuffer.Remove(stateRecord.tickId);
                    TickRttDuration tickRttDuration = new TickRttDuration();
                    tickRttDuration.tickId = tickRttRecord.tickId;
                    tickRttDuration.duration = Time.realtimeSinceStartupAsDouble - tickRttRecord.sentTime;
                    lastClientTickRTT = tickRttDuration.duration;
                    onTickRttDuration.Dispatch(tickRttDuration);
                }
            }
        }
        
        public void OnEntityOwnershipChanged(uint entityId, bool owned)
        {
            if (DEBUG)
                Debug.Log($"[PredictionManager][OnEntityOwnershipChanged] entityId:{entityId} owned:{owned}");

            if (owned)
            {
                SetLocalEntity(entityId);
            }
            else
            {
                UnsetLocalEntity(entityId);
            }
        }
        
        void MeasureServerRecvIntervalDuration()
        {
            lastServerRecvIntervalDuration = _serverRecvTimer.Stop();
            //TODO: this can't work from here sadly - you could receive 2 or more updates at the same time
            _serverRecvTimer.Start();
        }
        
        public static uint GetServerTickDelay()
        {
            return (uint) Mathf.CeilToInt((float)(ROUND_TRIP_GETTER() / Time.fixedDeltaTime));
        }
        
        public uint GetAverageResimPerTick()
        {
            return totalResimulationSteps / tickId;
        }
        
        public override void Clear()
        {
            resimulating = false;
            base.Clear();
            localEntityIds.Clear();
            localEntities.Clear();
            _clientEntities.Clear();
            
            clientTickRTTBuffer.Clear();
            missedTicksBuffer.Clear();
            tickResimCounter.Clear();
        }
        
        public struct EntityProcessingError
        {
            public Exception exception;
            public uint entityId;
        }
        
        public struct TickRttRecord
        {
            public uint tickId;
            public double sentTime;

            public override bool Equals(object obj)
            {
                if (obj is TickRttRecord rec)
                {
                    return tickId == rec.tickId;
                }
                return false;
            }
        }
        
        public struct TickRttDuration
        {
            public uint tickId;
            public double duration;
        }
        
        public SafeEventDispatcher<uint> onPreResimTick = new();
        public SafeEventDispatcher<uint> onPostResimTick = new();
        public SafeEventDispatcher<TickRttDuration> onTickRttDuration = new();
        public SafeEventDispatcher<int> onPacketLoss = new();
        
        public SafeEventDispatcher<EntityProcessingError> onClientStateSendError = new();
        
        public SafeEventDispatcher<bool> resimulation = new();
        public SafeEventDispatcher<bool> resimulationStep = new();
        public SafeEventDispatcher<uint> onSnapToServer = new();
    }
}