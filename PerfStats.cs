using Godot;
using System;
using System.Text;

[Flags]
public enum PerfReasonFlags
{
	None = 0,
	NoPixels = 1 << 0,
	NoSegs = 1 << 1,
	NoSegsTested = 1 << 2,
	NoSubRayCalls = 1 << 3,
	NoHits = 1 << 4,
	ShadingSkippedNoHits = 1 << 5,
	ResizedFilm = 1 << 6
}

public struct PerfFrameReport
{
	public double Pass1Ms;
	public double Pass2PhysMs;
	public double Pass2ShadeMs;
	public double FilmUpdateMs;
	public double OverlayBuildMs;
	public double OverlayEnqueueMs;

	public int Pixels;
	public int TracedPixels;
	public int FilledPixels;
	public int EffectiveStride;
	public int EffectiveWidth;
	public int EffectiveHeight;
	public int EffectiveRenderPixels;
	public int Segs;
	public int SegsTested;
	public int Hits;
	public int IntersectRayCalls;
	public int IntersectShapeCalls;
	public int SubdividedRayCalls;
	public int SubdividedRayQueries;
	public int SubdividedRaySkipped;
	public int SubRaySkippedByStride;
	public int SubdividedRaySubsteps;
	public int ShadingSkippedPixels;
	public int Pass2ForceStride1Pixels;
	public int Pass2ForceInstabilityPixels;
	public int Pass2ForcePrevHitLostPixels;
	public int BackfaceNdotVHits;
	public long Pass2SoftGateAttempts;
	public long Pass2SoftGateHits;
	public long SoftGateTriggered;
	public long SoftGateAttempted;
	public long SoftGateHitChangedResult;
	public long SoftGateNewPixelFilled;
	public long SoftGateCandidateNull;
	public long SoftGateLoopGuardTripped;
	public long PixelDeltaChanged;
	public long PixelDeltaNewFilled;
	public long SegsSkippedByPass2Stride; // skipped expensive subdivided pass due to stride
	public long SegsForcedTestByPass2Stride; // forced subdivided pass due to stride (first/last/short)
	public long Pass2StrideSum;
	public long Pass2StrideCount;
	public long BandSegsIntegrated;
	public long BandSegsTested;
	public long BandPhysicsQueries;
	public bool ShadingSkippedNoHits;
	public bool RequireHitToRender;
	public bool ResizedFilm;

	public PerfReasonFlags ReasonFlags;

	public void Reset()
	{
		this = default;
	}

	public void AddPass1Usec(ulong usec) => Pass1Ms += usec * 0.001;
	public void AddPass2PhysUsec(ulong usec) => Pass2PhysMs += usec * 0.001;
	public void AddPass2ShadeUsec(ulong usec) => Pass2ShadeMs += usec * 0.001;
	public void AddFilmUpdateUsec(ulong usec) => FilmUpdateMs += usec * 0.001;
	public void AddOverlayBuildUsec(ulong usec) => OverlayBuildMs += usec * 0.001;
	public void AddOverlayEnqueueUsec(ulong usec) => OverlayEnqueueMs += usec * 0.001;

	public void UpdateReasonFlags()
	{
		PerfReasonFlags flags = PerfReasonFlags.None;
		if (Pixels <= 0) flags |= PerfReasonFlags.NoPixels;
		if (Segs <= 0) flags |= PerfReasonFlags.NoSegs;
		if (SegsTested <= 0) flags |= PerfReasonFlags.NoSegsTested;
		if (SubdividedRayCalls <= 0) flags |= PerfReasonFlags.NoSubRayCalls;
		if (Hits <= 0) flags |= PerfReasonFlags.NoHits;
		if (ShadingSkippedNoHits) flags |= PerfReasonFlags.ShadingSkippedNoHits;
		if (ResizedFilm) flags |= PerfReasonFlags.ResizedFilm;
		ReasonFlags = flags;
	}
}

public sealed class PerfStats
{
	private readonly PerfFrameReport[] _frames;
	private int _writeIndex;
	private int _count;
	private PerfFrameSums _sum;
	private readonly StringBuilder _sb = new StringBuilder(512);

#if DEBUG
	private bool _warnedSegsTested;
	private bool _warnedSubRaySkipped;
	private bool _warnedShadingSkip;
#endif

	private struct PerfFrameSums
	{
		public double Pass1Ms;
		public double Pass2PhysMs;
		public double Pass2ShadeMs;
		public double FilmUpdateMs;
		public double OverlayBuildMs;
		public double OverlayEnqueueMs;
		public long Pixels;
		public long TracedPixels;
		public long FilledPixels;
		public long EffectiveRenderPixels;
		public long Segs;
		public long SegsTested;
		public long Hits;
		public long IntersectRayCalls;
		public long IntersectShapeCalls;
		public long SubdividedRayCalls;
		public long SubdividedRayQueries;
		public long SubdividedRaySkipped;
		public long SubRaySkippedByStride;
		public long SubdividedRaySubsteps;
		public long ShadingSkippedPixels;
		public long Pass2ForceStride1Pixels;
		public long Pass2ForceInstabilityPixels;
		public long Pass2ForcePrevHitLostPixels;
		public long BackfaceNdotVHits;
		public long Pass2SoftGateAttempts;
		public long Pass2SoftGateHits;
		public long SoftGateTriggered;
		public long SoftGateAttempted;
		public long SoftGateHitChangedResult;
		public long SoftGateNewPixelFilled;
		public long SoftGateCandidateNull;
		public long SoftGateLoopGuardTripped;
		public long PixelDeltaChanged;
		public long PixelDeltaNewFilled;
	}

	public PerfStats(int windowSize = 60)
	{
		windowSize = Math.Max(1, windowSize);
		_frames = new PerfFrameReport[windowSize];
	}

	public int WindowSize => _frames.Length;

	public void FinalizeAndPrint(ref PerfFrameReport frame, bool verbose)
	{
		frame.UpdateReasonFlags();
		AddFrame(in frame);
		Print(in frame, verbose);
#if DEBUG
		CheckInvariants(in frame);
#endif
	}

	private void AddFrame(in PerfFrameReport frame)
	{
		if (_count == _frames.Length)
		{
			RemoveFrame(in _frames[_writeIndex]);
		}
		else
		{
			_count++;
		}

		_frames[_writeIndex] = frame;
		ApplyFrame(in frame, 1);

		_writeIndex++;
		if (_writeIndex >= _frames.Length) _writeIndex = 0;
	}

	private void ApplyFrame(in PerfFrameReport frame, int sign)
	{
		_sum.Pass1Ms += frame.Pass1Ms * sign;
		_sum.Pass2PhysMs += frame.Pass2PhysMs * sign;
		_sum.Pass2ShadeMs += frame.Pass2ShadeMs * sign;
		_sum.FilmUpdateMs += frame.FilmUpdateMs * sign;
		_sum.OverlayBuildMs += frame.OverlayBuildMs * sign;
		_sum.OverlayEnqueueMs += frame.OverlayEnqueueMs * sign;
		_sum.Pixels += frame.Pixels * sign;
		_sum.TracedPixels += frame.TracedPixels * sign;
		_sum.FilledPixels += frame.FilledPixels * sign;
		_sum.EffectiveRenderPixels += frame.EffectiveRenderPixels * sign;
		_sum.Segs += frame.Segs * sign;
		_sum.SegsTested += frame.SegsTested * sign;
		_sum.Hits += frame.Hits * sign;
		_sum.IntersectRayCalls += frame.IntersectRayCalls * sign;
		_sum.IntersectShapeCalls += frame.IntersectShapeCalls * sign;
		_sum.SubdividedRayCalls += frame.SubdividedRayCalls * sign;
		_sum.SubdividedRayQueries += frame.SubdividedRayQueries * sign;
		_sum.SubdividedRaySkipped += frame.SubdividedRaySkipped * sign;
		_sum.SubRaySkippedByStride += frame.SubRaySkippedByStride * sign;
		_sum.SubdividedRaySubsteps += frame.SubdividedRaySubsteps * sign;
		_sum.ShadingSkippedPixels += frame.ShadingSkippedPixels * sign;
		_sum.Pass2ForceStride1Pixels += frame.Pass2ForceStride1Pixels * sign;
		_sum.Pass2ForceInstabilityPixels += frame.Pass2ForceInstabilityPixels * sign;
		_sum.Pass2ForcePrevHitLostPixels += frame.Pass2ForcePrevHitLostPixels * sign;
		_sum.BackfaceNdotVHits += frame.BackfaceNdotVHits * sign;
		_sum.Pass2SoftGateAttempts += frame.Pass2SoftGateAttempts * sign;
		_sum.Pass2SoftGateHits += frame.Pass2SoftGateHits * sign;
		_sum.SoftGateTriggered += frame.SoftGateTriggered * sign;
		_sum.SoftGateAttempted += frame.SoftGateAttempted * sign;
		_sum.SoftGateHitChangedResult += frame.SoftGateHitChangedResult * sign;
		_sum.SoftGateNewPixelFilled += frame.SoftGateNewPixelFilled * sign;
		_sum.SoftGateCandidateNull += frame.SoftGateCandidateNull * sign;
		_sum.SoftGateLoopGuardTripped += frame.SoftGateLoopGuardTripped * sign;
		_sum.PixelDeltaChanged += frame.PixelDeltaChanged * sign;
		_sum.PixelDeltaNewFilled += frame.PixelDeltaNewFilled * sign;
	}

	private void RemoveFrame(in PerfFrameReport frame)
	{
		ApplyFrame(in frame, -1);
	}

	private void Print(in PerfFrameReport frame, bool verbose)
	{
		double inv = _count > 0 ? 1.0 / _count : 0.0;
		double avgPass1 = _sum.Pass1Ms * inv;
		double avgPass2Phys = _sum.Pass2PhysMs * inv;
		double avgPass2Shade = _sum.Pass2ShadeMs * inv;
		double avgFilmUpdate = _sum.FilmUpdateMs * inv;
		double avgOverlayBuild = _sum.OverlayBuildMs * inv;
		double avgOverlayEnqueue = _sum.OverlayEnqueueMs * inv;

		double avgPixels = _sum.Pixels * inv;
		double avgTracedPixels = _sum.TracedPixels * inv;
		double avgFilledPixels = _sum.FilledPixels * inv;
		double avgEffectiveRenderPixels = _sum.EffectiveRenderPixels * inv;
		double avgSegs = _sum.Segs * inv;
		double avgSegsTested = _sum.SegsTested * inv;
		double avgHits = _sum.Hits * inv;
		double avgSubRayCalls = _sum.SubdividedRayCalls * inv;
		double avgSubRaySubsteps = _sum.SubdividedRaySubsteps * inv;

		int tracedPixels = frame.TracedPixels > 0 ? frame.TracedPixels : frame.Pixels;
		double avgSegPerPixel = tracedPixels > 0 ? (double)frame.Segs / tracedPixels : 0.0;
		double avgSegsTestedPerPixel = tracedPixels > 0 ? (double)frame.SegsTested / tracedPixels : 0.0;
		double avgSubsteps = frame.SubdividedRayCalls > 0 ? (double)frame.SubdividedRaySubsteps / frame.SubdividedRayCalls : 0.0;
		double hitPct = tracedPixels > 0 ? (frame.Hits * 100.0) / tracedPixels : 0.0;

		double avgSegPerPixelRoll = avgTracedPixels > 0 ? avgSegs / avgTracedPixels : 0.0;
		double avgSegsTestedPerPixelRoll = avgTracedPixels > 0 ? avgSegsTested / avgTracedPixels : 0.0;
		double avgSubstepsRoll = avgSubRayCalls > 0 ? avgSubRaySubsteps / avgSubRayCalls : 0.0;
		double hitPctRoll = avgTracedPixels > 0 ? (avgHits * 100.0) / avgTracedPixels : 0.0;
		double avgP2Stride = frame.Pass2StrideCount > 0 ? (double)frame.Pass2StrideSum / frame.Pass2StrideCount : 1.0;

		if (verbose)
		{
			GD.Print($"Film frame stats: pixels={frame.Pixels} traced={frame.TracedPixels} filled={frame.FilledPixels} effPx={frame.EffectiveRenderPixels} segs={frame.Segs} segsTested={frame.SegsTested} hits={frame.Hits} qRay={frame.IntersectRayCalls} overlap={frame.IntersectShapeCalls} subRayCalls={frame.SubdividedRayCalls} subRayQueries={frame.SubdividedRayQueries} subRaySkipped={frame.SubdividedRaySkipped} subRaySkipStride={frame.SubRaySkippedByStride} p2ForcePx={frame.Pass2ForceStride1Pixels} p2ForceInstabilityPx={frame.Pass2ForceInstabilityPixels} p2ForcePrevHitLostPx={frame.Pass2ForcePrevHitLostPixels} p2SoftGate={frame.Pass2SoftGateAttempts}/{frame.Pass2SoftGateHits} sgTrig={frame.SoftGateTriggered} sgAttempt={frame.SoftGateAttempted} sgHitChange={frame.SoftGateHitChangedResult} sgNewPx={frame.SoftGateNewPixelFilled} sgNull={frame.SoftGateCandidateNull} sgGuard={frame.SoftGateLoopGuardTripped} pixDelta={frame.PixelDeltaChanged} pixNew={frame.PixelDeltaNewFilled} backfaceNdotV={frame.BackfaceNdotVHits} skipSegs={frame.SegsSkippedByPass2Stride} forceSegs={frame.SegsForcedTestByPass2Stride} avgP2Stride={avgP2Stride:0.00}");
			string statFlags = FormatReasonFlags(frame.ReasonFlags);
			GD.Print($"Film physics summary: avgSegPerPixel={avgSegPerPixel:0.00} avgSegsTestedPerPixel={avgSegsTestedPerPixel:0.00} avgSubsteps={avgSubsteps:0.00} hitPct={hitPct:0.00}%{(statFlags.Length > 0 ? " " + statFlags : string.Empty)}");
			GD.Print($"Film timings(ms): pass1={frame.Pass1Ms:0.00} pass2.physics={frame.Pass2PhysMs:0.00} pass2.shading={frame.Pass2ShadeMs:0.00} film.update={frame.FilmUpdateMs:0.00} overlay.build={frame.OverlayBuildMs:0.00} overlay.enqueue={frame.OverlayEnqueueMs:0.00}");
			GD.Print($"Film avg(ms): pass1={avgPass1:0.00} pass2.physics={avgPass2Phys:0.00} pass2.shading={avgPass2Shade:0.00} film.update={avgFilmUpdate:0.00} overlay.build={avgOverlayBuild:0.00} overlay.enqueue={avgOverlayEnqueue:0.00}");
			GD.Print($"Film avg summary: avgSegPerPixel={avgSegPerPixelRoll:0.00} avgSegsTestedPerPixel={avgSegsTestedPerPixelRoll:0.00} avgSubsteps={avgSubstepsRoll:0.00} hitPct={hitPctRoll:0.00}% avgTracedPixels={avgTracedPixels:0.00} avgFilledPixels={avgFilledPixels:0.00} avgEffPx={avgEffectiveRenderPixels:0.00}");
			return;
		}

		_sb.Clear();
		_sb.Append("Film perf: px=").Append(frame.Pixels)
			.Append(" tpx=").Append(frame.TracedPixels)
			.Append(" fpx=").Append(frame.FilledPixels)
			.Append(" effPx=").Append(frame.EffectiveRenderPixels)
			.Append(" segs=").Append(frame.Segs)
			.Append(" tested=").Append(frame.SegsTested)
			.Append(" hits=").Append(frame.Hits)
			.Append(" hitPct=").Append(hitPct.ToString("0.00"))
			.Append("% avgSeg=").Append(avgSegPerPixel.ToString("0.00"))
			.Append(" avgTested=").Append(avgSegsTestedPerPixel.ToString("0.00"))
			.Append(" avgSub=").Append(avgSubsteps.ToString("0.00"))
			.Append(" pxStride=").Append(frame.EffectiveStride)
			.Append(" avgP2Stride=").Append(avgP2Stride.ToString("0.00"))
			.Append(" p2ForcePx=").Append(frame.Pass2ForceStride1Pixels)
			.Append(" p2ForceInstabilityPx=").Append(frame.Pass2ForceInstabilityPixels)
			.Append(" p2ForcePrevHitLostPx=").Append(frame.Pass2ForcePrevHitLostPixels)
			.Append(" p2SoftGate=").Append(frame.Pass2SoftGateAttempts).Append("/").Append(frame.Pass2SoftGateHits)
			.Append(" sgTrig=").Append(frame.SoftGateTriggered)
			.Append(" sgAttempt=").Append(frame.SoftGateAttempted)
			.Append(" sgHitChange=").Append(frame.SoftGateHitChangedResult)
			.Append(" sgNewPx=").Append(frame.SoftGateNewPixelFilled)
			.Append(" sgNull=").Append(frame.SoftGateCandidateNull)
			.Append(" sgGuard=").Append(frame.SoftGateLoopGuardTripped)
			.Append(" pixDelta=").Append(frame.PixelDeltaChanged)
			.Append(" pixNew=").Append(frame.PixelDeltaNewFilled)
			.Append(" backfaceNdotV=").Append(frame.BackfaceNdotVHits)
			.Append(" skipSegs=").Append(frame.SegsSkippedByPass2Stride)
			.Append(" forceSegs=").Append(frame.SegsForcedTestByPass2Stride)
			.Append(" eff=").Append(frame.EffectiveWidth).Append("x").Append(frame.EffectiveHeight)
			.Append(" ms p1=").Append(frame.Pass1Ms.ToString("0.00"))
			.Append(" p2p=").Append(frame.Pass2PhysMs.ToString("0.00"))
			.Append(" p2s=").Append(frame.Pass2ShadeMs.ToString("0.00"))
			.Append(" upd=").Append(frame.FilmUpdateMs.ToString("0.00"))
			.Append(" ovB=").Append(frame.OverlayBuildMs.ToString("0.00"))
			.Append(" ovE=").Append(frame.OverlayEnqueueMs.ToString("0.00"))
			.Append(" avg(ms) p1=").Append(avgPass1.ToString("0.00"))
			.Append(" p2p=").Append(avgPass2Phys.ToString("0.00"))
			.Append(" p2s=").Append(avgPass2Shade.ToString("0.00"))
			.Append(" upd=").Append(avgFilmUpdate.ToString("0.00"))
			.Append(" ovB=").Append(avgOverlayBuild.ToString("0.00"))
			.Append(" ovE=").Append(avgOverlayEnqueue.ToString("0.00"));

		string flags = FormatReasonFlags(frame.ReasonFlags);
		if (flags.Length > 0) _sb.Append(" ").Append(flags);

		GD.Print(_sb.ToString());
	}

	private static string FormatReasonFlags(PerfReasonFlags flags)
	{
		if (flags == PerfReasonFlags.None) return string.Empty;
		return $"flags={flags}";
	}

#if DEBUG
	private void CheckInvariants(in PerfFrameReport frame)
	{
		if (!_warnedSegsTested && frame.SegsTested > frame.Segs)
		{
			_warnedSegsTested = true;
			GD.PushWarning($"[PerfStats] Invariant: segsTested ({frame.SegsTested}) > segs ({frame.Segs}).");
		}

		if (!_warnedSubRaySkipped && frame.SubdividedRaySkipped > frame.SegsTested)
		{
			_warnedSubRaySkipped = true;
			GD.PushWarning($"[PerfStats] Invariant: subRaySkipped ({frame.SubdividedRaySkipped}) > segsTested ({frame.SegsTested}). Expected: skipped sub-rays imply a tested segment.");
		}

		if (!_warnedShadingSkip && frame.RequireHitToRender && frame.ShadingSkippedNoHits == false && frame.Hits == 0 && frame.TracedPixels > 0)
		{
			_warnedShadingSkip = true;
			GD.PushWarning("[PerfStats] RequireHitToRender is enabled and hits==0, but shading did not short-circuit.");
		}
	}
#endif
}
