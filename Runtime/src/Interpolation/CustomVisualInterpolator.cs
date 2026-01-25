using System;
using System.Runtime.CompilerServices;
using Prediction.data;
using Prediction.Interpolation;
using Prediction.utils;
using UnityEngine;

namespace Adapters.Prediction
{
    //TODO: somehow there's no late additions to the buffer, yet the interpolation runs past the end of the buffer.
    public class CustomVisualInterpolator : VisualsInterpolationsProvider
    {
        public static bool DEBUG = true;
        public static bool LOG_POS = true;
        public static bool DEEP_DEBUG = false;
        public static int DEBUG_COUNTER = 0;
        public static int FOLLOWER_SMOOTH_WINDOW = 4;
        public static bool USE_INTERPOLATION = true;
        public static bool USE_SMOOTH_BUFFER = true;
        public static int BUFFER_SIZE = 60;
        public static int SMOOTH_BUFFER_SIZE = 6;
        public static bool INTERPOLATE_MANUAL = false;
        
        RingBuffer<PhysicsStateRecord> buffer = new RingBuffer<PhysicsStateRecord>(BUFFER_SIZE);
        public RingBuffer<PhysicsStateRecord> averagedBuffer = new RingBuffer<PhysicsStateRecord>(SMOOTH_BUFFER_SIZE);

        private Transform target;
        private double tickInterval = Time.fixedDeltaTime;
        
        private double time = 0;
        private uint smoothingTick = 0;

        public static int startAfterBfrTicks = 2;
        public int slidingWindowTickSize = 6;
        
        public bool autosizeWindow = false;
        public float MinVisualDelay = 0.5f;
        public uint minVisualTickDelay = 2;
        public Func<uint> GetServerTickLag;
        
        public int debugCounterLocal;
        
        
        public CustomVisualInterpolator()
        {
            debugCounterLocal = DEBUG_COUNTER;
            DEBUG_COUNTER++;
            minVisualTickDelay = (uint) Mathf.CeilToInt(MinVisualDelay / Time.fixedDeltaTime);
        }
        
        public void ConfigureWindowAutosizing(Func<uint> serverLatencyFetcher)
        {
            if (serverLatencyFetcher == null)
            {
                autosizeWindow = false;
            }
            else
            {
                autosizeWindow = true;
                GetServerTickLag = serverLatencyFetcher;
            }
        }

        private double totalTime = 0;
        private float lastDt = 0;
        public void Update(float deltaTime, uint currentTick)
        {
            totalTime += deltaTime;
            /*
            if (autosizeWindow)
            {
                uint serverTickLag = GetServerTickLag();
                slidingWindowTickSize = (int) Math.Max(minVisualTickDelay, serverTickLag / 2);
            }
            */
            lastDt = deltaTime;
            if (CanStartInterpolation())
            {
                GetNextInterpolationTarget(time, out PhysicsStateRecord from, out PhysicsStateRecord to, out float interpTarget, out bool hasFrame);
                if (!hasFrame)
                {
                    Debug.Log($"[CustomVisualInterpolator][Update][LERP]({target.gameObject.GetInstanceID()})({debugCounterLocal}) WARNING. no data! time:{time} dTime:{deltaTime} startTime:{GetTime(GetInterpolationBuffer().GetStart())} fill:{GetInterpolationBuffer().GetFill()}");
                    return;
                }
                
                if (DEBUG)
                {
                    Debug.Log($"[CustomVisualInterpolator][ApplyState]({target.gameObject.GetInstanceID()}) intT:{interpTarget} dt:{lastDt} time:{time} from({GetTime(from)})[Tid:{from.tickId}] to({GetTime(to)})[Tid:{to.tickId}]");
                }

                ApplyState(from, to, interpTarget);
                if (interpTarget == 1f)
                {
                    Debug.Log($"[CustomVisualInterpolator][ApplyState] NOT_ENOUGH_DATA_IN_BUFFER, this shouldn't be possible... Time has be set to end of buffer in waiting for more data");
                    time = GetTime(to);
                }
                time += deltaTime;
            } 
            else if (GetInterpolationBuffer().GetFill() > 0)
            {
                PhysicsStateRecord start = GetInterpolationBuffer().GetStart();
                time = GetTime(start);
                if (DEBUG)
                {
                    Debug.Log($"[CustomVisualInterpolator][Update]({target.gameObject.GetInstanceID()}) time:{time} fill:{GetInterpolationBuffer().GetFill()} start:{start} startTime:{GetTime(GetInterpolationBuffer().GetStart())}");
                }
            }
        }
        
        private Vector3 prevPos = Vector3.zero;
        private Vector3 pos = Vector3.zero;
        //NOTE: the problem here is that our averaged state is jumping around somehow...
        void ApplyState(PhysicsStateRecord from, PhysicsStateRecord to, float t)
        {
            prevPos = pos;
            pos = target.position;

            if (USE_INTERPOLATION)
            {
                target.position = Vector3.Lerp(from.position, to.position, t);
                target.rotation = Quaternion.Slerp(from.rotation, to.rotation, t);
                //Note, no simulated rigid body for the visuals, no need to look at the physics data.
            }
            else
            {
                target.position = to.position;
                target.rotation = to.rotation; 
            }
            
            //TODO: apply rotation in relation to own current rotation as experiment
            
            if (DEBUG)
            {
                float dist = (target.position - pos).magnitude;
                Vector3 crntDir = target.position - pos;
                Vector3 pastDir = pos - prevPos;
                float angle = Mathf.Abs(Vector3.Angle(crntDir, pastDir));
                Debug.Log($"[CustomVisualInterpolator][DT_DBG][VISUAL_ADVANCE]{(angle > ANGLE_THRESHOLD ? "[BREACH]" : "")} dist:{dist} angle:{angle}");
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        double GetTime(uint tickId)
        {
            return tickId * tickInterval;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        double GetTime(PhysicsStateRecord record)
        {
            return GetTime(record.tickId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool CanStartInterpolation()
        {
            if (DEEP_DEBUG)
            {
                Debug.Log($"[CustomVisualInterpolator][CanStartInterpolation]({target.gameObject.GetInstanceID()}) Fill:{GetInterpolationBuffer().GetFill()}");
            }
            return GetInterpolationBuffer().GetFill() >= startAfterBfrTicks;
        }
        
        //NOTE: this is broken, interpolation time is somehow always ahead of the god damned buffer...
        void GetNextInterpolationTarget(double time, out PhysicsStateRecord from, out PhysicsStateRecord to, out float t, out bool hasFrame)
        {
            from = null;
            to = null;
            t = 0;
            hasFrame = false;
            
            int fill = GetInterpolationBuffer().GetFill();
            int index = GetInterpolationBuffer().GetStartIndex();
            do
            {
                from = to;
                to = GetInterpolationBuffer().Get(index);
                //if (DEBUG && from != null && to != null)
                //{
                //    Debug.Log($"[CustomVisualInterpolator][GetNextInterpolationTarget][CHECK] time:{time} from({GetTime(from)}):{from} to({GetTime(to)}):{to}");
                //}
                
                if (time == 0 || GetTime(to) > time)
                {
                    if (from == null)
                    {
                        //NOTE: not enough data in the buffer yet for interpolation
                        continue;
                    }
                    
                    t = (float)((time - GetTime(from)) / tickInterval);
                    if (t < 0)
                    {
                        Debug.Log($"[CustomVisualInterpolator][GetNextInterpolationTarget] negative interpolation target:{t}");
                        t = 0;
                    }
                    hasFrame = true;
                    return;
                }

                index++;
                fill--;
            }
            while(fill > 0);
            
            t = 1;
            hasFrame = from != null && to != null;
            Debug.Log($"[CustomVisualInterpolator][GetNextInterpolationTarget][TIME_PAST_END_OF_BFR] time:{time} toT:{(to == null ? "X" :GetTime(to))} fromT:{(from == null ? "X": GetTime(from))} startT:{(GetInterpolationBuffer().GetFill() == 0 ? "X" : GetTime(GetInterpolationBuffer().GetStart()))} fill:{GetInterpolationBuffer().GetFill()}");
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private RingBuffer<PhysicsStateRecord> GetInterpolationBuffer()
        {
            if (USE_SMOOTH_BUFFER)
                return averagedBuffer;
            return buffer;
        }


        public static Vector3 GetDirVector(PhysicsStateRecord from, PhysicsStateRecord to)
        {
            return to.position - from.position;
        }

        private double lastAddTime;
        public static float ANGLE_THRESHOLD = 120;

        public void Add(PhysicsStateRecord record)
        {
            if (LOG_POS)
            {
                Debug.Log($"[Visuals][Positions] t:{record.tickId} pos:{record.position} rot:{record.rotation}");
            }
            
            double deltaAdd = totalTime - lastAddTime;
            if (deltaAdd > Time.fixedDeltaTime) 
            {
                float percent = (float)(deltaAdd / Time.fixedDeltaTime);
                Debug.Log($"[CustomVisualInterpolator][DT_DBG][LATE_ADD] t:{record.tickId} deltaTime:{deltaAdd} shouldBe:{Time.fixedDeltaTime} missBy:{deltaAdd - Time.fixedDeltaTime} p:{percent}");
            }
            lastAddTime = totalTime;
            
            //NOTE: do not use record as is without deep copy... memory will be altered by prediction...
            PhysicsStateRecord newData = PhysicsStateRecord.Alloc();
            newData.From(record);
            buffer.Add(newData);
            averagedBuffer.Add(GetNextProcessedState());
            
            if (DEBUG)
            {
                double endTime = GetInterpolationBuffer().GetFill() == 0
                    ? 0
                    : GetTime(GetInterpolationBuffer().GetEnd());
                Debug.Log($"[CustomVisualInterpolator][DT_DBG][Add]{(time > endTime ? "[LATE]":"[OK]")} FrameNo:{Time.frameCount} deltaT:{deltaAdd} time:{time} EndTime:{endTime} tickId:{record.tickId} startTick:{buffer.GetStart().tickId} endTick:{buffer.GetEnd().tickId} startIndex:{buffer.GetStartIndex()} endIndex:{buffer.GetEndIndex()} DATA:{record}");
                
                if (buffer.GetFill() > 1)
                {
                    LogBufferEndStats(buffer, "[BUFFER_NORMAL_ADVANCE]");
                }
                if (averagedBuffer.GetFill() > 1)
                {
                    LogBufferEndStats(averagedBuffer, "[BUFFER_SMOOTH_ADVANCE]");
                }
            }
        }
        
        public static float GetBufferEndAngle(RingBuffer<PhysicsStateRecord> bfr) 
        {
            Vector3 crntDir = GetDirVector(GetWithOffset(bfr, -1), bfr.GetEnd());
            Vector3 pastDir = GetDirVector(GetWithOffset(bfr, -2), GetWithOffset(bfr, -1));
            return Mathf.Abs(Vector3.Angle(crntDir, pastDir));
        }
        
        static void LogBufferEndStats(RingBuffer<PhysicsStateRecord> bfr, string prefix)
        {
            float dist = (GetWithOffset(bfr, -1).position - bfr.GetEnd().position).magnitude;
            Vector3 crntDir = GetDirVector(GetWithOffset(bfr, -1), bfr.GetEnd());
            Vector3 pastDir = GetDirVector(GetWithOffset(bfr, -2), GetWithOffset(bfr, -1));
            float angle = Mathf.Abs(Vector3.Angle(crntDir, pastDir));
            Debug.Log($"[CustomVisualInterpolator][DT_DBG]{prefix}{(angle > ANGLE_THRESHOLD ? "[BREACH]" : "")} dist:{dist} angle:{angle} DATA:{bfr.GetEnd()} DATA_PREV:{GetWithOffset(bfr, -1)}");
        }
        
        public static PhysicsStateRecord GetWithOffset(RingBuffer<PhysicsStateRecord> bfr, int offset)
        {
            int pos = bfr.GetEndIndex() - 1;
            pos += offset;
            if (pos < 0)
            {
                pos = bfr.GetCapacity() + pos;
            }
            pos = pos % bfr.GetCapacity();
            return bfr.Get(pos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddToWindow(PhysicsStateRecord accumulator, PhysicsStateRecord newItem)
        {
            accumulator.position += newItem.position;
            accumulator.rotation = newItem.rotation;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FinalizeWindow(PhysicsStateRecord accumulator, int count)
        {
            accumulator.position /= count;
        }
        
        public PhysicsStateRecord GetNextProcessedState()
        {
            PhysicsStateRecord psr = PhysicsStateRecord.Alloc();
            psr.tickId = smoothingTick;
            smoothingTick++;
            
            int windowSize = Math.Min(slidingWindowTickSize, buffer.GetFill());
            //NOTE: end index is always a non added position.
            int index = buffer.GetEndIndex() - windowSize;
            if (index < 0)
            {
                index = buffer.GetCapacity() + index;
            }
            
            for (int i = 0; i < windowSize; ++i)
            {
                AddToWindow(psr, buffer.Get(index));
                if (DEEP_DEBUG)
                {
                    Debug.Log($"[CustomVisualInterpolator][GetNextProcessedState][AddToWindow] Fill:{buffer.GetFill()} WindowSize:{windowSize} index:{index} addedTickId:{buffer.Get(index).tickId} endIndex:{buffer.GetEndIndex()} DATA:{psr} ADD_DATA:{buffer.Get(index)}");
                }
                index++;
            }
            FinalizeWindow(psr, windowSize);

            if (DEEP_DEBUG && averagedBuffer.GetFill() > 1)
            {
                Debug.Log($"[CustomVisualInterpolator][GetNextProcessedState][FinalizeWindow] tickId:{psr.tickId} startIndex:{averagedBuffer.GetStartIndex()} endIndex:{averagedBuffer.GetEndIndex()} startTick:{averagedBuffer.GetStart().tickId} endTick:{averagedBuffer.GetEnd().tickId} DATA:{psr}");
            }
            return psr;
        }

        public void SetInterpolationTarget(Transform t)
        {
            target = t;
        }

        public void Reset()
        {
            buffer.Clear();
        }

        public void SetControlledLocally(bool isLocalAuthority)
        {
            if (isLocalAuthority)
            {
                slidingWindowTickSize = FOLLOWER_SMOOTH_WINDOW;
            }
            else
            {
                //TODO: adapt to ping
                //TODO: remove coupling to Time.fixedDeltaTime
                //int ticks = Mathf.CeilToInt((float) (PredictionManager.ROUND_TRIP_GETTER() / Time.fixedDeltaTime) * 0.55f);
                //slidingWindowTickSize = 12; //Math.Max(12, ticks);
                //NOTE: not sure we really need this, set it the same as the client
                slidingWindowTickSize = FOLLOWER_SMOOTH_WINDOW;
            }
        }
    }
}