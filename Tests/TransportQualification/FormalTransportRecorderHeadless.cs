using Godot;
using System;
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
		bool stopOnHit = false)
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
				formalProbePixelIndex: pixel);
			return hitInfo;
		}
		finally
		{
			query.Dispose();
		}
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
