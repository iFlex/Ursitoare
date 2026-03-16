// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
﻿using Prediction.data;

namespace Prediction.policies.singleInstance
{
    public interface SingleSnapshotInstanceResimChecker
    {
        PredictionDecision Check(uint entityId, uint tickId, PhysicsStateRecord local, PhysicsStateRecord server);
    }
}
