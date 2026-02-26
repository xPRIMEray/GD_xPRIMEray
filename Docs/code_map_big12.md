Pipeline / entrypoints (the “spine”)

GrinFilmCamera.cs — orchestration / RenderStep (primary entrypoint)

RayBeamRenderer.cs — core ray casting / stepping / pixel loop

RendererCore/Testing/RenderTestRunner.cs — harness + repeatability

Scene snapshot & data layout (research-mode stability)

GodotAdapter/SnapshotBuilder.cs — Godot → snapshot extraction boundary

RendererCore/SceneSnapshot/SceneSnapshot.cs — canonical snapshot container

RendererCore/SceneSnapshot/PackedParamBuffer.cs — compact params / SOA-ish packing

RendererCore/SceneSnapshot/GeometryEntitySOA.cs + InstanceSOA.cs — perf layout backbone

Fields + curvature selection (GRIN / metric models)

RendererCore/Fields/FieldSystem.cs — field evaluation entrypoint

RendererCore/Fields/FieldModels.cs — model definitions / IOR profiles / params

RendererCore/Fields/FieldCurves.cs — curvature functions / curve families

RendererCore/Fields/FieldTLAS.cs — broadphase for field sources (field acceleration)

RendererCore/Geometry/GeometryTLAS.cs — broadphase for geometry (geom acceleration)

“Always-on” instrumentation (keeps you honest)

Bonus (often referenced everywhere):

PerfScope.cs, PerfStats.cs

RendererCore/Common/DebugOverlayBus.cs, FrameSnapshotBus.cs

FilmOverlay2D.cs (your visible telemetry layer)