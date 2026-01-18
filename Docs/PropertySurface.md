# Property Surface Map

This document inventories inspector-exposed properties for the GRIN / curved-ray Film renderer system. Defaults listed are the current code defaults. Deprecated entries remain for scene serialization compatibility.

## GrinFilmCamera (GrinFilmCamera.cs)

| Property Name | Script/Class | Category | Default | What it does | Dependencies | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| RayBeamRendererPath | GrinFilmCamera | Rendering / Film Output | null | NodePath to the RayBeamRenderer used for ray segment generation and collision settings. | Required for rendering. | If missing, render will abort with an error. |
| FilmViewPath | GrinFilmCamera | Rendering / Film Output | null | Optional TextureRect used to display the film image. | Optional. | When empty, an overlay TextureRect is auto-created. |
| Width | GrinFilmCamera | Rendering / Film Output | 160 | Base film image width before scaling. | Uses FilmResolutionScale. | Affects performance and output resolution. |
| Height | GrinFilmCamera | Rendering / Film Output | 90 | Base film image height before scaling. | Uses FilmResolutionScale. | Affects performance and output resolution. |
| FilmResolutionScale | GrinFilmCamera | Rendering / Film Output | 1.0 | Scales film resolution (0.25 to 1.0). | Width and Height. | Lower values reduce render cost. |
| PixelStride | GrinFilmCamera | Rendering / Film Output | 1 | Traces every Nth pixel and fills blocks. | Resolution and sampling. | Higher stride reduces cost and detail. |
| RowsPerFrame | GrinFilmCamera | Rendering / Film Output | 8 | Progressive band height per frame. | UpdateEveryFrame. | Larger values increase per-frame cost. |
| MaxDistance | GrinFilmCamera | Rendering / Film Output | 50 | Max ray distance when AutoRangeDepth is off. | AutoRangeDepth. | Used for depth normalization. |
| SkyColor | GrinFilmCamera | Rendering / Film Output | (0,0,0,1) | Background color when no hit is found. | RequireHitToRender in RayBeamRenderer. | Used as fill color for no-hit pixels. |
| FilmOpacity | GrinFilmCamera | Rendering / Film Output | 0.7 | Alpha of film display texture. | FilmViewPath or overlay. | Applied to TextureRect modulate. |
| ShadingMode | GrinFilmCamera | Rendering / Film Output | DepthHeatmap | Film shading mode (depth, normal RGB, NdotV). | Depends on hit normals for normal modes. | Normal modes use physics normals. |
| FlipNormalToCamera | GrinFilmCamera | Rendering / Film Output | true | Flips hit normals to face camera for shading. | ShadingMode != DepthHeatmap. | No effect in depth mode. |
| UseCameraPropsBetaGamma | GrinFilmCamera | Ray March / Sampling | true | Reads Beta/Gamma from active Camera3D. | Camera with Beta/Gamma properties. | If false, defaults are used. |
| AutoRangeDepth | GrinFilmCamera | Rendering / Film Output | true | Auto-adjusts depth range based on recent hits. | AutoRange* settings. | Drives depth heatmap scaling. |
| AutoRangeMin | GrinFilmCamera | Rendering / Film Output | 0.25 | Minimum allowed auto-range far distance. | AutoRangeDepth. | Clamps dynamic far plane. |
| AutoRangeMax | GrinFilmCamera | Rendering / Film Output | 200 | Maximum allowed auto-range far distance. | AutoRangeDepth. | Clamps dynamic far plane. |
| AutoRangeSmoothing | GrinFilmCamera | Rendering / Film Output | 0.15 | Lerp factor for auto-range updates. | AutoRangeDepth. | Higher values respond faster. |
| AutoRangeSafety | GrinFilmCamera | Rendering / Film Output | 1.15 | Safety multiplier for robust far estimate. | AutoRangeDepth. | Helps avoid clipping. |
| DepthHistoryFrames | GrinFilmCamera | Rendering / Film Output | 30 | Frames tracked for robust far estimate. | AutoRangeDepth. | Higher values smooth more. |
| UseBroadphaseQuickRay | GrinFilmCamera | Physics / Collision | true | Uses quick raycast pre-test. | UseBroadphasePolicy. | Can be overridden by BroadphasePolicy. |
| UseBroadphaseOverlap | GrinFilmCamera | Physics / Collision | false | Uses sphere overlap pre-test. | UseBroadphasePolicy. | Can be overridden by BroadphasePolicy. |
| BroadphaseMargin | GrinFilmCamera | Physics / Collision | 0.03 | Extra radius for overlap sphere. | UseBroadphaseOverlap. | Added to collision radius. |
| BroadphaseMaxResults | GrinFilmCamera | Physics / Collision | 8 | Cap on overlap query results. | UseBroadphaseOverlap. | Used by IntersectShape. |
| UseBroadphasePolicy | GrinFilmCamera | Physics / Collision | false | Uses BroadphasePolicy enum to override booleans. | BroadphasePolicy. | When true, QuickRay/Overlap toggles are ignored. |
| BroadphasePolicy | GrinFilmCamera | Physics / Collision | QuickRayOnly | Broadphase policy enum when UseBroadphasePolicy is true. | UseBroadphasePolicy. | None/QuickRay/Overlap/Both. |
| UseSingleProbeThenSubdivide | GrinFilmCamera | Physics / Collision | false | Uses a quick probe then subdivides collisions if needed. | UseBroadphaseQuickRay or UseBroadphasePolicy. | Trades accuracy for speed. |
| NearestHitOnly | GrinFilmCamera | Physics / Collision | true | Keeps searching segments for nearest hit. | Collision settings. | If false, first hit wins. |
| TinySegmentSkipLen | GrinFilmCamera | Ray March / Sampling | 0.0 | Skips collision checks for tiny segments. | Collision segments. | Higher values can skip near hits. |
| EarlyOutDistanceEps | GrinFilmCamera | Ray March / Sampling | 0.0 | Early-out distance for nearest-hit search. | NearestHitOnly. | Stops once distance is below epsilon. |
| UseAdaptiveSubsteps | GrinFilmCamera | Ray March / Sampling | false | Refines collision checks by subdividing segments. | Collision segments. | More accurate but slower. |
| UseBandHitSkip | GrinFilmCamera | Ray March / Sampling | false | Skips physics for low-hit bands. | BandSkip* settings. | Can reduce cost on empty areas. |
| BandSkipHitThreshold | GrinFilmCamera | Ray March / Sampling | 0.001 | Hit rate below this enables skipping. | UseBandHitSkip. | Lower value skips less. |
| BandSkipFrames | GrinFilmCamera | Ray March / Sampling | 3 | Frames below threshold before skipping. | UseBandHitSkip. | Debounces skipping. |
| BandSkipInvalidatePosDelta | GrinFilmCamera | Ray March / Sampling | 0.05 | Camera position delta that resets skip history. | UseBandHitSkip. | Measured in world units. |
| BandSkipInvalidateBasisDelta | GrinFilmCamera | Ray March / Sampling | 0.02 | Camera basis delta that resets skip history. | UseBandHitSkip. | Rotation sensitivity. |
| BandSkipInvalidateRangeDelta | GrinFilmCamera | Ray March / Sampling | 0.25 | Range delta that resets skip history. | UseBandHitSkip and AutoRangeDepth. | Stops skipping when range changes. |
| UseFieldSourceCache | GrinFilmCamera | Performance / Profiling | false | Caches field source snapshots for faster updates. | FieldSourceRefreshIntervalFrames. | Useful with many field sources. |
| FieldSourceRefreshIntervalFrames | GrinFilmCamera | Performance / Profiling | 30 | How often to refresh cached field sources. | UseFieldSourceCache. | Lower values are more responsive. |
| NeedColliderNames | GrinFilmCamera | Performance / Profiling | false | Fetches collider names for debug info. | VerbosePerfLogs or debug overlays. | Adds overhead. |
| VerbosePerfLogs | GrinFilmCamera | Performance / Profiling | false | Prints verbose perf logs per band/frame. | EnableProfiling. | Can be noisy. |
| EnableProfiling | GrinFilmCamera | Performance / Profiling | true | Enables perf stats collection. | Used by PerfStats. | Disable for minimal overhead. |
| UpdateEveryFrame | GrinFilmCamera | Performance / Profiling | true | Runs RenderStep each frame. | RenderStep. | If false, call RenderStep manually. |
| Preset | GrinFilmCamera | Performance / Profiling | Preview | Preset selection for tuning. | ApplyPresetOnReady. | Walk/Preview/Cinematic. |
| ApplyPresetOnReady | GrinFilmCamera | Performance / Profiling | false | Applies Preset in _Ready. | Preset. | Use for editor defaults. |
| DebugEveryNPixels | GrinFilmCamera | Debug Visualization | 8 | Debug ray sampling density. | FilmOverlay2D and DebugMaxFilmRays. | Higher values reduce overlay cost. |
| DebugMaxFilmRays | GrinFilmCamera | Debug Visualization | 2048 | Cap on debug rays per band. | FilmOverlay2D. | Prevents large overlays. |
| FilmOverlayPath | GrinFilmCamera | Debug Visualization | null | NodePath to FilmOverlay2D for debug overlay. | RayBeamRenderer.DebugOverlayOwnedByFilm. | Optional; can be null. |
| UseInsightPlanePass2 | GrinFilmCamera | Deprecated | true | Legacy pass-2 insight plane toggle. | None. | Deprecated; currently no effect. |
| InsightPlaneEps | GrinFilmCamera | Deprecated | 0.10 | Legacy insight plane slab thickness. | None. | Deprecated; currently no effect. |
| UseSmoothNormals | GrinFilmCamera | Deprecated | false | Placeholder for future normal smoothing. | None. | Deprecated; currently unused. |

## RayBeamRenderer (RayBeamRenderer.cs)

| Property Name | Script/Class | Category | Default | What it does | Dependencies | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| CameraPath | RayBeamRenderer | Rendering / Film Output | null | Optional camera override for ray construction. | Uses viewport camera when empty. | Affects field center and bend direction. |
| UpdateEveryFrame | RayBeamRenderer | Performance / Profiling | true | Rebuilds rays when camera/field sources change. | AllowRebuild. | Disable for manual rebuilds. |
| AllowRebuild | RayBeamRenderer | Performance / Profiling | true | Allows Rebuild when UpdateEveryFrame is on. | UpdateEveryFrame. | Film camera may disable it. |
| StepsPerRay | RayBeamRenderer | Ray March / Sampling | 64 | Number of integration steps per ray. | StepLength, RenderEveryNSteps. | Higher values add detail and cost. |
| StepLength | RayBeamRenderer | Ray March / Sampling | 0.25 | Base step length for integration. | MinStepLength, MaxStepLength. | Used in both modes. |
| MinStepLength | RayBeamRenderer | Ray March / Sampling | 0.05 | Clamp minimum step length. | StepLength, StepAdaptGain. | Avoids tiny steps. |
| MaxStepLength | RayBeamRenderer | Ray March / Sampling | 0.5 | Clamp maximum step length. | StepLength, StepAdaptGain. | Avoids huge steps. |
| StepAdaptGain | RayBeamRenderer | Ray March / Sampling | 0.05 | Adaptation strength for step sizing. | UseIntegratedField. | Higher values shrink steps. |
| UseIntegratedField | RayBeamRenderer | Ray March / Sampling | true | Integrates field acceleration instead of closed form. | FieldStrength, BendScale. | Required for adaptive steps. |
| BendScale | RayBeamRenderer | Ray March / Sampling | 0.12 | Base bend strength. | FieldStrength and Beta/Gamma. | Affects curve curvature. |
| FieldStrength | RayBeamRenderer | Ray March / Sampling | 1.0 | Extra multiplier for field strength. | UseIntegratedField. | Scales acceleration. |
| FieldCenter | RayBeamRenderer | Ray March / Sampling | (0,0,0) | World center for field when not using camera. | FieldCenterIsCamera. | Used in non-integrated path. |
| FieldCenterIsCamera | RayBeamRenderer | Ray March / Sampling | true | Uses camera position as field center. | FieldCenter. | If true, FieldCenter is ignored. |
| RenderEveryNSteps | RayBeamRenderer | Rendering / Film Output | 1 | Samples every N steps for drawing. | StepsPerRay. | Higher values thin draw trail. |
| QuadSize | RayBeamRenderer | Rendering / Film Output | 0.04 | Billboard size for each sample. | RenderEveryNSteps. | Affects ray thickness. |
| Alpha | RayBeamRenderer | Rendering / Film Output | 0.5 | Base alpha for ray samples. | Material setup. | Used in color alpha. |
| ColorByField | RayBeamRenderer | Rendering / Film Output | true | Colors rays based on field magnitude. | FieldColorGain, HotColor. | If false, uses emitter color. |
| FieldColorGain | RayBeamRenderer | Rendering / Film Output | 0.15 | Strength of field-based color ramp. | ColorByField. | Higher = hotter faster. |
| HotColor | RayBeamRenderer | Rendering / Film Output | (0.2,1,1,1) | Color for maximum field heat. | ColorByField. | Cyan glow default. |
| DrawHitMarker | RayBeamRenderer | Rendering / Film Output | true | Draws a marker at hit position. | RequireHitToRender. | Adds one extra billboard. |
| HitMarkerColor | RayBeamRenderer | Rendering / Film Output | (1,0,0,1) | Color of the hit marker. | DrawHitMarker. | Used when a hit occurs. |
| TerminateTrailOnHit | RayBeamRenderer | Rendering / Film Output | true | Stops drawing samples after first hit. | StopOnHit/RequireHitToRender. | Does not change simulation. |
| StopOnHit | RayBeamRenderer | Physics / Collision | false | Stops simulation on first hit. | Collision settings. | Independent from TerminateTrailOnHit. |
| CollisionMask | RayBeamRenderer | Physics / Collision | 0xFFFFFFFF | Collision mask for ray tests. | Physics layers. | Applies to ray/sweep. |
| CollisionEveryNSteps | RayBeamRenderer | Physics / Collision | 1 | Collision test cadence in steps. | StepsPerRay. | Higher values reduce tests. |
| CollisionRadius | RayBeamRenderer | Physics / Collision | 0.03 | Sphere radius for collision. | UseSphereSweepCollision. | Also used for insight plane thickness. |
| UseSphereSweepCollision | RayBeamRenderer | Physics / Collision | false | Uses IntersectShape sphere sweep. | CollisionRadius. | Slower but thicker collisions. |
| UseInsightPlaneFilter | RayBeamRenderer | Physics / Collision | false | Rejects segments outside a plane slab. | InsightPlaneNode. | Used for pass filtering. |
| InsightPlaneNode | RayBeamRenderer | Physics / Collision | null | NodePath to plane source. | UseInsightPlaneFilter. | Node must be Node3D. |
| CollisionRaySubdivideThreshold | RayBeamRenderer | Physics / Collision | 0.25 | Segment length that triggers subdivision. | UseSphereSweepCollision false. | Reduces miss chances. |
| MaxCollisionSubsteps | RayBeamRenderer | Physics / Collision | 16 | Max sub-rays per segment. | CollisionRaySubdivideThreshold. | Upper cap for cost. |
| RequireHitToRender | RayBeamRenderer | Physics / Collision | false | Only render rays that hit. | StopOnHit, TerminateTrailOnHit. | Affects film shading too. |
| CheckCollisionsEvenIfNotStopping | RayBeamRenderer | Physics / Collision | false | Keeps collision checks even if StopOnHit is false. | CollisionEveryNSteps. | Useful for debug. |
| UseScreenSpaceCollisionCadence | RayBeamRenderer | Physics / Collision | true | Adjusts collision cadence to limit screen error. | CollisionMaxErrorPixels. | Requires camera. |
| CollisionMaxErrorPixels | RayBeamRenderer | Physics / Collision | 0.75 | Target sagitta error in pixels. | UseScreenSpaceCollisionCadence. | Lower values increase tests. |
| MinDepthForError | RayBeamRenderer | Physics / Collision | 0.10 | Min depth for screen error calculations. | UseScreenSpaceCollisionCadence. | Avoids division issues. |
| MinCollisionEveryNSteps | RayBeamRenderer | Physics / Collision | 1 | Lower bound on adaptive collision cadence. | UseScreenSpaceCollisionCadence. | Prevents too frequent tests. |
| DebugRender | RayBeamRenderer | Debug Visualization | false | Enables per-ray debug logs. | DebugEveryNRays. | Affects rebuild logs. |
| DebugEveryNRays | RayBeamRenderer | Debug Visualization | 25 | Log every N rays during rebuild. | DebugRender. | 1 logs every ray. |
| DebugSetBillboardRejects | RayBeamRenderer | Debug Visualization | false | Logs billboard rejects (bounds, NaN). | DebugRender. | Can be noisy. |
| DebugMaxRejectPrints | RayBeamRenderer | Debug Visualization | 10 | Max billboard reject logs per ray. | DebugSetBillboardRejects. | Prevents spam. |
| DebugMode | RayBeamRenderer | Debug Visualization | RaysAndNormals | Debug overlay mode (off/rays/normals). | DebugOverlayOwnedByFilm. | F1 toggles in runtime. |
| DebugMaxRays | RayBeamRenderer | Debug Visualization | 256 | Cap on debug overlay rays. | DebugMode. | Limits overlay density. |
| DebugMaxSegmentsPerRay | RayBeamRenderer | Debug Visualization | 64 | Cap on segments per debug ray. | DebugMode. | Limits overlay cost. |
| DebugNormalLen | RayBeamRenderer | Debug Visualization | 0.25 | Length of debug hit normals. | DebugMode. | In world units. |
| DebugDrawOnlyHits | RayBeamRenderer | Debug Visualization | false | Draw only rays that hit. | DebugMode. | F2 toggles in runtime. |
| DebugOverlayOwnedByFilm | RayBeamRenderer | Debug Visualization | true | Film camera drives overlay drawing. | GrinFilmCamera. | If false, renderer draws overlay. |

## FilmOverlay2D (FilmOverlay2D.cs)

| Property Name | Script/Class | Category | Default | What it does | Dependencies | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| CameraPath | FilmOverlay2D | Debug Visualization | null | Optional camera override for projection. | Uses provided camera in SetData. | If empty, uses last SetData camera. |
| DrawRays | FilmOverlay2D | Debug Visualization | true | Draws ray polylines. | SetData ray arrays. | Requires ray data. |
| DrawHitNormals | FilmOverlay2D | Debug Visualization | true | Draws physics hit normals. | SetData hit payloads. | Uses NormalWidth and NormalLenWorld. |
| DrawFilmGradientNormals | FilmOverlay2D | Debug Visualization | false | Draws film gradient normals from image. | SetFilmImage. | Uses FilmGradientScale. |
| RayWidth | FilmOverlay2D | Debug Visualization | 1.0 | Line width for rays. | DrawRays. | In pixels. |
| NormalWidth | FilmOverlay2D | Debug Visualization | 2.0 | Line width for normals. | DrawHitNormals/DrawFilmGradientNormals. | In pixels. |
| NormalLenWorld | FilmOverlay2D | Debug Visualization | 0.25 | World-space normal length. | DrawHitNormals. | In world units. |
| FilmGradientScale | FilmOverlay2D | Debug Visualization | 6.0 | Scale for film gradient normal lines. | DrawFilmGradientNormals. | In screen pixels. |
| RayColor | FilmOverlay2D | Debug Visualization | (0.6,1,0.6,0.9) | Base ray color. | DrawRays. | Alpha is preserved. |
| HitRayColor | FilmOverlay2D | Debug Visualization | (1,0.9,0.2,1) | Color for rays that hit. | DrawRays. | Overrides RayColor. |
| NormalColor | FilmOverlay2D | Debug Visualization | (1,0.2,0.2,1) | Color for normals and gradients. | DrawHitNormals/DrawFilmGradientNormals. | Shared for both. |

## Recommended presets

- Realtime preview: Preset = Preview, FilmResolutionScale = 0.5 to 1.0, PixelStride = 2, RowsPerFrame = 16, UseBandHitSkip = true.
- Quality: Preset = Cinematic, FilmResolutionScale = 1.0, PixelStride = 1, RowsPerFrame = 4, UseBandHitSkip = false.
- Debugging: EnableProfiling = true, VerbosePerfLogs = true, DebugEveryNPixels = 8, DebugMaxFilmRays = 2048, DebugMode = RaysAndNormals.

## Deprecation notes

- UseInsightPlanePass2, InsightPlaneEps, and UseSmoothNormals are retained for scene compatibility but currently have no effect.
