using Prediction.data;
using UnityEngine;

namespace Prediction
{
    public class PredictedEntityVisuals : MonoBehaviour
    {
        [SerializeField] private GameObject visualsEntity;
        [SerializeField] private bool debug = false;
        [SerializeField] private GameObject serverGhostPrefab;
        [SerializeField] private GameObject clientGhostPrefab;
        
        private ClientPredictedEntity clientPredictedEntity;
        [SerializeField] private GameObject follow;
        
        private GameObject serverGhost;
        private GameObject clientGhost;
        private PlayerController pc;
        
        public void SetClientPredictedEntity(ClientPredictedEntity clientPredictedEntity)
        {
            this.clientPredictedEntity = clientPredictedEntity;
            follow = clientPredictedEntity.gameObject;
            //TODO: listen for destruction events
            
            visualsEntity.transform.SetParent(null);
            if (debug)
            {
                serverGhost = Instantiate(serverGhostPrefab, Vector3.zero, Quaternion.identity);
                clientGhost = Instantiate(clientGhostPrefab, Vector3.zero, Quaternion.identity, follow.transform);
            }
            
            //TODO: better wiring
            pc = clientPredictedEntity.gameObject.GetComponent<PlayerController>();
        }

        //TODO: configurable
        private float defaultLerpFactor = 20f;
        void Update()
        {
            if (!follow)
                return;
            
            if (pc.pcam)
            {
                pc.pcam.Follow = visualsEntity.transform;
            }
            
            float lerpFactor = Mathf.Max(0, defaultLerpFactor - clientPredictedEntity.GetResimulationOverbudget() * 20f);
            visualsEntity.transform.position = Vector3.Lerp(visualsEntity.transform.position, follow.transform.position, Time.deltaTime * lerpFactor);
            visualsEntity.transform.rotation = Quaternion.Lerp(visualsEntity.transform.rotation, follow.transform.rotation, Time.deltaTime * lerpFactor);

            if (debug)
            {
                PhysicsStateRecord rec = clientPredictedEntity.serverStateBuffer.GetEnd();
                if (rec != null)
                {
                    serverGhost.transform.position = rec.position;
                    serverGhost.transform.rotation = rec.rotation;   
                }
            }
        }
    }
}