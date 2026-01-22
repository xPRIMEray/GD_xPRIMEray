using System;
using System.Diagnostics;

namespace XPrimeRay.Perf
{
    public enum PerfStage
    {
        FrameTotal,
        TileLoop,
        RayGen,
        Pass0_Broadphase,
        Pass1_Integrate,
        Pass1_FieldEval,
        Pass2_Subdivide,
        Shade,
        UploadTexture
    }

    public sealed class FramePerf
    {
        public long FrameIndex;

        // stage ticks
        private readonly long[] _stageTicks = new long[Enum.GetValues(typeof(PerfStage)).Length];

        // counters
        public long PixelsUpdated;
        public long RaysTraced;
        public long SegmentsIntegrated;
        public long StepsIntegrated;
        public long SegmentsTested;
        public long FieldEvals;
        public long PhysicsQueries;
        public long Pass1Raycasts;
        public long Pass1ProbeHits;
        public long Hits;
        public long EarlyOutAabb;
        public long EarlyOutFar;
        public long EarlyStopOnHitPixels;
        public long CacheHits;
        public long CacheMisses;
        public long FieldGridHits;
        public long FieldGridMisses;
        public long Pass2Skip_InsightPlane;
        public long Pass2Skip_OverlapEmpty;
        public long Pass2Skip_QuickRayMiss;
        public long Pass2Skip_SingleProbeMiss;
        public long Pass2Skip_Stride;
        public long Pass2Skip_BestHitDist;
        public long Pass2QuickRayHits;
        public long Pass2QuickRayMisses;
        public long Pass2OverlapHits;
        public long Pass2OverlapMisses;
        public long Pass2SoftGateAttempts;
        public long Pass2SoftGateHits;

        public void Reset()
        {
            FrameIndex = 0;
            Array.Clear(_stageTicks, 0, _stageTicks.Length);
            PixelsUpdated = 0;
            RaysTraced = 0;
            SegmentsIntegrated = 0;
            StepsIntegrated = 0;
            SegmentsTested = 0;
            FieldEvals = 0;
            PhysicsQueries = 0;
            Pass1Raycasts = 0;
            Pass1ProbeHits = 0;
            Hits = 0;
            EarlyOutAabb = 0;
            EarlyOutFar = 0;
            EarlyStopOnHitPixels = 0;
            CacheHits = 0;
            CacheMisses = 0;
            FieldGridHits = 0;
            FieldGridMisses = 0;
            Pass2Skip_InsightPlane = 0;
            Pass2Skip_OverlapEmpty = 0;
            Pass2Skip_QuickRayMiss = 0;
            Pass2Skip_SingleProbeMiss = 0;
            Pass2Skip_Stride = 0;
            Pass2Skip_BestHitDist = 0;
            Pass2QuickRayHits = 0;
            Pass2QuickRayMisses = 0;
            Pass2OverlapHits = 0;
            Pass2OverlapMisses = 0;
            Pass2SoftGateAttempts = 0;
            Pass2SoftGateHits = 0;
        }

        public void AddTicks(PerfStage stage, long ticks) => _stageTicks[(int)stage] += ticks;

        public double Ms(PerfStage stage)
        {
            return _stageTicks[(int)stage] * 1000.0 / Stopwatch.Frequency;
        }

        public string ToOneLineSummary()
        {
            long gridSamples = FieldGridHits + FieldGridMisses;
            double gridHitRate = gridSamples > 0 ? (double)FieldGridHits / gridSamples : 0.0;
            return
                $"Frame#{FrameIndex} " +
                $"ms(total={Ms(PerfStage.FrameTotal):0.00} tile={Ms(PerfStage.TileLoop):0.00} " +
                $"gen={Ms(PerfStage.RayGen):0.00} p0={Ms(PerfStage.Pass0_Broadphase):0.00} " +
                $"p1={Ms(PerfStage.Pass1_Integrate):0.00} field={Ms(PerfStage.Pass1_FieldEval):0.00} " +
                $"p2={Ms(PerfStage.Pass2_Subdivide):0.00} shade={Ms(PerfStage.Shade):0.00} " +
                $"upl={Ms(PerfStage.UploadTexture):0.00}) " +
                $"px={PixelsUpdated} rays={RaysTraced} seg={SegmentsIntegrated} steps={StepsIntegrated} tested={SegmentsTested} " +
                $"fieldEvals={FieldEvals} physQ={PhysicsQueries} p1rc={Pass1Raycasts} p1hit={Pass1ProbeHits} hits={Hits} " +
                $"gridHitRate={gridHitRate:0.000} grid(h={FieldGridHits} m={FieldGridMisses}) " +
                $"early(AABB={EarlyOutAabb} far={EarlyOutFar} hitStopPx={EarlyStopOnHitPixels}) cache(h={CacheHits} m={CacheMisses}) " +
                $"p2Skip(insight={Pass2Skip_InsightPlane} overlap={Pass2Skip_OverlapEmpty} qRay={Pass2Skip_QuickRayMiss} " +
                $"single={Pass2Skip_SingleProbeMiss} stride={Pass2Skip_Stride} bestDist={Pass2Skip_BestHitDist}) " +
                $"p2Broad(qRayHit={Pass2QuickRayHits} qRayMiss={Pass2QuickRayMisses} overlapHit={Pass2OverlapHits} overlapMiss={Pass2OverlapMisses}) " +
                $"p2SoftGate={Pass2SoftGateAttempts}/{Pass2SoftGateHits}";
        }
    }

    public readonly ref struct PerfScope
    {
        private readonly FramePerf _perf;
        private readonly PerfStage _stage;
        private readonly long _start;

        public PerfScope(FramePerf perf, PerfStage stage)
        {
            _perf = perf;
            _stage = stage;
            _start = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            long end = Stopwatch.GetTimestamp();
            _perf.AddTicks(_stage, end - _start);
        }
    }
}
