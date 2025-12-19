using Prediction.data;

namespace Prediction.policies.singleInstance
{
    public interface SingleSnapshotInstanceResimChecker
    {
        PredictionDecision Check(PhysicsStateRecord local, PhysicsStateRecord server);
    }
}