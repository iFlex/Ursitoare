using System.Diagnostics;

namespace Prediction.Stats
{
    public class DefaultTimer : Timer
    {
        private Stopwatch _stopwatch = new Stopwatch();
        
        public void Stat()
        {
            _stopwatch.Start();
        }

        public float Stop()
        {
            _stopwatch.Stop();
            float dur = _stopwatch.ElapsedMilliseconds / 1000f;
            _stopwatch.Reset();
            return dur;
        }
    }
}