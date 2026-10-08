// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sector0.Ursitoare.Data;

namespace Sector0.Ursitoare.Components
{
    //All components that apply physics forces to the Rigidbody must implement this interface to support prediction.
    public interface PredictableComponent
    {
        void ApplyForces();
        bool HasState();
        void SampleComponentState(PhysicsStateRecord physicsStateRecord);
        void LoadComponentState(PhysicsStateRecord physicsStateRecord);
        int GetStateFloatCount();
        int GetStateBoolCount();
    }
}
