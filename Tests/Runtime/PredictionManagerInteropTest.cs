// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if (UNITY_EDITOR) 
using NUnit.Framework;
using Prediction.Resimulation.Detection;
using Prediction.Tests.mocks;

namespace Prediction.Tests
{
    public class PredictionManagerInteropTest
    {
        MockPhysicsController physicsController;
        SimpleConfigurableResimulationDecider resimDecider = new SimpleConfigurableResimulationDecider();
        PredictionManager managerClient;
        PredictionManager managerServer;
        
        //TODO: these tests may not be relevant
        [SetUp]
        public void SetUp()
        {
            physicsController = new MockPhysicsController();
            
        }
        
        [Test]
        public void HappyPathMinimalDelay()
        {
            
        }
        
        //TODO: test swapping ownership, see that the buffers are cleared and new ticks are accepted correctly
    }
}
#endif
