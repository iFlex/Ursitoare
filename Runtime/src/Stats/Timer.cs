namespace Prediction.Stats
{
    public interface Timer
    {
        public void Stat();
        
        //returns seconds elapsed
        public float Stop();
    }
}