using System;
using Prediction.data;

namespace Prediction
{
    public class PredictionContributor : PredictableComponent
    {
        //NOTE: I favor composition over inheritance, hence  the need for these handlers.
        public Action<PredictionInputRecord> _specializedInputReader;
        public Action<PredictionInputRecord> _specializedInputWriter;
        
        public virtual void SampleInput(PredictionInputRecord input)
        {
            if (_specializedInputReader != null)
            {
                _specializedInputReader.Invoke(input);
                return;
            }
            throw new System.NotImplementedException();
        }

        public bool ValidateState(uint tickId, PredictionInputRecord input)
        {
            throw new System.NotImplementedException();
        }

        public void LoadInput(PredictionInputRecord input)
        {
            if (_specializedInputWriter != null)
            {
                _specializedInputWriter.Invoke(input);
                return;
            }
            throw new System.NotImplementedException();
        }

        public void ApplyForces()
        {
            throw new System.NotImplementedException();
        }
    }
}