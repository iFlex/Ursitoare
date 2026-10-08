// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sector0.Ursitoare.Components;
using Sector0.Ursitoare.Data;

namespace Sector0.Ursitoare.Resimulation.Detection
{
    public interface SingleSnapshotInstanceResimChecker
    {
        PredictionDecision Check(uint entityId, uint tickId, PhysicsStateRecord local, PhysicsStateRecord server);
    }
}
