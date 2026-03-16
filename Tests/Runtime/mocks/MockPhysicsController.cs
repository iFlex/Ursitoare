// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
﻿#if (UNITY_EDITOR) 
using Prediction.Simulation;
using UnityEngine;

namespace Prediction.Tests.mocks
{
    public class MockPhysicsController : PhysicsController
    {
        public MockPhysicsController()
        {
            Physics.simulationMode = SimulationMode.Script;
        }
        
        public void Setup(bool isServer)
        {
        }

        public void Simulate()
        {
        }

        public void BeforeResimulate(ClientPredictedEntity entity)
        {
        }

        public bool Rewind(uint ticks)
        {
            return true;
        }

        public void Resimulate(ClientPredictedEntity entity)
        {
        }

        public void AfterResimulate(ClientPredictedEntity entity)
        {
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
#endif
