using UnityEngine;

namespace Prediction.Simulation
{
    public interface PhysicsController
    {
        void Setup(bool isServer);
        void Simulate();
        bool Rewind(uint ticks);
        //Called after Rewind
        public void BeforeResimulate();
        void BeforeResimulate(ClientPredictedEntity entity);
        void Resimulate(ClientPredictedEntity entity);
        public void AfterResimulate();
        void AfterResimulate(ClientPredictedEntity entity);
        void Track(Rigidbody rigidbody);
        void Untrack(Rigidbody rigidbody);
        void Clear();
    }
}