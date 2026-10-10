// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if (UNITY_EDITOR)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sector0.Ursitoare.Components;
using Sector0.Ursitoare.Data;
using Sector0.Ursitoare.Resimulation.Detection;
using Sector0.Ursitoare.Tests.mocks;
using UnityEngine;

namespace Sector0.Ursitoare.Tests
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
        //Every ownership message the server sent: (connId, entityId, controlledLocally)
        List<(int connId, uint entityId, bool owned)> ownershipMessages = new List<(int, uint, bool)>();

        //Optional second client (connection 2), created by CreateSecondClient
        ClientPredictionManager managerClient2;
        ClientPredictedEntity[] client2Entities;

        //TODO: these tests may not be relevant
        [SetUp]
        public void SetUp()
        {
            followersSqrDistance = ClientPredictionManager.RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD;
            preciseFollowersSqrDistance = ClientPredictionManager.RESIMULATE_PRECISE_FOLLOWERS_SQR_DISTANCE_THRESHOLD;
            serverUseBuffering = ServerPredictedEntity.USE_BUFFERING;
            serverBufferFullThreshold = ServerPredictedEntity.BUFFER_FULL_THRESHOLD;
            connections = Array.Empty<int>();
            stateTicksSentToClient.Clear();
            stateEntitiesSentToClient.Clear();
            ownershipMessages.Clear();
            managerClient2 = null;
            client2Entities = null;
            physicsController = new MockPhysicsController();
            
            managerClient = new ClientPredictionManager(clientHeartbeatSenderBridge, clientStateSenderBridge);
            managerServer = new ServerPredictionManager(-1, 0, ServerSendStateBridge, ServerWorldStateSender, ServerSetControlledLocally, connectionsIterator);
            
            managerClient.SetPhysicsController(physicsController);
            managerServer.SetPhysicsController(physicsController);
        }

        [TearDown]
        public void TearDown()
        {
            ClientPredictionManager.RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = followersSqrDistance;
            ClientPredictionManager.RESIMULATE_PRECISE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = preciseFollowersSqrDistance;
            ServerPredictedEntity.USE_BUFFERING = serverUseBuffering;
            ServerPredictedEntity.BUFFER_FULL_THRESHOLD = serverBufferFullThreshold;
            managerServer.Clear();
            managerClient.Clear();
            managerClient2?.Clear();
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
            ownershipMessages.Add((connId, entityId, controlledLocally));
            if (connId == 1)
            {
                managerClient.OnEntityOwnershipChanged(entityId, controlledLocally);
            }
            else if (connId == 2)
            {
                managerClient2?.OnEntityOwnershipChanged(entityId, controlledLocally);
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
        
        #region OWNERSHIP BOOKKEEPING

        //Connections ownership can be indexed under on the server. 0 is the server itself.
        static readonly int[] KNOWN_CONNECTIONS = { 0, 1, 2, 3 };

        public enum ReleaseMode
        {
            //UnsetOwnership(entity, owner): entity falls back to the server
            ReleaseToServer,
            //UnsetOwnership(entity): entity is left without an owner
            Revoke,
            //SetEntityOwner(entity, server)
            GiveToServer,
        }

        void CreateSecondClient()
        {
            managerClient2 = new ClientPredictionManager(
                tid => managerServer.OnHeartbeatReceived(2, tid),
                (tid, entityId, record) => managerServer.OnClientStateReceived(2, tid, entityId, record));
            managerClient2.SetPhysicsController(physicsController);

            client2Entities = new ClientPredictedEntity[serverEntities.Length];
            for (int i = 0; i < serverEntities.Length; ++i)
            {
                GameObject visuals = new GameObject("Client2Visual_" + i);
                extraGOs.Add(visuals);
                Rigidbody rb = CreateExtraBody("Client2_predicted_" + i);
                client2Entities[i] = new ClientPredictedEntity((uint)i, false, BUFFER_SIZE, rb, visuals, Array.Empty<PredictableControllableComponent>(), Array.Empty<PredictableComponent>());
                managerClient2.AddPredictedEntity(client2Entities[i]);
            }
        }

        ClientPredictionManager ClientOf(int connId)
        {
            if (connId == 1)
                return managerClient;
            if (connId == 2)
                return managerClient2;
            return null;
        }

        ClientPredictedEntity[] ClientEntitiesOf(int connId)
        {
            return connId == 1 ? clientEntities : client2Entities;
        }

        HashSet<ServerPredictedEntity> OwnedOnServer(int connId)
        {
            return managerServer.GetEntitiesByOwner(connId) ?? new HashSet<ServerPredictedEntity>();
        }

        int MessagesTo(int connId, int entity, bool owned)
        {
            return ownershipMessages.Count(m => m.connId == connId && m.entityId == (uint)entity && m.owned == owned);
        }

        //Checks a single entity's owner is consistent across every server index and every client.
        void AssertOwner(int i, int expectedOwner)
        {
            ServerPredictedEntity sv = serverEntities[i];
            Assert.AreEqual(expectedOwner, managerServer.GetOwner(sv), $"Server owner of entity {i}");
            Assert.AreEqual(expectedOwner == 0, managerServer.IsServerOwned(sv), $"Server owned flag of entity {i}");
            foreach (int connId in KNOWN_CONNECTIONS)
            {
                Assert.AreEqual(connId == expectedOwner, OwnedOnServer(connId).Contains(sv), $"Entity {i} indexed under connection {connId} on the server");
            }

            foreach (int connId in new[] { 1, 2 })
            {
                ClientPredictionManager client = ClientOf(connId);
                if (client == null)
                    continue;

                bool expected = connId == expectedOwner;
                ClientPredictedEntity cl = ClientEntitiesOf(connId)[i];
                Assert.AreEqual(expected, client.IsControlledLocally((uint)i), $"Client {connId} id ownership of entity {i}");
                Assert.AreEqual(expected, client.IsControlledLocally(cl), $"Client {connId} local entity set contains entity {i}");
                Assert.AreEqual(expected, cl.isControlledLocally, $"Client {connId} entity {i} isControlledLocally");
            }
        }

        //Checks the full set of entities owned by a connection on the server and, for a client connection, on that client.
        void AssertOwnedBy(int connId, params int[] entities)
        {
            CollectionAssert.AreEquivalent(entities.Select(i => serverEntities[i]), OwnedOnServer(connId), $"Server entities owned by connection {connId}");

            ClientPredictionManager client = ClientOf(connId);
            if (client == null)
                return;

            ClientPredictedEntity[] ents = ClientEntitiesOf(connId);
            CollectionAssert.AreEquivalent(entities.Select(i => ents[i]), client.GetLocalEntities(), $"Client {connId} local entities");
            Assert.AreEqual(entities.Length > 0, client.HasLocallyControlledEntities(), $"Client {connId} has locally controlled entities");
        }

        void Release(int i, int ownerId, ReleaseMode mode)
        {
            switch (mode)
            {
                case ReleaseMode.ReleaseToServer:
                    managerServer.UnsetOwnership(serverEntities[i], ownerId);
                    break;
                case ReleaseMode.Revoke:
                    managerServer.UnsetOwnership(serverEntities[i]);
                    break;
                case ReleaseMode.GiveToServer:
                    managerServer.SetEntityOwner(serverEntities[i], 0);
                    break;
            }
        }

        [Test]
        public void SetSameOwnerTwiceIsNoOpOnBothSides()
        {
            CreateEntities(2);
            managerServer.SetEntityOwner(serverEntities[0], 1);
            managerServer.OnClientStateReceived(1, 5, 0, new PredictionInputRecord(0, 0));
            Assert.AreEqual(1, serverEntities[0].BufferFill(), "Test setup: owner input was not buffered");

            managerServer.SetEntityOwner(serverEntities[0], 1);

            AssertOwner(0, 1);
            AssertOwner(1, -1);
            AssertOwnedBy(1, 0);
            Assert.AreEqual(1, MessagesTo(1, 0, true), "Ownership re-sent to the client");
            Assert.AreEqual(0, MessagesTo(1, 0, false), "Client told it lost ownership");
            //Re-setting the same owner must not reset the entity's input stream
            Assert.AreEqual(1, serverEntities[0].BufferFill(), "Re-setting the same owner wiped the buffered client input");
        }

        [Test]
        public void SetServerAsOwnerTwiceIsNoOp()
        {
            CreateEntities(1);
            managerServer.SetEntityOwner(serverEntities[0], 0);
            managerServer.SetEntityOwner(serverEntities[0], 0);

            AssertOwner(0, 0);
            AssertOwnedBy(0, 0);
            AssertOwnedBy(1);
            Assert.AreEqual(1, MessagesTo(0, 0, true));
        }

        [Test]
        public void ReleaseOwnershipRepeatedlyStaysReleased()
        {
            CreateEntities(2);
            managerServer.SetEntityOwner(serverEntities[0], 1);
            managerServer.SetEntityOwner(serverEntities[1], 1);

            for (int n = 0; n < 3; ++n)
            {
                managerServer.UnsetOwnership(serverEntities[0], 1);
            }

            AssertOwner(0, 0);
            AssertOwner(1, 1);
            AssertOwnedBy(0, 0);
            AssertOwnedBy(1, 1);
            Assert.AreEqual(1, MessagesTo(1, 0, false), "Client told more than once that it lost ownership");
            Assert.AreEqual(1, MessagesTo(0, 0, true), "Entity handed back to the server more than once");

            //Fully revoking the now server owned entity, also repeatedly
            managerServer.UnsetOwnership(serverEntities[0]);
            managerServer.UnsetOwnership(serverEntities[0]);

            AssertOwner(0, -1);
            AssertOwner(1, 1);
            AssertOwnedBy(0);
            AssertOwnedBy(1, 1);
        }

        [Test]
        public void ReleaseOwnershipByNonOwnerIsIgnored()
        {
            CreateEntities(1);
            managerServer.SetEntityOwner(serverEntities[0], 1);

            managerServer.UnsetOwnership(serverEntities[0], 2);
            managerServer.UnsetOwnership(serverEntities[0], 0);
            managerServer.UnsetOwnership(serverEntities[0], -1);

            AssertOwner(0, 1);
            AssertOwnedBy(1, 0);
            Assert.AreEqual(0, MessagesTo(1, 0, false));
        }

        [TestCase(ReleaseMode.ReleaseToServer, 0)]
        [TestCase(ReleaseMode.Revoke, -1)]
        [TestCase(ReleaseMode.GiveToServer, 0)]
        public void MultipleEntitiesOwnedByOneClientReleasedOneByOne(ReleaseMode mode, int ownerAfterRelease)
        {
            CreateEntities(4);
            for (int i = 0; i < 4; ++i)
            {
                managerServer.SetEntityOwner(serverEntities[i], 1);
            }
            AssertOwnedBy(1, 0, 1, 2, 3);

            List<int> remaining = new List<int> { 0, 1, 2, 3 };
            List<int> released = new List<int>();
            foreach (int i in new[] { 1, 3, 0, 2 })
            {
                Release(i, 1, mode);
                remaining.Remove(i);
                released.Add(i);

                AssertOwnedBy(1, remaining.ToArray());
                foreach (int r in remaining)
                    AssertOwner(r, 1);
                foreach (int r in released)
                    AssertOwner(r, ownerAfterRelease);
                if (ownerAfterRelease == 0)
                    AssertOwnedBy(0, released.ToArray());

                Assert.DoesNotThrow(() => managerClient.Tick(), $"Client tick after releasing entity {i}");
            }

            Assert.AreEqual(false, managerClient.HasLocallyControlledEntities());
            for (int i = 0; i < 4; ++i)
            {
                Assert.AreEqual(1, MessagesTo(1, i, true), $"Ownership grants for entity {i}");
                Assert.AreEqual(1, MessagesTo(1, i, false), $"Ownership revokes for entity {i}");
            }
        }

        [Test]
        public void SomeEntitiesOfOneClientReassignedToAnotherClient()
        {
            CreateEntities(4);
            CreateSecondClient();
            for (int i = 0; i < 4; ++i)
            {
                managerServer.SetEntityOwner(serverEntities[i], 1);
            }

            managerServer.SetEntityOwner(serverEntities[1], 2);
            managerServer.SetEntityOwner(serverEntities[3], 2);

            AssertOwnedBy(1, 0, 2);
            AssertOwnedBy(2, 1, 3);
            AssertOwner(0, 1);
            AssertOwner(1, 2);
            AssertOwner(2, 1);
            AssertOwner(3, 2);
            Assert.AreEqual(1, MessagesTo(1, 1, false));
            Assert.AreEqual(1, MessagesTo(1, 3, false));
            Assert.AreEqual(1, MessagesTo(2, 1, true));
            Assert.AreEqual(1, MessagesTo(2, 3, true));

            //Each client drives exactly the entities it owns, and the server accepts each of those inputs
            uint[] updatesBefore = serverEntities.Select(e => e.clUpdateCount).ToArray();
            Assert.DoesNotThrow(() => managerClient.Tick());
            Assert.DoesNotThrow(() => managerClient2.Tick());
            for (int i = 0; i < 4; ++i)
            {
                Assert.AreEqual(updatesBefore[i] + 1, serverEntities[i].clUpdateCount, $"Server accepted input for entity {i}");
            }

            //Partially swap back
            managerServer.SetEntityOwner(serverEntities[1], 1);
            managerServer.SetEntityOwner(serverEntities[0], 2);

            AssertOwnedBy(1, 1, 2);
            AssertOwnedBy(2, 0, 3);
            for (int i = 0; i < 4; ++i)
            {
                AssertOwner(i, i == 0 || i == 3 ? 2 : 1);
            }

            //Second client hands everything back to the server
            managerServer.UnsetOwnership(serverEntities[0], 2);
            managerServer.UnsetOwnership(serverEntities[3], 2);

            AssertOwnedBy(1, 1, 2);
            AssertOwnedBy(2);
            AssertOwnedBy(0, 0, 3);
            Assert.DoesNotThrow(() => managerClient2.Tick());
        }

        [Test]
        public void OwnershipPingPongBetweenClientsEndsConsistent()
        {
            const int ROUNDS = 5;
            CreateEntities(2);
            CreateSecondClient();
            managerServer.SetEntityOwner(serverEntities[1], 1);

            for (int n = 0; n < ROUNDS; ++n)
            {
                managerServer.SetEntityOwner(serverEntities[0], 1);
                managerServer.SetEntityOwner(serverEntities[0], 2);
            }

            AssertOwner(0, 2);
            AssertOwner(1, 1);
            AssertOwnedBy(1, 1);
            AssertOwnedBy(2, 0);
            Assert.AreEqual(ROUNDS, MessagesTo(1, 0, true));
            Assert.AreEqual(ROUNDS, MessagesTo(1, 0, false));
            Assert.AreEqual(ROUNDS, MessagesTo(2, 0, true));
            Assert.AreEqual(ROUNDS - 1, MessagesTo(2, 0, false));
        }

        [Test]
        public void TransferredEntityClearsInputStreamAndAcceptsNewOwnerTicks()
        {
            ServerPredictedEntity.USE_BUFFERING = false;
            CreateEntities(1);
            PredictionInputRecord input = new PredictionInputRecord(0, 0);
            ServerPredictedEntity sv = serverEntities[0];

            managerServer.SetEntityOwner(sv, 1);
            managerServer.OnClientStateReceived(1, 100, 0, input);
            managerServer.Tick();
            managerServer.OnClientStateReceived(1, 101, 0, input);
            Assert.AreEqual(100, sv.GetClientTickId(), "Test setup: server did not apply the first owner's input");
            Assert.AreEqual(1, sv.BufferFill(), "Test setup: first owner's next input was not buffered");

            managerServer.SetEntityOwner(sv, 2);
            Assert.AreEqual(0, sv.BufferFill(), "Previous owner's buffered input survived the transfer");
            Assert.AreEqual(0, sv.GetClientTickId(), "Previous owner's tick id survived the transfer");

            //New owner's tick ids are unrelated to the previous owner's and must not be treated as late
            managerServer.OnClientStateReceived(2, 5, 0, input);
            Assert.AreEqual(1, sv.BufferFill(), "New owner's input rejected");
            Assert.AreEqual(0, sv.lateTickCount, "New owner's input treated as late");

            //Previous owner is no longer accepted
            managerServer.OnClientStateReceived(1, 102, 0, input);
            Assert.AreEqual(1, sv.BufferFill(), "Previous owner's input accepted after the transfer");

            managerServer.Tick();
            Assert.AreEqual(5, sv.GetClientTickId());
        }

        #endregion

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
            ClientPredictionManager.RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = 10 * 10;
            ClientPredictionManager.RESIMULATE_PRECISE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = 5 * 5;

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

        //TODO: more tests
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
