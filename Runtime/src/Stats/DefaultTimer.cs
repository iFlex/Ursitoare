using System;
using System.Diagnostics;
using UnityEngine;

namespace Prediction.Stats
{
    public class DefaultTimer : Timer
    {
        //private long startTime;
        private double timeSinceStart;
        
        public void Start()
        {
            //startTime = Stopwatch.GetTimestamp();
            timeSinceStart = Time.realtimeSinceStartupAsDouble;
        }

        public double Stop()
        {
            //TimeSpan ts = new TimeSpan(Stopwatch.GetTimestamp() - startTime);
            //return (float)ts.TotalMilliseconds / 1000f;
            
            return Time.realtimeSinceStartupAsDouble - timeSinceStart;
        }
    }
}