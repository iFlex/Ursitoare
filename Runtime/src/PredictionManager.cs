using System;
using System.Collections.Generic;
using Prediction.Components.Controllers;
using Sector0.Events;
using Prediction.Interpolation;
using Prediction.Resimulation.Detection;
using Prediction.Simulation;
using Prediction.Stats;
using UnityEngine;

namespace Prediction
{
    public abstract class PredictionManager
    {
        public static PredictionManager Instance;
        
        public static bool DEBUG = false;
        public static bool DEBUG_OWNERSHIP = false;
        //Occasional but important events: entities added/removed/created/destroyed, ownership changes, snap-to-server resets, misuse warnings.
        public static bool LOG_EVENTS = false;
        //Errors and warnings (e.g. NULL_PREDICTED_GAME_OBJECT, POTENTIAL_EXPLOIT_ATTEMPT). Off by default like all other logging; turn on while integrating or debugging.
        public static bool LOG_ERRORS = false;
        public static bool LOG_TIMING = false;
        public static bool DO_RESIM = true;
        public static bool DO_SNAP = true;

        public static bool PREDICT_FOLLOWERS = true;
        public static bool LOG_PRE_SIM_STATE = false;
        public static bool PREDICTION_ENABLED = true;
        //FUDO: we might not need the RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD, it gives some good flexibilty for now.
        public static float RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = 0;
        public static float RESIMULATE_PRECISE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = 0;
        public static bool TRACK_TIMING_STATS = true;
        public static int MISSING_PACKETS_BUFFER_SIZE = 10;
        public static int RESIM_TICK_COUNT_BUFFER_SIZE = 30;
        public static bool TRACK_PACKET_LOSS = true;
        
        public static int CLIENT_RTT_MEASUREMENTS_BUFFER_SIZE = 20;
        
        //TODO: validate presence of all static providers
        //TODO: move to client impl
        public static Func<VisualsInterpolationsProvider> INTERPOLATION_PROVIDER = () => new MovingAverageInterpolator();
        public static SingleSnapshotInstanceResimChecker SNAPSHOT_INSTANCE_RESIM_CHECKER = new SimpleConfigurableResimulationDecider();
        public static SingleSnapshotInstanceResimChecker FOLLOWER_INSTANCE_RESIM_CHECKER = new SimpleConfigurableResimulationDecider();
        public static Func<Timer> TIMER_PROVIDER = () => new DefaultTimer();
        //TODO: do we still need this?
        public static Func<double> ROUND_TRIP_GETTER;
        
        protected PhysicsController PhysicsController = new RewindablePhysicsController();
        protected HashSet<PredictedEntity> _predictedEntities = new HashSet<PredictedEntity>();
        protected HashSet<GameObject> _predictedEntitiesGO = new HashSet<GameObject>();
        
        protected uint tickId = 1;
        
        public uint reportedServerTickId { get; protected set; }
        private bool setup = false;
        public bool autoTrackRigidbodies = true;
        public bool useServerWorldStateMessage = false;
        
        protected uint lastAckTickId = 0;
        
        protected uint clientLastReceivedTickId = 0;
        
        //NOTE: either use protectFromOversimulation or TRUST_ALREADY_RESIMULATED_TICKS, no both
        public bool protectFromOversimulation = true;
        public uint maxTickResimulationCount = 1;
        public uint totalResimulationsDueToAuthority = 0;
        public uint totalResimulationsDueToFollowers = 0;
        public uint totalResimulationsDueToBoth = 0;
        
        public uint totalResimulations = 0;
        public uint totalTickFreezes = 0;
        public uint totalResimulationSteps = 0;
        public uint totalDesyncToSnapCount = 0;
        
        public uint totalResimulationsTriggeredByLocalAuthority = 0;
        public uint totalResimulationsTriggeredByFollowers = 0;
        public uint totalResimulationsTriggeredByBoth = 0;
        public uint totalResimulationsSkipped = 0;

        protected Timer _tickTimer;
        protected Timer _interTickTimer;
        protected Timer _resimTimer;
        protected Timer _serverRecvTimer;
        
        public double lastServerRecvIntervalDuration = 0;
        public double lastClientTickRTT = 0;
        public double lastInterTickDuration = 0;
        public double lastTickDuration = 0;
        
        protected bool resimulatedThisTick = false;
        protected double lastResimDuration = 0;
        protected uint lastResimmedTicks = 0;
        
        //TODO: unit test this!!!
        public bool resimUseAvailableServerTicks = true;
        public bool correctWholeWorldWhenResimulating = true;
        public uint resimSkipNotEnoughHistory = 0;
        public uint maxRewindDistance = 0;
        public uint totalRewindDistance = 0;
        
        long lastTickTimestamp = 0;
        long interTickDuration = 0;
        long tickDuration = 0;
        long preSimDuration = 0;
        long postSimDuration = 0;
        
        public bool shouldResimThisTick = false;
        public uint clientSendErrors = 0;
        public uint clientStatesReceived = 0;
        
        protected uint ticksSinceResim = 0;
        public bool oversimProtectWithTickInterval = true;
        public uint minTicksBetweenResims = 0;
        
        public PredictionManager()
        {
            Instance = this;
            _tickTimer = TIMER_PROVIDER();
            _interTickTimer = TIMER_PROVIDER();
            _resimTimer = TIMER_PROVIDER();
            _serverRecvTimer = TIMER_PROVIDER();
        }

        public void SetPhysicsController(PhysicsController controller)
        {
            PhysicsController = controller;
        }
        
        protected abstract void Validate();

        protected void AddPredictedEntity(GameObject entity)
        {
            _predictedEntitiesGO.Add(entity.gameObject);
            _predictedEntities.Add(entity.gameObject.GetComponent<PredictedEntity>());
        }
        
        protected void RemovePredictedEntity(GameObject entity)
        {
            _predictedEntitiesGO.Remove(entity);
            _predictedEntities.Remove(entity.GetComponent<PredictedEntity>());
        }

        public ICollection<PredictedEntity> GetPredictedEntities()
        {
            return _predictedEntities;
        }
        
        public bool IsPredicted(GameObject entity)
        {
            return _predictedEntitiesGO.Contains(entity);
        }

        public bool IsPredicted(Rigidbody entity)
        {
            if (!entity)
                return false;
            return _predictedEntitiesGO.Contains(entity.gameObject);
        }
        
        protected abstract void PreSimTick();
        protected abstract void PostSimTick();
        
        //TODO: package private
        public virtual void Tick()
        {    
            //Debug.Log($"[PredictionManager][Tick] ${Time.realtimeSinceStartup}");
            lastInterTickDuration = _interTickTimer.Stop();
            _tickTimer.Start();
            
            ticksSinceResim++;
            //TODO: fix these counters now that the client and server have been separated! they are not accurate
            resimulatedThisTick = false;
            shouldResimThisTick = false;
            lastResimDuration = 0;
            lastResimmedTicks = 0;
            
            onPreTick.Dispatch(tickId);
            
            PreSimTick();
            PhysicsController.Simulate();
            PostSimTick();
            
            onPostTick.Dispatch(tickId);
            
            _interTickTimer.Start();
            
            TickStat tickStat = new TickStat();
            tickStat.tickId = tickId;
            tickStat.duration = _tickTimer.Stop();
            tickStat.didResimulate = resimulatedThisTick;
            tickStat.resimDuration = lastResimDuration;
            tickStat.resimTicks = lastResimmedTicks;
            lastTickDuration = tickStat.duration;
            onTickStat.Dispatch(tickStat);
            
            if (LOG_TIMING || DEBUG) {
                Debug.Log($"[PredictionManager][Tick] t:{tickId} deltaPrevTick:{interTickDuration} td:{tickDuration} pre:{preSimDuration} post:{postSimDuration} sim:{(tickDuration - preSimDuration - postSimDuration)} resim:{(resimulatedThisTick ? "1" : "0")} freq:{System.Diagnostics.Stopwatch.Frequency} shouldResim:{(shouldResimThisTick ? "1" : "0")}");
            }
            
            tickId++;
        }

        public abstract uint GetServerTickId();

        public uint GetTickId()
        {
            return tickId;
        }
        
        public virtual void Clear()
        {
            //TODO: unit test
            tickId = 1;
            _predictedEntities.Clear();
            _predictedEntitiesGO.Clear();
            PhysicsController.Clear();

            lastAckTickId = 0;
            lastServerRecvIntervalDuration = 0;
            lastClientTickRTT = 0;
            lastInterTickDuration = 0;
            lastTickDuration = 0;
            clientLastReceivedTickId = 0;
        }
        
        public static uint GetServerTickDelay()
        {
            return (uint) Mathf.CeilToInt((float)(ROUND_TRIP_GETTER() / Time.fixedDeltaTime));
        }
        
        public struct EntityProcessingError
        {
            public Exception exception;
            public uint entityId;
        }
        
        public struct ServerUpdateSendError
        {
            public Exception exception;
            public int connId;
            public uint entityId;
            public uint tickId;
        }
        
        public uint GetTotalTicks()
        {
            return tickId;
        }
        
        public uint GetAverageResimPerTick()
        {
            return totalResimulationSteps / tickId;
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
        
        public struct TickStat
        {
            public uint tickId;
            public double duration;
            public double resimDuration;
            public bool didResimulate;
            public uint resimTicks;
        }
        
        public SafeEventDispatcher<uint> onPreTick = new();
        public SafeEventDispatcher<uint> onPreResimTick = new();
        public SafeEventDispatcher<uint> onPostTick = new();
        public SafeEventDispatcher<uint> onPostResimTick = new();
        public SafeEventDispatcher<TickStat> onTickStat = new();
        public SafeEventDispatcher<TickRttDuration> onTickRttDuration = new();
        public SafeEventDispatcher<int> onPacketLoss = new();
            
        public SafeEventDispatcher<ServerUpdateSendError> onServerStateSendError = new();
        public SafeEventDispatcher<EntityProcessingError> onClientStateSendError = new();
        public SafeEventDispatcher<bool> resimulation = new();
        public SafeEventDispatcher<bool> resimulationStep = new();
        public SafeEventDispatcher<uint> onSnapToServer = new();
    }
}