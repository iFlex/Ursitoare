using Prediction.data;

namespace Prediction
{
    //All components that take user input must implement this interface to support prediction. Even if no forces are applied.
    public interface PredictableControllableComponent
    {
        /*
         * Samples input and stores it in the input record entity. Has side effects!
         */
        public int GetFloatInputCount();
        public int GetBinaryInputCount();
        void SampleInput(PredictionInputRecord input);
        bool ValidateState(uint tickId, PredictionInputRecord input);
        void LoadInput(PredictionInputRecord input);
    }
}