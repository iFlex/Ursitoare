using System.Collections.Generic;
using Prediction.data;
using Prediction.utils;
using Sector0.Events;
using UnityEngine;

namespace Prediction.Simulation
{
    //NOTE: the edgecase where a resimulation overlaps a spawn or despawn is not handled explicitly here for simplicity's sake.
    //      Also, I suppose we can swallow the cost of it by just resimulating again later. If this provest o be an issue, then will need to track ticks when spawn and despawn happen and take that into account.
    public class RewindablePhysicsController : PhysicsController
    {
        public static bool DEBUG_STEP = false;

        public static RewindablePhysicsController Instance;
        public PhysicsStateRecord outOfBoundsRecord = new PhysicsStateRecord();
        
        public int bufferSize = 60;
        protected uint tickId = 1;
        protected uint unrewindableTickId = 1;
        protected Dictionary<Rigidbody, RingBuffer<PhysicsStateRecord>> worldHistory = new();
        protected Dictionary<Rigidbody, uint> spawnTime = new();
        protected Dictionary<uint, HashSet<Rigidbody>> spawnTicks = new();

        private bool isResimulating = false;
        private List<Rigidbody> pendingTracks = new();
        private List<Rigidbody> pendingUntracks = new();

        public RewindablePhysicsController()
        {
        }
        
        public RewindablePhysicsController(int bufferSize)
        {
            this.bufferSize = bufferSize;
        }

        public uint GetTick()
        {
            return tickId;
        }
        
        public void Setup(bool isServer)
        {
            Physics.simulationMode = SimulationMode.Script;
            Instance = this;
        }

        void LogState(bool isPreStep)
        {
            foreach (KeyValuePair<Rigidbody, RingBuffer<PhysicsStateRecord>> pair in worldHistory)
            {
                PhysicsStateRecord psr = pair.Value.Get((int) tickId);
                psr.From(pair.Key);
                psr.tickId = tickId;
                PhysicsStateLogInfo info;
                info.gameObjectInstanceId = pair.Key.gameObject.GetInstanceID();
                info.tickId = tickId;
                info.position = pair.Key.position;
                info.rotation = pair.Key.rotation;
                info.velocity = pair.Key.linearVelocity;
                info.angularVelocity = pair.Key.angularVelocity;
                info.accumulatedForce = pair.Key.GetAccumulatedForce();
                info.accumulatedTorque = pair.Key.GetAccumulatedTorque();
                info.isPreStep = isPreStep;
                onPhysicsStateLog.Dispatch(info);
            }
        }

        public void Simulate()
        {
            LogState(true);
            _SimStep();
            LogState(false);
        }
        
        public void Resimulate(ClientPredictedEntity entity)
        {
            _SimStep();
            if (DEBUG_STEP)
            {
                Debug.Break();
            }
        }

        private void _SimStep()
        {
            Physics.Simulate(Time.fixedDeltaTime);
            if (isResimulating)
            {
                RestorePreSpawnBodies(tickId);
            }
            
            SampleWorldState();
            tickId++;
            
            if (!isResimulating)
            {
                unrewindableTickId = tickId;
            }
        }

        void RestorePreSpawnBodies(uint tid)
        {
            HashSet<Rigidbody> bodies = spawnTicks.GetValueOrDefault(tid, null);
            if (bodies != null)
            {
                foreach (Rigidbody b in bodies)
                {
                    ApplyBodyWorldState(tid, b);
                }
            }
        }
        
        public void BeforeResimulate()
        {
            isResimulating = true;
        }

		public void BeforeResimulate(ClientPredictedEntity entity)
        {
			//NOOP
        }
    
        public bool Rewind(uint ticks)
        {
            if (tickId <= ticks)
                return false;
            
            tickId -= ticks;
            ApplyWorldState(tickId);
            //NOTE: at this point the current tickId was reached!
            tickId++;
            return true;
        }

        public void AfterResimulate()
        {
            isResimulating = false;
            FlushPendingTrackOperations();
        }	
		
		public void AfterResimulate(ClientPredictedEntity entity)
        {
			//NOOP
        }

        void SampleWorldState()
        {
            foreach (KeyValuePair<Rigidbody, RingBuffer<PhysicsStateRecord>> pair in worldHistory)
            {
                PhysicsStateRecord psr = pair.Value.Get((int) tickId);
                psr.From(pair.Key);
                psr.tickId = tickId;
            }
        }

        void ApplyWorldState(uint tid)
        {
            foreach (KeyValuePair<Rigidbody, RingBuffer<PhysicsStateRecord>> pair in worldHistory)
            {
                if (spawnTime.GetValueOrDefault(pair.Key, 0u) > tid)
                {
                    //Set position to out of bounds if item wasn't spawned already. Not ideal but should work
                    outOfBoundsRecord.To(pair.Key);
                }
                else
                {
                    PhysicsStateRecord psr = pair.Value.Get((int) tid);
                    psr.To(pair.Key);
                }
            }
        }

        void ApplyBodyWorldState(uint tid, Rigidbody body)
        {
            RingBuffer<PhysicsStateRecord> ringBuffer = worldHistory.GetValueOrDefault(body, null);
            if (ringBuffer != null)
            {
                PhysicsStateRecord psr = ringBuffer.Get((int) tid);
                psr.To(body);
            }
        }
        
        public void Track(Rigidbody rigidbody)
        {
            if (isResimulating)
            {
                pendingTracks.Add(rigidbody);
                spawnTime[rigidbody] = unrewindableTickId;
                if (!spawnTicks.ContainsKey(unrewindableTickId))
                {
                    spawnTicks[unrewindableTickId] = new HashSet<Rigidbody>();
                }
                spawnTicks[unrewindableTickId].Add(rigidbody);
                return;
            }
            TrackImmediate(rigidbody);
        }

        public void Untrack(Rigidbody rigidbody)
        {
            if (isResimulating)
            {
                pendingUntracks.Add(rigidbody);
                return;
            }
            UntrackImmediate(rigidbody);
        }

        void TrackImmediate(Rigidbody rigidbody)
        {
            RingBuffer<PhysicsStateRecord> ringBuffer = new RingBuffer<PhysicsStateRecord>(bufferSize);
            for (int i = 0; i < bufferSize; i++)
            {
                //NOTE: the physics controller doesn't account for complex vehicles with rewindable internal state. So we rely on the PredictionManager to rewind that state.
                ringBuffer.Set(i, PhysicsStateRecord.Alloc());
            }
            worldHistory[rigidbody] = ringBuffer;
            rigidbody.interpolation = RigidbodyInterpolation.None;
        }

        void UntrackImmediate(Rigidbody rigidbody)
        {
            worldHistory.Remove(rigidbody);
            spawnTime.Remove(rigidbody);
        }

        void FlushPendingTrackOperations()
        {
            for (int i = 0; i < pendingUntracks.Count; i++)
            {
                UntrackImmediate(pendingUntracks[i]);
            }
            pendingUntracks.Clear();

            for (int i = 0; i < pendingTracks.Count; i++)
            {
                TrackImmediate(pendingTracks[i]);
            }
            pendingTracks.Clear();
        }

        public int GetTrackedCount()
        {
            return worldHistory.Count;
        }

        public bool IsTracked(Rigidbody rigidbody)
        {
            return worldHistory.ContainsKey(rigidbody);
        }

        public struct PhysicsStateLogInfo
        {
            public int gameObjectInstanceId;
            public uint tickId;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 velocity;
            public Vector3 angularVelocity;
            public Vector3 accumulatedForce;
            public Vector3 accumulatedTorque;
            public bool isPreStep;
        }

        public SafeEventDispatcher<PhysicsStateLogInfo> onPhysicsStateLog = new();

        public void Clear()
        {
            worldHistory.Clear();
            pendingTracks.Clear();
            pendingUntracks.Clear();
            spawnTime.Clear();
            spawnTicks.Clear();
            isResimulating = false;
            tickId = 1;
            unrewindableTickId = 1;
        }
    }
}