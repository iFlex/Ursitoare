using System.Collections.Generic;
using Prediction.data;
using Prediction.utils;
using UnityEngine;

namespace Prediction.Simulation
{
    public class RewindablePhysicsController : PhysicsController
    {
        public static bool DEBUG_STEP = false;
        public static bool  USE_MAX_DEPEN_SPEED = false;
        public static float MAX_DEPEN_SPEED = 4f;
        public static RewindablePhysicsController Instance;
        
        public int bufferSize = 60;
        private uint tickId;
        private Dictionary<Rigidbody, RingBuffer<PhysicsStateRecord>> worldHistory = new();
        private ClientPredictedEntity mainResimulationEntity;
        
        public void Setup(bool isServer)
        {
            Physics.simulationMode = SimulationMode.Script;
            Instance = this;
        }

        public void Simulate()
        {
            _SimStep();
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
            tickId++;
            SampleWorldState();
        }
        
        public void BeforeResimulate(ClientPredictedEntity entity)
        {
            mainResimulationEntity = entity;
        }

        public void Rewind(uint ticks)
        {
            tickId -= ticks;
            ApplyWorldState(tickId);
        }

        public void AfterResimulate(ClientPredictedEntity entity)
        {
            mainResimulationEntity = null;
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

        void ApplyWorldState(uint pos)
        {
            foreach (KeyValuePair<Rigidbody, RingBuffer<PhysicsStateRecord>> pair in worldHistory)
            {
                PhysicsStateRecord psr = pair.Value.Get((int) pos);
                psr.To(pair.Key);
            }
        }
        
        public void Track(Rigidbody rigidbody)
        {
            RingBuffer<PhysicsStateRecord> ringBuffer = new RingBuffer<PhysicsStateRecord>(bufferSize);
            for (int i = 0; i < bufferSize; i++)
            {
                ringBuffer.Set(i, new PhysicsStateRecord());
            }
            worldHistory[rigidbody] = ringBuffer;
            rigidbody.maxDepenetrationVelocity = MAX_DEPEN_SPEED;
        }

        public void Untrack(Rigidbody rigidbody)
        {
            worldHistory.Remove(rigidbody);
        }

        public void SetMaxDepenetrationVelocity(float velocity)
        {
            MAX_DEPEN_SPEED = velocity;
            foreach (KeyValuePair<Rigidbody, RingBuffer<PhysicsStateRecord>> pair in worldHistory)
            {
                pair.Key.maxDepenetrationVelocity = MAX_DEPEN_SPEED;
            }
        }
    }
}