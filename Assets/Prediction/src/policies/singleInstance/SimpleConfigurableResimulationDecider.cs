using Prediction.data;
using UnityEngine;

namespace Prediction.policies.singleInstance
{
    public class SimpleConfigurableResimulationDecider : SingleSnapshotInstanceResimChecker
    {
        private float maxDelta;
        private float maxAngleDelta;
        private float maxVeloAngleDelta;
        private float maxAngularVeloMagDelta;

        public SimpleConfigurableResimulationDecider()
        {
            maxDelta = 1.0f;
            maxAngleDelta = 0f;
            maxVeloAngleDelta = 0;
            maxAngularVeloMagDelta = 0;
        }
        
        public SimpleConfigurableResimulationDecider(float maxDistDelta, float maxAngleDelta, float maxVeloAngleDelta, float maxAngularVeloMagDelta)
        {
            this.maxDelta = maxDistDelta;
            this.maxAngleDelta = maxAngleDelta;
            this.maxVeloAngleDelta = maxVeloAngleDelta;
            this.maxAngularVeloMagDelta = maxAngularVeloMagDelta;
        }

        public virtual bool Check(PhysicsStateRecord local, PhysicsStateRecord server)
        {
            if (maxDelta > 0)
            {
                if ((local.position - server.position).magnitude > maxDelta)
                {
                    return true;
                }
            }

            if (maxAngleDelta > 0)
            {
                if (Quaternion.Angle(local.rotation, server.rotation) > maxAngleDelta)
                {
                    return true;
                }
            }
            
            if (maxVeloAngleDelta > 0)
            {
                if (Vector3.Angle(local.velocity, server.velocity) > maxVeloAngleDelta)
                {
                    return true;
                }
            }
            
            if (maxAngularVeloMagDelta > 0)
            {
                if ((local.velocity.magnitude - server.velocity.magnitude) > maxAngularVeloMagDelta)
                {
                    return true;
                }
            }
            
            return false;
        }
    }
}