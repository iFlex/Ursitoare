// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using UnityEngine;

namespace Prediction.Components
{
    public class WrapperHelpers
    {
        public static PredictableControllableComponent[] GetControllableComponents(MonoBehaviour[] objects, MonoBehaviour self = null)
        {
            List<PredictableControllableComponent> compos = new List<PredictableControllableComponent>();
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] is PredictableControllableComponent)
                {
                    compos.Add((PredictableControllableComponent)objects[i]);
                }   
            }
            if (self is PredictableControllableComponent)
            {
                compos.Add((PredictableControllableComponent)self);
            }
            return compos.ToArray();
        }
        
        public static PredictableComponent[] GetComponents(MonoBehaviour[] controllable, MonoBehaviour self = null)
        {
            List<PredictableComponent> compos = new List<PredictableComponent>();
            for (int i = 0; i < controllable.Length; i++)
            {
                if (controllable[i] is PredictableComponent)
                {
                    compos.Add((PredictableComponent)controllable[i]);
                }   
            }
            if (self is PredictableComponent)
            {
                compos.Add((PredictableComponent)self);
            }
            return compos.ToArray();
        }
    }
}
