namespace Prediction.Stats
{
    public interface Timer
    {
        public void Start();
        
        //returns seconds elapsed
        public double Stop();
    }
}