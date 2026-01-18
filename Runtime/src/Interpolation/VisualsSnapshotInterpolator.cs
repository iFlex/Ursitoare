using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mirror;
using Prediction.data;
using Prediction.Interpolation;
using Prediction.utils;
using UnityEngine;

namespace Adapters.Prediction
{
    public class VisualsSnapshotInterpolator : VisualsInterpolationsProvider
    {
        public static bool DEBUG = true;
        public static bool LOG_POS = true;
        public static bool INTERPOLATE = true;
        public static bool INTERPOLATE_MANUAL = true;
        
        private Transform target;
        
        RingBuffer<PhysicsStateRecord> buffer = new RingBuffer<PhysicsStateRecord>(CustomVisualInterpolator.BUFFER_SIZE);
        public RingBuffer<PhysicsStateRecord> smoothBuffer = new RingBuffer<PhysicsStateRecord>(CustomVisualInterpolator.SMOOTH_BUFFER_SIZE);
        private SortedList<double, State> snapshotBuffer = new SortedList<double, State>();
        
        private double localTimeline;
        private double localTimescale;
        
        private int sendRate = 0;
        private int bufferLimit = 30;
        private ExponentialMovingAverage driftEma;
        private ExponentialMovingAverage deliveryTimeEma;
        private SnapshotInterpolationSettings settings = new SnapshotInterpolationSettings();
        private PosAnalyser _posAnalyser = new();
        
        public double bufferTime => Time.fixedDeltaTime * settings.bufferTimeMultiplier;
        
        public struct State : Snapshot
        {
            public Vector3 position;
            public Quaternion rotation;
            public double remoteTime { get; set; }
            public double localTime { get; set; }
        }

        public VisualsSnapshotInterpolator()
        {
            sendRate = Mathf.FloorToInt(1 / Time.fixedDeltaTime);
            
            // initialize EMA with 'emaDuration' seconds worth of history.
            // 1 second holds 'sendRate' worth of values.
            // multiplied by emaDuration gives n-seconds.
            driftEma = new ExponentialMovingAverage(sendRate * settings.driftEmaDuration);
            deliveryTimeEma = new ExponentialMovingAverage(sendRate * settings.deliveryTimeEmaDuration);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float GetTime(uint tickId)
        {
            return tickId * Time.fixedDeltaTime;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float GetTime(PhysicsStateRecord record)
        {
            return GetTime(record.tickId);
        }
        
        private float timeScale = 0;
        public void Update(float deltaTime, uint currentTick)
        {
            if (smoothBuffer.GetFill() > 1 && INTERPOLATE_MANUAL)
            {
                float t = 1;
                PhysicsStateRecord to = smoothBuffer.GetEnd();
                float targetTime = GetTime(to);
                float budget = targetTime - timeScale;
                if (budget > 0 && deltaTime < budget)
                {
                    t = deltaTime / budget;
                }
                
                target.position = Vector3.Lerp(target.position, to.position, t);
                target.rotation = Quaternion.Slerp(target.rotation, to.rotation, t);
                timeScale += deltaTime;

                _posAnalyser.LogAndPrintPosRot("SELF_LERP", target.position, target.rotation);
                return;
            }
            
            if (snapshotBuffer.Count > 0)
            {
                if (INTERPOLATE)
                {
                    SnapshotInterpolation.Step(
                        snapshotBuffer, // snapshot buffer
                        deltaTime, // engine delta time (unscaled)
                        ref localTimeline, // local interpolation time based on server time
                        1, // catchup / slowdown is applied to time every update
                        out State fromSnapshot, // we interpolate 'from' this snapshot
                        out State toSnapshot, // 'to' this snapshot
                        out double t); // at ratio 't' [0,1]

                    ApplyState(fromSnapshot, toSnapshot, (float)t);
                }
                else
                {
                    State state = snapshotBuffer.Values[0];
                    target.position = state.position;
                    target.rotation = state.rotation;
                    _posAnalyser.LogAndPrintPosRot("SNAP", target.position, target.rotation);
                }
            }
        }

        public static bool CORRECT_SPIKED = true;
        public static float SPIKE_DISTANCE_THRESHOLD = 1f;
        
        public void Add(PhysicsStateRecord record)
        {
            //SMOOTHING
            PhysicsStateRecord newData = new PhysicsStateRecord();
            newData.From(record);
            buffer.Add(newData);
            smoothBuffer.Add(GetNextProcessedState());
            
            if (LOG_POS)
            {
                Debug.Log($"[Visuals][Positions] t:{record.tickId} pos:{record.position} rot:{record.rotation} posAng:{CustomVisualInterpolator.GetBufferEndAngle(buffer)}");
                PhysicsStateRecord smthR = smoothBuffer.GetEnd();
                Debug.Log($"[Visuals][SmoothState] t:{record.tickId} st:{smthR.tickId} pos:{smthR.position} rot:{smthR.rotation} posAng:{CustomVisualInterpolator.GetBufferEndAngle(smoothBuffer)}");
            }
            
            PhysicsStateRecord latest;
            if (CustomVisualInterpolator.USE_SMOOTH_BUFFER)
            {
                latest = smoothBuffer.GetEnd();
            }
            else
            {
                latest = record;
            }
            
            ////SNAPSHOT INTERPOLATION
            State state = new State();
            state.position = latest.position;
            state.rotation = latest.rotation;
            state.localTime = localTimeline;
            state.remoteTime = latest.tickId * Time.fixedDeltaTime;
            
            SnapshotInterpolation.InsertAndAdjust(
                snapshotBuffer,                                // snapshot buffer
                bufferLimit,                           // don't grow infinitely
                state,                                 // the newly received snapshot
                ref localTimeline,                     // local interpolation time based on server time
                ref localTimescale,                    // timeline multiplier to apply catchup / slowdown over time
                Time.fixedDeltaTime,        // for debugging
                bufferTime,                            // offset for buffering
                settings.catchupSpeed,                 // in % [0,1]
                settings.slowdownSpeed,                // in % [0,1]
                ref driftEma,                          // for catchup / slowdown
                settings.catchupNegativeThreshold,     // in % of sendInteral (careful, we may run out of snapshots)
                settings.catchupPositiveThreshold,     // in % of sendInterval
                ref deliveryTimeEma                    // for dynamic buffer time adjustment
                );
        }
        
        public static float ANGLE_THRESHOLD = 120;
        private Vector3 prevPos = Vector3.zero;
        private Vector3 pos = Vector3.zero;
        //NOTE: the problem here is that our averaged state is jumping around somehow...
        
        void ApplyState(State from, State to, float t)
        {
            prevPos = pos;
            pos = target.position;

            target.position = Vector3.Lerp(from.position, to.position, t);
            target.rotation = Quaternion.Slerp(from.rotation, to.rotation, t);
            _posAnalyser.LogAndPrintPosRot("SNAPSHOT_INTERP", target.position, target.rotation);
            if (DEBUG)
            {
                float dist = (target.position - pos).magnitude;
                Vector3 crntDir = target.position - pos;
                Vector3 pastDir = pos - prevPos;
                float angle = Mathf.Abs(Vector3.Angle(crntDir, pastDir));
                Debug.Log($"[VisualsSnapshotInterpolator][DT_DBG][VISUAL_ADVANCE]{(angle > ANGLE_THRESHOLD ? "[BREACH]" : "")} dist:{dist} angle:{angle}");
            }
        }
        
        public void SetInterpolationTarget(Transform t)
        {
            target = t;
        }

        public void Reset()
        {
            snapshotBuffer.Clear();
        }

        public void SetControlledLocally(bool isLocalAuthority)
        {
            //NOOP
        }
    
        QuaternionAverage quaternionAverage = new QuaternionAverage();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddToWindow(PhysicsStateRecord accumulator, PhysicsStateRecord newItem)
        {
            accumulator.position += newItem.position;
            quaternionAverage.AccumulateRot(newItem.rotation);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FinalizeWindow(PhysicsStateRecord accumulator, int count)
        {
            accumulator.position /= count;
            accumulator.rotation = quaternionAverage.GetAverageRotation(count);
            quaternionAverage.Reset();
        }
        
        private uint smoothingTick = 0;
        public int slidingWindowTickSize = 6;
        public PhysicsStateRecord GetNextProcessedState()
        {
            PhysicsStateRecord psr = new PhysicsStateRecord();
            psr.Empty();
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
                if (CustomVisualInterpolator.DEEP_DEBUG)
                {
                    Debug.Log($"[CustomVisualInterpolator][GetNextProcessedState][AddToWindow] Fill:{buffer.GetFill()} WindowSize:{windowSize} index:{index} addedTickId:{buffer.Get(index).tickId} endIndex:{buffer.GetEndIndex()} DATA:{psr} ADD_DATA:{buffer.Get(index)}");
                }
                index++;
            }
            FinalizeWindow(psr, windowSize);

            if (CustomVisualInterpolator.DEEP_DEBUG && smoothBuffer.GetFill() > 1)
            {
                Debug.Log($"[CustomVisualInterpolator][GetNextProcessedState][FinalizeWindow] tickId:{psr.tickId} startIndex:{smoothBuffer.GetStartIndex()} endIndex:{smoothBuffer.GetEndIndex()} startTick:{smoothBuffer.GetStart().tickId} endTick:{smoothBuffer.GetEnd().tickId} DATA:{psr}");
            }
            return psr;
        }
    }
}