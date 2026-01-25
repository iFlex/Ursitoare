using Prediction.data;
using UnityEngine;

namespace Prediction
{
    //All components that apply physics forces to the Rigidbody must implement this interface to support prediction.
    public interface PredictableComponent
    {
        void ApplyForces();
        bool HasState();
        void SampleComponentState(PhysicsStateRecord physicsStateRecord);
        void LoadComponentState(PhysicsStateRecord physicsStateRecord);
        int GetStateFloatCount();
        int GetStateBoolCount();
    }
}