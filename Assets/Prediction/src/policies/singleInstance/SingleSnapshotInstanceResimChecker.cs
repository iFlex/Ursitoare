using Prediction.data;

namespace Prediction.policies.singleInstance
{
    public interface SingleSnapshotInstanceResimChecker
    {
        bool Check(PhysicsStateRecord local, PhysicsStateRecord server);
    }
}