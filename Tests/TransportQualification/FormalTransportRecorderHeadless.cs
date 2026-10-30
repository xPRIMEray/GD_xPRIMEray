using Godot;
using System;
using System.Collections.Generic;
using GodotAdapter;
using RendererCore.Config;
using XPrimeRay.ObservationLayer;

public partial class FormalTransportRecorderHeadless : Node3D
{
	private RayBeamRenderer _renderer;
	private Camera3D _camera;
	private StaticBody3D _target;

	public override async void _Ready()
	{
		try
		{
			_camera = new Camera3D { Current = true };
			AddChild(_camera);
			_renderer = new RayBeamRenderer
			{
				StepsPerRay = 2,
				StepLength = 0.25f,
				MinStepLength = 0.25f,
				MaxStepLength = 0.25f,
				CollisionEveryNSteps = 1,
				UseIntegratedField = true,
				UseScreenSpaceCollisionCadence = false
			};
			AddChild(_renderer);
			_target = new StaticBody3D { Position = new Vector3(0.4f, 0f, 0f), CollisionLayer = 1 };
			_target.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.2f, 0.2f, 0.2f) } });
			AddChild(_target);
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			Run();
			GD.Print("FORMAL_TRANSPORT_RECORDER PASS");
			GetTree().Quit(0);
		}
		catch (Exception exception)
		{
			GD.PushError("FORMAL_TRANSPORT_RECORDER FAIL: " + exception);
			GetTree().Quit(1);
		}
	}

	private void Run()
	{
		VerifyDisabledAndNonFormalPaths();
		VerifyTerminal(Pass1TransportTerminationV1.AbsorbedAtSample, 1, Vector3.Zero, Vector3.Right, 10f, 8, AbsorbingSource());
		VerifyTerminal(Pass1TransportTerminationV1.MaxDistance, 2, Vector3.Zero, Vector3.Up, 0.25f, 8, Array.Empty<RayBeamRenderer.FieldSourceSnap>());
		VerifyTerminal(Pass1TransportTerminationV1.MaxSteps, 3, Vector3.Zero, Vector3.Up, 10f, 8, Array.Empty<RayBeamRenderer.FieldSourceSnap>());
		VerifyTerminal(Pass1TransportTerminationV1.SegmentCapacityTruncated, 4, Vector3.Zero, Vector3.Up, 10f, 0, Array.Empty<RayBeamRenderer.FieldSourceSnap>());
		VerifyGeometryAndBackgroundClassification();
		VerifyUnsupportedStopOnHit();
		VerifyReplacementAndCrossCaptureTickets();
		VerifyContinuousBoundaryBias();
		VerifyCrossingAndSuppressedTransitions();
		VerifySameStepBoundaryOrdering();
		VerifyMirrorAndDielectricDecisions();
		VerifyPortalRemapAndSyntheticEgress();
		VerifyFailedProbeQueryEvent();
		VerifyRecorderCapacityOverflowParity();
	}

	private void VerifyContinuousBoundaryBias()
	{
		ConfigureIntegrator(1, 0.25f);
		BoundaryLayerVolume boundary = AddBoundary(
			"ContinuousBias", Vector3.Zero, 10f,
			BoundaryLayerVolume.BoundaryExecutionMode.Continuous,
			BoundaryLayerVolume.BoundaryBehavior.DirectionBias);
		boundary.BiasDirection = Vector3.Up;
		boundary.BiasStrength = 0.1f;
		FrozenBoundarySnapshot snapshot = CaptureBoundaries(boundary);
		_renderer.BeginFormalProbeQueryCapture(true);
		RayBeamRenderer.Pass1HitInfo hit = Execute(30, Vector3.Zero, Vector3.Right, 0.25f, 8,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _, frozenBoundaries: snapshot);
		Assert(_renderer.TryFinalizeFormalTransportAttempt(in hit.FormalTransportTicket, hit.Found,
			hit.Found ? TransportSurfaceClassV1.Geometry : TransportSurfaceClassV1.None), "continuous bias finalize");
		RayBeamRenderer.FormalTransportEventTestingSnapshot transportEvent =
			FirstEvent(OnlyPixel(), TransportEventKindV1.ContinuousBiasApplied);
		Vector3 expected = (Vector3.Right + Vector3.Up * 0.1f).Normalized();
		Assert(transportEvent.PreDirection == Bits(Vector3.Right) &&
			transportEvent.PostDirection == Bits(expected), "continuous bias exact pre/post direction");
		Assert(transportEvent.CanonicalSubjectId == "boundary-v1:ContinuousBias" &&
			transportEvent.SubjectExecutionOrdinal == 0, "continuous bias canonical boundary identity");
		_renderer.EndFormalProbeQueryCapture();
		RemoveBoundaries(boundary);
	}

	private void VerifyCrossingAndSuppressedTransitions()
	{
		ConfigureIntegrator(4, 0.25f);
		BoundaryLayerVolume boundary = AddBoundary(
			"EntryExitBias", new Vector3(0.5f, 0f, 0f), 0.2f,
			BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent,
			BoundaryLayerVolume.BoundaryBehavior.DirectionBias);
		boundary.CrossingPolicy = BoundaryLayerVolume.BoundaryCrossingPolicy.ExitOnly;
		boundary.BiasDirection = Vector3.Up;
		boundary.BiasStrength = 0.05f;
		FrozenBoundarySnapshot snapshot = CaptureBoundaries(boundary);
		_renderer.BeginFormalProbeQueryCapture(true);
		RayBeamRenderer.Pass1HitInfo hit = Execute(31, Vector3.Zero, Vector3.Right, 2f, 16,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _, frozenBoundaries: snapshot);
		Assert(_renderer.TryFinalizeFormalTransportAttempt(in hit.FormalTransportTicket, hit.Found,
			hit.Found ? TransportSurfaceClassV1.Geometry : TransportSurfaceClassV1.None), "crossing finalize");
		RayBeamRenderer.FormalTransportPixelTestingSnapshot record = OnlyPixel();
		RayBeamRenderer.FormalTransportEventTestingSnapshot suppressed =
			FirstEvent(record, TransportEventKindV1.SuppressedMaskTransition);
		RayBeamRenderer.FormalTransportEventTestingSnapshot exit =
			FirstEvent(record, TransportEventKindV1.BoundaryCrossingApplied);
		Assert(suppressed.Reason == TransportReasonV1.BoundaryEntry &&
			exit.Reason == TransportReasonV1.BoundaryExit, "entry suppression and exit dispatch");
		Assert((suppressed.Observability & TransportObservabilityV1.PointSample) == 0,
			"crossing-policy suppression remains interval-only");
		Assert(exit.PreDirection != exit.PostDirection, "exit bias exact direction mutation");
		_renderer.EndFormalProbeQueryCapture();
		RemoveBoundaries(boundary);
	}

	private void VerifySameStepBoundaryOrdering()
	{
		ConfigureIntegrator(3, 0.25f);
		BoundaryLayerVolume first = AddBoundary(
			"OrderedA", new Vector3(0.5f, 0f, 0f), 0.2f,
			BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent,
			BoundaryLayerVolume.BoundaryBehavior.DirectionBias);
		BoundaryLayerVolume second = AddBoundary(
			"OrderedB", new Vector3(0.5f, 0f, 0f), 0.2f,
			BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent,
			BoundaryLayerVolume.BoundaryBehavior.DirectionBias);
		first.BiasDirection = Vector3.Up; first.BiasStrength = 0.03f;
		second.BiasDirection = Vector3.Back; second.BiasStrength = 0.04f;
		FrozenBoundarySnapshot snapshot = CaptureBoundaries(first, second);
		_renderer.BeginFormalProbeQueryCapture(true);
		RayBeamRenderer.Pass1HitInfo hit = Execute(32, Vector3.Zero, Vector3.Right, 2f, 16,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _, frozenBoundaries: snapshot);
		Assert(_renderer.TryFinalizeFormalTransportAttempt(in hit.FormalTransportTicket, hit.Found,
			hit.Found ? TransportSurfaceClassV1.Geometry : TransportSurfaceClassV1.None), "ordered crossing finalize");
		var crossings = EventsOfKind(OnlyPixel(), TransportEventKindV1.BoundaryCrossingApplied);
		Assert(crossings.Count >= 2 && crossings[0].CanonicalSubjectId == "boundary-v1:OrderedA" &&
			crossings[1].CanonicalSubjectId == "boundary-v1:OrderedB", "same-step legacy execution order");
		Assert(crossings[0].IntegrationStepOrdinal == crossings[1].IntegrationStepOrdinal &&
			(crossings[0].Observability & TransportObservabilityV1.ExecutionOrderedSimultaneous) != 0 &&
			(crossings[1].Observability & TransportObservabilityV1.ExecutionOrderedSimultaneous) != 0,
			"same-step events explicitly execution ordered");
		Assert(crossings[1].PreDirection == crossings[0].PostDirection,
			"second boundary consumes first boundary post-direction");
		_renderer.EndFormalProbeQueryCapture();
		RemoveBoundaries(first, second);
	}

	private void VerifyMirrorAndDielectricDecisions()
	{
		ConfigureIntegrator(3, 0.25f);
		BoundaryLayerVolume mirror = AddBoxBoundary(
			"Mirror", new Vector3(0.5f, 0f, 0f), new Vector3(0.2f, 1f, 1f),
			BoundaryLayerVolume.BoundaryBehavior.PerfectMirrorReflection,
			BoundaryLayerVolume.BoundaryCrossingPolicy.EntryOnly);
		FrozenBoundarySnapshot mirrorSnapshot = CaptureBoundaries(mirror);
		_renderer.BeginFormalProbeQueryCapture(true);
		RayBeamRenderer.Pass1HitInfo mirrorHit = Execute(33, Vector3.Zero, Vector3.Right, 2f, 16,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _, frozenBoundaries: mirrorSnapshot);
		Assert(_renderer.TryFinalizeFormalTransportAttempt(in mirrorHit.FormalTransportTicket, mirrorHit.Found,
			mirrorHit.Found ? TransportSurfaceClassV1.Geometry : TransportSurfaceClassV1.None), "mirror finalize");
		var mirrorEvent = FirstEvent(OnlyPixel(), TransportEventKindV1.DielectricApplied);
		Assert(mirrorEvent.Reason == TransportReasonV1.PerfectMirror &&
			mirrorEvent.PreDirection == Bits(Vector3.Right) && mirrorEvent.PostDirection == Bits(Vector3.Left),
			"mirror records actual direction mutation");
		_renderer.EndFormalProbeQueryCapture();
		RemoveBoundaries(mirror);

		BoundaryLayerVolume dielectric = AddBoundary(
			"Dielectric", new Vector3(0.5f, 0f, 0f), 0.2f,
			BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent,
			BoundaryLayerVolume.BoundaryBehavior.DielectricRefraction);
		dielectric.IOR = 1.5f;
		FrozenBoundarySnapshot dielectricSnapshot = CaptureBoundaries(dielectric);
		_renderer.BeginFormalProbeQueryCapture(true);
		RayBeamRenderer.Pass1HitInfo dielectricHit = Execute(34, Vector3.Zero, Vector3.Right, 2f, 16,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _, frozenBoundaries: dielectricSnapshot);
		Assert(_renderer.TryFinalizeFormalTransportAttempt(in dielectricHit.FormalTransportTicket, dielectricHit.Found,
			dielectricHit.Found ? TransportSurfaceClassV1.Geometry : TransportSurfaceClassV1.None), "dielectric finalize");
		var dielectricEvent = FirstEvent(OnlyPixel(), TransportEventKindV1.DielectricApplied);
		Assert(dielectricEvent.Reason == TransportReasonV1.DielectricRefraction &&
			dielectricEvent.Decision == TransportDecisionV1.StateMutated,
			"dielectric refraction event");
		_renderer.EndFormalProbeQueryCapture();
		RemoveBoundaries(dielectric);

		ConfigureIntegrator(8, 0.25f);
		BoundaryLayerVolume tir = AddBoundary(
			"TirCandidate", Vector3.Zero, 1f,
			BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent,
			BoundaryLayerVolume.BoundaryBehavior.DielectricRefraction);
		tir.CrossingPolicy = BoundaryLayerVolume.BoundaryCrossingPolicy.ExitOnly;
		tir.IOR = 1.5f;
		FrozenBoundarySnapshot tirSnapshot = CaptureBoundaries(tir);
		_renderer.BeginFormalProbeQueryCapture(true);
		Vector3 tirDirection = new Vector3(0.1f, 0.995f, 0f).Normalized();
		RayBeamRenderer.Pass1HitInfo tirHit = Execute(35, new Vector3(0.9f, 0f, 0f), tirDirection, 3f, 24,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _, frozenBoundaries: tirSnapshot);
		Assert(_renderer.TryFinalizeFormalTransportAttempt(in tirHit.FormalTransportTicket, tirHit.Found,
			tirHit.Found ? TransportSurfaceClassV1.Geometry : TransportSurfaceClassV1.None), "TIR finalize");
		var tirEvent = FirstEvent(OnlyPixel(), TransportEventKindV1.DielectricApplied);
		Assert(tirEvent.Reason == TransportReasonV1.TotalInternalReflectionCandidate &&
			tirEvent.Decision == TransportDecisionV1.DegenerateDirection &&
			(tirEvent.Flags & (TransportEventFlagsV1.DegenerateDirection | TransportEventFlagsV1.TirCandidate)) ==
				(TransportEventFlagsV1.DegenerateDirection | TransportEventFlagsV1.TirCandidate) &&
			tirEvent.PostDirection == default,
			"actual zero-direction TIR behavior is recorded without calling it reflection");
		_renderer.EndFormalProbeQueryCapture();
		RemoveBoundaries(tir);
	}

	private void VerifyPortalRemapAndSyntheticEgress()
	{
		ConfigureIntegrator(4, 0.25f);
		BoundaryLayerVolume source = AddBoundary(
			"PortalSource", new Vector3(0.5f, 0f, 0f), 0.2f,
			BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent,
			BoundaryLayerVolume.BoundaryBehavior.SceneTransform);
		BoundaryLayerVolume destination = AddBoundary(
			"PortalDestination", new Vector3(5f, 0f, 0f), 0.2f,
			BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent,
			BoundaryLayerVolume.BoundaryBehavior.DirectionBias);
		BoundaryLayerVolume landingMembership = AddBoundary(
			"PortalLandingMembership", new Vector3(5f, 0f, 0f), 1f,
			BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent,
			BoundaryLayerVolume.BoundaryBehavior.DirectionBias);
		source.LinkedBoundaryPath = new NodePath("../PortalDestination");
		source.SceneTransformExitOffset = 0.1f;
		FrozenBoundarySnapshot snapshot = CaptureBoundaries(source, destination, landingMembership);
		_renderer.BeginFormalProbeQueryCapture(true);
		RayBeamRenderer.Pass1HitInfo hit = Execute(36, Vector3.Zero, Vector3.Right, 2f, 16,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _, frozenBoundaries: snapshot);
		Assert(_renderer.TryFinalizeFormalTransportAttempt(in hit.FormalTransportTicket, hit.Found,
			hit.Found ? TransportSurfaceClassV1.Geometry : TransportSurfaceClassV1.None), "portal finalize");
		RayBeamRenderer.FormalTransportPixelTestingSnapshot record = OnlyPixel();
		var remap = FirstEvent(record, TransportEventKindV1.BoundaryRemapApplied);
		var egress = FirstEvent(record, TransportEventKindV1.BoundarySyntheticEgress);
		var membership = EventsOfKind(record, TransportEventKindV1.SuppressedMaskTransition);
		var sourceCleared = membership.Find(e =>
			e.CanonicalSubjectId == "boundary-v1:PortalSource" && e.Reason == TransportReasonV1.BoundaryExit);
		var landingEntered = membership.Find(e =>
			e.CanonicalSubjectId == "boundary-v1:PortalLandingMembership" && e.Reason == TransportReasonV1.BoundaryEntry);
		Assert(remap.CanonicalSubjectId == "boundary-v1:PortalSource" &&
			egress.CanonicalSubjectId == "boundary-v1:PortalDestination", "portal source and linked egress identities");
		Assert(remap.PrePosition != remap.PostPosition &&
			egress.Decision == TransportDecisionV1.NoTransportEffect && egress.Presence == TransportEventPresenceV1.None,
			"portal remap mutates state; synthetic egress does not fabricate state");
		Assert(sourceCleared.CanonicalSubjectId != null && landingEntered.CanonicalSubjectId != null,
			"portal remap records source-shell clear and destination landing membership");
		Assert(sourceCleared.SubjectExecutionOrdinal == 0 && landingEntered.SubjectExecutionOrdinal == 2,
			"post-remap membership uses canonical IDs and legacy execution ordinals");
		Assert(remap.EventOrdinal < egress.EventOrdinal && egress.EventOrdinal < sourceCleared.EventOrdinal &&
			sourceCleared.EventOrdinal < landingEntered.EventOrdinal,
			"post-remap membership observations follow actual remap and synthetic egress order");
		TransportObservabilityV1 remapMembershipFlags =
			TransportObservabilityV1.PointSample | TransportObservabilityV1.SampledIntervalTransition |
			TransportObservabilityV1.SuppressedMaskTransition;
		Assert((sourceCleared.Observability & remapMembershipFlags) == remapMembershipFlags &&
			(landingEntered.Observability & remapMembershipFlags) == remapMembershipFlags &&
			(sourceCleared.Observability & TransportObservabilityV1.ExactMutation) == 0 &&
			(landingEntered.Observability & TransportObservabilityV1.ExactMutation) == 0 &&
			sourceCleared.Decision == TransportDecisionV1.NoTransportEffect &&
			landingEntered.Decision == TransportDecisionV1.NoTransportEffect &&
			sourceCleared.Presence == TransportEventPresenceV1.None &&
			landingEntered.Presence == TransportEventPresenceV1.None,
			"post-remap membership is observation-only with no invented intersection or mutation");
		GD.Print($"FORMAL_TRANSPORT_PORTAL_MASK PASS remap_event={remap.EventOrdinal} " +
			$"synthetic_egress_event={egress.EventOrdinal} source_exit_event={sourceCleared.EventOrdinal} " +
			$"landing_entry_event={landingEntered.EventOrdinal} source_ordinal={sourceCleared.SubjectExecutionOrdinal} " +
			$"landing_ordinal={landingEntered.SubjectExecutionOrdinal}");
		_renderer.EndFormalProbeQueryCapture();
		RemoveBoundaries(source, destination, landingMembership);
	}

	private void VerifyFailedProbeQueryEvent()
	{
		ConfigureIntegrator(2, 0.25f);
		_renderer.BeginFormalProbeQueryCapture(true);
		_renderer.ForceNextRecordedPass1ProbeFailureForTesting();
		RayBeamRenderer.Pass1HitInfo hit = Execute(37, Vector3.Zero, Vector3.Up, 2f, 8,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _);
		Assert(_renderer.TryFinalizeFormalTransportAttempt(in hit.FormalTransportTicket, false, TransportSurfaceClassV1.None),
			"failed query finalize");
		RayBeamRenderer.FormalTransportPixelTestingSnapshot record = OnlyPixel();
		var failure = FirstEvent(record, TransportEventKindV1.ProbeQueryFailed);
		Assert(failure.Decision == TransportDecisionV1.QueryFailed &&
			(record.RawTerminalFlags & RawTerminalFlagsV1.ProbeQueryFailed) != 0,
			"failed guarded query records event and raw flag");
		_renderer.EndFormalProbeQueryCapture();

		_renderer.ForceNextRecordedPass1ProbeFailureForTesting();
		_renderer.BeginFormalProbeQueryCapture(true);
		RayBeamRenderer.Pass1HitInfo resetHit = Execute(370, Vector3.Zero, Vector3.Up, 2f, 8,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _);
		Assert(_renderer.TryFinalizeFormalTransportAttempt(in resetHit.FormalTransportTicket, resetHit.Found,
			resetHit.Found ? TransportSurfaceClassV1.Geometry : TransportSurfaceClassV1.None), "query failure reset finalize");
		Assert(EventsOfKind(OnlyPixel(), TransportEventKindV1.ProbeQueryFailed).Count == 0,
			"new Formal capture resets recorder-only forced query failure flag");
		_renderer.EndFormalProbeQueryCapture();
	}

	private void VerifyRecorderCapacityOverflowParity()
	{
		ConfigureIntegrator(1, 0.25f);
		var boundaries = new List<BoundaryLayerVolume>();
		for (int i = 0; i < 40; i++)
		{
			BoundaryLayerVolume boundary = AddBoundary(
				$"Overflow{i:D2}", Vector3.Zero, 10f,
				BoundaryLayerVolume.BoundaryExecutionMode.Continuous,
				BoundaryLayerVolume.BoundaryBehavior.DirectionBias);
			boundary.BiasDirection = Vector3.Up;
			boundary.BiasStrength = 0.001f;
			boundaries.Add(boundary);
		}
		FrozenBoundarySnapshot snapshot = CaptureBoundaries(boundaries.ToArray());

		_renderer.BeginFormalProbeQueryCapture(false);
		RayBeamRenderer.Pass1HitInfo offHit = Execute(38, Vector3.Zero, Vector3.Right, 0.25f, 8,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out int offWritten, frozenBoundaries: snapshot);
		string offQueries = _renderer.GetFormalProbeQueryDatasetSha256();
		_renderer.EndFormalProbeQueryCapture();

		_renderer.BeginFormalProbeQueryCapture(true);
		RayBeamRenderer.Pass1HitInfo onHit = Execute(38, Vector3.Zero, Vector3.Right, 0.25f, 8,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out int onWritten, frozenBoundaries: snapshot);
		string onQueries = _renderer.GetFormalProbeQueryDatasetSha256();
		Assert(_renderer.TryFinalizeFormalTransportAttempt(in onHit.FormalTransportTicket, onHit.Found,
			onHit.Found ? TransportSurfaceClassV1.Geometry : TransportSurfaceClassV1.None), "overflow finalize");
		RayBeamRenderer.FormalTransportPixelTestingSnapshot record = OnlyPixel();
		Assert(record.Overflowed && record.Events.Length == record.EventCapacity &&
			record.ProvisionalReason.Contains("recorder_event_capacity_overflow", StringComparison.Ordinal),
			"capacity exhaustion is explicit and incomplete");
		Assert(!record.CanSeal && record.RecorderCompletion == TransportRecorderCompletionV1.Failed &&
			record.AuthorityStatus == TransportAuthorityStatusV1.Pending,
			"overflow remains failed, pending, and non-sealable");
		Assert(offQueries == onQueries && offWritten == onWritten && offHit.Found == onHit.Found &&
			BitConverter.SingleToUInt32Bits(offHit.Distance) == BitConverter.SingleToUInt32Bits(onHit.Distance),
			"overflow does not alter query stream or transport result");
		GD.Print($"FORMAL_TRANSPORT_CAP initial_capacity=8 event_capacity={record.EventCapacity} " +
			$"recorded={record.Events.Length} overflow=1 estimated_payload_bytes={record.EventCapacity * 120}");
		_renderer.EndFormalProbeQueryCapture();
		RemoveBoundaries(boundaries.ToArray());
	}

	private void VerifyDisabledAndNonFormalPaths()
	{
		_renderer.BeginFormalProbeQueryCapture(false);
		_ = Execute(0, Vector3.Zero, Vector3.Up, 10f, 8, Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _);
		RayBeamRenderer.FormalTransportLedgerTestingSnapshot disabled = _renderer.GetFormalTransportLedgerForTesting();
		Assert(disabled.AttemptAllocationCount == 0 && disabled.RecordedEventCount == 0 && disabled.Pixels.Length == 0,
			"recorder OFF allocation/event/pixel counts");
		_renderer.EndFormalProbeQueryCapture();

		_renderer.BeginFormalProbeQueryCapture(true);
		_ = Execute(-1, Vector3.Zero, Vector3.Up, 10f, 8, Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _);
		RayBeamRenderer.FormalTransportLedgerTestingSnapshot nonFormal = _renderer.GetFormalTransportLedgerForTesting();
		Assert(nonFormal.AttemptAllocationCount == 0 && nonFormal.RecordedEventCount == 0 && nonFormal.Pixels.Length == 0,
			"non-Formal pass-1 does not activate recorder");
		_renderer.EndFormalProbeQueryCapture();
	}

	private void VerifyUnsupportedStopOnHit()
	{
		_renderer.BeginFormalProbeQueryCapture(true);
		RayBeamRenderer.Pass1HitInfo hit = Execute(13, Vector3.Zero, Vector3.Right, 10f, 8,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _, stopOnHit: true);
		Assert(hit.Found, "StopOnHit reaches the real Godot collider");
		Assert(_renderer.TryFinalizeFormalTransportAttempt(
			in hit.FormalTransportTicket, true, TransportSurfaceClassV1.Geometry), "StopOnHit classification finalize");

		RayBeamRenderer.FormalTransportPixelTestingSnapshot record = OnlyPixel();
		Assert(record.Termination == Pass1TransportTerminationV1.None, "StopOnHit is not misclassified as a supported terminal cause");
		Assert(!ContainsKind(record, TransportEventKindV1.Terminal), "StopOnHit does not fabricate a terminal event");
		Assert(record.ProvisionalReason.Contains("unsupported", StringComparison.Ordinal),
			"StopOnHit remains explicitly incomplete");
		Assert(!record.CanSeal, "StopOnHit provisional record cannot seal");
		_renderer.EndFormalProbeQueryCapture();
	}

	private void VerifyTerminal(
		Pass1TransportTerminationV1 expected,
		int pixel,
		Vector3 origin,
		Vector3 direction,
		float maxDistance,
		int capacity,
		RayBeamRenderer.FieldSourceSnap[] sources)
	{
		_renderer.BeginFormalProbeQueryCapture(true);
		RayBeamRenderer.Pass1HitInfo hit = Execute(pixel, origin, direction, maxDistance, capacity, sources, out _);
		Assert(_renderer.TryFinalizeFormalTransportAttempt(
			in hit.FormalTransportTicket, hit.Found,
			hit.Found ? TransportSurfaceClassV1.Geometry : TransportSurfaceClassV1.None), $"finalize {expected}");
		RayBeamRenderer.FormalTransportPixelTestingSnapshot record = OnlyPixel();
		Assert(record.Termination == expected, $"terminal {expected}");
		Assert(record.Events.Length > 0 && record.Events[^1].Kind == TransportEventKindV1.Terminal,
			$"terminal event {expected}");
		Assert(!record.CanSeal && record.RecorderCompletion == TransportRecorderCompletionV1.Failed &&
			record.AuthorityStatus == TransportAuthorityStatusV1.Pending &&
			record.TransactionStatus == TransportTransactionStatusV1.Pass1Committed,
			$"provisional axes {expected}");
		_renderer.EndFormalProbeQueryCapture();
	}

	private void VerifyGeometryAndBackgroundClassification()
	{
		_renderer.BeginFormalProbeQueryCapture(true);
		RayBeamRenderer.Pass1HitInfo geometry = Execute(10, Vector3.Zero, Vector3.Right, 10f, 8,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _);
		Assert(geometry.Found, "real Godot geometry hit");
		Assert(_renderer.TryFinalizeFormalTransportAttempt(in geometry.FormalTransportTicket, true, TransportSurfaceClassV1.Geometry),
			"geometry classification finalize");

		RayBeamRenderer.Pass1HitInfo background = Execute(11, Vector3.Zero, Vector3.Right, 10f, 8,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _);
		Assert(background.Found, "real Godot background-classified hit");
		Assert(_renderer.TryFinalizeFormalTransportAttempt(in background.FormalTransportTicket, true, TransportSurfaceClassV1.Background),
			"background classification finalize");

		RayBeamRenderer.Pass1HitInfo miss = Execute(12, Vector3.Zero, Vector3.Up, 10f, 8,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _);
		Assert(!miss.Found, "real no-hit path");
		Assert(_renderer.TryFinalizeFormalTransportAttempt(in miss.FormalTransportTicket, false, TransportSurfaceClassV1.None),
			"no-hit classification finalize");

		RayBeamRenderer.FormalTransportPixelTestingSnapshot[] pixels = _renderer.GetFormalTransportLedgerForTesting().Pixels;
		Assert(pixels.Length == 3, "three classification records");
		Assert(pixels[0].SurfaceClass == TransportSurfaceClassV1.Geometry &&
			pixels[1].SurfaceClass == TransportSurfaceClassV1.Background &&
			pixels[2].SurfaceClass == TransportSurfaceClassV1.None, "surface classifications retained");
		Assert(ContainsKind(pixels[0], TransportEventKindV1.GeometryContactObserved) &&
			ContainsKind(pixels[1], TransportEventKindV1.GeometryContactObserved) &&
			!ContainsKind(pixels[2], TransportEventKindV1.GeometryContactObserved), "contact events truthfully follow hits");
		_renderer.EndFormalProbeQueryCapture();
	}

	private void VerifyReplacementAndCrossCaptureTickets()
	{
		_renderer.BeginFormalProbeQueryCapture(true);
		RayBeamRenderer.Pass1HitInfo first = Execute(20, Vector3.Zero, Vector3.Up, 10f, 8,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _);
		RayBeamRenderer.Pass1HitInfo replacement = Execute(20, Vector3.Zero, Vector3.Up, 10f, 8,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _);
		Assert(!_renderer.TryFinalizeFormalTransportAttempt(in first.FormalTransportTicket, false, TransportSurfaceClassV1.None),
			"replaced attempt ticket rejected");
		Assert(_renderer.TryFinalizeFormalTransportAttempt(in replacement.FormalTransportTicket, false, TransportSurfaceClassV1.None),
			"replacement attempt finalizes");
		RayBeamRenderer.FormalTransportLedgerTestingSnapshot replacementSnapshot = _renderer.GetFormalTransportLedgerForTesting();
		Assert(replacementSnapshot.Pixels.Length == 1 && replacementSnapshot.Pixels[0].CommitVersion == 2,
			"whole-pixel replacement owns one version-2 slot");
		Assert(replacementSnapshot.FinalizationEligibleCommitCount == 1 &&
			replacementSnapshot.AcceptedFinalizationCount == 1,
			"only the current replacement commit remains eligible and it finalizes");
		Assert(_renderer.FormalProbeWorkUnitCommitCount == 2 && _renderer.FormalProbeWorkUnitReplacementCount == 1,
			"question and ledger replacement share transaction");
		GD.Print($"FORMAL_TRANSPORT_REPLACEMENT capture={replacementSnapshot.CaptureGeneration} " +
			$"commits={_renderer.FormalProbeWorkUnitCommitCount} replacements={_renderer.FormalProbeWorkUnitReplacementCount} " +
			$"current_version={replacementSnapshot.Pixels[0].CommitVersion} eligible={replacementSnapshot.FinalizationEligibleCommitCount} " +
			$"accepted={replacementSnapshot.AcceptedFinalizationCount} replaced_ticket_rejected=1 current_ticket_accepted=1");
		RayBeamRenderer.FormalTransportAttemptTicket priorCaptureTicket = replacement.FormalTransportTicket;

		_renderer.BeginFormalProbeQueryCapture(true);
		RayBeamRenderer.Pass1HitInfo current = Execute(20, Vector3.Zero, Vector3.Up, 10f, 8,
			Array.Empty<RayBeamRenderer.FieldSourceSnap>(), out _);
		Assert(!_renderer.TryFinalizeFormalTransportAttempt(in priorCaptureTicket, false, TransportSurfaceClassV1.None),
			"previous-capture ticket rejected");
		Assert(_renderer.TryFinalizeFormalTransportAttempt(in current.FormalTransportTicket, false, TransportSurfaceClassV1.None),
			"current-capture ticket accepted");
		RayBeamRenderer.FormalTransportLedgerTestingSnapshot currentSnapshot = _renderer.GetFormalTransportLedgerForTesting();
		Assert(currentSnapshot.Pixels.Length == 1 && currentSnapshot.Pixels[0].Finalized &&
			currentSnapshot.Pixels[0].CaptureGeneration == currentSnapshot.CaptureGeneration,
			"new capture contains only its finalized current record");
		GD.Print($"FORMAL_TRANSPORT_CROSS_CAPTURE previous_generation={priorCaptureTicket.CaptureGeneration} " +
			$"current_generation={currentSnapshot.CaptureGeneration} stale_rejects={currentSnapshot.StaleFinalizationRejectCount} " +
			$"current_version={currentSnapshot.Pixels[0].CommitVersion} previous_ticket_rejected=1 current_ticket_accepted=1");
		_renderer.EndFormalProbeQueryCapture();
	}

	private RayBeamRenderer.Pass1HitInfo Execute(
		int pixel,
		Vector3 origin,
		Vector3 direction,
		float maxDistance,
		int capacity,
		RayBeamRenderer.FieldSourceSnap[] sources,
		out int written,
		bool stopOnHit = false,
		FrozenBoundarySnapshot frozenBoundaries = null)
	{
		RayBeamRenderer.RaySeg[] segments = new RayBeamRenderer.RaySeg[Math.Max(1, capacity)];
		PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, origin + direction, 1);
		try
		{
			PhysicsDirectSpaceState3D space = GetWorld3D().DirectSpaceState;
			written = _renderer.BuildRaySegmentsCamera_Pass1(
				space, ref query, _camera, 1f, _camera.GlobalPosition,
				origin, direction.Normalized(), Vector3.Up, Vector3.Zero, 0f, 1f,
				sources, sources.Length > 0, maxDistance,
				segments, 0, capacity,
				default, false, 0f,
				stopOnHit, true, 1, 0f,
				"formal_transport_recorder_fixture", "formal_transport_recorder", "headless", "formal",
				hitInfo: out RayBeamRenderer.Pass1HitInfo hitInfo,
				stoppedEarly: out _,
				maxStepsReached: out _,
				hitSegIndex: out _,
				stepsIntegrated: out _,
				fieldEvals: out _,
				pass1Raycasts: out _,
				pass1ProbeHits: out _,
				fieldGridHits: out _,
				fieldGridMisses: out _,
				fieldGridFallbacks: out _,
				fieldSourceEvals: out _,
				curvatureMax: out _,
				curvatureMean: out _,
				dkMax: out _,
				d2kMax: out _,
				turnSum: out _,
				turnMax: out _,
				physicsUsec: out _,
				curvatureGrid: null,
				fieldGrid: null,
				contactClassifier: null,
				formalProbePixelIndex: pixel,
				frozenBoundarySnapshot: frozenBoundaries,
				boundaryExecutionOwnership: frozenBoundaries != null
					? RayBeamRenderer.BoundaryExecutionOwnership.FrozenFormal
					: RayBeamRenderer.BoundaryExecutionOwnership.LiveCache);
			return hitInfo;
		}
		finally
		{
			query.Dispose();
		}
	}

	private void ConfigureIntegrator(int stepsPerRay, float stepLength)
	{
		_renderer.StepsPerRay = stepsPerRay;
		_renderer.StepLength = stepLength;
		_renderer.MinStepLength = stepLength;
		_renderer.MaxStepLength = stepLength;
		_renderer.CollisionEveryNSteps = 1;
		_renderer.UseIntegratedField = true;
		_renderer.UseScreenSpaceCollisionCadence = false;
	}

	private BoundaryLayerVolume AddBoundary(
		string name,
		Vector3 center,
		float radius,
		BoundaryLayerVolume.BoundaryExecutionMode executionMode,
		BoundaryLayerVolume.BoundaryBehavior behavior)
	{
		var boundary = new BoundaryLayerVolume
		{
			Name = name,
			Position = center,
			Radius = radius,
			ExecutionMode = executionMode,
			Behavior = behavior,
			CrossingPolicy = BoundaryLayerVolume.BoundaryCrossingPolicy.EntryOnly,
			Enabled = true
		};
		AddChild(boundary);
		return boundary;
	}

	private BoundaryLayerVolume AddBoxBoundary(
		string name,
		Vector3 center,
		Vector3 halfExtents,
		BoundaryLayerVolume.BoundaryBehavior behavior,
		BoundaryLayerVolume.BoundaryCrossingPolicy crossingPolicy)
	{
		BoundaryLayerVolume boundary = AddBoundary(
			name, center, 1f,
			BoundaryLayerVolume.BoundaryExecutionMode.CrossingEvent,
			behavior);
		boundary.ShapeType = BoundaryLayerVolume.BoundaryShapeType.Box;
		boundary.BoxExtents = halfExtents;
		boundary.CrossingPolicy = crossingPolicy;
		return boundary;
	}

	private FrozenBoundarySnapshot CaptureBoundaries(params BoundaryLayerVolume[] boundaries)
	{
		var nodes = new Godot.Collections.Array<Node>();
		foreach (BoundaryLayerVolume boundary in boundaries)
			nodes.Add(boundary);
		return FrozenBoundarySnapshot.Capture(this, nodes);
	}

	private void RemoveBoundaries(params BoundaryLayerVolume[] boundaries)
	{
		foreach (BoundaryLayerVolume boundary in boundaries)
		{
			if (boundary == null || !GodotObject.IsInstanceValid(boundary)) continue;
			RemoveChild(boundary);
			boundary.Free();
		}
	}

	private static RawFloat3BitsV1 Bits(in Vector3 value) =>
		RawFloat3BitsV1.FromSingle(value.X, value.Y, value.Z);

	private static RayBeamRenderer.FormalTransportEventTestingSnapshot FirstEvent(
		RayBeamRenderer.FormalTransportPixelTestingSnapshot pixel,
		TransportEventKindV1 kind)
	{
		foreach (RayBeamRenderer.FormalTransportEventTestingSnapshot transportEvent in pixel.Events)
			if (transportEvent.Kind == kind)
				return transportEvent;
		throw new InvalidOperationException($"missing event {kind}");
	}

	private static List<RayBeamRenderer.FormalTransportEventTestingSnapshot> EventsOfKind(
		RayBeamRenderer.FormalTransportPixelTestingSnapshot pixel,
		TransportEventKindV1 kind)
	{
		var events = new List<RayBeamRenderer.FormalTransportEventTestingSnapshot>();
		foreach (RayBeamRenderer.FormalTransportEventTestingSnapshot transportEvent in pixel.Events)
			if (transportEvent.Kind == kind)
				events.Add(transportEvent);
		return events;
	}

	private RayBeamRenderer.FormalTransportPixelTestingSnapshot OnlyPixel()
	{
		RayBeamRenderer.FormalTransportPixelTestingSnapshot[] pixels = _renderer.GetFormalTransportLedgerForTesting().Pixels;
		Assert(pixels.Length == 1, "one provisional pixel");
		return pixels[0];
	}

	private static bool ContainsKind(
		RayBeamRenderer.FormalTransportPixelTestingSnapshot pixel,
		TransportEventKindV1 kind)
	{
		foreach (RayBeamRenderer.FormalTransportEventTestingSnapshot transportEvent in pixel.Events)
			if (transportEvent.Kind == kind)
				return true;
		return false;
	}

	private static RayBeamRenderer.FieldSourceSnap[] AbsorbingSource() => new[]
	{
		new RayBeamRenderer.FieldSourceSnap
		{
			Enabled = true,
			Center = Vector3.Zero,
			RInner = 1f,
			ROuter = 2f,
			ModeFlags = FieldMath.ModeFlagAbsorbInsideInnerRadius,
			TransportModel = TransportModel.GRIN_Optical
		}
	};

	private static void Assert(bool condition, string label)
	{
		if (!condition)
			throw new InvalidOperationException(label);
	}
}
