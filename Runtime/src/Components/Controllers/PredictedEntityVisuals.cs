// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Prediction.Data;
using Prediction.Interpolation;
using Sector0.Events;
using UnityEngine;

namespace Prediction.Components.Controllers
{
    public class PredictedEntityVisuals : MonoBehaviour
    {
        //TODO: larger smooth window for followers!
        
        public static bool SHOW_DBG = false;
        public static bool DETACH_VISUALS = true;
        //TODO: differentiate jumps in the direction of travel versus sideways
        public static float LARGE_POS_JUMP = 0.35f;
        public static float LARGE_ANGLE_JUMP = 2.5f;
        
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

            if (DETACH_VISUALS)
            {
                DetachVisuals();
            }
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
            if (DETACH_VISUALS)
            {
                DetachVisuals();
            }
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
                    Vector3 beforePos = visualsEntity.transform.position;
                    Quaternion rotBefore = visualsEntity.transform.rotation;
                    
                    interpolationProvider.Update(Time.deltaTime, PredictionManager.Instance.tickId);
                    interpolationDistance = (visualsEntity.transform.position - logicalEntityTransform.position).magnitude;

                    Vector3 posDiff = visualsEntity.transform.position - beforePos;
                    Quaternion rotDiff = visualsEntity.transform.rotation * Quaternion.Inverse(rotBefore);
                    if (posDiff.magnitude > LARGE_POS_JUMP || 
                        ClosestAngleRot(Mathf.Abs(rotDiff.eulerAngles.x)) > LARGE_ANGLE_JUMP || 
                        ClosestAngleRot(Mathf.Abs(rotDiff.eulerAngles.y)) > LARGE_ANGLE_JUMP || 
                        ClosestAngleRot(Mathf.Abs(rotDiff.eulerAngles.z)) > LARGE_ANGLE_JUMP)
                    {
                        TransformJump jump = new TransformJump();
                        jump.positionDiff = posDiff;
                        jump.rotationDiff = rotDiff;
                        onLargeTransformJump.Dispatch(jump);
                        
                        GlobalTransformJump gjump = new GlobalTransformJump();
                        gjump.entity = gameObject;
                        gjump.jump = jump;
                        onLargeTransformJumpGlobal.Dispatch(gjump);
                    }
                }
                else if (serverEntityTransform)
                {
                    //TODO: use transform or visualsEntity.transform? wat?
                    transform.position = serverEntityTransform.position;
                    transform.rotation = serverEntityTransform.rotation;
                }
            }
        }

        float ClosestAngleRot(float angle)
        {
            return Mathf.Min(angle, 360 - angle);
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

        public struct TransformJump
        {
            public Vector3 positionDiff;
            public Quaternion rotationDiff;
        }

        public struct GlobalTransformJump
        {
            public GameObject entity;
            public TransformJump jump;
        }
        
        public SafeEventDispatcher<TransformJump> onLargeTransformJump = new();
        public static SafeEventDispatcher<GlobalTransformJump> onLargeTransformJumpGlobal = new();
    }
}
