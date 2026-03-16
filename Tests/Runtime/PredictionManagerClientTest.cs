// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if (UNITY_EDITOR)
using System.Collections.Generic;
using NUnit.Framework;
using Prediction.Components;
using Prediction.Components.Controllers;
using Prediction.Data;
using Prediction.Resimulation.Detection;
using Prediction.Tests.mocks;
using Prediction.Tests.Mocks;
using UnityEngine;

namespace Prediction.Tests
{
    public class PredictionManagerClientTest
    {
        GameObject test;
        Rigidbody rigidbody;
        MockPhysicsController physicsController;
        MockPredictableControllableComponent component;
        SimpleConfigurableResimulationDecider resimDecider = new SimpleConfigurableResimulationDecider();
        PredictionManager manager;

        int clientSends = 0;
        int clientHearatbeatSends = 0;
        int serverSends = 0;
        int serverWorldSends = 0;

        [SetUp]
        public void SetUp()
        {
            test = new GameObject("test");
            test.transform.position = Vector3.zero;
            rigidbody = test.AddComponent<Rigidbody>();
            
            component = new MockPredictableControllableComponent();
            component.rigidbody = rigidbody;
            physicsController = new MockPhysicsController();

            clientSends = clientHearatbeatSends = serverWorldSends = serverSends = 0;
            manager = new PredictionManager();
            PredictionManager.PHYSICS_CONTROLLER = physicsController;
            manager.clientStateSender = (a, b) => { clientSends++; };
            manager.clientHeartbeadSender = (a) => { clientHearatbeatSends++; };
            manager.serverStateSender = (a, b, c) => { serverSends++;  };
            manager.serverWorldStateSender = (a, b) => { serverWorldSends++; };
            manager.Setup(false, true);

        }

        [TearDown]
        public void TeaDown()
        {
            rigidbody = null;
            GameObject.Destroy(test);
            test = null;
            component = null;
            physicsController = null;
        }

        [Test]
        public void TestResimulationDecision()
        {
            MockClientPredictedEntity mock1 = new MockClientPredictedEntity(0, false, 20, rigidbody, test, new PredictableControllableComponent[0], new PredictableComponent[0]);
            MockClientPredictedEntity mock2 = new MockClientPredictedEntity(1,false, 20, rigidbody, test, new PredictableControllableComponent[0], new PredictableComponent[0]);
            MockClientPredictedEntity mock3 = new MockClientPredictedEntity(2,false, 20, rigidbody, test, new PredictableControllableComponent[0], new PredictableComponent[0]);
            MockClientPredictedEntity mock4 = new MockClientPredictedEntity(3,false, 20, rigidbody, test, new PredictableControllableComponent[0], new PredictableComponent[0]);
            MockClientPredictedEntity mock5 = new MockClientPredictedEntity(4,false, 20, rigidbody, test, new PredictableControllableComponent[0], new PredictableComponent[0]);
            MockClientPredictedEntity mock6 = new MockClientPredictedEntity(5,false, 20, rigidbody, test, new PredictableControllableComponent[0], new PredictableComponent[0]);
            
            manager.AddPredictedEntity(mock1);
            manager.AddPredictedEntity(mock2);
            manager.AddPredictedEntity(mock3);
            manager.AddPredictedEntity(mock4);
            manager.AddPredictedEntity(mock5);
            manager.AddPredictedEntity(mock6);
            
            mock1._predictionDecision = PredictionDecision.NOOP;       
            mock2._predictionDecision = PredictionDecision.SNAP;
            mock3._predictionDecision = PredictionDecision.RESIMULATE;
            mock4._predictionDecision = PredictionDecision.SNAP;
            mock5._predictionDecision = PredictionDecision.RESIMULATE;
            mock6._predictionDecision = PredictionDecision.NOOP;
            
            mock1._fromTick = 1;
            mock2._fromTick = 2;
            mock3._fromTick = 3;
            mock4._fromTick = 4;
            mock5._fromTick = 5;
            mock6._fromTick = 6;

            manager.OnEntityOwnershipChanged(1, true);
            Assert.AreEqual(PredictionDecision.RESIMULATE, manager.ComputePredictionDecision(out uint resimFrom1));
            Assert.AreEqual(3, resimFrom1);
            
            manager.OnEntityOwnershipChanged(2, true);
            Assert.AreEqual(PredictionDecision.RESIMULATE, manager.ComputePredictionDecision(out uint resimFrom2));
            Assert.AreEqual(3, resimFrom2);
            
            manager.OnEntityOwnershipChanged(3, true);
            Assert.AreEqual(PredictionDecision.RESIMULATE, manager.ComputePredictionDecision(out uint resimFrom3));
            Assert.AreEqual(3, resimFrom3);
            
            mock1._predictionDecision = PredictionDecision.NOOP;       
            mock2._predictionDecision = PredictionDecision.SNAP;
            mock3._predictionDecision = PredictionDecision.SNAP;
            mock4._predictionDecision = PredictionDecision.SNAP;
            mock5._predictionDecision = PredictionDecision.SNAP;
            mock6._predictionDecision = PredictionDecision.NOOP;
            
            Assert.AreEqual(PredictionDecision.SNAP, manager.ComputePredictionDecision(out uint resimFrom4));
            
            manager.OnEntityOwnershipChanged(1, true);
            Assert.AreEqual(PredictionDecision.SNAP, manager.ComputePredictionDecision(out uint resimFrom5));
            
            mock1._predictionDecision = PredictionDecision.SNAP;       
            mock2._predictionDecision = PredictionDecision.NOOP;
            mock3._predictionDecision = PredictionDecision.NOOP;
            mock4._predictionDecision = PredictionDecision.NOOP;
            mock5._predictionDecision = PredictionDecision.NOOP;
            mock6._predictionDecision = PredictionDecision.NOOP;
                
            Assert.AreEqual(PredictionDecision.SNAP, manager.ComputePredictionDecision(out uint resimFrom6));
            
            manager.OnEntityOwnershipChanged(3, true);
            
            Assert.AreEqual(PredictionDecision.SNAP, manager.ComputePredictionDecision(out uint resimFrom7));
            
            mock1._predictionDecision = PredictionDecision.RESIMULATE;
            Assert.AreEqual(PredictionDecision.RESIMULATE, manager.ComputePredictionDecision(out uint resimFrom8));
            Assert.AreEqual(1, resimFrom8);
            
            manager.OnEntityOwnershipChanged(1, true);
            Assert.AreEqual(PredictionDecision.RESIMULATE, manager.ComputePredictionDecision(out uint resimFrom9));
            Assert.AreEqual(1, resimFrom9);
            
            mock1._predictionDecision = PredictionDecision.NOOP;
            Assert.AreEqual(PredictionDecision.NOOP, manager.ComputePredictionDecision(out uint resimFrom10));
        }
        
        [Test]
        public void TestServerDriftAndResimulate()
        {            
            MockClientPredictedEntity mock1 = new MockClientPredictedEntity(1, false, 20, rigidbody, test, new []{component}, new[]{component});
            mock1.SetControlledLocally(true);
            mock1.SetSingleStateEligibilityCheckHandler(resimDecider.Check);
            mock1.decisionPassThrough = true;
            
            manager.AddPredictedEntity(mock1);
            manager.OnEntityOwnershipChanged(1, true);
            
            var inputs = new []       { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.up, Vector3.right,  Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up, Vector3.right,   Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverInputs = new [] { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.up,    Vector3.up,  Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up, Vector3.up,      Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverTicks = ClientPredictedEntityTest.GenerateServerStates(serverInputs, rigidbody);
            Dictionary<int, Vector3> posCorrections = new Dictionary<int, Vector3>();
            posCorrections.Add(5, serverTicks[5].position);
            posCorrections.Add(11, serverTicks[11].position);
            
            int serverDelay = 3;
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                component.inputVector = inputs[tickId];
                manager.Tick();
                if (tickId > serverDelay)
                {
                    manager.OnServerStateReceived(1, serverTicks[tickId - serverDelay]);
                }
                Assert.AreEqual(tickId + 1, manager.tickId);
            }
            
            Assert.AreEqual(serverTicks[serverTicks.Length - 1].position, rigidbody.position);
            Assert.AreEqual(2, manager.totalResimulations);
            Vector3[] expectedPosStream = ClientPredictedEntityTest.ComputePosStream(serverInputs, posCorrections);
            for (int i = 1; i < inputs.Length; ++i)
            {
                //TODO: can we make it sample on the same tick?
                if (i < inputs.Length - 1)
                    Assert.AreEqual(expectedPosStream[i], mock1.localStateBuffer.Get(i).position);
                Assert.AreEqual(inputs[i], ClientPredictedEntityTest.GetInputFromInputRecord(mock1.localInputBuffer.Get(i)));
                if (i < inputs.Length - serverDelay)
                    Assert.AreEqual(serverTicks[i].position, mock1.serverStateBuffer.Get((uint) i).position);
            }
        }
        
        [Test]
        public void TestExactlyOneResimNeededForOneMissedInput()
        {
            MockClientPredictedEntity mock1 = new MockClientPredictedEntity(1, false, 20, rigidbody, test, new []{component}, new[]{component});
            mock1.SetControlledLocally(true);
            mock1.SetSingleStateEligibilityCheckHandler(resimDecider.Check);
            mock1.decisionPassThrough = true;

            manager.AddPredictedEntity(mock1);
            manager.OnEntityOwnershipChanged(1, true);
            
            var inputs = new []       { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.up,    Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverInputs = new [] { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.right, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverTicks = ClientPredictedEntityTest.GenerateServerStates(serverInputs, rigidbody);
            Dictionary<int, Vector3> posCorrections = new Dictionary<int, Vector3>();
            posCorrections.Add(4, serverTicks[4].position);
            
            int serverDelay = 1;
            
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                component.inputVector = inputs[tickId];
                manager.Tick();
                if (tickId > serverDelay)
                {
                    manager.OnServerStateReceived(1, serverTicks[tickId - serverDelay]);
                }
            }
            
            //TODO: move this test in PredicitonManager test
            Assert.AreEqual(1, manager.totalResimulations);
            Assert.AreEqual(serverDelay, manager.totalResimulationSteps);
            Vector3[] expectedPosStream = ClientPredictedEntityTest.ComputePosStream(serverInputs, posCorrections);
            for (int i = 1; i < inputs.Length; ++i)
            {
                //TODO: can we make it sample on the same tick?
                if (i < inputs.Length - 1)
                    Assert.AreEqual(expectedPosStream[i], mock1.localStateBuffer.Get(i).position);
                Assert.AreEqual(inputs[i], ClientPredictedEntityTest.GetInputFromInputRecord(mock1.localInputBuffer.Get(i)));
                if (i < inputs.Length - serverDelay)
                    Assert.AreEqual(serverTicks[i].position, mock1.serverStateBuffer.Get((uint) i).position);
            }
        }
        
        //TODO: implement protection!
        //TODO: revisit 
        /*
        [Test]
        public void TestMultiResimulationPrevention()
        {
            MockClientPredictedEntity mock1 = new MockClientPredictedEntity(1, false, 20, rigidbody, test, new []{component}, new[]{component});
            mock1.SetControlledLocally(true);
            mock1.SetSingleStateEligibilityCheckHandler(resimDecider.Check);
            mock1.decisionPassThrough = true;

            manager.AddPredictedEntity(mock1);
            manager.OnEntityOwnershipChanged(1, true);
            manager.protectFromOversimulation = true;
            
            int predictionAcceptable = 0;
            PhysicsStateRecord psr = new PhysicsStateRecord();
            Vector3 serverPos = Vector3.zero;
            psr.rotation = Quaternion.identity;
            for (uint tickId = 5; tickId < 105; ++tickId)
            {
                component.inputVector = Vector3.right * 2;
                serverPos += Vector3.left * 2;
                psr.position = serverPos;
                manager.Tick();
                
                PhysicsStateRecord spsr = new PhysicsStateRecord();
                spsr.From(psr);
                spsr.tickId = tickId - 4;
                manager.OnServerStateReceived(1, psr);
            }
            Assert.AreEqual(0, predictionAcceptable);
            Assert.AreEqual(67, manager.totalResimulationsSkipped); //33% resimulations
            Assert.AreEqual(33, manager.totalResimulations);
        }
        */

        [Test]
        public void TestHeartbeatSending()
        {
            //No controlled entity
            for (int i = 0; i < 100; ++i)
            {
                manager.Tick();
            }

            Assert.AreEqual(100, clientHearatbeatSends);
            Assert.AreEqual(0, clientSends);
            Assert.AreEqual(0, serverSends);
            Assert.AreEqual(0, serverWorldSends);
            
            MockClientPredictedEntity mock1 = new MockClientPredictedEntity(1, false, 20, rigidbody, test, new []{component}, new[]{component});
            mock1.SetControlledLocally(true);
            mock1.SetSingleStateEligibilityCheckHandler(resimDecider.Check);
            mock1.decisionPassThrough = true;

            clientHearatbeatSends = clientSends = serverSends = serverWorldSends = 0;
            manager.AddPredictedEntity(mock1);
            manager.OnEntityOwnershipChanged(1, true);
            
            for (int i = 0; i < 20; ++i)
            {
                manager.Tick();
            }
            
            Assert.AreEqual(0, clientHearatbeatSends);
            Assert.AreEqual(20, clientSends);
            Assert.AreEqual(0, serverSends);
            Assert.AreEqual(0, serverWorldSends);
            
            clientHearatbeatSends = clientSends = serverSends = serverWorldSends = 0;
            mock1.SetControlledLocally(false);
            manager.OnEntityOwnershipChanged(1, false);
            
            for (int i = 0; i < 20; ++i)
            {
                manager.Tick();
            }
            
            Assert.AreEqual(20, clientHearatbeatSends);
            Assert.AreEqual(0, clientSends);
            Assert.AreEqual(0, serverSends);
            Assert.AreEqual(0, serverWorldSends);
        }
        
        //TODO: test resimulation with rewind and exact positions checked as well as exact number of steps applied checked
        //TODO: test SetLocalEntity changes

        // ---- SIMULATION_FREEZE tests ----

        MockClientPredictedEntity MakeMockFollower(uint id)
        {
            return new MockClientPredictedEntity(id, false, 20, rigidbody, test,
                new PredictableControllableComponent[0], new PredictableComponent[0]);
        }

        [Test]
        public void TestComputePredictionDecisionSimulationFreezePrecedence()
        {
            MockClientPredictedEntity mock1 = MakeMockFollower(0);
            MockClientPredictedEntity mock2 = MakeMockFollower(1);
            MockClientPredictedEntity mock3 = MakeMockFollower(2);
            mock1._fromTick = 1;
            mock2._fromTick = 2;
            mock3._fromTick = 3;
            manager.AddPredictedEntity(mock1);
            manager.AddPredictedEntity(mock2);
            manager.AddPredictedEntity(mock3);

            // SIMULATION_FREEZE beats RESIMULATE
            mock1._predictionDecision = PredictionDecision.NOOP;
            mock2._predictionDecision = PredictionDecision.RESIMULATE;
            mock3._predictionDecision = PredictionDecision.SIMULATION_FREEZE;
            Assert.AreEqual(PredictionDecision.SIMULATION_FREEZE,
                manager.ComputePredictionDecision(out uint ignoreA));

            // SIMULATION_FREEZE beats SNAP
            mock2._predictionDecision = PredictionDecision.SNAP;
            Assert.AreEqual(PredictionDecision.SIMULATION_FREEZE,
                manager.ComputePredictionDecision(out uint ignoreB));

            // Removing SIMULATION_FREEZE → decision reverts to highest remaining
            mock3._predictionDecision = PredictionDecision.NOOP;
            Assert.AreNotEqual(PredictionDecision.SIMULATION_FREEZE,
                manager.ComputePredictionDecision(out uint ignoreC));
        }

        [Test]
        public void TestTickFrozenAndTickIdDoesNotIncrementOnSimulationFreeze()
        {
            MockClientPredictedEntity mock1 = MakeMockFollower(1);
            mock1._predictionDecision = PredictionDecision.SIMULATION_FREEZE;
            mock1._fromTick = 5;
            manager.AddPredictedEntity(mock1);

            uint initialTickId = manager.tickId;
            manager.Tick();

            Assert.AreEqual(initialTickId, manager.tickId,
                "TickId must not advance while the tick is frozen.");
            Assert.AreEqual(1u, manager.totalTickFreezes);

            // Consecutive frozen ticks keep tickId frozen and accumulate the counter.
            manager.Tick();
            manager.Tick();
            Assert.AreEqual(initialTickId, manager.tickId);
            Assert.AreEqual(3u, manager.totalTickFreezes);
        }

        [Test]
        public void TestNormalTickNotFrozenOnNoopDecision()
        {
            MockClientPredictedEntity mock1 = MakeMockFollower(1);
            mock1._predictionDecision = PredictionDecision.NOOP;
            manager.AddPredictedEntity(mock1);

            uint initialTickId = manager.tickId;
            manager.Tick();

            Assert.AreEqual(initialTickId + 1, manager.tickId,
                "TickId must advance on a normal (non-frozen) tick.");
            Assert.AreEqual(0u, manager.totalTickFreezes);
        }

        [Test]
        public void TestSimulationFreezeWithRealEntityAfterHistoryOverflow()
        {
            const int bufferSize = 20;
            MockClientPredictedEntity mock1 = new MockClientPredictedEntity(1, false, bufferSize, rigidbody, test,
                new[] {component}, new[] {component});
            mock1.SetControlledLocally(true);
            mock1.SetSingleStateEligibilityCheckHandler(resimDecider.Check);
            mock1.decisionPassThrough = true;

            manager.AddPredictedEntity(mock1);
            manager.OnEntityOwnershipChanged(1, true);

            // Run 25 normal ticks (no server state → NOOP each tick).
            // After tick 25: localHistoryEndTickId = 25, localHistoryStartTickId = 25 - 20 = 5.
            for (uint tick = 1; tick <= 25; tick++)
            {
                component.inputVector = Vector3.zero;
                manager.Tick();
            }
            Assert.AreEqual(26u, manager.tickId);

            // Server reports a state from tick 5 — exactly at the history boundary.
            PhysicsStateRecord oldState = PhysicsStateRecord.Alloc();
            oldState.From(rigidbody);
            oldState.tickId = 5;
            manager.OnServerStateReceived(1, oldState);

            uint frozenAt = manager.tickId; // 26
            manager.Tick();

            Assert.AreEqual(frozenAt, manager.tickId,
                "TickId must not advance while tick is frozen.");
            Assert.AreEqual(1u, manager.totalTickFreezes);

            // Two more frozen ticks while server state is still outside history.
            manager.Tick();
            manager.Tick();
            Assert.AreEqual(frozenAt, manager.tickId);
            Assert.AreEqual(3u, manager.totalTickFreezes);

            // Server now reports a state from tick 6 — just inside the history window.
            // localHistoryStartTickId is still 5, so (6 <= 5) = false → no freeze.
            PhysicsStateRecord newerState = PhysicsStateRecord.Alloc();
            newerState.From(rigidbody);
            newerState.tickId = 6;
            manager.OnServerStateReceived(1, newerState);

            manager.Tick(); // should thaw and advance normally
            Assert.AreEqual(frozenAt + 1, manager.tickId,
                "TickId should resume incrementing once the freeze is resolved.");
            Assert.AreEqual(3u, manager.totalTickFreezes,
                "No new freeze should occur after the freeze resolves.");
        }
    }
}
#endif
