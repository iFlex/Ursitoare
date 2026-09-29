// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if (UNITY_EDITOR) 
using NUnit.Framework;
using Prediction.Components.Controllers;
using Prediction.Data;
using Prediction.Resimulation.Detection;
using Prediction.Tests.mocks;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;

//TODO: move these tests to PredictionManagerInteropTest file
namespace Prediction.Tests
{
    //TODO: interpo test should be a PredictionManager test.
    public class ClientServerPredictionInteropTest
    {
        public static GameObject client;
        public static Rigidbody clientRigidbody;
        public static MockPredictableControllableComponent clientComponent;
        public static ClientPredictedEntity clientEntity;
        
        public static GameObject server;
        public static Rigidbody serverRigidbody;
        public static MockPredictableControllableComponent serverComponent;
        public static ServerPredictedEntity serverEntity;
        
        public static GameObject follower;
        public static Rigidbody followerRigidbody;
        public static MockPredictableControllableComponent followerComponent;
        public static ClientPredictedEntity followerEntity;

        public static MockPhysicsController physicsController;
        public static SimpleConfigurableResimulationDecider resimDecider = new SimpleConfigurableResimulationDecider();

        //TODO: these tests may not be relevant
        [SetUp]
        public void SetUp()
        {
            physicsController = new MockPhysicsController();
            
            client = new GameObject("test");
            client.transform.position = Vector3.zero;
            clientRigidbody = client.AddComponent<Rigidbody>();
            
            clientComponent = new MockPredictableControllableComponent();
            clientComponent.rigidbody = clientRigidbody;
            
            clientEntity = new ClientPredictedEntity(0, false,20, clientRigidbody, client, new []{clientComponent}, new[]{clientComponent});
            clientEntity.SetSingleStateEligibilityCheckHandler(resimDecider.Check);
            clientEntity.SetControlledLocally(true);
            
            server = new GameObject("test");
            server.transform.position = Vector3.zero;
            serverRigidbody = server.AddComponent<Rigidbody>();
            
            serverComponent = new MockPredictableControllableComponent();
            serverComponent.rigidbody = serverRigidbody;
            
            serverEntity = new ServerPredictedEntity(0 ,20, serverRigidbody, server, new []{serverComponent}, new[]{serverComponent});
            ServerPredictedEntity.USE_BUFFERING = false;

            //A second client's mirror of the same entity. It follows, it does not control.
            follower = new GameObject("follower");
            follower.transform.position = Vector3.zero;
            followerRigidbody = follower.AddComponent<Rigidbody>();

            followerComponent = new MockPredictableControllableComponent();
            followerComponent.rigidbody = followerRigidbody;

            followerEntity = new ClientPredictedEntity(0, false, 20, followerRigidbody, follower, new []{followerComponent}, new[]{followerComponent});
            followerEntity.SetSingleStateEligibilityCheckHandler(resimDecider.Check);
        }

        //NOTE: mimics an integration serialising a state message and rebuilding it on the receiving client.
        static PhysicsStateRecord WireCopy(PhysicsStateRecord state)
        {
            PhysicsStateRecord copy = PhysicsStateRecord.Alloc();
            copy.tickId = state.tickId;
            copy.position = state.position;
            copy.rotation = state.rotation;
            copy.velocity = state.velocity;
            copy.angularVelocity = state.angularVelocity;
            if (state.input != null)
            {
                copy.input = new PredictionInputRecord(state.input.scalarInput.Length, state.input.binaryInput.Length);
                copy.input.From(state.input);
            }
            return copy;
        }
        
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
    }
}
#endif
