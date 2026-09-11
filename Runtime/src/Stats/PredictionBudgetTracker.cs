// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Prediction.Utils;
using Sector0.Events;
using UnityEngine;

namespace Prediction.Stats
{
    //NOTE: immutable snapshot of one measured category (e.g. all ticks, or only resimulating ticks).
    //      Durations are in milliseconds, fractions are relative to the frame budget where 1.0 means the
    //      category consumed the whole budget.
    public readonly struct BudgetCategoryStats
    {
        //NOTE: for the resimulation category this only counts ticks that actually resimulated.
        public readonly uint sampleCount;
        public readonly uint overBudgetCount;
        public readonly float lastDurationMs;
        public readonly float lastBudgetFraction;
        public readonly float maxDurationMs;
        public readonly float maxBudgetFraction;
        public readonly float totalDurationMs;
        public readonly float averageDurationMs;
        public readonly float averageBudgetFraction;

        public BudgetCategoryStats(uint sampleCount, uint overBudgetCount, float lastDurationMs,
            float lastBudgetFraction, float maxDurationMs, float maxBudgetFraction, float totalDurationMs,
            float averageDurationMs, float averageBudgetFraction)
        {
            this.sampleCount = sampleCount;
            this.overBudgetCount = overBudgetCount;
            this.lastDurationMs = lastDurationMs;
            this.lastBudgetFraction = lastBudgetFraction;
            this.maxDurationMs = maxDurationMs;
            this.maxBudgetFraction = maxBudgetFraction;
            this.totalDurationMs = totalDurationMs;
            this.averageDurationMs = averageDurationMs;
            this.averageBudgetFraction = averageBudgetFraction;
        }

        public override string ToString()
        {
            return $"n:{sampleCount} over:{overBudgetCount} last:{lastDurationMs:F3}ms({lastBudgetFraction:F3}) max:{maxDurationMs:F3}ms({maxBudgetFraction:F3}) avg:{averageDurationMs:F3}ms({averageBudgetFraction:F3}) total:{totalDurationMs:F3}ms";
        }
    }

    //NOTE: what the last N ticks look like. Cumulative stats cannot show a spike, a session long max latches the
    //      worst tick forever and a session long average dilutes a burst, so the recent window is what a reaction
    //      should read.
    public readonly struct BudgetWindowStats
    {
        public readonly uint capacity;
        public readonly uint sampleCount;
        public readonly uint resimSampleCount;
        //NOTE: average duration of the ticks in the window that did not resimulate. This is the cost a tick pays
        //      no matter what, so it is the reference the marginal resimulation cost is measured against.
        public readonly float baselineDurationMs;
        public readonly float resimAverageDurationMs;
        //NOTE: what resimulation added on top of the baseline, i.e. what throttling it could recover.
        public readonly float marginalResimAverageMs;
        //NOTE: share of the total window budget spent on the marginal resimulation cost.
        public readonly float resimLoadFraction;
        public readonly float maxDurationMs;
        public readonly float maxBudgetFraction;

        public BudgetWindowStats(uint capacity, uint sampleCount, uint resimSampleCount, float baselineDurationMs,
            float resimAverageDurationMs, float marginalResimAverageMs, float resimLoadFraction, float maxDurationMs,
            float maxBudgetFraction)
        {
            this.capacity = capacity;
            this.sampleCount = sampleCount;
            this.resimSampleCount = resimSampleCount;
            this.baselineDurationMs = baselineDurationMs;
            this.resimAverageDurationMs = resimAverageDurationMs;
            this.marginalResimAverageMs = marginalResimAverageMs;
            this.resimLoadFraction = resimLoadFraction;
            this.maxDurationMs = maxDurationMs;
            this.maxBudgetFraction = maxBudgetFraction;
        }

        public override string ToString()
        {
            return $"n:{sampleCount}/{capacity} resims:{resimSampleCount} baseline:{baselineDurationMs:F3}ms resimAvg:{resimAverageDurationMs:F3}ms marginal:{marginalResimAverageMs:F3}ms load:{resimLoadFraction:F3} max:{maxDurationMs:F3}ms({maxBudgetFraction:F3})";
        }
    }

    //NOTE: one resimulating tick whose marginal cost stood out against the recent baseline. Dispatched on the tick
    //      it happened, so a reaction can tune resimulation down while the spike is still current.
    public readonly struct ResimulationSpike
    {
        public readonly float tickDurationMs;
        public readonly float tickBudgetFraction;
        public readonly float baselineDurationMs;
        //NOTE: tick duration minus the baseline, i.e. what this resimulation actually added.
        public readonly float marginalDurationMs;
        public readonly float marginalBudgetFraction;
        //NOTE: how many times the baseline the marginal cost was. This is what crossed the threshold.
        public readonly float baselineMultiple;
        public readonly float windowResimLoadFraction;

        public ResimulationSpike(float tickDurationMs, float tickBudgetFraction, float baselineDurationMs,
            float marginalDurationMs, float marginalBudgetFraction, float baselineMultiple,
            float windowResimLoadFraction)
        {
            this.tickDurationMs = tickDurationMs;
            this.tickBudgetFraction = tickBudgetFraction;
            this.baselineDurationMs = baselineDurationMs;
            this.marginalDurationMs = marginalDurationMs;
            this.marginalBudgetFraction = marginalBudgetFraction;
            this.baselineMultiple = baselineMultiple;
            this.windowResimLoadFraction = windowResimLoadFraction;
        }

        public override string ToString()
        {
            return $"tick:{tickDurationMs:F3}ms({tickBudgetFraction:F3}) baseline:{baselineDurationMs:F3}ms marginal:{marginalDurationMs:F3}ms({marginalBudgetFraction:F3}) x{baselineMultiple:F2} load:{windowResimLoadFraction:F3}";
        }
    }

    //NOTE: the resimulation category is a subset of the tick category: a tick that resimulates has its whole
    //      duration counted in both.
    public readonly struct PredictionBudgetStats
    {
        public readonly float budgetMs;
        public readonly BudgetCategoryStats tick;
        public readonly BudgetCategoryStats resimulation;
        public readonly BudgetWindowStats window;
        public readonly uint totalSpikes;

        public PredictionBudgetStats(float budgetMs, BudgetCategoryStats tick, BudgetCategoryStats resimulation,
            BudgetWindowStats window, uint totalSpikes)
        {
            this.budgetMs = budgetMs;
            this.tick = tick;
            this.resimulation = resimulation;
            this.window = window;
            this.totalSpikes = totalSpikes;
        }

        public override string ToString()
        {
            return $"budget:{budgetMs:F3}ms spikes:{totalSpikes} tick[{tick}] resim[{resimulation}] window[{window}]";
        }
    }

    //NOTE: tracks how much of the per frame time budget the prediction tick consumes, and separately how much of it
    //      the ticks that resimulated consumed. Compose it into a ticking system by calling BeginTick at the start
    //      of the tick and EndTick at the end of it.
    public class PredictionBudgetTracker
    {
        public const int DEFAULT_WINDOW_CAPACITY = 64;

        static readonly double MILLISECONDS_PER_TIMESTAMP = 1000.0 / Stopwatch.Frequency;

        //NOTE: the budget is the fixed timestep by default, read on every tick so a runtime change of it is picked
        //      up. Replace it to decouple the tracker from Time.fixedDeltaTime.
        public Func<float> budgetSecondsProvider = () => Time.fixedDeltaTime;

        //NOTE: a resimulating tick spikes when its marginal cost exceeds this many times the recent baseline.
        public float spikeBaselineMultiplier = 10f;
        //NOTE: no spike is reported until the window holds at least this many non resimulating ticks, otherwise the
        //      baseline is too thin to compare against.
        public uint minBaselineSamples = 8;

        //NOTE: dispatched on the spiking tick itself. Advisory only, the tracker never tunes prediction by itself.
        public SafeEventDispatcher<ResimulationSpike> onResimulationSpike = new();

        public uint totalSpikes { get; private set; }
        public ResimulationSpike lastSpike { get; private set; }

        struct CategoryAccumulator
        {
            public uint sampleCount;
            public uint overBudgetCount;
            public long lastDurationTimestamps;
            public long maxDurationTimestamps;
            public long totalDurationTimestamps;
            public double lastBudgetFraction;
            public double maxBudgetFraction;
            public double totalBudgetFraction;

            public void Record(long durationTimestamps, double budgetTimestamps)
            {
                sampleCount++;
                lastDurationTimestamps = durationTimestamps;
                totalDurationTimestamps += durationTimestamps;
                if (durationTimestamps > maxDurationTimestamps)
                {
                    maxDurationTimestamps = durationTimestamps;
                }

                //NOTE: budgetTimestamps can be zero when no budget is available, keep fractions at zero then.
                double fraction = budgetTimestamps > 0 ? durationTimestamps / budgetTimestamps : 0;
                lastBudgetFraction = fraction;
                totalBudgetFraction += fraction;
                if (fraction > maxBudgetFraction)
                {
                    maxBudgetFraction = fraction;
                }
                if (fraction > 1.0)
                {
                    overBudgetCount++;
                }
            }

            public void Reset()
            {
                this = default;
            }

            public BudgetCategoryStats ToStats()
            {
                double inverseSampleCount = sampleCount > 0 ? 1.0 / sampleCount : 0;
                return new BudgetCategoryStats(
                    sampleCount,
                    overBudgetCount,
                    (float)(lastDurationTimestamps * MILLISECONDS_PER_TIMESTAMP),
                    (float)lastBudgetFraction,
                    (float)(maxDurationTimestamps * MILLISECONDS_PER_TIMESTAMP),
                    (float)maxBudgetFraction,
                    (float)(totalDurationTimestamps * MILLISECONDS_PER_TIMESTAMP),
                    (float)(totalDurationTimestamps * MILLISECONDS_PER_TIMESTAMP * inverseSampleCount),
                    (float)(totalBudgetFraction * inverseSampleCount));
            }
        }

        struct WindowSample
        {
            public long durationTimestamps;
            public double budgetTimestamps;
            public bool resimulated;
        }

        long tickStartTimestamp = 0;
        double lastBudgetTimestamps = 0;
        CategoryAccumulator tickStats;
        CategoryAccumulator resimulationStats;

        //NOTE: the running sums are kept incrementally so the per tick spike check stays a few arithmetic ops. The
        //      window itself is only walked when stats are queried.
        readonly RingBuffer<WindowSample> window;
        long windowResimDurationSum;
        long windowNonResimDurationSum;
        uint windowResimCount;
        double windowBudgetSum;

        public PredictionBudgetTracker() : this(DEFAULT_WINDOW_CAPACITY)
        {
        }

        public PredictionBudgetTracker(int windowCapacity)
        {
            if (windowCapacity < 1)
            {
                throw new ArgumentException("INVALID_CONFIG: windowCapacity must be at least 1", nameof(windowCapacity));
            }
            window = new RingBuffer<WindowSample>(windowCapacity);
        }

        public void BeginTick()
        {
            tickStartTimestamp = Stopwatch.GetTimestamp();
        }

        //NOTE: a resimulating tick has its whole duration counted in both categories.
        public void EndTick(bool resimulated)
        {
            long durationTimestamps = Stopwatch.GetTimestamp() - tickStartTimestamp;
            lastBudgetTimestamps = (double)budgetSecondsProvider() * Stopwatch.Frequency;

            tickStats.Record(durationTimestamps, lastBudgetTimestamps);
            if (resimulated)
            {
                resimulationStats.Record(durationTimestamps, lastBudgetTimestamps);
            }

            PushWindowSample(durationTimestamps, lastBudgetTimestamps, resimulated);

            if (resimulated)
            {
                CheckForSpike(durationTimestamps, lastBudgetTimestamps);
            }
        }

        public PredictionBudgetStats GetStats()
        {
            return new PredictionBudgetStats(
                (float)(lastBudgetTimestamps * MILLISECONDS_PER_TIMESTAMP),
                tickStats.ToStats(),
                resimulationStats.ToStats(),
                GetWindowStats(),
                totalSpikes);
        }

        public BudgetWindowStats GetWindowStats()
        {
            uint sampleCount = (uint)window.GetFill();
            uint baselineCount = sampleCount - windowResimCount;
            double baseline = baselineCount > 0 ? windowNonResimDurationSum / (double)baselineCount : 0;
            double resimAverage = windowResimCount > 0 ? windowResimDurationSum / (double)windowResimCount : 0;
            double marginalAverage = resimAverage > baseline ? resimAverage - baseline : 0;

            long maxDurationTimestamps = 0;
            double maxBudgetFraction = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                WindowSample sample = window.GetWithLocalIndex(i);
                if (sample.durationTimestamps > maxDurationTimestamps)
                {
                    maxDurationTimestamps = sample.durationTimestamps;
                }

                double fraction = sample.budgetTimestamps > 0
                    ? sample.durationTimestamps / sample.budgetTimestamps
                    : 0;
                if (fraction > maxBudgetFraction)
                {
                    maxBudgetFraction = fraction;
                }
            }

            return new BudgetWindowStats(
                (uint)window.GetCapacity(),
                sampleCount,
                windowResimCount,
                (float)(baseline * MILLISECONDS_PER_TIMESTAMP),
                (float)(resimAverage * MILLISECONDS_PER_TIMESTAMP),
                (float)(marginalAverage * MILLISECONDS_PER_TIMESTAMP),
                (float)ComputeWindowResimLoadFraction(baseline),
                (float)(maxDurationTimestamps * MILLISECONDS_PER_TIMESTAMP),
                (float)maxBudgetFraction);
        }

        public void Reset()
        {
            tickStats.Reset();
            resimulationStats.Reset();
            lastBudgetTimestamps = 0;

            window.Clear();
            windowResimDurationSum = 0;
            windowNonResimDurationSum = 0;
            windowResimCount = 0;
            windowBudgetSum = 0;

            totalSpikes = 0;
            lastSpike = default;
        }

        void PushWindowSample(long durationTimestamps, double budgetTimestamps, bool resimulated)
        {
            if (window.GetFill() == window.GetCapacity())
            {
                //NOTE: RingBuffer.Add silently drops the oldest sample, so read it out before it is overwritten to
                //      keep the running sums in step with the window contents.
                WindowSample evicted = window.GetStart();
                windowBudgetSum -= evicted.budgetTimestamps;
                if (evicted.resimulated)
                {
                    windowResimDurationSum -= evicted.durationTimestamps;
                    windowResimCount--;
                }
                else
                {
                    windowNonResimDurationSum -= evicted.durationTimestamps;
                }
            }

            window.Add(new WindowSample
            {
                durationTimestamps = durationTimestamps,
                budgetTimestamps = budgetTimestamps,
                resimulated = resimulated
            });

            windowBudgetSum += budgetTimestamps;
            if (resimulated)
            {
                windowResimDurationSum += durationTimestamps;
                windowResimCount++;
            }
            else
            {
                windowNonResimDurationSum += durationTimestamps;
            }
        }

        void CheckForSpike(long durationTimestamps, double budgetTimestamps)
        {
            uint baselineCount = (uint)window.GetFill() - windowResimCount;
            if (baselineCount < minBaselineSamples)
                return;

            double baseline = windowNonResimDurationSum / (double)baselineCount;
            if (baseline <= 0)
                return;

            double marginal = durationTimestamps - baseline;
            if (marginal <= baseline * spikeBaselineMultiplier)
                return;

            lastSpike = new ResimulationSpike(
                (float)(durationTimestamps * MILLISECONDS_PER_TIMESTAMP),
                (float)(budgetTimestamps > 0 ? durationTimestamps / budgetTimestamps : 0),
                (float)(baseline * MILLISECONDS_PER_TIMESTAMP),
                (float)(marginal * MILLISECONDS_PER_TIMESTAMP),
                (float)(budgetTimestamps > 0 ? marginal / budgetTimestamps : 0),
                (float)(marginal / baseline),
                (float)ComputeWindowResimLoadFraction(baseline));
            totalSpikes++;

            onResimulationSpike.Dispatch(lastSpike);
        }

        //NOTE: the share of the window budget that resimulation added on top of the baseline. This is the number a
        //      reaction should throttle against, the total resimulating tick time includes cost that would be paid
        //      anyway.
        double ComputeWindowResimLoadFraction(double baseline)
        {
            if (windowBudgetSum <= 0 || windowResimCount == 0)
                return 0;

            double marginalSum = windowResimDurationSum - baseline * windowResimCount;
            return marginalSum > 0 ? marginalSum / windowBudgetSum : 0;
        }
    }
}
