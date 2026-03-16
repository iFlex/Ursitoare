// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Prediction.Components.Controllers;
using UnityEngine;

namespace Prediction.Simulation
{
    public interface PhysicsController
    {
        void Setup(bool isServer);
        void Simulate();
        void BeforeResimulate(ClientPredictedEntity entity);
        bool Rewind(uint ticks);
        void Resimulate(ClientPredictedEntity entity);
        void AfterResimulate(ClientPredictedEntity entity);
        void Track(Rigidbody rigidbody);
        void Untrack(Rigidbody rigidbody);
        void Clear();
    }
}
