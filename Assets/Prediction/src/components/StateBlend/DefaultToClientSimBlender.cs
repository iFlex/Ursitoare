using Prediction.data;
using Prediction.utils;

namespace Prediction.StateBlend
{
    public class DefaultToClientSimBlender : FollowerStateBlender
    {
        public void Reset()
        {
            //ONLY IF NEEDED
        }

        public bool BlendStep(ClientPredictedEntity.FollowerState state, RingBuffer<PhysicsStateRecord> blendedStateBuffer, RingBuffer<PhysicsStateRecord> followerStateBuffer,
            TickIndexedBuffer<PhysicsStateRecord> serverStateBuffer)
        {
            int prevTick = (int) state.tickId - 1;
            PhysicsStateRecord prevState = followerStateBuffer.Get(prevTick); 
            PhysicsStateRecord blendState = blendedStateBuffer.Get((int)state.tickId);
            blendState.tickId = state.tickId;
            blendState.From(prevState, state.tickId);
            return true;
        }

        public void SetSmoothingFactor(float factor)
        {
            //TODO - modify window based on this.
        }
    }
}