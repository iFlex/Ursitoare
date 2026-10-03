// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if (UNITY_EDITOR)
using NUnit.Framework;
using Prediction.Data;
using Prediction.Simulation;
using UnityEngine;

namespace Prediction.Tests.simulation
{
    public class RewindablePhysicsControllerTest
    {
        private RewindablePhysicsController controller;

        [SetUp]
        public void SetUp()
        {
            controller = new RewindablePhysicsController();
        }

        [Test]
        public void TestRewindAndResim()
        {
            GameObject test1 = new GameObject("test1");
            test1.transform.position = Vector3.zero;
            Rigidbody rigidbody1 = test1.AddComponent<Rigidbody>();
            
            GameObject test2 = new GameObject("test2");
            test2.transform.position = Vector3.right;
            Rigidbody rigidbody2 = test2.AddComponent<Rigidbody>();
            
            GameObject test3 = new GameObject("test3");
            test3.transform.position = Vector3.right * 2;
            Rigidbody rigidbody3 = test3.AddComponent<Rigidbody>();

            controller.Track(rigidbody1);
            controller.Track(rigidbody2);
            controller.Track(rigidbody3);
            
            PhysicsStateRecord final1 = PhysicsStateRecord.Alloc();
            PhysicsStateRecord final2 = PhysicsStateRecord.Alloc();
            PhysicsStateRecord final3 = PhysicsStateRecord.Alloc();
            
            PhysicsStateRecord psr1 = PhysicsStateRecord.Alloc();
            PhysicsStateRecord psr2 = PhysicsStateRecord.Alloc();
            PhysicsStateRecord psr3 = PhysicsStateRecord.Alloc();
            
            uint rewindBy = 5;
            uint maxTick = 11;
            Vector3 force = Vector3.forward * 10;
            for (int i = 0; i < 10; ++i) //10 ticks
            {
                rigidbody1.AddForce(force);
                rigidbody2.AddForce(force);
                rigidbody3.AddForce(force);
                
                Assert.AreEqual(i + 1, controller.GetTick());
                controller.Simulate();
                if (i == maxTick - rewindBy - 1)
                {
                    psr1.From(rigidbody1);
                    psr2.From(rigidbody2);
                    psr3.From(rigidbody3);
                }
                Assert.AreEqual(i + 2, controller.GetTick());
            }
            final1.From(rigidbody1);
            final2.From(rigidbody2);
            final3.From(rigidbody3);
            
            Assert.AreEqual(maxTick, controller.GetTick());
            controller.Rewind(rewindBy);
            uint resimFromTick = maxTick - rewindBy + 1;
            Assert.AreEqual(resimFromTick, controller.GetTick());
            AssertEqualState(rigidbody1, psr1);
            AssertEqualState(rigidbody2, psr2);
            AssertEqualState(rigidbody3, psr3);

            for (int i = 1; i < rewindBy; ++i) //4 ticks as the very first one does not require a simulation step
            {
                Assert.AreEqual(resimFromTick + i - 1, controller.GetTick());
                rigidbody1.AddForce(force);
                rigidbody2.AddForce(force);
                rigidbody3.AddForce(force);
                //TODO: resimulate is no needed as a separate method ?!
                controller.Resimulate(null);
                Assert.AreEqual(resimFromTick + i, controller.GetTick());
            }
            
            Assert.AreEqual(maxTick, controller.GetTick());
            AssertEqualState(rigidbody1, final1);
            AssertEqualState(rigidbody2, final2);
            AssertEqualState(rigidbody3, final3);
        }

        void AssertEqualState(Rigidbody body, PhysicsStateRecord expected)
        {
            Assert.AreEqual(expected.position, body.position);
            Assert.AreEqual(expected.rotation, body.rotation);
            Assert.AreEqual(expected.velocity, body.linearVelocity);
            Assert.AreEqual(expected.angularVelocity, body.angularVelocity);
        }

        Rigidbody CreateBody(string name, Vector3 position)
        {
            GameObject go = new GameObject(name);
            go.transform.position = position;
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            return body;
        }

        [Test]
        public void RewindToBeforeBodyWasTrackedDoesNotMoveIt()
        {
            //NOTE: e.g. an entity spawned after the tick a resimulation starts from. Its history for that tick is empty
            //      and restoring it would teleport the body to the origin with zero velocity.
            controller.Setup(false);
            Rigidbody early = CreateBody("early", Vector3.zero);
            controller.Track(early);
            for (int i = 0; i < 5; ++i)
                controller.Simulate();

            Rigidbody spawned = CreateBody("spawned", new Vector3(10, 5, 0));
            spawned.linearVelocity = Vector3.forward;
            controller.Track(spawned);
            for (int i = 0; i < 3; ++i)
                controller.Simulate();

            PhysicsStateRecord beforeRewind = PhysicsStateRecord.Alloc();
            beforeRewind.From(spawned);

            //Rewind to tick 4, spawned only has history from tick 6 onwards
            Assert.AreEqual(9, controller.GetTick());
            Assert.IsTrue(controller.Rewind(5));
            AssertEqualState(spawned, beforeRewind);

            GameObject.DestroyImmediate(early.gameObject);
            GameObject.DestroyImmediate(spawned.gameObject);
        }

        [Test]
        public void RewindBeyondHistoryIsRefused()
        {
            controller = new RewindablePhysicsController(10);
            controller.Setup(false);
            Rigidbody body = CreateBody("body", new Vector3(1, 2, 3));
            controller.Track(body);
            for (int i = 0; i < 25; ++i)
                controller.Simulate();

            PhysicsStateRecord beforeRewind = PhysicsStateRecord.Alloc();
            beforeRewind.From(body);

            //History only holds the last 10 ticks, tick 11 slot has been overwritten by tick 21
            Assert.IsFalse(controller.Rewind(15));
            Assert.AreEqual(26, controller.GetTick());
            AssertEqualState(body, beforeRewind);

            GameObject.DestroyImmediate(body.gameObject);
        }

        //TODO: try to resimulate 1 step behind
        //TODO: try to resimulate with object despawn
    }
}
#endif
