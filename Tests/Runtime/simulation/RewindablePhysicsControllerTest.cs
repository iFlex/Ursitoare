#if (UNITY_EDITOR)
using NUnit.Framework;
using Prediction.data;
using Prediction.Simulation;
using UnityEngine;

namespace Prediction.Tests.simulation
{
    //TODO: read every test and validate
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
                controller.Resimulate(null);
                Assert.AreEqual(resimFromTick + i, controller.GetTick());
            }

            Assert.AreEqual(maxTick, controller.GetTick());
            AssertEqualState(rigidbody1, final1);
            AssertEqualState(rigidbody2, final2);
            AssertEqualState(rigidbody3, final3);
        }

        [Test]
        public void TestRewindTooFarReturnsFalse()
        {
            GameObject go = new GameObject("test");
            Rigidbody rb = go.AddComponent<Rigidbody>();
            controller.Track(rb);

            // Simulate 3 ticks: tickId goes from 1 to 4
            for (int i = 0; i < 3; i++)
            {
                controller.Simulate();
            }
            Assert.AreEqual(4u, controller.GetTick());

            // Rewinding exactly tickId ticks should fail
            Assert.IsFalse(controller.Rewind(4));
            // Rewinding more than tickId should fail
            Assert.IsFalse(controller.Rewind(100));
            // Tick should remain unchanged after failed rewind
            Assert.AreEqual(4u, controller.GetTick());
        }

        [Test]
        public void TestRewindOneStep()
        {
            GameObject go = new GameObject("test");
            Rigidbody rb = go.AddComponent<Rigidbody>();
            controller.Track(rb);

            Vector3 force = Vector3.up * 5;

            // Simulate 5 ticks
            PhysicsStateRecord stateAtTick4 = PhysicsStateRecord.Alloc();
            for (int i = 0; i < 5; i++)
            {
                rb.AddForce(force);
                controller.Simulate();
                if (i == 3) // After tick 4 (0-indexed), before tick 5
                {
                    stateAtTick4.From(rb);
                }
            }
            Assert.AreEqual(6u, controller.GetTick());

            // Rewind 1 step
            Assert.IsTrue(controller.Rewind(1));
            // After rewind(1) from tick 6: tickId = 6-1 = 5, apply state at 5, then tickId = 6
            // Wait - rewind sets tickId = tickId - ticks, applies, then increments
            // So from 6: tickId=5, apply(5), tickId=6
            Assert.AreEqual(6u, controller.GetTick());

            // The body should be at the state after tick 5 (which was applied then incremented)
            // Actually the rewind restores state at tick (tickId - ticks) = 5
            // That state was sampled during _SimStep for tick 5
        }

        [Test]
        public void TestTrackOutsideResimIsImmediate()
        {
            GameObject go = new GameObject("test");
            Rigidbody rb = go.AddComponent<Rigidbody>();

            Assert.AreEqual(0, controller.GetTrackedCount());
            controller.Track(rb);
            Assert.AreEqual(1, controller.GetTrackedCount());
            Assert.IsTrue(controller.IsTracked(rb));
        }

        [Test]
        public void TestUntrackOutsideResimIsImmediate()
        {
            GameObject go = new GameObject("test");
            Rigidbody rb = go.AddComponent<Rigidbody>();

            controller.Track(rb);
            Assert.AreEqual(1, controller.GetTrackedCount());

            controller.Untrack(rb);
            Assert.AreEqual(0, controller.GetTrackedCount());
            Assert.IsFalse(controller.IsTracked(rb));
        }

        [Test]
        public void TestTrackDuringResimIsDeferred()
        {
            GameObject go = new GameObject("test");
            Rigidbody rb = go.AddComponent<Rigidbody>();

            controller.BeforeResimulate();

            controller.Track(rb);
            // Should not be tracked yet
            Assert.AreEqual(0, controller.GetTrackedCount());
            Assert.IsFalse(controller.IsTracked(rb));

            controller.AfterResimulate();
            // Should be tracked after flush
            Assert.AreEqual(1, controller.GetTrackedCount());
            Assert.IsTrue(controller.IsTracked(rb));
        }

        [Test]
        public void TestUntrackDuringResimIsDeferred()
        {
            GameObject go = new GameObject("test");
            Rigidbody rb = go.AddComponent<Rigidbody>();

            controller.Track(rb);
            Assert.AreEqual(1, controller.GetTrackedCount());

            controller.BeforeResimulate();

            controller.Untrack(rb);
            // Should still be tracked during resim
            Assert.AreEqual(1, controller.GetTrackedCount());
            Assert.IsTrue(controller.IsTracked(rb));

            controller.AfterResimulate();
            // Should be untracked after flush
            Assert.AreEqual(0, controller.GetTrackedCount());
            Assert.IsFalse(controller.IsTracked(rb));
        }

        [Test]
        public void TestTrackAndUntrackDuringResimCancelOut()
        {
            GameObject go = new GameObject("test");
            Rigidbody rb = go.AddComponent<Rigidbody>();

            controller.BeforeResimulate();

            controller.Track(rb);
            controller.Untrack(rb);

            Assert.AreEqual(0, controller.GetTrackedCount());

            controller.AfterResimulate();
            // Untrack is flushed first, then track - so the body ends up tracked
            // because the untrack of a non-existent body is a no-op, then track adds it
            Assert.AreEqual(1, controller.GetTrackedCount());
            Assert.IsTrue(controller.IsTracked(rb));
        }

        [Test]
        public void TestUntrackThenTrackDuringResimForExistingBody()
        {
            GameObject go = new GameObject("test");
            Rigidbody rb = go.AddComponent<Rigidbody>();

            controller.Track(rb);
            Assert.AreEqual(1, controller.GetTrackedCount());

            controller.BeforeResimulate();

            // Untrack and re-track during resim
            controller.Untrack(rb);
            controller.Track(rb);

            // Still tracked during resim (deferred untrack hasn't happened)
            Assert.AreEqual(1, controller.GetTrackedCount());

            controller.AfterResimulate();
            // Untracks flush first (removes it), then tracks flush (re-adds it)
            Assert.AreEqual(1, controller.GetTrackedCount());
            Assert.IsTrue(controller.IsTracked(rb));
        }

        [Test]
        public void TestMultipleBodiesStagedDuringResim()
        {
            GameObject go1 = new GameObject("test1");
            Rigidbody rb1 = go1.AddComponent<Rigidbody>();
            GameObject go2 = new GameObject("test2");
            Rigidbody rb2 = go2.AddComponent<Rigidbody>();
            GameObject go3 = new GameObject("test3");
            Rigidbody rb3 = go3.AddComponent<Rigidbody>();

            controller.Track(rb1);
            Assert.AreEqual(1, controller.GetTrackedCount());

            controller.BeforeResimulate();

            controller.Track(rb2);
            controller.Track(rb3);
            controller.Untrack(rb1);

            // Only rb1 remains tracked during resim
            Assert.AreEqual(1, controller.GetTrackedCount());
            Assert.IsTrue(controller.IsTracked(rb1));
            Assert.IsFalse(controller.IsTracked(rb2));
            Assert.IsFalse(controller.IsTracked(rb3));

            controller.AfterResimulate();

            // rb1 removed, rb2 and rb3 added
            Assert.AreEqual(2, controller.GetTrackedCount());
            Assert.IsFalse(controller.IsTracked(rb1));
            Assert.IsTrue(controller.IsTracked(rb2));
            Assert.IsTrue(controller.IsTracked(rb3));
        }

        [Test]
        public void TestClearResetsEverything()
        {
            GameObject go1 = new GameObject("test1");
            Rigidbody rb1 = go1.AddComponent<Rigidbody>();
            GameObject go2 = new GameObject("test2");
            Rigidbody rb2 = go2.AddComponent<Rigidbody>();

            controller.Track(rb1);
            controller.Simulate();
            controller.Simulate();

            // Stage a pending track
            controller.BeforeResimulate();
            controller.Track(rb2);

            controller.Clear();

            Assert.AreEqual(0, controller.GetTrackedCount());
            Assert.AreEqual(1u, controller.GetTick());

            // Pending track should also be cleared, so tracking rb2 after clear should not happen spontaneously
            // Track rb1 fresh to verify clean state
            controller.Track(rb1);
            Assert.AreEqual(1, controller.GetTrackedCount());
            Assert.IsTrue(controller.IsTracked(rb1));
            Assert.IsFalse(controller.IsTracked(rb2));
        }

        [Test]
        public void TestSimulateDuringResimWithDeferredTrackDoesNotIncludeNewBody()
        {
            GameObject go1 = new GameObject("existing");
            Rigidbody rb1 = go1.AddComponent<Rigidbody>();
            GameObject go2 = new GameObject("spawned");
            Rigidbody rb2 = go2.AddComponent<Rigidbody>();

            controller.Track(rb1);

            Vector3 force = Vector3.forward * 10;
            // Simulate a few ticks to build history
            for (int i = 0; i < 5; i++)
            {
                rb1.AddForce(force);
                controller.Simulate();
            }

            PhysicsStateRecord stateBeforeResim = PhysicsStateRecord.Alloc();
            stateBeforeResim.From(rb1);

            controller.BeforeResimulate();

            // Spawn a new body during resim
            controller.Track(rb2);
            Assert.IsFalse(controller.IsTracked(rb2));

            // Resimulate should only process rb1, not rb2
            rb1.AddForce(force);
            controller.Resimulate(null);

            controller.AfterResimulate();

            // Now rb2 should be tracked
            Assert.IsTrue(controller.IsTracked(rb2));
            Assert.AreEqual(2, controller.GetTrackedCount());
        }

        [Test]
        public void TestRewindPastSpawnTimePlacesBodyAtOutOfBounds()
        {
            GameObject go1 = new GameObject("existing");
            Rigidbody rb1 = go1.AddComponent<Rigidbody>();
            GameObject go2 = new GameObject("spawned");
            Rigidbody rb2 = go2.AddComponent<Rigidbody>();

            controller.Track(rb1);

            // Simulate 5 ticks to build history (tickId goes 1 -> 6)
            for (int i = 0; i < 5; i++)
            {
                controller.Simulate();
            }
            Assert.AreEqual(6u, controller.GetTick());

            // Simulate a resim where rb2 gets spawned
            // unrewindableTickId = 6 at this point
            controller.BeforeResimulate();
            controller.Track(rb2);
            controller.AfterResimulate();

            // Normal simulate after resim - gives rb2 its first real sample
            Vector3 spawnPos = new Vector3(10, 20, 30);
            rb2.position = spawnPos;
            controller.Simulate(); // tickId 6 -> 7, unrewindableTickId = 7

            // Simulate a couple more ticks
            controller.Simulate(); // 7 -> 8
            controller.Simulate(); // 8 -> 9

            // Now rewind past rb2's spawnTime (6) - should place rb2 at outOfBounds
            controller.Rewind(5); // tickId 9 -> 4, apply(4), tickId = 5
            Assert.AreEqual(5u, controller.GetTick());
            Assert.AreEqual(controller.outOfBoundsRecord.position, rb2.position);
        }

        [Test]
        public void TestResimRestoresBodyAtSpawnTick()
        {
            GameObject go1 = new GameObject("existing");
            Rigidbody rb1 = go1.AddComponent<Rigidbody>();
            rb1.useGravity = false;
            GameObject go2 = new GameObject("spawned");
            Rigidbody rb2 = go2.AddComponent<Rigidbody>();
            rb2.useGravity = false;

            controller.Track(rb1);

            // Simulate 5 ticks (tickId 1 -> 6, unrewindableTickId = 6)
            for (int i = 0; i < 5; i++)
            {
                controller.Simulate();
            }

            // Resim spawns rb2, spawnTime = unrewindableTickId = 6
            controller.BeforeResimulate();
            controller.Track(rb2);
            controller.AfterResimulate();

            // Normal simulate - rb2 gets its first sample at tickId 6
            Vector3 spawnPos = new Vector3(10, 20, 30);
            rb2.position = spawnPos;
            controller.Simulate(); // tickId 6 -> 7

            // Record rb2's state after its first simulate (the sample stored at tick 6)
            PhysicsStateRecord spawnState = PhysicsStateRecord.Alloc();
            spawnState.From(rb2);

            // Simulate more ticks
            controller.Simulate(); // 7 -> 8
            controller.Simulate(); // 8 -> 9

            // Rewind past spawn time
            controller.Rewind(5); // tickId 9 -> 4, apply(4), tickId = 5
            Assert.AreEqual(controller.outOfBoundsRecord.position, rb2.position);

            // Resim ticks 5, 6, 7, 8
            controller.BeforeResimulate();
            controller.Resimulate(null); // tick 5 - pre-spawn, body drifts from outOfBounds
            controller.Resimulate(null); // tick 6 == spawnTime, RestorePreSpawnBodies snaps rb2 back
            AssertEqualState(rb2, spawnState);

            controller.AfterResimulate();
        }

        [Test]
        public void TestResimWithMultipleSpawnedBodiesAtDifferentTicks()
        {
            GameObject go1 = new GameObject("existing");
            Rigidbody rb1 = go1.AddComponent<Rigidbody>();
            rb1.useGravity = false;

            controller.Track(rb1);

            // Simulate 3 ticks (tickId 1 -> 4, unrewindableTickId = 4)
            for (int i = 0; i < 3; i++)
            {
                controller.Simulate();
            }

            // First resim: spawn rb2 at unrewindableTickId = 4
            GameObject go2 = new GameObject("spawned1");
            Rigidbody rb2 = go2.AddComponent<Rigidbody>();
            rb2.useGravity = false;

            controller.BeforeResimulate();
            controller.Track(rb2);
            controller.AfterResimulate();

            // Normal sim at tick 4 -> 5, rb2 gets first sample
            Vector3 spawnPos2 = new Vector3(5, 5, 5);
            rb2.position = spawnPos2;
            controller.Simulate(); // unrewindableTickId = 5

            // Simulate more (tick 5 -> 6)
            controller.Simulate(); // unrewindableTickId = 6

            // Second resim: spawn rb3 at unrewindableTickId = 6
            GameObject go3 = new GameObject("spawned2");
            Rigidbody rb3 = go3.AddComponent<Rigidbody>();
            rb3.useGravity = false;

            controller.BeforeResimulate();
            controller.Track(rb3);
            controller.AfterResimulate();

            // Normal sim at tick 6 -> 7, rb3 gets first sample
            Vector3 spawnPos3 = new Vector3(99, 99, 99);
            rb3.position = spawnPos3;
            controller.Simulate(); // unrewindableTickId = 7

            PhysicsStateRecord rb2SpawnState = PhysicsStateRecord.Alloc();
            PhysicsStateRecord rb3SpawnState = PhysicsStateRecord.Alloc();
            rb2SpawnState.From(rb2);
            rb3SpawnState.From(rb3);

            // Simulate a couple more
            controller.Simulate(); // 7 -> 8
            controller.Simulate(); // 8 -> 9

            // Rewind past both spawn times
            controller.Rewind(7); // tickId 9 -> 2, apply(2), tickId = 3

            // Both should be at outOfBounds
            Assert.AreEqual(controller.outOfBoundsRecord.position, rb2.position);
            Assert.AreEqual(controller.outOfBoundsRecord.position, rb3.position);

            // Resim forward
            controller.BeforeResimulate();
            controller.Resimulate(null); // tick 3 - both pre-spawn
            controller.Resimulate(null); // tick 4 == rb2 spawnTime, rb2 restored
            AssertEqualState(rb2, rb2SpawnState);
            Assert.AreNotEqual(rb3.position, rb3SpawnState.position); // rb3 still not at its spawn pos

            controller.Resimulate(null); // tick 5
            controller.Resimulate(null); // tick 6 == rb3 spawnTime, rb3 restored
            AssertEqualState(rb3, rb3SpawnState);

            controller.AfterResimulate();
        }

        void AssertEqualState(Rigidbody body, PhysicsStateRecord expected)
        {
            Assert.AreEqual(expected.position, body.position);
            Assert.AreEqual(expected.rotation, body.rotation);
            Assert.AreEqual(expected.velocity, body.linearVelocity);
            Assert.AreEqual(expected.angularVelocity, body.angularVelocity);
        }
    }
}
#endif