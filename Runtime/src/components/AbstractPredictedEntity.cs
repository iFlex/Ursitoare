using Prediction.data;
using UnityEngine;

namespace Prediction
{
    public abstract class AbstractPredictedEntity : PredictableComponent
    {
        public Rigidbody rigidbody;
        protected GameObject detachedVisualsIdentity;
        public uint id { get; private set; }

        protected PredictableControllableComponent[] controllablePredictionContributors;
        protected PredictableComponent[] predictionContributors;
        protected PredictableComponent[] statefulComponents = new PredictableComponent[0];

        protected int totalStateFloats = 0;
        protected int totalStateBools = 0;
        protected int totalFloatInputs = 0;
        protected int totalBinaryInputs = 0;
        protected bool isControllable = false;
        
        protected AbstractPredictedEntity(uint identifier, Rigidbody rb, GameObject visuals, PredictableControllableComponent[] controllablePredictionContributors, PredictableComponent[] predictionContributors)
        {
            id = identifier;
            rigidbody = rb;
            detachedVisualsIdentity = visuals;
            this.controllablePredictionContributors = controllablePredictionContributors;
            this.predictionContributors = predictionContributors;
            
            isControllable = controllablePredictionContributors.Length > 0;

            int statefulComponentCount = 0;
            for (int i = 0; i < controllablePredictionContributors.Length; i++)
            {
                totalFloatInputs += controllablePredictionContributors[i].GetFloatInputCount();
                totalBinaryInputs += controllablePredictionContributors[i].GetBinaryInputCount();
            }

            for (int i = 0; i < predictionContributors.Length; i++)
            {
                if (predictionContributors[i].HasState())
                {
                    statefulComponentCount++;
                    totalStateFloats += predictionContributors[i].GetStateFloatCount();
                    totalBinaryInputs += predictionContributors[i].GetStateBoolCount();
                }
            }
            
            statefulComponents = new PredictableComponent[statefulComponentCount];
            int j = 0;
            for (int i = 0; i < predictionContributors.Length; i++)
            {
                if (predictionContributors[i].HasState())
                {
                    statefulComponents[j++] = predictionContributors[i];
                }
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

        public bool ValidateState(float deltaTime, PredictionInputRecord input)
        {
            for (int i = 0; i < controllablePredictionContributors.Length; ++i)
            {
                if (!controllablePredictionContributors[i].ValidateInput(deltaTime, input))
                {
                    return false;
                }
            }
            return true;
        }
        
        public void LoadInput(PredictionInputRecord input)
        {
            input.ReadReset();
            //NOTE: loading in the exact same order all the time on both server and client is critical!
            //NOTE: at the moment all components must read their data even if not using it...
            for (int i = 0; i < controllablePredictionContributors.Length; ++i)
            {
                //TODO: what about skipping invalid input!?!?!
                //TODO: what about adding & removing components?
                //TODO: what about new components? always add at the end for compatibility?
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

        public void SampleComponentState(PhysicsStateRecord psr)
        {
            if (psr.componentState != null)
            {
                psr.componentState.WriteReset();
            }
            for (int i = 0; i < statefulComponents.Length; ++i)
            {
                statefulComponents[i].SampleComponentState(psr);
            }
        }
    
        public void LoadComponentState(PhysicsStateRecord psr)
        {
            if (psr.componentState != null)
            {
                psr.componentState.ReadReset();
            }
            for (int i = 0; i < statefulComponents.Length; ++i)
            {
                statefulComponents[i].LoadComponentState(psr);
            }
        }

        public override int GetHashCode()
        {
            return (int) id;
        }

        public bool IsControllable()
        {
            return isControllable;
        }
        
        public virtual bool HasState()
        {
            return false;
        }
        
        public int GetStateFloatCount()
        {
            return totalStateFloats;
        }

        public int GetStateBoolCount()
        {
            return totalStateBools;
        }
    }
}