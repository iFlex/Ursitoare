// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Prediction.Data;

namespace Prediction.Components
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
        bool ValidateInput(float deltaTime, PredictionInputRecord input);
        void LoadInput(PredictionInputRecord input);
        void ClearInput();
    }
}
