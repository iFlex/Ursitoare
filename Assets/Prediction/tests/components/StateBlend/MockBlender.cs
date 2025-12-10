using Prediction.data;
using Prediction.StateBlend;
using Prediction.utils;

namespace Prediction.Tests.StateBlend
{
    public class MockBlender : FollowerStateBlender
    {
        public void Reset()
        {
            throw new System.NotImplementedException();
        }

        public bool BlendStep(ClientPredictedEntity.FollowerState state, RingBuffer<PhysicsStateRecord> blendedStateBuffer, RingBuffer<PhysicsStateRecord> followerStateBuffer,
            TickIndexedBuffer<PhysicsStateRecord> serverStateBuffer)
        {
            throw new System.NotImplementedException();
        }
        
        //EXPERIMENTAL
        public void SetSmoothingFactor(float factor)
        {
            throw new System.NotImplementedException();
        }
    }
}