// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Prediction.Components.Controllers;
using Prediction.Data;

namespace Prediction.Resimulation.Detection
{
    public interface SingleSnapshotInstanceResimChecker
    {
        PredictionDecision Check(uint entityId, uint tickId, PhysicsStateRecord local, PhysicsStateRecord server);
    }
}
