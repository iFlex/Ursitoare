using System;
using System.Runtime.CompilerServices;
using Adapters.Prediction;
using Prediction.data;
using Prediction.utils;
using Sector0.Events;
using UnityEngine;

namespace Prediction.Interpolation
{
    //TODO: do we need a common interpolator class with the buffering logic? can this live in the visuals class?
    public class MovingAverageInterpolator: VisualsInterpolationsProvider
    {
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

        public PosAnalyser posAnalyser = new PosAnalyser();

        public MovingAverageInterpolator()
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
            lastDt = deltaTime;
            if (CanStartInterpolation())
            {
                GetNextInterpolationTarget(time, out PhysicsStateRecord from, out PhysicsStateRecord to, out float interpTarget, out bool hasFrame);
                if (!hasFrame)
                {
                    NoDataWarningInfo noData;
                    noData.instanceId = target.gameObject.GetInstanceID();
                    noData.debugCounter = debugCounterLocal;
                    noData.time = time;
                    noData.deltaTime = deltaTime;
                    noData.startTime = GetTime(GetInterpolationBuffer().GetStart());
                    noData.fill = GetInterpolationBuffer().GetFill();
                    onNoInterpolationData.Dispatch(noData);
                    return;
                }

                InterpolationAppliedInfo applied;
                applied.instanceId = target.gameObject.GetInstanceID();
                applied.interpTarget = interpTarget;
                applied.deltaTime = lastDt;
                applied.time = time;
                applied.from = from;
                applied.to = to;
                onInterpolationApplied.Dispatch(applied);

                if (INTERPOLATE_MANUAL)
                {
                    float t = 1;
                    double targetTime = GetTime(to);
                    double budget = targetTime - time;
                    if (budget > 0 && deltaTime < budget)
                    {
                        t = (float) (deltaTime / budget);
                    }

                    target.position = Vector3.Lerp(target.position, to.position, t);
                    target.rotation = Quaternion.Slerp(target.rotation, to.rotation, t);
                    posAnalyser.LogAndPrintPosRot("SELF_LERP", target.position, target.rotation);
                }
                else
                {
                    ApplyState(from, to, interpTarget);
                }

                if (interpTarget == 1f)
                {
                    InsufficientBufferInfo insuf;
                    insuf.interpTarget = interpTarget;
                    onInsufficientBufferData.Dispatch(insuf);
                    time = GetTime(to);
                }
                time += deltaTime;
            }
            else if (GetInterpolationBuffer().GetFill() > 0)
            {
                PhysicsStateRecord start = GetInterpolationBuffer().GetStart();
                time = GetTime(start);
                BufferUpdateInfo bui;
                bui.instanceId = target.gameObject.GetInstanceID();
                bui.time = time;
                bui.fill = GetInterpolationBuffer().GetFill();
                bui.start = start;
                onBufferUpdate.Dispatch(bui);
            }
        }

        private Vector3 prevPos = Vector3.zero;
        private Vector3 pos = Vector3.zero;
        void ApplyState(PhysicsStateRecord from, PhysicsStateRecord to, float t)
        {
            prevPos = pos;
            pos = target.position;

            if (USE_INTERPOLATION)
            {
                target.position = Vector3.Lerp(from.position, to.position, t);
                target.rotation = Quaternion.Slerp(from.rotation, to.rotation, t);
            }
            else
            {
                target.position = to.position;
                target.rotation = to.rotation;
            }

            posAnalyser.LogAndPrintPosRot("SELF_LERP", target.position, target.rotation);

            float dist = (target.position - pos).magnitude;
            Vector3 crntDir = target.position - pos;
            Vector3 pastDir = pos - prevPos;
            float angle = Mathf.Abs(Vector3.Angle(crntDir, pastDir));
            VisualAdvanceInfo vai;
            vai.dist = dist;
            vai.angle = angle;
            vai.breach = angle > ANGLE_THRESHOLD;
            onVisualAdvance.Dispatch(vai);
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
            CanStartInterpolationInfo csi;
            csi.instanceId = target.gameObject.GetInstanceID();
            csi.fill = GetInterpolationBuffer().GetFill();
            onCanStartInterpolation.Dispatch(csi);
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

                if (time == 0 || GetTime(to) > time)
                {
                    if (from == null)
                    {
                        continue;
                    }

                    t = (float)((time - GetTime(from)) / tickInterval);
                    if (t < 0)
                    {
                        NegativeInterpTargetInfo neg;
                        neg.t = t;
                        onNegativeInterpTarget.Dispatch(neg);
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
            TimePastBufferEndInfo tpb;
            tpb.time = time;
            tpb.toTime = to == null ? double.NaN : GetTime(to);
            tpb.fromTime = from == null ? double.NaN : GetTime(from);
            tpb.startTime = GetInterpolationBuffer().GetFill() == 0 ? double.NaN : GetTime(GetInterpolationBuffer().GetStart());
            tpb.fill = GetInterpolationBuffer().GetFill();
            onTimePastBufferEnd.Dispatch(tpb);
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
            PositionAddedInfo pai;
            pai.tickId = record.tickId;
            pai.position = record.position;
            pai.rotation = record.rotation;
            onPositionAdded.Dispatch(pai);

            double deltaAdd = totalTime - lastAddTime;
            if (deltaAdd > Time.fixedDeltaTime)
            {
                LateAddInfo lai;
                lai.tickId = record.tickId;
                lai.deltaTime = deltaAdd;
                lai.shouldBe = Time.fixedDeltaTime;
                lai.missBy = deltaAdd - Time.fixedDeltaTime;
                lai.percent = (float)(deltaAdd / Time.fixedDeltaTime);
                onLateAdd.Dispatch(lai);
            }
            lastAddTime = totalTime;

            //NOTE: do not use record as is without deep copy... memory will be altered by prediction...
            PhysicsStateRecord newData = PhysicsStateRecord.Alloc();
            newData.From(record);
            buffer.Add(newData);
            averagedBuffer.Add(GetNextProcessedState());

            double endTime = GetInterpolationBuffer().GetFill() == 0
                ? 0
                : GetTime(GetInterpolationBuffer().GetEnd());
            AddTimingInfo ati;
            ati.late = time > endTime;
            ati.frameNo = Time.frameCount;
            ati.deltaT = deltaAdd;
            ati.time = time;
            ati.endTime = endTime;
            ati.tickId = record.tickId;
            onAddTiming.Dispatch(ati);

            if (buffer.GetFill() > 1)
            {
                DispatchBufferStats(buffer, false);
            }
            if (averagedBuffer.GetFill() > 1)
            {
                DispatchBufferStats(averagedBuffer, true);
            }
        }

        public static float GetBufferEndAngle(RingBuffer<PhysicsStateRecord> bfr)
        {
            Vector3 crntDir = GetDirVector(GetWithOffset(bfr, -1), bfr.GetEnd());
            Vector3 pastDir = GetDirVector(GetWithOffset(bfr, -2), GetWithOffset(bfr, -1));
            return Mathf.Abs(Vector3.Angle(crntDir, pastDir));
        }

        void DispatchBufferStats(RingBuffer<PhysicsStateRecord> bfr, bool isSmoothed)
        {
            float dist = (GetWithOffset(bfr, -1).position - bfr.GetEnd().position).magnitude;
            Vector3 crntDir = GetDirVector(GetWithOffset(bfr, -1), bfr.GetEnd());
            Vector3 pastDir = GetDirVector(GetWithOffset(bfr, -2), GetWithOffset(bfr, -1));
            float angle = Mathf.Abs(Vector3.Angle(crntDir, pastDir));
            BufferStatsInfo bsi;
            bsi.dist = dist;
            bsi.angle = angle;
            bsi.breach = angle > ANGLE_THRESHOLD;
            bsi.isSmoothedBuffer = isSmoothed;
            bsi.endRecord = bfr.GetEnd();
            bsi.prevRecord = GetWithOffset(bfr, -1);
            onBufferStats.Dispatch(bsi);
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

        private QuaternionAverage _quaternionAverage = new();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToWindow(PhysicsStateRecord accumulator, PhysicsStateRecord newItem)
        {
            accumulator.position += newItem.position;
            _quaternionAverage.AccumulateRot(newItem.rotation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FinalizeWindow(PhysicsStateRecord accumulator, int count)
        {
            accumulator.position /= count;
            accumulator.rotation = _quaternionAverage.GetAverageRotation(count);
            _quaternionAverage.Reset();
        }

        public PhysicsStateRecord GetNextProcessedState()
        {
            PhysicsStateRecord psr = PhysicsStateRecord.Alloc();
            psr.tickId = smoothingTick;
            smoothingTick++;

            int windowSize = Math.Min(slidingWindowTickSize, buffer.GetFill());
            int index = buffer.GetEndIndex() - windowSize;
            if (index < 0)
            {
                index = buffer.GetCapacity() + index;
            }

            for (int i = 0; i < windowSize; ++i)
            {
                AddToWindow(psr, buffer.Get(index));
                WindowAddInfo wai;
                wai.fill = buffer.GetFill();
                wai.windowSize = windowSize;
                wai.index = index;
                wai.addedTickId = buffer.Get(index).tickId;
                wai.data = psr;
                wai.addedData = buffer.Get(index);
                onWindowAdd.Dispatch(wai);
                index++;
            }
            FinalizeWindow(psr, windowSize);

            if (averagedBuffer.GetFill() > 1)
            {
                WindowFinalizedInfo wfi;
                wfi.tickId = psr.tickId;
                wfi.startIndex = averagedBuffer.GetStartIndex();
                wfi.endIndex = averagedBuffer.GetEndIndex();
                wfi.startTick = averagedBuffer.GetStart().tickId;
                wfi.endTick = averagedBuffer.GetEnd().tickId;
                wfi.data = psr;
                onWindowFinalized.Dispatch(wfi);
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
                slidingWindowTickSize = FOLLOWER_SMOOTH_WINDOW;
            }
        }

        // ── Events ──────────────────────────────────────────────────────────

        public struct NoDataWarningInfo
        {
            public int instanceId;
            public int debugCounter;
            public double time;
            public double deltaTime;
            public double startTime;
            public int fill;
        }
        public SafeEventDispatcher<NoDataWarningInfo> onNoInterpolationData = new();

        public struct InterpolationAppliedInfo
        {
            public int instanceId;
            public float interpTarget;
            public float deltaTime;
            public double time;
            public PhysicsStateRecord from;
            public PhysicsStateRecord to;
        }
        public SafeEventDispatcher<InterpolationAppliedInfo> onInterpolationApplied = new();

        public struct InsufficientBufferInfo { public float interpTarget; }
        public SafeEventDispatcher<InsufficientBufferInfo> onInsufficientBufferData = new();

        public struct BufferUpdateInfo
        {
            public int instanceId;
            public double time;
            public int fill;
            public PhysicsStateRecord start;
        }
        public SafeEventDispatcher<BufferUpdateInfo> onBufferUpdate = new();

        public struct VisualAdvanceInfo
        {
            public float dist;
            public float angle;
            public bool breach;
        }
        public SafeEventDispatcher<VisualAdvanceInfo> onVisualAdvance = new();

        public struct CanStartInterpolationInfo { public int instanceId; public int fill; }
        public SafeEventDispatcher<CanStartInterpolationInfo> onCanStartInterpolation = new();

        public struct NegativeInterpTargetInfo { public float t; }
        public SafeEventDispatcher<NegativeInterpTargetInfo> onNegativeInterpTarget = new();

        public struct TimePastBufferEndInfo
        {
            public double time;
            public double toTime;
            public double fromTime;
            public double startTime;
            public int fill;
        }
        public SafeEventDispatcher<TimePastBufferEndInfo> onTimePastBufferEnd = new();

        public struct PositionAddedInfo
        {
            public uint tickId;
            public Vector3 position;
            public Quaternion rotation;
        }
        public SafeEventDispatcher<PositionAddedInfo> onPositionAdded = new();

        public struct LateAddInfo
        {
            public uint tickId;
            public double deltaTime;
            public double shouldBe;
            public double missBy;
            public float percent;
        }
        public SafeEventDispatcher<LateAddInfo> onLateAdd = new();

        public struct AddTimingInfo
        {
            public bool late;
            public int frameNo;
            public double deltaT;
            public double time;
            public double endTime;
            public uint tickId;
        }
        public SafeEventDispatcher<AddTimingInfo> onAddTiming = new();

        public struct BufferStatsInfo
        {
            public float dist;
            public float angle;
            public bool breach;
            public bool isSmoothedBuffer;
            public PhysicsStateRecord endRecord;
            public PhysicsStateRecord prevRecord;
        }
        public SafeEventDispatcher<BufferStatsInfo> onBufferStats = new();

        public struct WindowAddInfo
        {
            public int fill;
            public int windowSize;
            public int index;
            public uint addedTickId;
            public PhysicsStateRecord data;
            public PhysicsStateRecord addedData;
        }
        public SafeEventDispatcher<WindowAddInfo> onWindowAdd = new();

        public struct WindowFinalizedInfo
        {
            public uint tickId;
            public int startIndex;
            public int endIndex;
            public uint startTick;
            public uint endTick;
            public PhysicsStateRecord data;
        }
        public SafeEventDispatcher<WindowFinalizedInfo> onWindowFinalized = new();
    }
}
