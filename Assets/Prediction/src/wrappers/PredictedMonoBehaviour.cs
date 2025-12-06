using Prediction.Interpolation;
using UnityEngine;

namespace Prediction.wrappers
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PredictedEntityVisuals))]
    public class PredictedMonoBehaviour : MonoBehaviour
    {
        //FUDO: can we make components serializable?
        [SerializeField] private MonoBehaviour[] components;
        [SerializeField] private int bufferSize;
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private PredictedEntityVisuals visuals;

        public ClientPredictedEntity clientPredictedEntity { get; private set; }
        public ServerPredictedEntity serverPredictedEntity { get; private set; }
        
        //TODO - wrap the prediction entities and configure
        void Awake()
        {
            //TODO: check visuals must be a child of this game object
        }

        void ConfigureAsServer()
        {
            serverPredictedEntity = new ServerPredictedEntity(bufferSize, _rigidbody, visuals.gameObject, WrapperHelpers.GetControllableComponents(components), WrapperHelpers.GetComponents(components));
        }

        void ConfigureAsClient(bool controlledLocally)
        {
            //TODO: detect or wire components
            clientPredictedEntity = new ClientPredictedEntity(30, _rigidbody, visuals.gameObject, WrapperHelpers.GetControllableComponents(components), WrapperHelpers.GetComponents(components));
            clientPredictedEntity.gameObject = gameObject;
            //TODO: configurable interpolator
            visuals.SetClientPredictedEntity(clientPredictedEntity, new MovingAverageInterpolator());
        }

        public bool IsControlledLocally()
        {
            if (clientPredictedEntity == null)
                return true;
            return clientPredictedEntity.isControlledLocally;
        }
        
        public void SetControlledLocally(bool controlledLocally)
        {
            visuals.Reset();
            clientPredictedEntity?.SetControlledLocally(controlledLocally);
        }

        public void ResetClient()
        {
            visuals.Reset();
            clientPredictedEntity?.Reset();
        }
        
        public void Reset()
        {
            ResetClient();
            serverPredictedEntity?.Reset();
        }
    }
}