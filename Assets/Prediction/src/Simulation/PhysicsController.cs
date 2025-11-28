namespace Prediction.Simulation
{
    public interface PhysicsController
    {
        void Setup(bool isServer);
        void Simulate();
        void BeforeResimulate(ClientPredictedEntity entity);
        void Resimulate(ClientPredictedEntity entity);
        void AfterResimulate(ClientPredictedEntity entity);
    }
}