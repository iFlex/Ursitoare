// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
﻿namespace Prediction
{
    public enum PredictionDecision
    {
        NOOP = 0,
        RESIMULATE = 1,
        SNAP = 2,
        SIMULATION_FREEZE = 3 // NOTE ENOUGH LOCAL HISTORY TO REWIND ALL THE WAY TO THE LAST SERVER POINT
    }
}
