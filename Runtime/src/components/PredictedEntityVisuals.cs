using Prediction.data;
using Prediction.Interpolation;
using UnityEngine;

namespace Prediction
{
    public class PredictedEntityVisuals : MonoBehaviour
    {
        //TODO: larger smooth window for followers!
        
        public static bool SHOW_DBG = false;
        [SerializeField] public GameObject visualsEntity;
        [SerializeField] private GameObject serverGhostPrefab;
        [SerializeField] private GameObject clientGhostPrefab;
        
        public VisualsInterpolationsProvider interpolationProvider { get; private set; }
        private ClientPredictedEntity clientPredictedEntity;

        private Transform serverEntityTransform;
        private Transform logicalEntityTransform;
        private GameObject serverGhost;
        private GameObject clientGhost;
        public bool hasVIP = false;
        
        public double currentTimeStep = 0;
        public double targetTime = 0;
        public double artifficialDelay = 1f;
        private bool visualsDetached = false;
        protected float interpolationDistance = 0;
        
        void DetachVisuals()
        {
            logicalEntityTransform = visualsEntity.transform.parent;
            visualsDetached = true;
            visualsEntity.transform.SetParent(null);
        }
        
        //NOTE: never call this on the server
        public void SetClientPredictedEntity(ClientPredictedEntity clientPredictedEntity, VisualsInterpolationsProvider provider)
        {
            interpolationProvider = provider;
            this.clientPredictedEntity = clientPredictedEntity;
            clientPredictedEntity.onReset.AddEventListener(OnShouldReset);
            //TODO: what? why artifficial delay?
            currentTimeStep -= artifficialDelay;
            
            DetachVisuals();
            interpolationProvider.SetInterpolationTarget(visualsEntity.transform);
            
            if (serverGhostPrefab)
            {
                serverGhost = Instantiate(serverGhostPrefab, Vector3.zero, Quaternion.identity);
            }
            if (clientGhostPrefab)
            {
                clientGhost = Instantiate(clientGhostPrefab, Vector3.zero, Quaternion.identity, clientPredictedEntity.gameObject.transform);
                clientGhost.transform.localPosition = Vector3.zero;
                clientGhost.transform.localRotation = Quaternion.identity;
            }
            
            clientPredictedEntity.newStateReached.AddEventListener(AggregateState);
            SetControlledLocally(false);
        }
        
        //NOTE: you should detach visuals even on server if they have colliders on them, because those colliders will behave differently on client vs server if one is detached and one is not.
        public void SetServerPredictedEntity(Transform serverPredictedEntity)
        {
            DetachVisuals();
            serverEntityTransform = serverPredictedEntity;
        }

        public void Destroy(bool ignore)
        {
            Debug.Log($"[PredictedEntityVisuals][Destroy]");
            if (visualsEntity)
            {
                GameObject.Destroy(visualsEntity);
                visualsEntity = null;
            }
        }

        void AggregateState(PhysicsStateRecord state)
        {
            //Debug.Log($"[PredictedEntityVisuals]({GetInstanceID()}) state: {state}");
            interpolationProvider.Add(state);
        }

        private PhysicsStateRecord rec;
        void Update()
        {
            //TODO: make this more efficient
            if (serverGhost)
                serverGhost.SetActive(SHOW_DBG);
            if (clientGhost)
                clientGhost.SetActive(SHOW_DBG);
            if (!visualsDetached)
                return;

            if (serverGhost)
            {
                rec = clientPredictedEntity.serverStateBuffer.GetEnd();
                if (rec != null && serverGhost)
                {
                    serverGhost.transform.position = rec.position;
                    serverGhost.transform.rotation = rec.rotation;   
                }
            }

            if (visualsDetached)
            {
                if (clientPredictedEntity != null)
                {
                    interpolationProvider.Update(Time.deltaTime, PredictionManager.Instance.tickId);
                    interpolationDistance = (visualsEntity.transform.position - logicalEntityTransform.position).magnitude;
                }
                else if (serverEntityTransform)
                {
                    transform.position = serverEntityTransform.position;
                    transform.rotation = serverEntityTransform.rotation;
                }
            }
        }

        void OnShouldReset(bool ign)
        {
            Reset();
        }
        
        public void Reset()
        {
            interpolationProvider?.Reset();
        }

        public void SetControlledLocally(bool ctlLoc)
        {
            interpolationProvider?.SetControlledLocally(ctlLoc);
        }

        public float GetInterpolationDistance()
        {
            return interpolationDistance;
        }
    }
}