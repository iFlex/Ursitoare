using Prediction.data;
using UnityEngine;

namespace Prediction
{
    public abstract class AbstractPredictedEntity : PredictableComponent
    {
        protected Rigidbody rigidbody;
        protected GameObject detachedVisualsIdentity;
        
        protected PredictableControllableComponent[] controllablePredictionContributors;
        protected PredictableComponent[] predictionContributors;

        protected int totalFloatInputs = 0;
        protected int totalBinaryInputs = 0;
        
        protected AbstractPredictedEntity(Rigidbody rb, GameObject visuals, PredictableControllableComponent[] controllablePredictionContributors, PredictableComponent[] predictionContributors)
        {
            rigidbody = rb;
            detachedVisualsIdentity = visuals;
            this.controllablePredictionContributors = controllablePredictionContributors;
            this.predictionContributors = predictionContributors;
            
            for (int i = 0; i < controllablePredictionContributors.Length; i++)
            {
                totalFloatInputs += controllablePredictionContributors[i].GetFloatInputCount();
                totalBinaryInputs += controllablePredictionContributors[i].GetBinaryInputCount();
            }
        }
        
        public void PopulatePhysicsStateRecord(uint tickId, PhysicsStateRecord stateData)
        {
            stateData.tickId = tickId;
            stateData.position = rigidbody.position;
            stateData.rotation = rigidbody.rotation;
            stateData.velocity = rigidbody.linearVelocity;
            stateData.angularVelocity = rigidbody.angularVelocity;
        }
        
        public void LoadInput(PredictionInputRecord input)
        {
            for (int i = 0; i < controllablePredictionContributors.Length; ++i)
            {
                controllablePredictionContributors[i].LoadInput(input);
            }
        }

        public void ApplyForces()
        {
            for (int i = 0; i < predictionContributors.Length; ++i)
            {
                predictionContributors[i].ApplyForces();
            }
        }
    }
}