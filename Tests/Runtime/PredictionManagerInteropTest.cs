// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if (UNITY_EDITOR)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Prediction.Components;
using Prediction.Components.Controllers;
using Prediction.Data;
using Prediction.Interpolation;
using Prediction.Resimulation.Detection;
using Prediction.Tests.mocks;
using UnityEngine;

namespace Prediction.Tests
{
    public class PredictionManagerInteropTest
    {
        MockPhysicsController physicsController;
        SimpleConfigurableResimulationDecider resimDecider = new SimpleConfigurableResimulationDecider();
        ClientPredictionManager managerClient;
        ServerPredictionManager managerServer;

        float followersSqrDistance;
        float preciseFollowersSqrDistance;
        bool serverUseBuffering;
        int serverBufferFullThreshold;

        //Connections the server broadcasts state to. Connection 1 is the client.
        int[] connections;
        //Tick ids the server stamped on the states sent to the client (connection 1)
        List<uint> stateTicksSentToClient = new List<uint>();
        //Entity ids of the states sent to the client (connection 1)
        List<uint> stateEntitiesSentToClient = new List<uint>();
        //Objects created by individual tests outside of CreateEntities
        List<GameObject> extraGOs = new List<GameObject>();

        //TODO: these tests may not be relevant
        [SetUp]
        public void SetUp()
        {
            followersSqrDistance = PredictionManager.RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD;
            preciseFollowersSqrDistance = PredictionManager.RESIMULATE_PRECISE_FOLLOWERS_SQR_DISTANCE_THRESHOLD;
            serverUseBuffering = ServerPredictedEntity.USE_BUFFERING;
            serverBufferFullThreshold = ServerPredictedEntity.BUFFER_FULL_THRESHOLD;
            connections = Array.Empty<int>();
            stateTicksSentToClient.Clear();
            stateEntitiesSentToClient.Clear();
            physicsController = new MockPhysicsController();
            
            managerClient = new ClientPredictionManager(clientHeartbeatSenderBridge, clientStateSenderBridge);
            managerServer = new ServerPredictionManager(-1, 0, ServerSendStateBridge, ServerWorldStateSender, ServerSetControlledLocally, connectionsIterator);
            
            managerClient.SetPhysicsController(physicsController);
            managerServer.SetPhysicsController(physicsController);
        }

        [TearDown]
        public void TearDown()
        {
            PredictionManager.RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = followersSqrDistance;
            PredictionManager.RESIMULATE_PRECISE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = preciseFollowersSqrDistance;
            ServerPredictedEntity.USE_BUFFERING = serverUseBuffering;
            ServerPredictedEntity.BUFFER_FULL_THRESHOLD = serverBufferFullThreshold;
            managerServer.Clear();
            managerClient.Clear();
            DestroyAll(svVisuals);
            DestroyAll(clVisuals);
            DestroyAll(serverGOs);
            DestroyAll(clientGOs);
            DestroyAll(extraGOs.ToArray());
            extraGOs.Clear();
        }

        void DestroyAll(GameObject[] gos)
        {
            if (gos == null)
                return;
            foreach (GameObject go in gos)
            {
                if (go)
                    GameObject.DestroyImmediate(go);
            }
        }
        
        void ServerSendStateBridge(int connId, uint entityId, PhysicsStateRecord record)
        {
            if (connId == 1)
            {
                stateTicksSentToClient.Add(record.tickId);
                stateEntitiesSentToClient.Add(entityId);
            }
            managerClient.OnServerStateReceived(entityId, record);
        }

        void ServerWorldStateSender(int connId, WorldStateRecord record)
        {
            managerClient.OnServerWorldStateReceived(record);
        }

        void ServerSetControlledLocally(int connId, uint entityId, bool controlledLocally)
        {
            if (connId == 1)
            {
                managerClient.OnEntityOwnershipChanged(entityId, controlledLocally);
            }
        }

        IEnumerable<int> connectionsIterator()
        {
            return connections;
        }

        //Action<uint> clientHeartbeadSender,
        //Action<uint, uint, PredictionInputRecord> clientStateSender
        void clientHeartbeatSenderBridge(uint connId)
        {
            managerServer.OnHeartbeatReceived(1, connId);
        }

        void clientStateSenderBridge(uint tickId, uint entityId, PredictionInputRecord record)
        {
            managerServer.OnClientStateReceived(1, tickId, entityId, record);
        }

        private GameObject[] svVisuals;
        private GameObject[] clVisuals;
        
        private GameObject[] serverGOs;
        private GameObject[] clientGOs;
        
        private ServerPredictedEntity[] serverEntities;
        private ClientPredictedEntity[] clientEntities;
        private static int BUFFER_SIZE = 10;
        
        void CreateEntities(int n, bool registerOnClient = true)
        {
            svVisuals = new GameObject[n];
            clVisuals = new GameObject[n];
            
            serverGOs = new GameObject[n];
            clientGOs = new GameObject[n];
            
            serverEntities = new ServerPredictedEntity[n];
            clientEntities = new ClientPredictedEntity[n];

            for (int i = 0; i < n; ++i)
            {
                svVisuals[i] = new GameObject("ServerVisual_" + i);
                serverGOs[i] = new GameObject("Server_predicted_" + i);
                Rigidbody srb = serverGOs[i].AddComponent<Rigidbody>();
                serverEntities[i] = new ServerPredictedEntity((uint)i, BUFFER_SIZE, srb, svVisuals[i], Array.Empty<PredictableControllableComponent>(), Array.Empty<PredictableComponent>());
                managerServer.AddPredictedEntity(serverEntities[i]);
                
                clVisuals[i] = new GameObject("ClientVisual_" + i);
                clientGOs[i] = new GameObject("Client_predicted_" + i);
                Rigidbody crb = clientGOs[i].AddComponent<Rigidbody>();
                clientEntities[i] = new ClientPredictedEntity((uint)i, false, BUFFER_SIZE, crb, clVisuals[i], Array.Empty<PredictableControllableComponent>(), Array.Empty<PredictableComponent>());
                if (registerOnClient)
                    managerClient.AddPredictedEntity(clientEntities[i]);
            }
        }

        void PlaceClientEntity(int i, Vector3 position)
        {
            clientGOs[i].transform.position = position;
            clientGOs[i].GetComponent<Rigidbody>().position = position;
        }

        [Test]
        public void UnsetOwnershipOnNotRegisteredEntity()
        {
            Assert.DoesNotThrow(() => managerServer.UnsetOwnership(null));
            
            var visuals = new GameObject("ServerVisual_");
            var go = new GameObject("Server_predicted_");
            Rigidbody srb = go.AddComponent<Rigidbody>();
            var svEnt = new ServerPredictedEntity((uint)0, BUFFER_SIZE, srb, visuals, Array.Empty<PredictableControllableComponent>(), Array.Empty<PredictableComponent>());
            
            managerServer.UnsetOwnership(svEnt);
            Assert.AreEqual(-1, managerServer.GetOwner(svEnt));
            Assert.AreEqual(false, managerServer.IsServerOwned(svEnt));
        }
        
        [Test]
        public void UnsetOwnershipOnNotOwnerEntity()
        {
            var visuals = new GameObject("ServerVisual_");
            var go = new GameObject("Server_predicted_");
            Rigidbody srb = go.AddComponent<Rigidbody>();
            var svEnt = new ServerPredictedEntity((uint)0, BUFFER_SIZE, srb, visuals, Array.Empty<PredictableControllableComponent>(), Array.Empty<PredictableComponent>());
            managerServer.AddPredictedEntity(svEnt);
            
            managerServer.UnsetOwnership(svEnt);
            Assert.AreEqual(-1, managerServer.GetOwner(svEnt));
            Assert.AreEqual(false, managerServer.IsServerOwned(svEnt));
        }
        
        [Test]
        public void SetOwnerReflectedOnBothServerAndClient()
        {
            CreateEntities(3);
            for (int i = 0; i < 3; ++i)
            {
                managerServer.SetEntityOwner(serverEntities[i], 0);
            }
            
            for (int i = 0; i < 3; ++i)
            {
                Assert.AreEqual(0, managerServer.GetOwner(serverEntities[i]));
                Assert.AreEqual(false, managerClient.IsControlledLocally(clientEntities[i]));
            }
            
            managerServer.SetEntityOwner(serverEntities[0], 1);
            Assert.AreEqual(1, managerServer.GetOwner(serverEntities[0]));
            Assert.AreEqual(true, managerClient.IsControlledLocally(clientEntities[0]));
            for (int i = 1; i < 3; ++i)
            {
                Assert.AreEqual(0, managerServer.GetOwner(serverEntities[i]));
                Assert.AreEqual(false, managerClient.IsControlledLocally(clientEntities[i]));
            }
        }
        
        [Test]
        public void SwapEntityOwnership()
        {
            CreateEntities(3);
            for (int i = 0; i < 3; ++i)
            {
                managerServer.SetEntityOwner(serverEntities[i], 0);
            }
            
            managerServer.UnsetOwnership(serverEntities[0], 0);
            managerServer.UnsetOwnership(serverEntities[1], 0);
            managerServer.UnsetOwnership(serverEntities[0], 0);
            managerServer.UnsetOwnership(serverEntities[2], 0);
            
            managerServer.SetEntityOwner(serverEntities[0], 1);
            managerServer.SetEntityOwner(serverEntities[0], 1);
            managerServer.SetEntityOwner(serverEntities[1], 2);
            
            Assert.AreEqual(1, managerServer.GetOwner(serverEntities[0]));
            Assert.AreEqual(true, managerClient.IsControlledLocally(clientEntities[0]));
            Assert.AreEqual(2, managerServer.GetOwner(serverEntities[1]));
            Assert.AreEqual(false, managerClient.IsControlledLocally(clientEntities[1]));
            Assert.AreEqual(0, managerServer.GetOwner(serverEntities[2]));
            Assert.AreEqual(false, managerClient.IsControlledLocally(clientEntities[2]));
        }
        
        //TODO: test swapping ownership, see that the buffers are cleared and new ticks are accepted correctly

        //NOTE: the following tests cover a client removing an entity it controls locally before the server revokes
        //      the ownership (e.g. the client destroys a networked rocket on its own). In the field this left the
        //      destroyed entity in the client's local entity set, and once the client took control of another entity
        //      every ClientPredictionManager.Tick threw (GetMinSqrDistToAllLocalEnts) - freezing the client simulation.
        const int ROCKET = 0;
        const int SHIP = 1;

        void ClientRemovesAndDestroys(int i)
        {
            managerClient.RemovePredictedEntity(clientEntities[i]);
            GameObject.DestroyImmediate(clientGOs[i]);
        }

        [Test]
        public void RemovedEntityIsNoLongerReportedAsLocallyControlled()
        {
            CreateEntities(2);
            managerServer.SetEntityOwner(serverEntities[ROCKET], 1);
            Assert.AreEqual(true, managerClient.IsControlledLocally(clientEntities[ROCKET]));

            //Client removes the entity without the server revoking ownership first
            managerClient.RemovePredictedEntity(clientEntities[ROCKET]);

            Assert.AreEqual(true, managerClient.IsControlledLocally((uint)ROCKET));
            Assert.AreEqual(false, managerClient.IsControlledLocally(clientEntities[ROCKET]));
            Assert.AreEqual(false, managerClient.HasLocallyControlledEntities());
            Assert.AreEqual(0, managerClient.GetLocalEntities().Count);
            Assert.AreEqual(false, clientEntities[ROCKET].isControlledLocally);
        }

        [Test]
        public void TickSurvivesDestroyedLocalEntityWhenTakingControlOfAnother()
        {
            CreateEntities(2);
            managerServer.SetEntityOwner(serverEntities[ROCKET], 1);
            Assert.DoesNotThrow(() => managerClient.Tick());

            ClientRemovesAndDestroys(ROCKET);
            //No locally controlled entities (i.e. gunner), the stale entry is never iterated
            Assert.DoesNotThrow(() => managerClient.Tick());

            //Client becomes pilot of another entity
            managerServer.SetEntityOwner(serverEntities[SHIP], 1);
            Assert.AreEqual(true, managerClient.IsControlledLocally(clientEntities[SHIP]));

            uint tickBefore = managerClient.GetTickId();
            Assert.DoesNotThrow(() => managerClient.Tick());
            Assert.AreEqual(tickBefore + 1, managerClient.GetTickId(), "Client tick did not complete");

            CollectionAssert.AreEquivalent(new[] { clientEntities[SHIP] }, managerClient.GetLocalEntities());
        }

        [Test]
        public void LateOwnershipRevokeAfterClientRemovalIsHarmless()
        {
            CreateEntities(2);
            managerServer.SetEntityOwner(serverEntities[ROCKET], 1);
            ClientRemovesAndDestroys(ROCKET);

            //Server catches up and releases the entity afterwards
            Assert.DoesNotThrow(() => managerServer.UnsetOwnership(serverEntities[ROCKET], 1));

            Assert.AreEqual(false, managerClient.IsControlledLocally((uint)ROCKET));
            Assert.AreEqual(0, managerClient.GetLocalEntities().Count);
            Assert.DoesNotThrow(() => managerClient.Tick());
        }

        //NOTE: ownership is a server fact about an entity id. The client keeps it across its own deregister/register
        //      (e.g. OnDisable -> OnEnable with the same ClientPredictedEntity instance). Only a server revoke or Clear() drops it.
        //      While deregistered, the entity is not reported as locally controlled.
        [Test]
        public void ReRegisteredOwnedEntityResumesLocalControl()
        {
            CreateEntities(2);
            managerServer.SetEntityOwner(serverEntities[ROCKET], 1);
            managerClient.RemovePredictedEntity(clientEntities[ROCKET]);

            managerClient.AddPredictedEntity(clientEntities[ROCKET]);

            Assert.AreEqual(true, managerClient.IsControlledLocally((uint)ROCKET));
            Assert.AreEqual(true, managerClient.IsControlledLocally(clientEntities[ROCKET]));
            Assert.AreEqual(true, clientEntities[ROCKET].isControlledLocally);
            CollectionAssert.AreEquivalent(new[] { clientEntities[ROCKET] }, managerClient.GetLocalEntities());

            //Client drives it again: inputs reach the server (which never stopped considering the client the owner)
            Assert.DoesNotThrow(() => managerClient.Tick());
            Assert.Greater(serverEntities[ROCKET].BufferFill(), 0, "Server received no client input for the owned entity");
        }

        [Test]
        public void OwnershipRevokedWhileDeregisteredIsNotResumed()
        {
            CreateEntities(2);
            managerServer.SetEntityOwner(serverEntities[ROCKET], 1);
            managerClient.RemovePredictedEntity(clientEntities[ROCKET]);

            managerServer.UnsetOwnership(serverEntities[ROCKET], 1);
            managerClient.AddPredictedEntity(clientEntities[ROCKET]);

            Assert.AreEqual(false, managerClient.IsControlledLocally((uint)ROCKET));
            Assert.AreEqual(false, clientEntities[ROCKET].isControlledLocally);
            Assert.AreEqual(0, managerClient.GetLocalEntities().Count);
            //A follower still flagged as locally controlled throws COMPONENT_MISUSE in ClientFollowerSimulationTick
            Assert.DoesNotThrow(() => managerClient.Tick());
        }

        [Test]
        public void ServerRemovingOwnedEntityRevokesClientOwnership()
        {
            CreateEntities(2);
            managerServer.SetEntityOwner(serverEntities[ROCKET], 1);

            managerServer.RemovePredictedEntity(serverEntities[ROCKET]);

            //Client still has the entity registered (despawn message not yet received), but no longer owns it
            Assert.AreEqual(false, managerClient.IsControlledLocally((uint)ROCKET));
            Assert.AreEqual(false, managerClient.IsControlledLocally(clientEntities[ROCKET]));
            Assert.AreEqual(false, clientEntities[ROCKET].isControlledLocally);

            HashSet<ServerPredictedEntity> owned = managerServer.GetEntitiesByOwner(1);
            Assert.IsTrue(owned == null || !owned.Contains(serverEntities[ROCKET]), "Removed entity still indexed under its owner");
        }

        [Test]
        public void ServerDespawnFollowedByClientDespawnLeavesNoStaleOwnership()
        {
            //Regular despawn order: server removes the entity, then the client receives the despawn
            CreateEntities(2);
            managerServer.SetEntityOwner(serverEntities[ROCKET], 1);

            managerServer.RemovePredictedEntity(serverEntities[ROCKET]);
            ClientRemovesAndDestroys(ROCKET);

            managerServer.SetEntityOwner(serverEntities[SHIP], 1);
            Assert.DoesNotThrow(() => managerClient.Tick());
            CollectionAssert.AreEquivalent(new[] { clientEntities[SHIP] }, managerClient.GetLocalEntities());
        }

        [Test]
        public void OwnershipGrantedBeforeClientRegistrationIsApplied()
        {
            CreateEntities(1, false);
            Assert.AreEqual(false, managerClient.IsControlledLocally((uint)0));

            //Ownership message arrives before the client registered the entity
            managerServer.SetEntityOwner(serverEntities[0], 1);
            managerClient.AddPredictedEntity(clientEntities[0]);

            Assert.AreEqual(true, managerClient.IsControlledLocally((uint)0));
            Assert.AreEqual(true, managerClient.IsControlledLocally(clientEntities[0]));
            Assert.AreEqual(true, clientEntities[0].isControlledLocally);
            //Client must drive it as the local authority, otherwise the server waits for inputs that never come
            Assert.DoesNotThrow(() => managerClient.Tick());
            Assert.Greater(serverEntities[0].BufferFill(), 0, "Server received no client input for the owned entity");
        }

        [Test]
        public void ClearReleasesLocalControlOnEntities()
        {
            CreateEntities(1);
            managerServer.SetEntityOwner(serverEntities[0], 1);
            Assert.AreEqual(true, clientEntities[0].isControlledLocally);

            managerClient.Clear();
            Assert.DoesNotThrow(() => managerClient.Tick());
            
            Assert.AreEqual(0, managerClient.GetLocalEntities().Count);

            //Entity surviving the clear registers again as a follower
            managerClient.AddPredictedEntity(clientEntities[0]);
            Assert.DoesNotThrow(() => managerClient.Tick());
        }

        [Test]
        public void FollowerResimulationUsesDistanceToLocalEntities()
        {
            const int LOCAL = 0, NEAR = 1, FAR = 2;
            PredictionManager.RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = 10 * 10;
            PredictionManager.RESIMULATE_PRECISE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = 5 * 5;

            CreateEntities(3);
            PlaceClientEntity(LOCAL, Vector3.zero);
            PlaceClientEntity(NEAR, new Vector3(1, 0, 0));
            PlaceClientEntity(FAR, new Vector3(1000, 0, 0));
            managerServer.SetEntityOwner(serverEntities[LOCAL], 1);

            managerClient.ComputePredictionDecision(out uint _);

            Assert.AreEqual(true, clientEntities[NEAR].predictAsFollower);
            Assert.AreEqual(true, clientEntities[NEAR].usePreciseResimChecker);
            Assert.AreEqual(false, clientEntities[FAR].predictAsFollower, "Follower 1000m away from the local entity is predicted as if near");
            Assert.AreEqual(false, clientEntities[FAR].usePreciseResimChecker);
        }

        static int PrivateCollectionCount(object owner, string fieldName)
        {
            FieldInfo field = owner.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"{owner.GetType().Name}.{fieldName} not found - update this test if the resimulation bookkeeping storage changed");
            return ((ICollection)field.GetValue(owner)).Count;
        }

        [Test]
        public void ResimulationBookkeepingStaysBoundedOverLongSessions()
        {
            const int TICKS = 500;
            CreateEntities(1);
            managerServer.SetEntityOwner(serverEntities[0], 1);

            for (int i = 0; i < TICKS; ++i)
            {
                uint tickId = managerClient.GetTickId();
                if (tickId > 1)
                {
                    //Server state for the previous tick disagreeing with the client: one resimulation per tick
                    PhysicsStateRecord serverState = PhysicsStateRecord.Alloc();
                    serverState.tickId = tickId - 1;
                    serverState.position = new Vector3(1000 + tickId, 0, 0);
                    managerClient.OnServerStateReceived(0, serverState);
                }
                managerClient.Tick();
            }

            Assert.Greater(managerClient.totalResimulations, (uint)(TICKS / 2), "Test setup did not produce resimulations");
            //Only ticks that can still be rewound to need a resimulation count - it must not grow with session length
            Assert.Less(PrivateCollectionCount(managerClient, "tickResimCounter"), 100, "ClientPredictionManager resimulation counters grow unbounded");
            Assert.Less(PrivateCollectionCount(clientEntities[0], "tickResimCounter"), 100, "ClientPredictedEntity resimulation counters grow unbounded");
        }

        //NOTE: the server stamps every state it sends a client with the latest client tick it applied for that client.
        //      After the client takes control of an entity, that entity buffers its first inputs and reports tick 0,
        //      which must not overwrite the tick already known for the connection.
        [Test]
        public void StateTickSentToClientDoesNotRegressWhenSwitchingOwnedEntity()
        {
            ServerPredictedEntity.USE_BUFFERING = true;
            ServerPredictedEntity.BUFFER_FULL_THRESHOLD = 3;
            connections = new[] { 1 };
            CreateEntities(2);
            PredictionInputRecord input = new PredictionInputRecord(0, 0);
            uint clientTick = 100;

            managerServer.SetEntityOwner(serverEntities[ROCKET], 1);
            for (int i = 0; i < 10; ++i)
            {
                managerServer.OnClientStateReceived(1, clientTick++, (uint)ROCKET, input);
                managerServer.Tick();
            }
            uint lastTickBeforeSwitch = stateTicksSentToClient[stateTicksSentToClient.Count - 1];
            Assert.Greater(lastTickBeforeSwitch, 0u, "Test setup: server never applied the client's input");
            stateTicksSentToClient.Clear();

            //Rocket ends, client goes back to piloting the ship
            managerServer.UnsetOwnership(serverEntities[ROCKET], 1);
            managerServer.SetEntityOwner(serverEntities[SHIP], 1);
            for (int i = 0; i < 6; ++i)
            {
                managerServer.OnClientStateReceived(1, clientTick++, (uint)SHIP, input);
                managerServer.Tick();
            }

            Assert.IsNotEmpty(stateTicksSentToClient);
            foreach (uint sentTick in stateTicksSentToClient)
            {
                Assert.GreaterOrEqual(sentTick, lastTickBeforeSwitch, "State sent to the client stamped with a tick older than already acknowledged");
            }
        }

        [Test]
        public void StateTickSentToClientDoesNotRegressWhenOwningSecondEntity()
        {
            ServerPredictedEntity.USE_BUFFERING = true;
            ServerPredictedEntity.BUFFER_FULL_THRESHOLD = 3;
            connections = new[] { 1 };
            CreateEntities(2);
            PredictionInputRecord input = new PredictionInputRecord(0, 0);
            uint clientTick = 100;

            managerServer.SetEntityOwner(serverEntities[ROCKET], 1);
            for (int i = 0; i < 10; ++i)
            {
                managerServer.OnClientStateReceived(1, clientTick++, (uint)ROCKET, input);
                managerServer.Tick();
            }
            uint lastTickBeforeSecond = stateTicksSentToClient[stateTicksSentToClient.Count - 1];
            Assert.Greater(lastTickBeforeSecond, 0u, "Test setup: server never applied the client's input");
            stateTicksSentToClient.Clear();

            //Client keeps the first entity and also takes control of a second one
            managerServer.SetEntityOwner(serverEntities[SHIP], 1);
            for (int i = 0; i < 6; ++i)
            {
                managerServer.OnClientStateReceived(1, clientTick, (uint)ROCKET, input);
                managerServer.OnClientStateReceived(1, clientTick, (uint)SHIP, input);
                clientTick++;
                managerServer.Tick();
            }

            Assert.IsNotEmpty(stateTicksSentToClient);
            foreach (uint sentTick in stateTicksSentToClient)
            {
                Assert.GreaterOrEqual(sentTick, lastTickBeforeSecond, "State sent to the client stamped with a tick older than already acknowledged");
            }
        }

        //NOTE: an exception thrown while processing one entity must not abort the tick for all other entities.
        class ThrowingComponent : PredictableComponent
        {
            public void ApplyForces()
            {
                throw new Exception("ThrowingComponent.ApplyForces");
            }

            public bool HasState()
            {
                return false;
            }

            public void SampleComponentState(PhysicsStateRecord physicsStateRecord)
            {
            }

            public void LoadComponentState(PhysicsStateRecord physicsStateRecord)
            {
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

        const uint BROKEN = 100;

        Rigidbody CreateExtraBody(string name)
        {
            GameObject go = new GameObject(name);
            extraGOs.Add(go);
            return go.AddComponent<Rigidbody>();
        }

        [Test]
        public void ClientTickSurvivesEntityThatThrows()
        {
            //Registered first so it is processed before the healthy entity
            Rigidbody brokenBody = CreateExtraBody("Client_broken");
            ClientPredictedEntity broken = new ClientPredictedEntity(BROKEN, false, BUFFER_SIZE, brokenBody, brokenBody.gameObject, Array.Empty<PredictableControllableComponent>(), new PredictableComponent[] { new ThrowingComponent() });
            managerClient.AddPredictedEntity(broken);
            CreateEntities(1);

            int healthyStates = 0;
            clientEntities[0].newStateReached.AddEventListener(_ => healthyStates++);

            uint tickBefore = managerClient.GetTickId();
            Assert.DoesNotThrow(() => managerClient.Tick());
            Assert.AreEqual(tickBefore + 1, managerClient.GetTickId(), "Client tick did not complete");
            Assert.AreEqual(1, healthyStates, "Healthy entity was not simulated");
        }

        [Test]
        public void ServerTickSurvivesEntityThatThrows()
        {
            connections = new[] { 1 };
            //Registered first so it is processed before the healthy entity
            Rigidbody brokenBody = CreateExtraBody("Server_broken");
            ServerPredictedEntity broken = new ServerPredictedEntity(BROKEN, BUFFER_SIZE, brokenBody, brokenBody.gameObject, Array.Empty<PredictableControllableComponent>(), new PredictableComponent[] { new ThrowingComponent() });
            managerServer.AddPredictedEntity(broken);
            CreateEntities(1);

            uint tickBefore = managerServer.GetTickId();
            Assert.DoesNotThrow(() => managerServer.Tick());
            Assert.AreEqual(tickBefore + 1, managerServer.GetTickId(), "Server tick did not complete");
            CollectionAssert.Contains(stateEntitiesSentToClient, 0u, "Healthy entity state was not sent to the client");
        }

        /*
         
        [Test]
        public void TestHappyPath()
        {
            var inputs = new [] { Vector3.zero, Vector3.right * 5, Vector3.up * 5, Vector3.right * 5, Vector3.up * 5, Vector3.right * 5, Vector3.up * 5, Vector3.right * 5, Vector3.up * 5 };
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                clientComponent.inputVector = inputs[tickId];
                PredictionInputRecord record = clientEntity.ClientSimulationTick(tickId);
                clientEntity.SamplePhysicsState(tickId);
                serverEntity.BufferClientTick(tickId, record);
                serverEntity.ServerSimulationTick();
                PhysicsStateRecord serverRecord = serverEntity.SamplePhysicsState(tickId);
                clientEntity.BufferServerTick(tickId, serverRecord);
                Assert.AreEqual(tickId, serverRecord.tickId);
                Assert.AreEqual(serverRecord.position, clientRigidbody.position);
                Assert.AreEqual(serverRigidbody.position, clientRigidbody.position);
            }
        }
        
        [Test]
        public void TestFollowerOnAnotherClientReceivesAndPredictsWithTheOwnersInput()
        {
            var inputs = new [] { Vector3.zero, Vector3.right * 5, Vector3.up * 5, Vector3.right * 5, Vector3.up * 5, Vector3.right * 5, Vector3.up * 5, Vector3.right * 5, Vector3.up * 5 };
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                clientComponent.inputVector = inputs[tickId];
                PredictionInputRecord record = clientEntity.ClientSimulationTick(tickId);
                clientEntity.SamplePhysicsState(tickId);

                serverEntity.BufferClientTick(tickId, record);
                serverEntity.ServerSimulationTick();
                PhysicsStateRecord serverRecord = serverEntity.SamplePhysicsState(tickId);

                //The owner's input travelled to the server and back out to the other client.
                Assert.IsNotNull(serverRecord.input);

                followerEntity.BufferServerTick(tickId, WireCopy(serverRecord));
                followerEntity.ClientFollowerSimulationTick(tickId);
                followerEntity.SamplePhysicsState(tickId);

                //The follower loaded that input and simulated one tick past the state it was given.
                Assert.AreEqual(inputs[tickId], followerComponent.stateVector);
                Assert.AreEqual(serverRigidbody.position + inputs[tickId], followerRigidbody.position);
            }
        }

        [Test]
        public void TestHappyPathWithDelay()
        {
            var inputs = new [] { Vector3.zero, Vector3.right * 5, Vector3.up * 5, Vector3.right * 5, Vector3.up * 5, Vector3.right * 5, Vector3.up * 5, Vector3.right * 5, Vector3.up * 5 };
            
            int resimulationsCounter = 0;
            clientEntity.resimulation.AddEventListener((started) =>
            {
                if (started)
                    resimulationsCounter++;
            });
            //TODO: fix
            uint delay = 3;
            for (uint tickId = 1; tickId < delay; ++tickId)
            {
                clientComponent.inputVector = inputs[tickId];
                PredictionInputRecord record = clientEntity.ClientSimulationTick(tickId);
                clientEntity.SamplePhysicsState(tickId);
                serverEntity.BufferClientTick(tickId, record);
            }
            for (uint tickId = delay; tickId < inputs.Length; ++tickId)
            {
                clientComponent.inputVector = inputs[tickId];
                PredictionInputRecord record = clientEntity.ClientSimulationTick(tickId);
                clientEntity.SamplePhysicsState(tickId);
                serverEntity.BufferClientTick(tickId, record);
                serverEntity.ServerSimulationTick();
                PhysicsStateRecord serverRecord = serverEntity.SamplePhysicsState(tickId);
                clientEntity.BufferServerTick(tickId, serverRecord);
            }
            clientComponent.rigidbody = null;
            for (uint tickId = (uint) inputs.Length; tickId < inputs.Length + delay; tickId++)
            {
                clientComponent.inputVector = inputs[tickId % inputs.Length];
                PredictionInputRecord record = clientEntity.ClientSimulationTick(tickId);
                clientEntity.SamplePhysicsState(tickId);
                serverEntity.BufferClientTick(tickId, record);
                serverEntity.ServerSimulationTick();
                PhysicsStateRecord serverRecord = serverEntity.SamplePhysicsState(tickId);
                clientEntity.BufferServerTick(tickId, serverRecord);
            }

            Assert.AreEqual(0, resimulationsCounter);
            Assert.AreEqual(serverRigidbody.position, clientRigidbody.position);
        }
         */
    }
}
#endif
