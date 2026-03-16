// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
﻿using Prediction.data;
using UnityEngine;

namespace Prediction.Interpolation
{
    public interface VisualsInterpolationsProvider
    {
        void Update(float deltaTime, uint currentTick);
        void Add(PhysicsStateRecord record);
        void SetInterpolationTarget(Transform t);
        void Reset();
        void SetControlledLocally(bool isLocalAuthority);
    }
}
