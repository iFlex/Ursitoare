using Prediction.data;
using Prediction.utils;

namespace Prediction.StateBlend
{
    public interface FollowerStateBlender
    {
        void Reset();
        bool BlendStep(ClientPredictedEntity.FollowerState state, RingBuffer<PhysicsStateRecord> blendedStateBuffer,
            RingBuffer<PhysicsStateRecord> followerStateBuffer,
            TickIndexedBuffer<PhysicsStateRecord> serverStateBuffer);
        void SetSmoothingFactor(float factor);
    }
}