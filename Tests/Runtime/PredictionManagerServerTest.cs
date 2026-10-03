// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if (UNITY_EDITOR)
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Prediction.Components.Controllers;
using Prediction.Data;
using Prediction.Resimulation.Detection;
using Prediction.Tests.mocks;
using UnityEngine;

namespace Prediction.Tests
{
    public class PredictionManagerServerTest
    {
        GameObject server1;
        Rigidbody serverRigidbody1;
        MockPredictableControllableComponent serverComponent1;
        ServerPredictedEntity serverEntity1;
        
        GameObject server2;
        Rigidbody serverRigidbody2;
        MockPredictableControllableComponent serverComponent2;
        ServerPredictedEntity serverEntity2;
        
        MockPhysicsController physicsController;
        SimpleConfigurableResimulationDecider resimDecider = new SimpleConfigurableResimulationDecider();
        ServerPredictionManager manager;

        int clientSends = 0;
        int clientHearatbeatSends = 0;
        int serverSends = 0;
        int serverWorldSends = 0;

        [SetUp]
        public void SetUp()
        {
            physicsController = new MockPhysicsController();
            
            server1 = new GameObject("test");
            server1.transform.position = Vector3.zero;
            serverRigidbody1 = server1.AddComponent<Rigidbody>();
            serverComponent1 = new MockPredictableControllableComponent();
            serverComponent1.rigidbody = serverRigidbody1;
            serverEntity1 = new ServerPredictedEntity(1, 20, serverRigidbody1, server1, new []{serverComponent1}, new[]{serverComponent1});
            ServerPredictedEntity.USE_BUFFERING = false;
            
            server2 = new GameObject("test2");
            server2.transform.position = Vector3.zero;
            serverRigidbody2 = server2.AddComponent<Rigidbody>();
            serverComponent2 = new MockPredictableControllableComponent();
            serverComponent2.rigidbody = serverRigidbody2;
            serverEntity2 = new ServerPredictedEntity(2, 20, serverRigidbody2, server2, new []{serverComponent2}, new[]{serverComponent2});

            clientSends = clientHearatbeatSends = serverWorldSends = serverSends = 0;
            manager = MakeMgr(null, null);
        }

        ServerPredictionManager MakeMgr(Action<int, uint, PhysicsStateRecord> serverStateSenderOverride, Action<int, WorldStateRecord> serverWorldStateSenderOverride)
        {
            var mgr = new ServerPredictionManager(-1, 0, 
                (serverStateSenderOverride == null) ? (a, b, c) => { serverSends++; } : serverStateSenderOverride, 
                (serverWorldStateSenderOverride == null) ? (a, b) => { serverWorldSends++; } : serverWorldStateSenderOverride, 
                (a, b, c) => { }, 
                () => { return new int[] { 1, 2, 3 }; });
            mgr.SetPhysicsController(physicsController);
            mgr.AddPredictedEntity(serverEntity1);
            mgr.AddPredictedEntity(serverEntity2);
            
            mgr.SetEntityOwner(serverEntity1, 1);
            mgr.SetEntityOwner(serverEntity2, 2);
            return mgr;
        }

        public class StateUpdate
        {
            public int connId;
            public uint entityId;
            public PhysicsStateRecord state;

            public static StateUpdate FindBy(int connID, uint entityID, List<StateUpdate> states)
            {
                foreach (StateUpdate state in states)
                {
                    if (state.connId == connID && state.entityId == entityID)
                    {
                        return state;
                    }
                }
                return null;
            }
        }
        
        [Test]
        public void CheckUpdates_WithClientInput_TicksDoesNotIncreaseWithoutClientInput_ExceptForServerOwnedObjects()
        {
            
            manager.useServerWorldStateMessage = false;
            Dictionary<int, List<StateUpdate>> sentStates = new Dictionary<int, List<StateUpdate>>();
            manager = MakeMgr((connId, entityId, state) =>
            {
                if (!sentStates.ContainsKey(connId))
                {
                    sentStates[connId] = new List<StateUpdate>();
                }
                
                StateUpdate su = new StateUpdate();
                su.connId = connId;
                su.entityId = entityId;
                su.state = PhysicsStateRecord.Alloc();
                su.state.From(state);
                sentStates[connId].Add(su);
            }, null);
            
            manager.Tick();
            //No input = no movement
            Assert.AreEqual(Vector3.zero, serverRigidbody1.position);
            Assert.AreEqual(Vector3.zero, serverRigidbody2.position);
            
            //2 entities, 3 connections
            Assert.AreEqual(3, sentStates.Count);
            Assert.AreEqual(2, sentStates[1].Count);
            Assert.AreEqual(2, sentStates[2].Count);
            Assert.AreEqual(2, sentStates[3].Count);
            
            StateUpdate su11 = StateUpdate.FindBy(1, 1, sentStates[1]);
            StateUpdate su12 = StateUpdate.FindBy(1, 2, sentStates[1]);
            StateUpdate su21 = StateUpdate.FindBy(2, 1, sentStates[2]);
            StateUpdate su22 = StateUpdate.FindBy(2, 2, sentStates[2]);
            StateUpdate su31 = StateUpdate.FindBy(3, 1, sentStates[3]);
            StateUpdate su32 = StateUpdate.FindBy(3, 2, sentStates[3]);
            
            Assert.AreEqual(0, su11.state.tickId);
            Assert.AreEqual(0, su12.state.tickId);
            Assert.AreEqual(0, su21.state.tickId);
            Assert.AreEqual(0, su22.state.tickId);
            Assert.AreEqual(1, su31.state.tickId);
            Assert.AreEqual(1, su32.state.tickId);
            sentStates.Clear();

            PredictionInputRecord pir1 = new PredictionInputRecord(3, 0);
            pir1.WriteReset();
            pir1.WriteNextScalar(1);
            pir1.WriteNextScalar(0);
            pir1.WriteNextScalar(0);
            
            PredictionInputRecord pir2 = new PredictionInputRecord(3, 0);
            pir2.WriteReset();
            pir2.WriteNextScalar(-1);
            pir2.WriteNextScalar(0);
            pir2.WriteNextScalar(0);
            
            manager.OnClientStateReceived(1, 10, 1, pir1);
            manager.OnClientStateReceived(2, 15, 2, pir2);
            manager.Tick();
            
            //No input = no movement
            Assert.AreEqual(-Vector3.left, serverRigidbody1.position);
            Assert.AreEqual(Vector3.left, serverRigidbody2.position);
            
            //2 entities, 3 connections
            Assert.AreEqual(3, sentStates.Count);
            Assert.AreEqual(2, sentStates[1].Count);
            Assert.AreEqual(2, sentStates[2].Count);
            Assert.AreEqual(2, sentStates[3].Count);
            
            su11 = StateUpdate.FindBy(1, 1, sentStates[1]);
            su12 = StateUpdate.FindBy(1, 2, sentStates[1]);
            su21 = StateUpdate.FindBy(2, 1, sentStates[2]);
            su22 = StateUpdate.FindBy(2, 2, sentStates[2]);
            su31 = StateUpdate.FindBy(3, 1, sentStates[3]);
            su32 = StateUpdate.FindBy(3, 2, sentStates[3]);
            
            Assert.AreEqual(10, su11.state.tickId);
            Assert.AreEqual(10, su12.state.tickId);
            Assert.AreEqual(15, su21.state.tickId);
            Assert.AreEqual(15, su22.state.tickId);
            Assert.AreEqual(2, su31.state.tickId);
            Assert.AreEqual(2, su32.state.tickId);
            sentStates.Clear();
            manager.Tick();
            
            su11 = StateUpdate.FindBy(1, 1, sentStates[1]);
            su12 = StateUpdate.FindBy(1, 2, sentStates[1]);
            su21 = StateUpdate.FindBy(2, 1, sentStates[2]);
            su22 = StateUpdate.FindBy(2, 2, sentStates[2]);
            su31 = StateUpdate.FindBy(3, 1, sentStates[3]);
            su32 = StateUpdate.FindBy(3, 2, sentStates[3]);
            
            Assert.AreEqual(10, su11.state.tickId);
            Assert.AreEqual(10, su12.state.tickId);
            Assert.AreEqual(15, su21.state.tickId);
            Assert.AreEqual(15, su22.state.tickId);
            Assert.AreEqual(3, su31.state.tickId);
            Assert.AreEqual(3, su32.state.tickId);
            sentStates.Clear();
            
            manager.OnClientStateReceived(1, 12, 1, pir1);
            manager.OnClientStateReceived(2, 16, 2, pir2);
            manager.Tick();
            
            su11 = StateUpdate.FindBy(1, 1, sentStates[1]);
            su12 = StateUpdate.FindBy(1, 2, sentStates[1]);
            su21 = StateUpdate.FindBy(2, 1, sentStates[2]);
            su22 = StateUpdate.FindBy(2, 2, sentStates[2]);
            su31 = StateUpdate.FindBy(3, 1, sentStates[3]);
            su32 = StateUpdate.FindBy(3, 2, sentStates[3]);
            
            Assert.AreEqual(12, su11.state.tickId);
            Assert.AreEqual(12, su12.state.tickId);
            Assert.AreEqual(16, su21.state.tickId);
            Assert.AreEqual(16, su22.state.tickId);
            Assert.AreEqual(4, su31.state.tickId);
            Assert.AreEqual(4, su32.state.tickId);
        }
        
        
        [Test]
        public void CheckUpdates_EveryConnectionReceivesTheInputOfEveryEntity()
        {
            manager.useServerWorldStateMessage = false;
            List<StateUpdate> sentStates = new List<StateUpdate>();
            manager = MakeMgr((connId, entityId, state) =>
            {
                StateUpdate su = new StateUpdate();
                su.connId = connId;
                su.entityId = entityId;
                //NOTE: keeping the record itself, the way an integration serialises it on the spot.
                su.state = state;
                sentStates.Add(su);
            }, null);

            PredictionInputRecord pir1 = new PredictionInputRecord(3, 0);
            pir1.WriteReset();
            pir1.WriteNextScalar(1);
            pir1.WriteNextScalar(0);
            pir1.WriteNextScalar(0);

            PredictionInputRecord pir2 = new PredictionInputRecord(3, 0);
            pir2.WriteReset();
            pir2.WriteNextScalar(-1);
            pir2.WriteNextScalar(0);
            pir2.WriteNextScalar(0);

            //Connection 1 drives entity 1, connection 2 drives entity 2, connection 3 drives nothing.
            manager.OnClientStateReceived(1, 10, 1, pir1);
            manager.OnClientStateReceived(2, 15, 2, pir2);
            manager.Tick();

            Assert.AreEqual(6, sentStates.Count);
            foreach (int connId in new[] { 1, 2, 3 })
            {
                //Every connection gets the input of both entities, including the ones it does not own.
                Assert.AreSame(pir1, StateUpdate.FindBy(connId, 1, sentStates).state.input);
                Assert.AreSame(pir2, StateUpdate.FindBy(connId, 2, sentStates).state.input);
            }
        }

        
        [Test]
        public void CheckWorldState_CarriesTheInputOfEveryEntity()
        {
            Dictionary<int, List<WorldStateRecord>> sentWorldStates = new();
            manager = MakeMgr(null, (connId, worldState) =>
            {
                if (!sentWorldStates.ContainsKey(connId))
                {
                    sentWorldStates.Add(connId, new List<WorldStateRecord>());
                }
                sentWorldStates[connId].Add(worldState);
            });
            manager.useServerWorldStateMessage = true;
            //NOTE: the batch buffer is sized while entities register, so re-register them after the mode switch.
            manager.AddPredictedEntity(serverEntity1);
            manager.AddPredictedEntity(serverEntity2);
            manager.SetEntityOwner(serverEntity1, 1);
            
            PredictionInputRecord pir1 = new PredictionInputRecord(3, 0);
            pir1.WriteReset();
            pir1.WriteNextScalar(1);
            pir1.WriteNextScalar(0);
            pir1.WriteNextScalar(0);

            manager.OnClientStateReceived(1, 10, 1, pir1);
            manager.Tick();

            //One batch per connection, each holding a state per entity.
            Assert.AreEqual(3, sentWorldStates.Count);
            foreach (List<WorldStateRecord> worldStatesPerCon in sentWorldStates.Values)
            {
                foreach (WorldStateRecord worldState in worldStatesPerCon)
                {
                    Assert.AreEqual(2, worldState.fill);
                    for (int i = 0; i < worldState.fill; i++)
                    {
                        if (worldState.entityIDs[i] == 1)
                        {
                            Assert.AreSame(pir1, worldState.states[i].input);
                        }
                    }
                }   
            }
        }

        [Test]
        public void TestOwnershipSetAndUnset()
        {
            manager.SetEntityOwner(serverEntity1, 3);
            Assert.AreEqual(3, manager.GetOwner(serverEntity1));
            Assert.AreEqual(2, manager.GetOwner(serverEntity2));
            
            manager.UnsetOwnership(serverEntity1, 3);
            Assert.AreEqual(0, manager.GetOwner(serverEntity1));
            Assert.AreEqual(2, manager.GetOwner(serverEntity2));

            manager.SetEntityOwner(serverEntity1, 1);
            Assert.AreEqual(1, manager.GetOwner(serverEntity1));
            Assert.AreEqual(2, manager.GetOwner(serverEntity2));
            Assert.AreEqual(serverEntity1, manager.GetEntity(1));
            
            manager.UnsetOwnership(serverEntity1);
            Assert.AreEqual(-1, manager.GetOwner(serverEntity1));
            Assert.AreEqual(2, manager.GetOwner(serverEntity2));
        }
        
        [Test]
        public void TestOwnershipSwap()
        {
            manager.SetEntityOwner(serverEntity1, 3);
            Assert.AreEqual(3, manager.GetOwner(serverEntity1));
            Assert.AreEqual(2, manager.GetOwner(serverEntity2));
            
            manager.SetEntityOwner(serverEntity2, 3);
            Assert.AreEqual(3, manager.GetOwner(serverEntity1));
            Assert.AreEqual(3, manager.GetOwner(serverEntity2));
            
            manager.SetEntityOwner(serverEntity1, 0);
            Assert.AreEqual(0, manager.GetOwner(serverEntity1));
            Assert.AreEqual(3, manager.GetOwner(serverEntity2));
            
            manager.SetEntityOwner(serverEntity2, 0);
            Assert.AreEqual(0, manager.GetOwner(serverEntity1));
            Assert.AreEqual(0, manager.GetOwner(serverEntity2));
        }
        
        #region CLIENT UPDATE OWNERSHIP VALIDATION

        PredictionInputRecord MakeInput(float x)
        {
            PredictionInputRecord pir = new PredictionInputRecord(3, 0);
            pir.WriteReset();
            pir.WriteNextScalar(x);
            pir.WriteNextScalar(0);
            pir.WriteNextScalar(0);
            return pir;
        }

        [Test]
        public void ClientUpdate_FromOwner_IsBuffered()
        {
            manager.OnClientStateReceived(1, 10, 1, MakeInput(1));
            manager.OnClientStateReceived(2, 10, 2, MakeInput(-1));

            Assert.AreEqual(1, serverEntity1.BufferFill());
            Assert.AreEqual(1, serverEntity1.clUpdateCount);
            Assert.AreEqual(1, serverEntity2.BufferFill());
            Assert.AreEqual(1, serverEntity2.clUpdateCount);
        }

        [Test]
        public void ClientUpdate_FromOtherOwningConnection_IsRejected()
        {
            //Connection 2 owns entity 2, but claims to be driving entity 1.
            manager.OnClientStateReceived(2, 10, 1, MakeInput(-1));

            Assert.AreEqual(0, serverEntity1.BufferFill());
            Assert.AreEqual(0, serverEntity1.clUpdateCount);
            //The spoofed update must not leak into the spoofer's own entity either.
            Assert.AreEqual(0, serverEntity2.BufferFill());
            Assert.AreEqual(0, serverEntity2.clUpdateCount);
        }

        [Test]
        public void ClientUpdate_FromConnectionWithoutEntities_IsRejected()
        {
            //Connection 3 is connected but owns nothing.
            manager.OnClientStateReceived(3, 10, 1, MakeInput(1));
            manager.OnClientStateReceived(3, 10, 2, MakeInput(1));

            Assert.AreEqual(0, serverEntity1.clUpdateCount);
            Assert.AreEqual(0, serverEntity1.BufferFill());
            Assert.AreEqual(0, serverEntity2.clUpdateCount);
            Assert.AreEqual(0, serverEntity2.BufferFill());
        }

        [Test]
        public void ClientUpdate_ForEntityWithoutOwner_IsRejected()
        {
            GameObject server3 = new GameObject("test3");
            Rigidbody serverRigidbody3 = server3.AddComponent<Rigidbody>();
            MockPredictableControllableComponent serverComponent3 = new MockPredictableControllableComponent();
            serverComponent3.rigidbody = serverRigidbody3;
            ServerPredictedEntity serverEntity3 = new ServerPredictedEntity(3, 20, serverRigidbody3, server3, new []{serverComponent3}, new[]{serverComponent3});
            manager.AddPredictedEntity(serverEntity3);

            manager.OnClientStateReceived(1, 10, 3, MakeInput(1));
            manager.OnClientStateReceived(3, 10, 3, MakeInput(1));

            Assert.AreEqual(0, serverEntity3.clUpdateCount);
        }

        [Test]
        public void ClientUpdate_ForUnknownEntity_IsIgnoredWithoutAffectingOthers()
        {
            Assert.DoesNotThrow(() => manager.OnClientStateReceived(1, 10, 99, MakeInput(1)));

            Assert.AreEqual(0, serverEntity1.clUpdateCount);
            Assert.AreEqual(0, serverEntity2.clUpdateCount);
        }

        [Test]
        public void ClientUpdate_ConnectionOwningMultipleEntities_IsAcceptedForEach()
        {
            manager.SetEntityOwner(serverEntity1, 1);
            manager.SetEntityOwner(serverEntity2, 1);

            manager.OnClientStateReceived(1, 10, 1, MakeInput(1));
            manager.OnClientStateReceived(1, 10, 2, MakeInput(1));
            
            //Connection 2 no longer owns entity 2.
            manager.SetEntityOwner(serverEntity2, 0);
            manager.OnClientStateReceived(2, 11, 2, MakeInput(-1));

            Assert.AreEqual(1, serverEntity1.clUpdateCount);
            Assert.AreEqual(1, serverEntity2.clUpdateCount);
        }

        [Test]
        public void ClientUpdate_AfterOwnershipTransfer_OnlyNewOwnerIsAccepted()
        {
            manager.SetEntityOwner(serverEntity1, 3);

            manager.OnClientStateReceived(1, 10, 1, MakeInput(-1));
            Assert.AreEqual(0, serverEntity1.clUpdateCount);

            manager.OnClientStateReceived(3, 10, 1, MakeInput(1));
            Assert.AreEqual(1, serverEntity1.clUpdateCount);
            Assert.AreEqual(1, serverEntity1.BufferFill());
        }

        [Test]
        public void ClientUpdate_AfterOwnerReleasesEntity_IsRejected()
        {
            manager.UnsetOwnership(serverEntity1, 1);
            Assert.AreEqual(0, manager.GetOwner(serverEntity1));

            manager.OnClientStateReceived(1, 10, 1, MakeInput(1));

            Assert.AreEqual(0, serverEntity1.clUpdateCount);
        }

        [Test]
        public void ClientUpdate_Spoofed_DoesNotOverrideOwnerInputInSimulation()
        {
            manager.useServerWorldStateMessage = false;

            //Spoofed updates arrive after the owner's for the same tick, so they would overwrite it if accepted.
            manager.OnClientStateReceived(1, 10, 1, MakeInput(1));
            manager.OnClientStateReceived(2, 10, 1, MakeInput(-1));
            manager.OnClientStateReceived(3, 10, 1, MakeInput(-1));
            manager.Tick();

            Assert.AreEqual(Vector3.right, serverRigidbody1.position);
            Assert.AreEqual(Vector3.zero, serverRigidbody2.position);
        }

        [Test]
        public void ClientUpdate_SpoofedOnly_DoesNotMoveEntity()
        {
            manager.useServerWorldStateMessage = false;

            manager.OnClientStateReceived(2, 10, 1, MakeInput(1));
            manager.OnClientStateReceived(3, 11, 1, MakeInput(1));
            manager.Tick();
            manager.Tick();

            Assert.AreEqual(Vector3.zero, serverRigidbody1.position);
            Assert.AreEqual(Vector3.zero, serverRigidbody2.position);
        }

        #endregion

        [Test]
        public void TestOwnershipSwap2()
        {
            manager.SetEntityOwner(serverEntity2, 0);
            manager.SetEntityOwner(serverEntity1, 3);
            Assert.AreEqual(3, manager.GetOwner(serverEntity1));
            Assert.AreEqual(0, manager.GetOwner(serverEntity2));
        }
    }
}
#endif
