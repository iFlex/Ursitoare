namespace Sector0.Ursitoare.Stats
{
    public interface Timer
    {
        public void Start();
        
        //returns seconds elapsed
        public double Stop();
    }
}