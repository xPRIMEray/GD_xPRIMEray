using System;

namespace RendererCore.Research.TriClock;

/// <summary>
/// Opt-in stages for the speculative tri-clock sandbox.
/// The enum is intentionally descriptive so callers can keep V1 scalar-only
/// behavior isolated from later experimental coupling work.
/// </summary>
public enum TriClockAnalysisMode
{
    Disabled = 0,
    ScalarFieldOnly = 1,
    RayCoupledAccumulation = 2,
    ExperimentalTransportCoupling = 3,
}

/// <summary>
/// Selects which heuristic score should be emphasized in reports or overlays.
/// These metrics are exploratory ranking aids, not validated observables.
/// </summary>
public enum TriClockScoreMode
{
    ResonanceScore = 0,
    BeatScore = 1,
    CrossoverScore = 2,
}
