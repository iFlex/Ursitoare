// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sector0.Ursitoare.Data;
using UnityEngine;

namespace Sector0.Ursitoare.Interpolation
{
    public interface VisualsInterpolationsProvider
    {
        void Update(float deltaTime, uint currentTick);
        void Add(PhysicsStateRecord record);
        void SetInterpolationTarget(Transform t);
        void Reset();
        void SetControlledLocally(bool isLocalAuthority);

        /// <summary>
        /// How far behind the newest state passed to Add() the visuals were drawn in the last Update(), in
        /// seconds. Smoothing that trails the newest state counts as delay. NaN until something has been drawn,
        /// or when the drawing mode has no single moment it shows.
        /// </summary>
        double GetVisualDelay();
    }
}
