#if (UNITY_EDITOR) 
using Prediction.data;
using UnityEngine;

namespace Prediction.Tests.mocks
{
    public class MockPredictableControllableComponent : PredictableControllableComponent, PredictableComponent
    {
        public Vector3 inputVector;
        public Vector3 stateVector;
        public int forceApplyCallCount = 0;
        public int inputLoadCallCount = 0;
        public Rigidbody rigidbody;
        
        public int GetFloatInputCount()
        {
            return 3;
        }

        public int GetBinaryInputCount()
        {
            return 0;
        }

        public void SampleInput(PredictionInputRecord input)
        {
            input.WriteReset();
            input.WriteNextScalar(inputVector.x);
            input.WriteNextScalar(inputVector.y);
            input.WriteNextScalar(inputVector.z);
        }

        public bool ValidateInput(float deltaTime, PredictionInputRecord input)
        {
            return true;
        }

        public void LoadInput(PredictionInputRecord input)
        {
            input.ReadReset();
            stateVector.x = input.ReadNextScalar();
            stateVector.y = input.ReadNextScalar();
            stateVector.z = input.ReadNextScalar();
            inputLoadCallCount++;
        }

        public void ApplyForces()
        {
            forceApplyCallCount++;
            if (rigidbody != null)
            {
                rigidbody.position += stateVector;
            }
        }

        public bool HasState()
        {
            return false;
        }

        public void SampleComponentState(PhysicsStateRecord physicsStateRecord)
        {
            //NOOP
        }

        public void LoadComponentState(PhysicsStateRecord physicsStateRecord)
        {
            //NOOP
        }

        public int GetStateFloatCount()
        {
            return 0;
        }

        public int GetStateBoolCount()
        {
            return 0;
        }
    }
}
#endif