using Prediction.data;
using Prediction.policies.singleInstance;
using UnityEngine;

namespace DefaultNamespace
{
    public class DebugResimChecker : SimpleConfigurableResimulationDecider
    {
        public static bool PRED_DEBUG = true;
        private float maxdist = 0;
        private float totalBreakingDist = 0;
        private int breakingDistCount = 0;
        
        public override bool Check(PhysicsStateRecord l, PhysicsStateRecord s)
        {
            float dist = (l.position - s.position).magnitude;
            if (dist > maxdist)
            {
                maxdist = dist;
            }
            
            bool outcome = base.Check(l, s);
            if (outcome)
            {
                totalBreakingDist += dist;
                breakingDistCount++;
            }
            if (PRED_DEBUG)
                Debug.Log($"[PredictionMirrorBridge][DebugResimCheck]{((l.tickId != s.tickId) ? "ERR_WARNING" : "")} tick_local:{l.tickId} tick_server:{s.tickId} distance:{dist} avgBreakDist:{(breakingDistCount  > 0 ? totalBreakingDist / breakingDistCount : 0)} maxDist:{maxdist} breakCount:{breakingDistCount}");
            return outcome;
        }
    }
}