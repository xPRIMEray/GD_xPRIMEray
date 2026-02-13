# Specification — Telemetry, Debug, and Diagnostics

**Charter section:** §12 Telemetry and Debug
**Status:** Implemented
**Key source files:** `RendererCore/Common/DebugOverlayBus.cs`, `RendererCore/Common/DebugLogConfig.cs`, `RendererCore/Common/FrameSnapshotBus.cs`, `PerfScope.cs`, `PerfStats.cs`, `FieldProbe3D.cs`

---

## 1) Purpose

The telemetry system collects per-frame performance data, supports debug
visualisation, and provides diagnostic nodes for field inspection. It is
designed to explain performance regressions, validate correctness, and
support iterative development.

---

## 2) PerfScope / PerfStats (Implemented)

### PerfScope

Frame-scoped timers and counters. Created per `RenderStep`, accumulates:
- Stage timing (snapshot build, Pass-1, Pass-2, film upload)
- Pixel/segment/hit counters
- Budget utilisation and guard exit reasons

### PerfStats

Rolling-window aggregation over N frames:
- Min/max/average per metric
- Invariant checks (e.g. hit-rate sanity, prune ratio bounds)
- Anomaly detection (sudden throughput drops)

---

## 3) DebugOverlayBus (Implemented)

Global static bus for 2D debug overlays drawn on top of the rendered film.

```csharp
public static class DebugOverlayBus
{
    public static void ClearFrame();
    public static void AddLine(Vector2 a, Vector2 b, Color color, float thickness = 1f);
    public static void AddText(Vector2 pos, string text, Color color);
    public static IReadOnlyList<DebugOverlayItem> Items { get; }
}
```

Items are accumulated during a frame, then drawn by the camera node's
`_Draw` override. Cleared each frame via `ClearFrame()`.

Used for: segment visualisation, TLAS bounds, hit markers, field probe readouts.

---

## 4) DebugLogConfig (Implemented)

Static runtime toggles for diagnostic logging:

```csharp
public static class DebugLogConfig
{
    public static bool EnableSnapshotLog = true;
    public static float SnapshotLogIntervalSec = 1.0f;
    public static bool EnableProbeLog = true;
    public static float ProbeLogIntervalSec = 1.0f;
    public static bool EnableGeomRejectSample = false;
}
```

Controls rate-limited `[SNAPSHOT]`, `[PROBE]`, and geometry rejection logs.

---

## 5) FieldProbe3D (Implemented)

Scene-tree node that queries `FieldSystem.AccelAt` at its world position
using the current `FrameSnapshotBus.CurrentSnapshot`. Reports:
- Acceleration vector (magnitude + direction)
- Contributing field count
- Metric model breakdown

Updated per-frame with rate limiting.

---

## 6) Render Health Signals (Implemented)

GrinFilmCamera tracks per-band health:
- No-progress detection (stall counter)
- Hit-rate monitoring (zero-hit bands trigger warnings)
- Budget overrun logging
- Re-entry contention detection

Reported via `RenderStepBandLog` and inspector properties.

---

## 7) Planned Extensions

- **Per-tile heatmaps:** step count, curvature, traversal depth per tile
- **Segment replay:** record RaySeg chains for offline analysis
- **Validation harness:** automated comparison between tiers (see spec_research_mode)
- **GPU profiling:** timer queries for future compute backends
