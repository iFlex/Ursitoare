using Sector0.Events;
using UnityEngine;

namespace Adapters.Prediction
{
    public class PosAnalyser
    {
        private Vector3 _prevPrevPos;
        private Vector3 _prevPos;

        public struct PosAnalysisInfo
        {
            public string prefix;
            public string prefix1;
            public Vector3 position;
            public Quaternion rotation;
            public float posAngle;
        }

        public SafeEventDispatcher<PosAnalysisInfo> onAnalysis = new();

        public void LogAndPrintPosRot(string prefix, Vector3 newPos, Quaternion newRot)
        {
            Vector3 prevVector = _prevPos - _prevPrevPos;
            Vector3 curVector = newPos - _prevPos;
            float posAngle = Vector3.Angle(prevVector, curVector);

            PosAnalysisInfo info;
            info.prefix = prefix;
            info.prefix1 = null;
            info.position = newPos;
            info.rotation = newRot;
            info.posAngle = posAngle;
            onAnalysis.Dispatch(info);

            _prevPrevPos = _prevPos;
            _prevPos = newPos;
        }

        public void LogAndPrintPosRot(string prefix1, string prefix, Vector3 newPos, Quaternion newRot)
        {
            Vector3 prevVector = _prevPos - _prevPrevPos;
            Vector3 curVector = newPos - _prevPos;
            float posAngle = Vector3.Angle(prevVector, curVector);

            PosAnalysisInfo info;
            info.prefix = prefix;
            info.prefix1 = prefix1;
            info.position = newPos;
            info.rotation = newRot;
            info.posAngle = posAngle;
            onAnalysis.Dispatch(info);

            _prevPrevPos = _prevPos;
            _prevPos = newPos;
        }
    }
}
