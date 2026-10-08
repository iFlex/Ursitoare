// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if (UNITY_EDITOR)
using NUnit.Framework;
using Sector0.Ursitoare.Components;
using Sector0.Ursitoare.Data;
using Sector0.Ursitoare.Tests.mocks;
using Sector0.Ursitoare.Tests.Mocks;
using UnityEngine;

namespace Sector0.Ursitoare.Tests
{
    // Followers are client entities that are not controlled locally (other players, free bodies).
    // These tests pin down that a follower which diverges from the server always has a way back to the server state.
    public class PredictionManagerFollowerTest
    {
        GameObject localGo;
        GameObject followerGo;
        MockPredictableControllableComponent localComponent;
        MockPredictableControllableComponent followerComponent;
        ClientPredictionManager manager;

        //PredictionManager configuration is static, restore it so other tests are unaffected.
        bool savedIgnoreControllableFollowerDecisions;
        bool savedIgnoreNonAuthResimDecisions;
        float savedFollowersSqrDistanceThreshold;
        float savedPreciseFollowersSqrDistanceThreshold;

        [SetUp]
        public void SetUp()
        {
            savedFollowersSqrDistanceThreshold = PredictionManager.RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD;
            savedPreciseFollowersSqrDistanceThreshold = PredictionManager.RESIMULATE_PRECISE_FOLLOWERS_SQR_DISTANCE_THRESHOLD;

            PredictionManager.RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = 0;
            PredictionManager.RESIMULATE_PRECISE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = 0;

            localGo = new GameObject("local");
            localGo.transform.position = Vector3.zero;
            localComponent = new MockPredictableControllableComponent();
            localComponent.rigidbody = localGo.AddComponent<Rigidbody>();

            followerGo = new GameObject("follower");
            followerGo.transform.position = Vector3.zero;
            followerComponent = new MockPredictableControllableComponent();
            followerComponent.rigidbody = followerGo.AddComponent<Rigidbody>();

            manager = new ClientPredictionManager((a) => { }, (a, e, b) => { });
            manager.SetPhysicsController(new MockPhysicsController());
        }

        [TearDown]
        public void TearDown()
        {
            PredictionManager.RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = savedFollowersSqrDistanceThreshold;
            PredictionManager.RESIMULATE_PRECISE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = savedPreciseFollowersSqrDistanceThreshold;

            GameObject.Destroy(localGo);
            GameObject.Destroy(followerGo);
            localGo = null;
            followerGo = null;
            localComponent = null;
            followerComponent = null;
            manager = null;
        }

        //Local entity (id 1) owned by this client, never asks for a resimulation by itself.
        MockClientPredictedEntity AddLocalEntity()
        {
            MockClientPredictedEntity local = new MockClientPredictedEntity(1, false, 20, localComponent.rigidbody, localGo,
                new PredictableControllableComponent[] { localComponent }, new PredictableComponent[] { localComponent });
            local._predictionDecision = PredictionDecision.NOOP;
            manager.AddPredictedEntity(local);
            manager.OnEntityOwnershipChanged(1, true);
            return local;
        }

        //Controllable follower (id 2): has input driven components but is owned by someone else.
        MockClientPredictedEntity AddControllableFollower(PredictionDecision decision, uint fromTick)
        {
            MockClientPredictedEntity follower = new MockClientPredictedEntity(2, false, 20, followerComponent.rigidbody, followerGo,
                new PredictableControllableComponent[] { followerComponent }, new PredictableComponent[] { followerComponent });
            follower._predictionDecision = decision;
            follower._fromTick = fromTick;
            manager.AddPredictedEntity(follower);
            return follower;
        }

        static PhysicsStateRecord ServerStateAt(uint tickId, Vector3 position)
        {
            PhysicsStateRecord psr = PhysicsStateRecord.Alloc();
            psr.tickId = tickId;
            psr.position = position;
            psr.rotation = Quaternion.identity;
            return psr;
        }

        [Test]
        public void TestControllableFollowerResimDecisionCountsWhenNotIgnored()
        {
            AddLocalEntity();
            AddControllableFollower(PredictionDecision.RESIMULATE, 3);

            Assert.AreEqual(PredictionDecision.RESIMULATE, manager.ComputePredictionDecision(out uint resimFrom));
            Assert.AreEqual(3, resimFrom);
        }

        //Ignoring a controllable follower's resimulation request is only safe if that follower is snapped to the server
        //instead. ClientFollowerSimulationTick only snaps when predictAsFollower is false ("otherwise let the resimulation
        //do the snapping"), but predictAsFollower is always true while a local entity exists, so the follower ends up
        //with neither a resimulation nor a snap.
        [Test]
        public void TestIgnoredControllableFollowerIsSnappedInstead()
        {
            AddLocalEntity();
            MockClientPredictedEntity follower = AddControllableFollower(PredictionDecision.RESIMULATE, 1);

            PredictionDecision decision = manager.ComputePredictionDecision(out uint resimFrom);

            Assert.IsTrue(decision != PredictionDecision.NOOP || !follower.predictAsFollower,
                "Controllable follower asked for a resimulation, the request was ignored and the follower is still predicted " +
                "(predictAsFollower=true), so nothing corrects it: it drifts until another entity triggers a resimulation.");
        }

        //Same gap observed end to end: the follower diverges from the server, the local entity is perfectly in sync,
        //so the only correction can come from the follower itself.
        [Test]
        public void TestIgnoredControllableFollowerConvergesToServerWithoutLocalResimulation()
        {
            AddLocalEntity();
            AddControllableFollower(PredictionDecision.RESIMULATE, 1);

            //First tick configures followers (predictAsFollower) while there is no server data yet.
            manager.Tick();

            Vector3 serverPosition = new Vector3(10, 0, 0);
            manager.OnServerStateReceived(2, ServerStateAt(1, serverPosition));
            manager.Tick();

            //NOTE: the local entity always reports NOOP, so either the follower is snapped or its own resimulation request is honoured.
            Assert.AreEqual(serverPosition, followerComponent.rigidbody.position,
                "Follower that is ahead of/away from the server was never moved to the server state.");
        }

        //GetMinSqrDistToAllLocalEnts computes (ent.position - ent.position), so the distance to the follower is always 0
        //and the distance thresholds can never classify a follower as far away.
        [Test]
        public void TestFarFollowerIsNotPredictedWhenOutsideDistanceThreshold()
        {
            PredictionManager.RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = 1f; // 1 m
            AddLocalEntity();
            MockClientPredictedEntity follower = AddControllableFollower(PredictionDecision.NOOP, 0);
            followerGo.transform.position = new Vector3(100, 0, 0);

            manager.ComputePredictionDecision(out uint _);

            Assert.IsFalse(follower.predictAsFollower,
                "Follower 100 m away from the only local entity is still predicted with a 1 m threshold: distance is measured as 0.");
        }

        [Test]
        public void TestNearFollowerIsPredictedWhenInsideDistanceThreshold()
        {
            PredictionManager.RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = 1f; // 1 m
            AddLocalEntity();
            MockClientPredictedEntity follower = AddControllableFollower(PredictionDecision.NOOP, 0);
            followerGo.transform.position = new Vector3(0.5f, 0, 0);

            manager.ComputePredictionDecision(out uint _);

            Assert.IsTrue(follower.predictAsFollower);
        }

        [Test]
        public void TestFarFollowerDoesNotUsePreciseCheckerWhenOutsidePreciseThreshold()
        {
            PredictionManager.RESIMULATE_PRECISE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = 9f; // 3 m
            AddLocalEntity();
            MockClientPredictedEntity follower = AddControllableFollower(PredictionDecision.NOOP, 0);
            followerGo.transform.position = new Vector3(100, 0, 0);

            manager.ComputePredictionDecision(out uint _);

            Assert.IsFalse(follower.usePreciseResimChecker,
                "Follower 100 m away uses the precise checker with a 3 m threshold: distance is measured as 0.");
        }
    }
}
#endif
