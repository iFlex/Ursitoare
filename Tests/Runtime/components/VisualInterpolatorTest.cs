// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if (UNITY_EDITOR)
using NUnit.Framework;
using Sector0.Ursitoare.Data;
using Sector0.Ursitoare.Interpolation;
using Sector0.Ursitoare.Utils;
using UnityEngine;

namespace Sector0.Ursitoare.Tests
{
    public class VisualInterpolatorTest
    {
        bool logPos;

        [SetUp]
        public void SetUp()
        {
            logPos = MovingAverageInterpolator.LOG_POS;
        }

        [TearDown]
        public void TearDown()
        {
            MovingAverageInterpolator.LOG_POS = logPos;
        }

        PhysicsStateRecord MakeState(uint tickId, Vector3 position)
        {
            PhysicsStateRecord state = PhysicsStateRecord.Alloc();
            state.tickId = tickId;
            state.position = position;
            return state;
        }

        //NOTE: the angle between the last two movement directions needs at least 3 states. Before that it must not throw
        //      (an exception thrown in Add drops the state from the visuals).
        [Test]
        public void BufferEndAngleWithTooFewStatesDoesNotThrow()
        {
            RingBuffer<PhysicsStateRecord> buffer = new RingBuffer<PhysicsStateRecord>(5);
            Assert.DoesNotThrow(() => CustomVisualInterpolator.GetBufferEndAngle(buffer));

            buffer.Add(MakeState(1, Vector3.zero));
            Assert.DoesNotThrow(() => CustomVisualInterpolator.GetBufferEndAngle(buffer));

            buffer.Add(MakeState(2, Vector3.forward));
            Assert.DoesNotThrow(() => CustomVisualInterpolator.GetBufferEndAngle(buffer));
        }

        [Test]
        public void MovingAverageInterpolatorFirstStatesWithPositionLoggingDoNotThrow()
        {
            MovingAverageInterpolator.LOG_POS = true;
            MovingAverageInterpolator interpolator = new MovingAverageInterpolator();

            Assert.DoesNotThrow(() => interpolator.Add(MakeState(1, Vector3.zero)));
            Assert.DoesNotThrow(() => interpolator.Add(MakeState(2, Vector3.forward)));
            Assert.DoesNotThrow(() => interpolator.Add(MakeState(3, Vector3.forward * 2)));
        }
    }
}
#endif
