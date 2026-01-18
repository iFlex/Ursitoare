using UnityEngine;

namespace Adapters.Prediction
{
    public class PosAnalyser
    {
        private Vector3 _prevPrevPos;
        private Vector3 _prevPos;
        
        public void LogAndPrintPosRot(string prefix, Vector3 newPos, Quaternion newRot)
        {
            Vector3 prevVector = _prevPos - _prevPrevPos;
            Vector3 curVector = newPos - _prevPos;
            float posAngle = Vector3.Angle(prevVector, curVector);
            
            Debug.Log($"[Visuals][State][{prefix}] pos:{newPos} rot:{newRot} pangle:{posAngle}");
            _prevPrevPos = _prevPos;
            _prevPos = newPos;
        }
        
        public void LogAndPrintPosRot(string prefix1, string prefix, Vector3 newPos, Quaternion newRot)
        {
            Vector3 prevVector = _prevPos - _prevPrevPos;
            Vector3 curVector = newPos - _prevPos;
            float posAngle = Vector3.Angle(prevVector, curVector);
            
            Debug.Log($"[{prefix1}][State][{prefix}] pos:{newPos} rot:{newRot} pangle:{posAngle}");
            _prevPrevPos = _prevPos;
            _prevPos = newPos;
        }
    }
}