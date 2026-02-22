using System.Runtime.CompilerServices;
using Prediction.data;
using Sector0.Events;

namespace Prediction.policies.singleInstance
{
    public class SimpleConfigurableResimulationDecider : SingleSnapshotInstanceResimChecker
    {
        public float _avgDistD = 0;
        public float _avgRotD = 0;
        public float _avgVeloD = 0;
        public float _avgAVeloD = 0;
        public int _checkCount = 0;

        public float _MaxDistD = 0;
        public float _MaxRotD = 0;
        public float _MaxVeloD = 0;
        public float _MaxAVeloD = 0;

        public float distResimThreshold;
        public float rotationResimThreshold;
        public float veloResimThreshold;
        public float angVeloResimThreshold;

        public SimpleConfigurableResimulationDecider()
        {
            distResimThreshold = 0.0001f;
            rotationResimThreshold = 0.0001f;
            veloResimThreshold = 0.001f;
            angVeloResimThreshold = 0.001f;
        }

        public SimpleConfigurableResimulationDecider(float distResimThreshold, float rotResimThreshold, float veloResimThreshold, float angVeloResimThreshold)
        {
            this.distResimThreshold = distResimThreshold;
            this.rotationResimThreshold = rotResimThreshold;
            this.veloResimThreshold = veloResimThreshold;
            this.angVeloResimThreshold = angVeloResimThreshold;
        }

        public virtual PredictionDecision Check(uint entityId, uint tickId, PhysicsStateRecord local, PhysicsStateRecord server)
        {
            float distD = (local.position - server.position).magnitude;
            float angD = Quaternion.Angle(local.rotation, server.rotation);
            float vdelta = (local.velocity - server.velocity).magnitude;
            float avdelta = (local.angularVelocity - server.angularVelocity).magnitude;

            _avgDistD += distD;
            _avgRotD += angD;
            _avgVeloD += vdelta;
            _avgAVeloD += avdelta;
            _checkCount++;

            if (_MaxDistD < distD) _MaxDistD = distD;
            if (_MaxRotD < angD) _MaxRotD = angD;
            if (_MaxVeloD < vdelta) _MaxVeloD = vdelta;
            if (_MaxAVeloD < avdelta) _MaxAVeloD = avdelta;

            if (distResimThreshold > 0 && distD > distResimThreshold)
            {
                DispatchCheckResult(entityId, tickId, distD, angD, vdelta, avdelta, local, server, true);
                return PredictionDecision.RESIMULATE;
            }

            if (rotationResimThreshold > 0 && angD > rotationResimThreshold)
            {
                DispatchCheckResult(entityId, tickId, distD, angD, vdelta, avdelta, local, server, true);
                return PredictionDecision.RESIMULATE;
            }

            if (veloResimThreshold > 0 && vdelta > veloResimThreshold)
            {
                DispatchCheckResult(entityId, tickId, distD, angD, vdelta, avdelta, local, server, true);
                return PredictionDecision.RESIMULATE;
            }

            if (angVeloResimThreshold > 0 && avdelta > angVeloResimThreshold)
            {
                DispatchCheckResult(entityId, tickId, distD, angD, vdelta, avdelta, local, server, true);
                return PredictionDecision.RESIMULATE;
            }

            DispatchCheckResult(entityId, tickId, distD, angD, vdelta, avdelta, local, server, false);
            return PredictionDecision.NOOP;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void DispatchCheckResult(uint entityId, uint tickId, float distD, float angD, float vdelta, float avdelta, PhysicsStateRecord local, PhysicsStateRecord server, bool isResim)
        {
            CheckResultInfo info;
            info.entityId = entityId;
            info.tickId = tickId;
            info.distD = distD;
            info.angD = angD;
            info.velocityDelta = vdelta;
            info.angularVelocityDelta = avdelta;
            info.localState = local;
            info.serverState = server;
            info.isResim = isResim;
            onCheckResult.Dispatch(info);
        }

        public struct CheckResultInfo
        {
            public uint entityId;
            public uint tickId;
            public float distD;
            public float angD;
            public float velocityDelta;
            public float angularVelocityDelta;
            public PhysicsStateRecord localState;
            public PhysicsStateRecord serverState;
            public bool isResim;
        }

        public SafeEventDispatcher<CheckResultInfo> onCheckResult = new();
    }
}
