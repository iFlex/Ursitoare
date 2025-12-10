using Prediction.data;
using Prediction.utils;

namespace Prediction.StateBlend
{
    public class WeightedAverageBlender : FollowerStateBlender
    {
        public void Reset()
        {
            //ONLY IF NEEDED
        }

        public bool BlendStep(ClientPredictedEntity.FollowerState state, RingBuffer<PhysicsStateRecord> blendedStateBuffer, RingBuffer<PhysicsStateRecord> followerStateBuffer,
            TickIndexedBuffer<PhysicsStateRecord> serverStateBuffer)
        {
            float serverBias = ((float)state.tickId) / state.overlapWithAuthorityEnd;
            
            //TODO: current or prev server state
            //TODO: average inputs out
            int prevTick = (int) state.tickId - 1;
            PhysicsStateRecord prevState = followerStateBuffer.Get(prevTick); 
            blendedStateBuffer.Get((int)state.tickId).From(prevState, state.tickId);
            //TODO
            return false;
        }

        public void SetSmoothingFactor(float factor)
        {
            //TODO - modify window based on this.
        }
    }
}