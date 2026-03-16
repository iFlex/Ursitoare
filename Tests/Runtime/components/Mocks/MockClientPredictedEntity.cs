// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if UNITY_EDITOR
using Prediction.Components;
using Prediction.Components.Controllers;
using UnityEngine;

namespace Prediction.Tests.Mocks
{
    public class MockClientPredictedEntity : ClientPredictedEntity
    {
        public bool decisionPassThrough = false;
        public uint _fromTick;
        public PredictionDecision _predictionDecision;
        
        public MockClientPredictedEntity(uint id, bool isServer, int bufferSize, Rigidbody rb, GameObject visuals, PredictableControllableComponent[] controllablePredictionContributors, PredictableComponent[] predictionContributors) : 
            base(id, isServer, bufferSize, rb, visuals, controllablePredictionContributors, predictionContributors)
        {
        }
        
        public override PredictionDecision GetPredictionDecision(uint lastAppliedTick, out uint fromTick)
        {
            if (decisionPassThrough)
            {
                return base.GetPredictionDecision(lastAppliedTick, out fromTick);
            }
            fromTick = _fromTick;
            return _predictionDecision;
        }
    }
}
#endif
