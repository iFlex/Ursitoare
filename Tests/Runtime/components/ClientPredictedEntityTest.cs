// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Sector0.Ursitoare.Components;
using Sector0.Ursitoare.Data;
using Sector0.Ursitoare.Resimulation.Detection;
using Sector0.Ursitoare.Tests.mocks;
using UnityEngine;

namespace Sector0.Ursitoare.Tests
{
    public class ClientPredictedEntityTest
    {
        public static GameObject test;
        public static Rigidbody rigidbody;
        public static MockPredictableControllableComponent component;
        public static ClientPredictedEntity entity;
        public static MockPhysicsController physicsController;
        public static SimpleConfigurableResimulationDecider resimDecider = new SimpleConfigurableResimulationDecider();
        
        [SetUp]
        public void SetUp()
        {
            test = new GameObject("test");
            test.transform.position = Vector3.zero;
            rigidbody = test.AddComponent<Rigidbody>();
            
            component = new MockPredictableControllableComponent();
            component.rigidbody = rigidbody;
            physicsController = new MockPhysicsController();
            
            entity = new ClientPredictedEntity(0, false, 20, rigidbody, test, new []{component}, new[]{component});
            entity.SetSingleStateEligibilityCheckHandler(resimDecider.Check);
            entity.SetControlledLocally(true);
        }

        [TearDown]
        public void TearDown()
        {
            entity.resimulation.Clear();
            entity.resimulationStep.Clear();
            
            rigidbody = null;
            GameObject.Destroy(test);
            test = null;
            component = null;
            entity = null;
            physicsController = null;
        }

        void AssertInputStateAtTick(uint tickId, Vector3 expectedInputVector)
        {
            PredictionInputRecord inputRecord = entity.localInputBuffer.Get((int) tickId);
            inputRecord.ReadReset();
            Vector3 buffered = new Vector3(inputRecord.ReadNextScalar(), inputRecord.ReadNextScalar(), inputRecord.ReadNextScalar());
            Assert.AreEqual(expectedInputVector, buffered);
        }

        public static Vector3[] ComputePosStream(Vector3[] stream, Dictionary<int, Vector3> positionCorrections)
        {
            Vector3[] output = new Vector3[stream.Length];
            Vector3 accumulator = Vector3.zero;
            bool switched = false;
            for (int i = 0; i < stream.Length; i++)
            {
                if (positionCorrections != null && positionCorrections.ContainsKey(i))
                {
                    accumulator = positionCorrections[i];
                }
                else
                {
                    accumulator += stream[i];
                }
                output[i] = accumulator;
            }
            return output;
        }

        public static PhysicsStateRecord[] GenerateServerStates(Vector3[] inputs, Rigidbody rigidbody)
        {
            PhysicsStateRecord[] states = new PhysicsStateRecord[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
            {
                rigidbody.position += inputs[i];
                states[i] = PhysicsStateRecord.Alloc();
                states[i].From(rigidbody);
                states[i].tickId = (uint) i;
            }
            rigidbody.position = Vector3.zero;
            return states;
        }

        public static Vector3 GetInputFromInputRecord(PredictionInputRecord record)
        {
            record.ReadReset();
            return new Vector3(record.ReadNextScalar(), record.ReadNextScalar(), record.ReadNextScalar());
        }
        
        [Test]
        public void TestTickAdvanceAndInputReport()
        {
            var inputs = new [] { Vector3.zero, Vector3.left, Vector3.right, Vector3.up, Vector3.down };
            var serverTicks = GenerateServerStates(inputs, rigidbody);

            //TODO: modernize test, generate all expected positions instead of sampling physics rb & check
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                component.inputVector = inputs[tickId];
                PredictionInputRecord inputRecord = entity.ClientSimulationTick(tickId);
                entity.SamplePhysicsState(tickId);
                
                inputRecord.ReadReset();
                Vector3 sampled = new Vector3(inputRecord.ReadNextScalar(), inputRecord.ReadNextScalar(), inputRecord.ReadNextScalar());
                //Correct sampling done by tick
                Assert.AreEqual(component.inputVector, sampled);
                //Correct setting of the state
                Assert.AreEqual(component.stateVector, sampled);
                //Force application call
                Assert.AreEqual(tickId, component.forceApplyCallCount);
                
                //Buffered state
                Assert.AreEqual(serverTicks[tickId], entity.localStateBuffer.Get((int)tickId));
            }
        }

        [Test]
        public void TestHappyPath()
        {
            var inputs = new [] { Vector3.zero, Vector3.left, Vector3.left, Vector3.right, Vector3.up, Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.down, Vector3.left };
            var serverTicks = GenerateServerStates(inputs, rigidbody);
            int serverDelay = 3;
            
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                component.inputVector = inputs[tickId];
                PredictionInputRecord inputRecord = entity.ClientSimulationTick(tickId);
                entity.SamplePhysicsState(tickId);
                if (tickId >= serverDelay)
                {
                    entity.BufferServerTick(tickId, serverTicks[tickId - serverDelay]);
                }
            }

            Assert.AreEqual(inputs.Length - 1, component.forceApplyCallCount);
            for (int i = 1; i < inputs.Length; ++i)
            {
                AssertInputStateAtTick((uint) i, inputs[i]);
                if (i < inputs.Length - 1)
                    Assert.AreEqual(serverTicks[i], entity.localStateBuffer.Get(i));
                if (i < inputs.Length - serverDelay)
                    Assert.AreEqual(serverTicks[i], entity.serverStateBuffer.Get((uint) i));
            }
        }

        [Test]
        public void TestServerOutOfOrderMessages()
        {
            var inputs = new [] { Vector3.left, Vector3.left, Vector3.right, Vector3.up, Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.down, Vector3.left };
            var serverTicks = GenerateServerStates(inputs, rigidbody);
            List<int[]> serverScramble = new List<int[]>();
            serverScramble.Add(new int[0]);
            serverScramble.Add(new int[0]);
            serverScramble.Add(new int[0]);
            serverScramble.Add(new [] { 0 });
            serverScramble.Add(new [] { 2 });
            serverScramble.Add(new [] { 4 });
            serverScramble.Add(new [] { 1, 5 });
            serverScramble.Add(new [] { 3, 6 });
            serverScramble.Add(new [] { 7 });
            serverScramble.Add(new [] { 8 });
                
            for (uint tickId = 0; tickId < inputs.Length; ++tickId)
            {
                component.inputVector = inputs[tickId];
                entity.ClientSimulationTick(tickId);
                
                int[] server = serverScramble[(int) tickId];
                foreach (int i in server)
                {
                    entity.BufferServerTick(tickId, serverTicks[i]);
                }
            }
            //TODO: tests!!
        }
        
        [Test]
        public void TestServerDriftAndResimulate()
        {
            var inputs = new []       { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.up, Vector3.right,  Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up, Vector3.right,   Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverInputs = new [] { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.up,    Vector3.up,  Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up, Vector3.up,      Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverTicks = GenerateServerStates(serverInputs, rigidbody);
            Dictionary<int, Vector3> posCorrections = new Dictionary<int, Vector3>();
            posCorrections.Add(5, serverTicks[5].position);
            posCorrections.Add(11, serverTicks[11].position);
            
            int serverDelay = 3;
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                component.inputVector = inputs[tickId];
                entity.ClientSimulationTick(tickId);
                entity.SamplePhysicsState(tickId);
                if (tickId > serverDelay)
                {
                    entity.BufferServerTick(tickId, serverTicks[tickId - serverDelay]);
                }

                if (tickId >= 5 && tickId < 8)
                {
                    Assert.AreEqual(PredictionDecision.NOOP, entity.GetPredictionDecision(tickId, out uint ignore));
                }
                if (tickId >= 8)
                {
                    Assert.AreEqual(PredictionDecision.RESIMULATE, entity.GetPredictionDecision(tickId, out uint from));
                    Assert.AreEqual(tickId - serverDelay, from);
                }
            }
        }
        
        [Test]
        public void TestExactlyOneResimNeededForOneMissedInput()
        {
            var inputs = new []       { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.up,    Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverInputs = new [] { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.right, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverTicks = GenerateServerStates(serverInputs, rigidbody);
            Dictionary<int, Vector3> posCorrections = new Dictionary<int, Vector3>();
            posCorrections.Add(4, serverTicks[4].position);
            
            int serverDelay = 1;
            
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                component.inputVector = inputs[tickId];
                entity.ClientSimulationTick(tickId);
                entity.SamplePhysicsState(tickId);
                if (tickId > serverDelay)
                {
                    entity.BufferServerTick(tickId, serverTicks[tickId - serverDelay]);
                }

                if (tickId == 4)
                {
                    Assert.AreEqual(PredictionDecision.NOOP, entity.GetPredictionDecision(tickId, out uint ignore));
                }
                if (tickId >= 5)
                {
                    Assert.AreEqual(PredictionDecision.RESIMULATE, entity.GetPredictionDecision(tickId, out uint from));
                    Assert.AreEqual(tickId - serverDelay, from);
                }
            }
        }

        [Test]
        public void TestResimulationDecision()
        {
            var serverInputs = new [] { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.right, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverTicks = GenerateServerStates(serverInputs, rigidbody);
            
            component.inputVector = Vector3.zero;
            entity.ClientSimulationTick(1);
            entity.SamplePhysicsState(1);
            Assert.AreEqual(PredictionDecision.NOOP, entity.GetPredictionDecision(1, out uint ignore0));
            Assert.AreEqual(0, ignore0);
            
            entity.BufferServerTick(1, serverTicks[2]);
            entity.BufferServerTick(1, serverTicks[3]);
            
            Assert.AreEqual(PredictionDecision.NOOP, entity.GetPredictionDecision(1, out uint ignore1));
            Assert.AreEqual(PredictionDecision.NOOP, entity.GetPredictionDecision(2, out uint ignore2));
            Assert.AreEqual(PredictionDecision.NOOP, entity.GetPredictionDecision(3, out uint ignore3));
            Assert.AreEqual(PredictionDecision.RESIMULATE, entity.GetPredictionDecision(4, out uint ignore4));
            Assert.AreEqual(PredictionDecision.RESIMULATE, entity.GetPredictionDecision(5, out uint ignore5));
            Assert.AreEqual(0, ignore1);
            Assert.AreEqual(0, ignore2);
            Assert.AreEqual(0, ignore3);
            Assert.AreEqual(3, ignore4);
            Assert.AreEqual(3, ignore5);
        }

        [Test]
        public void TestSnapToServerNoData()
        {
            rigidbody.position = Vector3.one * 10;
            entity.SnapToServer(1);
            Assert.AreEqual(Vector3.one * 10, rigidbody.position);
        }
        
        [Test]
        public void TestSnapToServer()
        {
            var serverInputs = new [] { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.right, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverTicks = GenerateServerStates(serverInputs, rigidbody);
            rigidbody.position = Vector3.one * 10;
            entity.BufferServerTick(0, serverTicks[1]);
            entity.BufferServerTick(0, serverTicks[2]);
            entity.BufferServerTick(0, serverTicks[3]);
            
            entity.SnapToServer(1);
            Assert.AreEqual(serverTicks[1].position, rigidbody.position);
            entity.SnapToServer(2);
            Assert.AreEqual(serverTicks[2].position, rigidbody.position);
            entity.SnapToServer(3);
            Assert.AreEqual(serverTicks[3].position, rigidbody.position);
            entity.SnapToServer(4);
            Assert.AreEqual(serverTicks[3].position, rigidbody.position);
        }
        
        //TODO: test larger packet drop? test repeated package?

        // ---- SIMULATION_FREEZE tests ----
        // Buffer size is 20 (SetUp). After simulating 25 ticks:
        //   localHistoryEndTickId = 25, localHistoryStartTickId = 25 - 20 = 5.
        // IsTickOutsideOfLocalHistory(fromId) = (fromId <= localHistoryStartTickId).

        void SimulateTicksToOverflowHistory(uint tickCount)
        {
            for (uint tickId = 1; tickId <= tickCount; tickId++)
            {
                entity.ClientSimulationTick(tickId);
                entity.SamplePhysicsState(tickId);
            }
        }

        PhysicsStateRecord MakeServerState(uint tickId)
        {
            PhysicsStateRecord state = PhysicsStateRecord.Alloc();
            state.From(rigidbody);
            state.tickId = tickId;
            return state;
        }

        [Test]
        public void TestSimulationFreezeAtExactHistoryBoundary()
        {
            // After 25 ticks with buffer 20: localHistoryStartTickId = 5.
            // Server tick 5: IsTickOutsideOfLocalHistory(5) = (5 <= 5) = true → SIMULATION_FREEZE.
            SimulateTicksToOverflowHistory(25);
            entity.BufferServerTick(25, MakeServerState(5));

            PredictionDecision decision = entity.GetPredictionDecision(26, out uint fromTick);

            Assert.AreEqual(PredictionDecision.SIMULATION_FREEZE, decision);
            Assert.AreEqual(5u, fromTick);
        }

        [Test]
        public void TestSimulationFreezeWhenServerTickWellBelowHistoryBoundary()
        {
            // After 25 ticks: localHistoryStartTickId = 5.
            // Server tick 1: (1 <= 5) = true → SIMULATION_FREEZE.
            SimulateTicksToOverflowHistory(25);
            entity.BufferServerTick(25, MakeServerState(1));

            PredictionDecision decision = entity.GetPredictionDecision(26, out uint fromTick);

            Assert.AreEqual(PredictionDecision.SIMULATION_FREEZE, decision);
            Assert.AreEqual(1u, fromTick);
        }

        [Test]
        public void TestNoSimulationFreezeWhenServerTickJustInsideLocalHistory()
        {
            // After 25 ticks: localHistoryStartTickId = 5.
            // Server tick 6: (6 <= 5) = false → eligibility check runs, not a freeze.
            SimulateTicksToOverflowHistory(25);
            entity.BufferServerTick(25, MakeServerState(6));

            PredictionDecision decision = entity.GetPredictionDecision(26, out uint fromTick);

            Assert.AreNotEqual(PredictionDecision.SIMULATION_FREEZE, decision);
            Assert.AreEqual(6u, fromTick);
        }

        //NOTE: SnapToServer falls back to the local history when the server has no state for the tick. The local
        //      buffer is indexed by tick % size, so the fallback must not use a slot holding a different tick.
        [Test]
        public void SnapToServerWithoutAnyHistoryForTickDoesNotMoveEntity()
        {
            Vector3 position = new Vector3(5, 0, 0);
            rigidbody.position = position;
            for (uint tickId = 41; tickId <= 43; ++tickId)
            {
                entity.SamplePhysicsState(tickId);
            }

            //Slot 5 was never written (pre-allocated empty record at the origin)
            entity.SnapToServer(5);

            Assert.AreEqual(position, rigidbody.position);
        }

        [Test]
        public void SnapToServerDoesNotUseOverwrittenHistorySlot()
        {
            rigidbody.position = new Vector3(5, 0, 0);
            for (uint tickId = 41; tickId <= 43; ++tickId)
            {
                entity.SamplePhysicsState(tickId);
            }
            Vector3 current = new Vector3(7, 0, 0);
            rigidbody.position = current;

            //Slot for tick 21 (21 % 20 = 1) now holds tick 41
            entity.SnapToServer(21);

            Assert.AreEqual(current, rigidbody.position);
        }

        [Test]
        public void GapInServerStreamIsReported()
        {
            List<ClientPredictedEntity.DesyncEvent> desyncs = new List<ClientPredictedEntity.DesyncEvent>();
            entity.potentialDesync.AddEventListener(desyncs.Add);
            SimulateTicksToOverflowHistory(12);

            entity.BufferServerTick(12, MakeServerState(5));
            entity.GetPredictionDecision(13, out uint _);
            //Server ticks 6..9 never arrived
            entity.BufferServerTick(13, MakeServerState(10));
            entity.GetPredictionDecision(13, out uint _);

            Assert.IsTrue(desyncs.Exists(d => d.reason == ClientPredictedEntity.DesyncReason.GAP_IN_SERVER_STREAM && d.gapSize == 5),
                "Gap between server ticks 5 and 10 was not reported");
        }

        [Test]
        public void ServerDelayIsZeroWhenServerIsAheadOfClient()
        {
            //e.g. right after a Reset, the server stream is ahead of the local tick
            entity.BufferServerTick(0, MakeServerState(50));
            entity.ClientSimulationTick(10);

            Assert.AreEqual(0u, entity.GetServerDelay());
            Assert.AreEqual(0u, entity.maxServerDelay);
        }

        [Test]
        public void DisablingPredictionSnapsLocalEntityToServerState()
        {
            //NOTE: must follow the flag the prediction tick uses (PredictionMngr), not the legacy PredictionManager one
            bool predictionEnabled = PredictionManager.PREDICTION_ENABLED;
            try
            {
                PredictionManager.PREDICTION_ENABLED = false;
                rigidbody.position = Vector3.zero;
                entity.ClientSimulationTick(1);
                entity.SamplePhysicsState(1);

                PhysicsStateRecord serverState = PhysicsStateRecord.Alloc();
                serverState.tickId = 1;
                serverState.position = new Vector3(3, 0, 0);
                entity.BufferServerTick(1, serverState);

                Assert.AreEqual(serverState.position, rigidbody.position);
            }
            finally
            {
                PredictionManager.PREDICTION_ENABLED = predictionEnabled;
            }
        }

        //NOTE: each component must write exactly the number of inputs it declares. Otherwise the inputs of the following
        //      components shift (or keep stale values) and the server loads different values than the client sampled.
        class MisreportingComponent : PredictableControllableComponent, PredictableComponent
        {
            public int declaredFloats;
            public int writtenFloats;

            public int GetFloatInputCount()
            {
                return declaredFloats;
            }

            public int GetBinaryInputCount()
            {
                return 0;
            }

            public void SampleInput(PredictionInputRecord input)
            {
                for (int i = 0; i < writtenFloats; i++)
                {
                    input.WriteNextScalar(i);
                }
            }

            public bool ValidateInput(float deltaTime, PredictionInputRecord input)
            {
                return true;
            }

            public void LoadInput(PredictionInputRecord input)
            {
            }

            public void ClearInput()
            {
            }

            public void ApplyForces()
            {
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

        ClientPredictedEntity CreateEntityWith(MisreportingComponent misreporting)
        {
            return new ClientPredictedEntity(1, false, 20, rigidbody, test, new PredictableControllableComponent[] { misreporting }, new PredictableComponent[] { misreporting });
        }

        [Test]
        public void ComponentWritingMoreInputsThanDeclaredIsRejected()
        {
            ClientPredictedEntity misreporting = CreateEntityWith(new MisreportingComponent { declaredFloats = 2, writtenFloats = 3 });
            misreporting.SetControlledLocally(true);

            Assert.Catch<Exception>(() => misreporting.SampleInput(1));
        }

        [Test]
        public void ComponentWritingFewerInputsThanDeclaredIsRejected()
        {
            ClientPredictedEntity misreporting = CreateEntityWith(new MisreportingComponent { declaredFloats = 2, writtenFloats = 1 });
            misreporting.SetControlledLocally(true);

            Assert.Catch<Exception>(() => misreporting.SampleInput(1));
        }
    }
}
#endif
