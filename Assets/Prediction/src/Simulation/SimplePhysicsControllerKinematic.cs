using Prediction.data;
using UnityEngine;

namespace Prediction.Simulation
{
    public class SimplePhysicsControllerKinematic : PhysicsController
    {
        //TODO: save velocity state before and after resim
        private Rigidbody[] bodies;
        private PhysicsStateRecord[] states;
        private Vector3[] accForces;
        private Vector3[] accTorques;
        
        public void DetectAllBodies()
        {
            bodies = Object.FindObjectsOfType<Rigidbody>();
            states = new PhysicsStateRecord[bodies.Length];
            accForces = new Vector3[bodies.Length];
            accTorques = new Vector3[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                states[i] = new PhysicsStateRecord();
                Debug.Log($"[SimplePhysicsControllerKinematic][DetectAllBodies] Detected:{bodies[i]} State:{states[i]}");
            }
            SaveStates();
        }

        void SaveStates()
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                states[i].From(bodies[i]);
                //TODO: probably not useful...
                accForces[i] = bodies[i].GetAccumulatedForce();
                accTorques[i] = bodies[i].GetAccumulatedTorque();
                Debug.Log($"[SimplePhysicsControllerKinematic][SaveStates] Body:{bodies[i]} State:{states[i]}");
            }
        }

        void LoadStates(Rigidbody ignore)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] == ignore)
                {
                    continue;
                }
                
                bodies[i].position = states[i].position;
                bodies[i].rotation = states[i].rotation;
                bodies[i].linearVelocity = states[i].velocity;
                bodies[i].angularVelocity = states[i].angularVelocity;
                Debug.Log($"[SimplePhysicsControllerKinematic][LoadStates] Body:{bodies[i]} State:{states[i]}");
            }
        }
        
        public void Setup(bool isServer)
        {
            Physics.simulationMode = SimulationMode.Script;
        }

        public void Simulate()
        {
            Physics.Simulate(Time.fixedDeltaTime);
        }

        public void BeforeResimulate(ClientPredictedEntity entity)
        {
            SaveStates();
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].isKinematic = true;
            }
            entity.rigidbody.isKinematic = false;
        }

        public void Resimulate(ClientPredictedEntity entity)
        {
            Physics.Simulate(Time.fixedDeltaTime);
        }

        public void AfterResimulate(ClientPredictedEntity entity)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].isKinematic = false;
            }
            LoadStates(entity.rigidbody);
        }
    }
}