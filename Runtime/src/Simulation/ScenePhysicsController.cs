// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
﻿using UnityEngine;

namespace Prediction.Simulation
{
    public class ScenePhysicsController : PhysicsController
    {
        public void Setup(bool isServer)
        {
            Physics.simulationMode = SimulationMode.Script;
            //TODO: setup separate scene
        }

        public void Simulate()
        {
            Physics.Simulate(Time.fixedDeltaTime);
        }

        public void BeforeResimulate(ClientPredictedEntity entity)
        {
            //TODO: use separate scene
        }

        public bool Rewind(uint ticks)
        {
            return true;
        }

        public void Resimulate(ClientPredictedEntity entity)
        {
            //TODO: use separate scene
        }

        public void AfterResimulate(ClientPredictedEntity entity)
        {
            //TODO: use separate scene
        }

        public void Track(Rigidbody rigidbody)
        {
        }

        public void Untrack(Rigidbody rigidbody)
        {
        }

        public void Clear()
        {
        }
    }
}
