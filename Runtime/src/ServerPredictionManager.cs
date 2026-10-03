using System;
using System.Collections.Generic;
using Prediction.Components.Controllers;
using Prediction.Data;
using UnityEngine;

namespace Prediction
{
    public class ServerPredictionManager : PredictionManager
    {
        public static ServerPredictionManager Instance;
        
        public Dictionary<ServerPredictedEntity, uint> _serverEntityToId = new Dictionary<ServerPredictedEntity, uint>();
        private Dictionary<uint, ServerPredictedEntity> _idToServerEntity = new Dictionary<uint, ServerPredictedEntity>();
        private Dictionary<ServerPredictedEntity, int> _entityToOwnerConnId = new Dictionary<ServerPredictedEntity, int>();
        private Dictionary<int, HashSet<ServerPredictedEntity>> _connIdToEntity = new Dictionary<int, HashSet<ServerPredictedEntity>>();
        public Dictionary<int, uint> _connIdToLatestTick = new Dictionary<int, uint>();
        
        //TODO: mark as can be unreliable channel
        // connectionId, entityId, state
        protected Action<int, uint, PhysicsStateRecord>    unreliableServerStateSender;
        //TODO: mark as can be unreliable channel
        // connectionId, world state
        protected Action<int, WorldStateRecord> unreliableServerWorldStateSender;
        //TODO: mark as must be reliable and ordered channel.
        // connectionId, entityId, controlledLocally
        protected Action<int, uint, bool>    reliableServerSetControlledLocally;
        
        protected Func<IEnumerable<int>> connectionsIterator;
        private WorldStateRecord _worldStateRecord = new WorldStateRecord();

        protected int invalidConnectionId;
        protected int serverConnectionId;
        
        public ServerPredictionManager(int invalidConnId, int serverConnId, Action<int, uint, PhysicsStateRecord> unreliableServerStateSender, Action<int, WorldStateRecord> unreliableServerWorldStateSender, Action<int, uint, bool> reliableServerSetControlledLocally, Func<IEnumerable<int>> connectionsIterator)
        {
            invalidConnectionId = invalidConnId;
            serverConnectionId = serverConnId;
            
            //Platform Specific Net Op Handlers
            this.unreliableServerStateSender = unreliableServerStateSender;
            this.unreliableServerWorldStateSender = unreliableServerWorldStateSender;
            this.reliableServerSetControlledLocally = reliableServerSetControlledLocally;
            this.connectionsIterator = connectionsIterator;
            
            Validate();
            PhysicsController.Setup(true);
            Instance = this;
        }
        
        protected override void Validate()
        {
            if (reliableServerSetControlledLocally == null)
            {
                throw new Exception("INVALID_CONFIG: no serverSetControlledLocally provided");
            }
            if (connectionsIterator == null)
            {
                throw new Exception("INVALID_CONFIG: no connectionsIterator provided");
            }
            if (useServerWorldStateMessage && unreliableServerWorldStateSender == null)
            {
                throw new Exception(
                    "INVALID_CONFIG: useServerWorldStateMessage = true but no serverWorldStateSender provided. Please provide a hook for sending world state packets.");
            }
            if (!useServerWorldStateMessage && unreliableServerStateSender == null)
            {
                throw new Exception(
                    "INVALID_CONFIG: useServerWorldStateMessage = false but no serverStateSender provided. Please provide a hook for sending individual state packets.");
            }   
            
            if (PhysicsController == null)
            {
                throw new Exception(
                    "INVALID_CONFIG: No Physics Controller provided.");
            }
        }

        #region CORE STAGES

        protected override void PreSimTick()
        {
            foreach (KeyValuePair<ServerPredictedEntity, uint> pair in _serverEntityToId)
            {
                ServerPredictedEntity entity = pair.Key;
                if (IsServerOwned(entity))
                {
                    entity.ServerOwnedSimulationTick();
                    MarkLatestAppliedTickId(tickId, entity);
                }
                else
                {
                    MarkLatestAppliedTickId(entity.ServerSimulationTick(), entity);
                }
            }
        }

        protected override void PostSimTick()
        {
            if (useServerWorldStateMessage)
            {
                _worldStateRecord.WriteReset();
            }
            
            foreach (KeyValuePair<ServerPredictedEntity, uint> pair in _serverEntityToId)
            {
                ServerPredictedEntity entity = pair.Key;
                uint id = pair.Value;
                PhysicsStateRecord state = entity.SamplePhysicsState(tickId);
                state.input = entity.GetLastInput();
                
                if (DEBUG)
                    Debug.Log($"[PredictionManager][ServerPostSimTick] id:{id} update:{state}");
                
                if (useServerWorldStateMessage)
                {
                    AccumulateWorldState(id, state);
                }
                else
                {
                    SendServerState(id, state);
                }
            }
            if (useServerWorldStateMessage)
            {
                SendWorldState(_worldStateRecord);
            }
        }
        
        #endregion

        public ServerPredictedEntity GetEntity(uint entityId)
        {
            return _idToServerEntity.GetValueOrDefault(entityId, null);    
        }
        
        public int GetOwner(ServerPredictedEntity entity)
    {
            return _entityToOwnerConnId.GetValueOrDefault(entity, invalidConnectionId);
        }

        public HashSet<ServerPredictedEntity> GetEntitiesByOwner(int ownerId)
        {
            return _connIdToEntity.GetValueOrDefault(ownerId, null);
        }
        
        //TODO: unit test!
        public void SetEntityOwner(ServerPredictedEntity entity, int ownerId)
        {
            if (entity == null || ownerId == invalidConnectionId)
                return;
            
            Debug.Log($"[PredictionManager][Ownership][SetEntityOwner] SERVER ({entity.id}) ownerId:{ownerId} entity:{entity}");
            if (GetOwner(entity) == ownerId)
            {
                //NOOP
                return;
            }
            
            UnsetOwnership(entity);
            SetOwnership(entity, ownerId);
        }
        
        public void UnsetOwnership(ServerPredictedEntity entity, int ownerId)
        {
            if (ownerId == invalidConnectionId)
                return;
            
            if (GetOwner(entity) == ownerId)
            {
                UnsetOwnership(entity);
                //If you release an entity from ownership, automatically give it back to the server until a new user is set as owner.
                SetOwnership(entity, serverConnectionId);
            }
        }
        
        //TODO: unit test
        public void UnsetOwnership(ServerPredictedEntity entity)
        {
            if (entity != null)
            {
                int ownerId = GetOwner(entity);
                _entityToOwnerConnId.Remove(entity);
                entity.Reset(); //Prepare for new stream of tickIds
                if (_connIdToEntity.TryGetValue(ownerId, out HashSet<ServerPredictedEntity> entities))
                {
                    entities.Remove(entity);
                }
                
                try
                {
                    reliableServerSetControlledLocally.Invoke(ownerId, entity.id, false);
                }
                catch (Exception e)
                {
                    //TODO: event
                }
                Debug.Log($"[PredictionManager][Ownership][UnsetOwnership] SERVER ownerId:{ownerId} entity:{entity}");
            }
        }
    
        //TODO: unit test
        void SetOwnership(ServerPredictedEntity entity, int ownerId)
        {
            if (entity == null || ownerId == invalidConnectionId)
                return;
            
            _entityToOwnerConnId[entity] = ownerId;
            if (!_connIdToEntity.ContainsKey(ownerId))
            {
                _connIdToEntity[ownerId] = new HashSet<ServerPredictedEntity>();
            }
            _connIdToEntity[ownerId].Add(entity);
            
            //Prepare for new stream of tickIds
            entity.Reset();
            reliableServerSetControlledLocally.Invoke(ownerId, entity.id, true);
        }
        
        public void AddPredictedEntity(ServerPredictedEntity entity)
        {
            if (entity == null)
                return;
            
            uint id = entity.id;
            Debug.Log($"[PredictionManager][AddPredictedEntity] SERVER ({id})=>({entity})");
            
            _serverEntityToId[entity] = id;
            _idToServerEntity[id] = entity;
            AddPredictedEntity(entity.gameObject);
            
            if (useServerWorldStateMessage)
            {
                _worldStateRecord.Resize(_serverEntityToId.Count);
            }
        }
        
        public void RemovePredictedEntity(ServerPredictedEntity entity)
        {
            if (entity == null)
                return;
            
            if (autoTrackRigidbodies)
            {
                PhysicsController.Untrack(entity.rigidbody);
            }
            
            SetEntityOwner(entity, invalidConnectionId);
            if (_serverEntityToId.ContainsKey(entity))
            {
                uint id = _serverEntityToId[entity];
                _serverEntityToId.Remove(entity);
                _idToServerEntity.Remove(id);
            }
            
            _serverEntityToId.Remove(entity);
            _entityToOwnerConnId.Remove(entity);
            RemovePredictedEntity(entity.gameObject);
            if (useServerWorldStateMessage)
            {
                _worldStateRecord.Resize(_serverEntityToId.Count);
            }
            Debug.Log($"[PredictionManager][RemovePredictedEntity] entity:{entity}");
        }
        
        public bool IsServerOwned(ServerPredictedEntity svEnt)
        {
            //NOTE: can't simplify with
            if (_entityToOwnerConnId.TryGetValue(svEnt, out int cid))
            {
                return cid == serverConnectionId;
            }
            return false;
        }
        
        public override uint GetServerTickId()
        {
            return tickId;
        }
        
        void MarkLatestAppliedTickId(uint tid, ServerPredictedEntity entity)
        {
            //FUDO: performance
            if (!_entityToOwnerConnId.ContainsKey(entity))
                return;
            
            int connId = _entityToOwnerConnId[entity];
            _connIdToLatestTick[connId] = tid;
            
            if (LOG_PRE_SIM_STATE)
            {
                Debug.Log($"[SV][PRESIMULATION][DATA] i:{entity.id} t:{tickId} p:{entity.rigidbody.position.ToString("F10")} r:{entity.rigidbody.rotation.ToString("F10")}");
            }
        }
        
        //FODO: performance
        uint GetLatestAppliedTickForConnection(int connId)
        {
            return _connIdToLatestTick.GetValueOrDefault(connId, tickId);
        }

        public void OnHeartbeatReceived(int connectionId, uint tid)
        {
            _connIdToLatestTick[connectionId] = tid;
        }
        
        public void OnClientStateReceived(int connId, uint clientTickId, uint entityId, PredictionInputRecord tickInputRecord)
        {
            if (connId != 0)
                clientStatesReceived++;

            if (_idToServerEntity.TryGetValue(entityId, out ServerPredictedEntity entity))
            {
                int ownerId = GetOwner(entity);
                if (ownerId != connId)
                {
                    Debug.LogWarning($"[PredictionManager][OnClientStateReceiver][POTENTIAL_EXPLOIT_ATTEMPT] CLIENT_UPDATE_FOR_NON_OWNERD_ENTITY connId:{connId} != ownerId:{ownerId} cTickId:{clientTickId} entityId:{entityId}");
                    return;
                }
                
                if (DEBUG)
                    Debug.Log($"[PredictionManager][OnClientStateReceiver] connId:{connId} clientTickId:{clientTickId} entityId:{entityId} tickInputRecord:{tickInputRecord} ENTITY:{entity}");
                entity?.BufferClientTick(clientTickId, tickInputRecord);
            }
        }

        void SendServerState(uint entityId, PhysicsStateRecord stateRecord)
        {
            IEnumerable<int> connections = connectionsIterator();
            foreach (int connId in connections)
            {
                uint connTickId = GetLatestAppliedTickForConnection(connId);
                try
                {
                    stateRecord.tickId = connTickId;
                    unreliableServerStateSender?.Invoke(connId, entityId, stateRecord);
                }
                catch (Exception e)
                {
                    ServerUpdateSendError err;
                    err.exception = e;
                    err.entityId = entityId;
                    err.connId = connId;
                    err.tickId = connTickId;
                    onServerStateSendError.Dispatch(err);
                }   
            }
        }
        
        void AccumulateWorldState(uint entityId, PhysicsStateRecord stateRecord)
        {
            _worldStateRecord.Set(entityId, stateRecord);
        }

        void SendWorldState(WorldStateRecord record)
        {
            IEnumerable<int> connections = connectionsIterator();
            record.serverTickId = tickId;
            foreach (int connId in connections)
            {
                uint connTickId = GetLatestAppliedTickForConnection(connId);
                try
                {
                    record.tickId = connTickId;
                    unreliableServerWorldStateSender?.Invoke(connId, record);
                }
                catch (Exception e)
                {
                    ServerUpdateSendError err;
                    err.exception = e;
                    err.entityId = 0;
                    err.connId = connId;
                    err.tickId = connTickId;
                    onServerStateSendError.Dispatch(err);
                }   
            }
        }
        
        public override void Clear()
        {
            base.Clear();
            _serverEntityToId.Clear();
            _idToServerEntity.Clear();
            _entityToOwnerConnId.Clear();
            _connIdToEntity.Clear();
            _connIdToLatestTick.Clear();
        }
    }
}