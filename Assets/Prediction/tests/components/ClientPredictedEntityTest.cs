#if (UNITY_EDITOR) 
using System.Collections.Generic;
using NUnit.Framework;
using Prediction.data;
using Prediction.policies.singleInstance;
using Prediction.Tests.mocks;
using UnityEngine;

namespace Prediction.Tests
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
            
            entity = new ClientPredictedEntity(false, 20, rigidbody, test, new []{component}, new[]{component});
            entity.SetSingleStateEligibilityCheckHandler(resimDecider.Check);
            entity.physicsController = physicsController;
        }

        [TearDown]
        public void TeaDown()
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

        PhysicsStateRecord[] GenerateServerStates(Vector3[] inputs)
        {
            PhysicsStateRecord[] states = new PhysicsStateRecord[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
            {
                rigidbody.position += inputs[i];
                states[i] = new PhysicsStateRecord();
                states[i].From(rigidbody);
                states[i].tickId = (uint) i;
            }
            rigidbody.position = Vector3.zero;
            return states;
        }

        Vector3 GetInputFromInputRecord(PredictionInputRecord record)
        {
            record.ReadReset();
            return new Vector3(record.ReadNextScalar(), record.ReadNextScalar(), record.ReadNextScalar());
        }
        
        [Test]
        public void TestTickAdvanceAndInputReport()
        {
            var inputs = new [] { Vector3.zero, Vector3.left, Vector3.right, Vector3.up, Vector3.down };
            var serverTicks = GenerateServerStates(inputs);

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
            var serverTicks = GenerateServerStates(inputs);
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
            var serverTicks = GenerateServerStates(inputs);
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
            
            //TODO: tests
        }
        
        [Test]
        public void TestServerDriftAndResimulate()
        {
            var inputs = new []       { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.up, Vector3.right,  Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up, Vector3.right,   Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverInputs = new [] { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.up,    Vector3.up,  Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up, Vector3.up,      Vector3.up, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverTicks = GenerateServerStates(serverInputs);
            Dictionary<int, Vector3> posCorrections = new Dictionary<int, Vector3>();
            posCorrections.Add(5, serverTicks[5].position);
            posCorrections.Add(11, serverTicks[11].position);
            
            int serverDelay = 3;
            int resimulationsCounter = 0;
            int resimSteps = 0;
            entity.resimulation.AddEventListener((started) =>
            {
                if (started)
                    resimulationsCounter++;
            });
            entity.resimulationStep.AddEventListener((started) =>
            {
                if (started)
                    resimSteps++;
            });
            
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                component.inputVector = inputs[tickId];
                entity.ClientSimulationTick(tickId);
                entity.SamplePhysicsState(tickId);
                if (tickId > serverDelay)
                {
                    entity.BufferServerTick(tickId, serverTicks[tickId - serverDelay]);
                }
            }
            
            Assert.AreEqual(2, resimulationsCounter);
            Vector3[] expectedPosStream = ComputePosStream(serverInputs, posCorrections);
            for (int i = 1; i < inputs.Length; ++i)
            {
                //TODO: can we make it sample on the same tick?
                if (i < inputs.Length - 1)
                    Assert.AreEqual(expectedPosStream[i], entity.localStateBuffer.Get(i).position);
                Assert.AreEqual(inputs[i], GetInputFromInputRecord(entity.localInputBuffer.Get(i)));
                if (i < inputs.Length - serverDelay)
                    Assert.AreEqual(serverTicks[i].position, entity.serverStateBuffer.Get((uint) i).position);
            }
        }
        
        [Test]
        public void TestExactlyOneResimNeededForOneMissedInput()
        {
            var inputs = new []       { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.up,    Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverInputs = new [] { Vector3.zero, Vector3.right, Vector3.up, Vector3.right, Vector3.right, Vector3.right, Vector3.up, Vector3.right, Vector3.up };
            var serverTicks = GenerateServerStates(serverInputs);
            Dictionary<int, Vector3> posCorrections = new Dictionary<int, Vector3>();
            posCorrections.Add(4, serverTicks[4].position);
            
            int serverDelay = 1;
            int resimulationsCounter = 0;
            int resimSteps = 0;
            entity.resimulation.AddEventListener((started) =>
            {
                if (started)
                    resimulationsCounter++;
            });
            entity.resimulationStep.AddEventListener((started) =>
            {
                if (started)
                    resimSteps++;
            });
            
            for (uint tickId = 1; tickId < inputs.Length; ++tickId)
            {
                component.inputVector = inputs[tickId];
                entity.ClientSimulationTick(tickId);
                entity.SamplePhysicsState(tickId);
                if (tickId > serverDelay)
                {
                    entity.BufferServerTick(tickId, serverTicks[tickId - serverDelay]);
                }
            }
            
            Assert.AreEqual(1, resimulationsCounter);
            Assert.AreEqual(serverDelay, resimSteps);
            Vector3[] expectedPosStream = ComputePosStream(serverInputs, posCorrections);
            for (int i = 1; i < inputs.Length; ++i)
            {
                //TODO: can we make it sample on the same tick?
                if (i < inputs.Length - 1)
                    Assert.AreEqual(expectedPosStream[i], entity.localStateBuffer.Get(i).position);
                Assert.AreEqual(inputs[i], GetInputFromInputRecord(entity.localInputBuffer.Get(i)));
                if (i < inputs.Length - serverDelay)
                    Assert.AreEqual(serverTicks[i].position, entity.serverStateBuffer.Get((uint) i).position);
            }
        }
        
        [Test]
        public void TestMultiResimulationPrevention()
        {
            entity.protectFromOversimulation = true;
            
            int predictionAcceptable = 0;
            entity.predictionAcceptable.AddEventListener((tr) =>
            {
                if (tr)
                    predictionAcceptable++;
            });
            
            PhysicsStateRecord psr = new PhysicsStateRecord();
            Vector3 serverPos = Vector3.zero;
            psr.rotation = Quaternion.identity;
            for (uint tickId = 5; tickId < 105; ++tickId)
            {
                component.inputVector = Vector3.right * 2;
                serverPos += Vector3.left * 2;
                psr.position = serverPos;
                entity.ClientSimulationTick(tickId);
                entity.SamplePhysicsState(tickId);
                psr.tickId = tickId - 4;
                entity.BufferServerTick(tickId - 1, psr);
            }
            Assert.AreEqual(0, predictionAcceptable);
            Assert.AreEqual(67, entity.totalSimulationSkips); //33% resimulations
            Assert.AreEqual(33, entity.totalResimulations);
        }
        
        //TODO: test follow server
        //TODO: test blend with follow server
    }
}
#endif