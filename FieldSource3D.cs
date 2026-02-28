using Godot;
using System;
using RendererCore.Fields;

/// <summary>
/// Authoring node for local field sources plus editor/runtime academic debug visualization.
/// This node is intentionally debug-heavy and does not modify renderer hot-loop behavior.
/// </summary>
[Tool]
public partial class FieldSource3D : Node3D
{
	public enum ProfileType
	{
		Power,
		InversePower,
		Gaussian,
		Shell
	}

	public enum DebugVizOpacityModeKind
	{
		Wireframe = 0,
		Ghosted = 1,
		Solid = 2
	}

	[Flags]
	public enum DebugVizPlaneFlags
	{
		None = 0,
		XY = 1 << 0,
		XZ = 1 << 1,
		YZ = 1 << 2,
		All = XY | XZ | YZ
	}

	public struct ResolvedFieldParams
	{
		public bool enabled;
		public uint modeFlags;
		public FieldShapeType shapeType;
		public FieldCurveType curveType;
		public float amp;
		public float a;
		public float b;
		public float c;
		public float rInner;
		public float rOuter;
		public float softening;
		public float sigma;
	}

	private const uint ModeFlagInvertSign = 1u << 0;
	private const float ResolveEps = 1e-6f;

	[Export] public bool Enabled = true;

	[ExportGroup("Field Model (Canonical)")]
	[Export] public MetricModel MetricModel { get; set; } = MetricModel.GRIN;
	[Export] public float RInner { get; set; } = 0f;
	[Export] public float ROuter { get; set; } = 0f;
	[Export] public float Amp { get; set; } = 0f;
	[Export] public uint ModeFlags { get; set; } = 0;
	[Export] public float Softening = 0.05f;
	[Export] public float Sigma = 5.0f;

	[ExportGroup("Shape")]
	[Export] public FieldShapeType ShapeType { get; set; } = FieldShapeType.SphereRadial;
	[Export] public Vector3 BoxExtents { get; set; } = new Vector3(10f, 10f, 10f);

	[ExportGroup("Curve")]
	[Export] public FieldCurveType CurveType { get; set; } = FieldCurveType.Linear;
	[Export] public float CurveA { get; set; } = 0f;
	[Export] public float CurveB { get; set; } = 0f;
	[Export] public float CurveC { get; set; } = 0f;

	[ExportGroup("Legacy (Deprecated)")]
	// Deprecated compat. Use Shape/Curve/Amp for new scenes.
	[Export] public bool Attract = true;
	// Deprecated compat. Use Shape/Curve/Amp for new scenes.
	[Export] public float Strength = 1.0f;
	// Deprecated compat. Use Shape/Curve/Amp for new scenes.
	[Export] public float MinRadius = 0.0f;
	// Deprecated compat. Use Shape/Curve/Amp for new scenes.
	[Export] public float MaxRadius = 0.0f;
	// Deprecated compat. Use Shape/Curve/Amp for new scenes.
	[Export] public ProfileType Profile = ProfileType.Power;
	// Deprecated compat. Use Shape/Curve/Amp for new scenes.
	[Export] public bool OverrideGamma = true;
	// Deprecated compat. Use Shape/Curve/Amp for new scenes.
	[Export] public float Gamma = 1.0f;
	// Deprecated compat. Use Shape/Curve/Amp for new scenes.
	[Export] public bool OverrideBetaScale = true;
	// Deprecated compat. Use Shape/Curve/Amp for new scenes.
	[Export] public float BetaScale = 0.0010f;
	// Deprecated compat. Use Shape/Curve/Amp for new scenes.
	[Export] public float InnerRadius = 3.0f;
	// Deprecated compat. Use Shape/Curve/Amp for new scenes.
	[Export] public float OuterRadius = 6.0f;
	// Deprecated compat. Use Shape/Curve/Amp for new scenes.
	[Export] public float EdgeSoftness = 0.5f;

	[ExportGroup("Academic Debug Viz")]
	[Export] public bool DebugVizEnabled { get; set; } = true;
	[Export] public DebugVizOpacityModeKind DebugVizOpacityMode { get; set; } = DebugVizOpacityModeKind.Wireframe;
	[Export(PropertyHint.Flags, "XY,XZ,YZ")]
	public int DebugVizPlanes
	{
		get => _debugVizPlanes;
		set => _debugVizPlanes = value & (int)DebugVizPlaneFlags.All;
	}

	public DebugVizPlaneFlags DebugVizPlaneMask => (DebugVizPlaneFlags)_debugVizPlanes;
	[Export] public bool DebugVizShowInnerOuter { get; set; } = true;
	[Export] public bool DebugVizShowSigma { get; set; } = false;
	[Export] public bool DebugVizShowDensityVectors { get; set; } = false;
	[Export(PropertyHint.Range, "4,96,1")] public int DebugVizDensityVectorCount { get; set; } = 16;
	[Export(PropertyHint.Range, "0.05,5.0,0.01")] public float DebugVizDensityVectorScale { get; set; } = 0.75f;
	[Export] public bool DebugVizShowDensityZones { get; set; } = false;
	[Export(PropertyHint.Range, "2,6,1")] public int DebugVizDensityZoneCount { get; set; } = 3;
	[Export(PropertyHint.Range, "8,256,1")] public int DebugVizRingSegments { get; set; } = 64;
	[Export(PropertyHint.Range, "0.25,8.0,0.05")] public float DebugVizLineWidth { get; set; } = 2.0f;
	[Export] public Color DebugVizColorInner { get; set; } = new Color(0.1f, 0.9f, 0.9f, 1.0f);
	[Export] public Color DebugVizColorOuter { get; set; } = new Color(0.15f, 0.95f, 0.35f, 1.0f);
	[Export] public Color DebugVizColorSigma { get; set; } = new Color(1.0f, 0.85f, 0.25f, 1.0f);
	[Export] public bool DebugVizAlwaysOnTop { get; set; } = true;
	[Export] public bool DebugVizInGame { get; set; } = false;

	[Export]
	public string DebugVizSummary
	{
		get => _debugVizSummary;
		private set
		{
			if (_debugVizSummary == value)
			{
				return;
			}

			_debugVizSummary = value;
			if (Engine.IsEditorHint())
			{
				NotifyPropertyListChanged();
			}
		}
	}

	[ExportGroup("Equation Preview (Read Only)")]
	[Export(PropertyHint.MultilineText)]
	public string EffectiveEquationCore
	{
		get => _effectiveEquationCore;
		private set
		{
			if (_effectiveEquationCore == value)
			{
				return;
			}

			_effectiveEquationCore = value;
			if (Engine.IsEditorHint())
			{
				NotifyPropertyListChanged();
			}
		}
	}

	[Export(PropertyHint.MultilineText)]
	public string EffectiveEquationIntegrated
	{
		get => _effectiveEquationIntegrated;
		private set
		{
			if (_effectiveEquationIntegrated == value)
			{
				return;
			}

			_effectiveEquationIntegrated = value;
			if (Engine.IsEditorHint())
			{
				NotifyPropertyListChanged();
			}
		}
	}

	public string EffectiveSummary => _effectiveSummary;

	[ExportGroup("Debug (Legacy)")]
	[Export] public bool DebugDrawBounds { get; set; } = false;
	[Export] public Color DebugColor { get; set; } = new Color(0.4f, 0.9f, 1.0f);

	[Export]
	public bool DebugDrawInGame
	{
		get => DebugVizInGame;
		set => DebugVizInGame = value;
	}

	[Export]
	public bool DebugDrawAlwaysOnTop
	{
		get => DebugVizAlwaysOnTop;
		set => DebugVizAlwaysOnTop = value;
	}

	[Export]
	public int DebugRingSegments
	{
		get => DebugVizRingSegments;
		set => DebugVizRingSegments = value;
	}

	[Export]
	public float DebugLineWidth
	{
		get => DebugVizLineWidth;
		set => DebugVizLineWidth = value;
	}

	private MeshInstance3D _debugMeshInstance;
	private ImmediateMesh _debugMesh;
	private DebugVizState _debugVizState;
	private bool _debugVizStateValid;
	private string _debugVizSummary = "inner=n/a outer=n/a source=none";
	private string _effectiveSummary = "shape=SphereRadial curve=Linear amp=0 a=0 b=0 c=0 r=[0,0] sigma=0 source=canonical";
	private string _effectiveEquationCore = "core: a_local = sign(metric)*normalize(p_local)*(amp*f(u))";
	private string _effectiveEquationIntegrated = "integrated: a = dir*(beta_eff*BendScale*FieldStrength)*profile(r)";
	private int _debugVizPlanes = (int)DebugVizPlaneFlags.All;
	private bool _usedLegacyMigration;
	private bool _loggedLegacyMigration;
	private bool _warnedCanonicalLegacyConflict;
	private bool _warnedNoRadii;
	private static bool _warnedZeroCurvatureAllSources;
	private double _rebuildWindowStartSec;
	private int _rebuildsThisWindow;
	private bool _warnedFrequentRebuilds;

	public override void _Ready()
	{
		AddToGroup("field_sources");
		WarnIfCanonicalAndLegacyBothSet();
		ApplyLegacyCompatibilityShim();
		ValidateAndClamp();
		ResolveEffectiveParams(out _);
		RefreshEquationPreviews();
		if (DebugVizEnabled)
		{
			GD.Print($"[FieldSource3D] {GetPath()} {EffectiveSummary}");
		}
		SetProcess(true);
		CallDeferred(nameof(DeferredWarnIfZeroCurvatureAcrossSources));
	}

	public override void _ExitTree()
	{
		ClearDebugDraw();
	}

	public override void _Process(double delta)
	{
		RefreshEquationPreviews();

		if (!ShouldDrawDebugViz())
		{
			HideDebugDraw();
			return;
		}

		EnsureDebugDraw();
		DebugVizState state = GetDebugVizState();

		if (!_debugVizStateValid || !_debugVizState.Equals(state))
		{
			RebuildDebugMesh(state);
			_debugVizState = state;
			_debugVizStateValid = true;
		}
	}

	public bool IsCanonicalUnset()
	{
		return Mathf.Abs(Amp) <= ResolveEps
			&& Mathf.Abs(CurveA) <= ResolveEps
			&& Mathf.Abs(CurveB) <= ResolveEps
			&& Mathf.Abs(CurveC) <= ResolveEps
			&& Mathf.Abs(RInner) <= ResolveEps
			&& Mathf.Abs(ROuter) <= ResolveEps
			&& ModeFlags == 0u
			&& ShapeType == FieldShapeType.SphereRadial
			&& CurveType == FieldCurveType.Linear;
	}

	public ResolvedFieldParams ResolveEffectiveParams(out string reason)
	{
		ResolvedFieldParams resolved;
		if (!IsCanonicalUnset())
		{
			resolved = BuildCanonicalParams();
			reason = _usedLegacyMigration ? "legacy_migrated" : "canonical";
		}
		else if (HasMeaningfulLegacyParams(out string legacyReason))
		{
			resolved = ResolveLegacyCanonicalParams();
			reason = "legacy_migrated";
			MaybeLogLegacyMigration(legacyReason);
		}
		else
		{
			resolved = BuildCanonicalParams();
			reason = "canonical";
		}

		_effectiveSummary = BuildEffectiveSummary(resolved, reason);
		return resolved;
	}

	/// <summary>
	/// Resolves effective academic radii used by debug visualization.
	/// Uses ResolveEffectiveParams as the single source of truth.
	/// </summary>
	public bool ResolveAcademicRadii(out float inner, out float outer, out string reasonString)
	{
		ResolvedFieldParams resolved = ResolveEffectiveParams(out reasonString);
		inner = 0f;
		outer = 0f;

		if (resolved.rOuter <= ResolveEps)
		{
			return false;
		}

		inner = Mathf.Min(resolved.rInner, resolved.rOuter);
		outer = Mathf.Max(resolved.rInner, resolved.rOuter);
		return true;
	}

	/// <summary>
	/// Resolves effective beta/gamma for debug workflows.
	/// </summary>
	public void ResolveAcademicBetaGamma(out float beta, out float gamma, out string reasonString)
	{
		ResolveAcademicBetaGamma(float.NaN, float.NaN, out beta, out gamma, out reasonString);
	}

	/// <summary>
	/// Resolves effective beta/gamma for debug workflows with explicit global inputs.
	/// </summary>
	public void ResolveAcademicBetaGamma(float globalBeta, float globalGamma, out float beta, out float gamma, out string reasonString)
	{
		ResolvedFieldParams resolved = ResolveEffectiveParams(out string source);
		float fallbackGamma = float.IsFinite(globalGamma) ? globalGamma : 2f;
		gamma = resolved.curveType == FieldCurveType.Power ? resolved.a : fallbackGamma;

		if (float.IsFinite(globalBeta) && MathF.Abs(globalBeta) > ResolveEps)
		{
			beta = globalBeta * resolved.amp;
			reasonString = $"globalBeta*amp ({source})";
			return;
		}

		beta = resolved.amp;
		reasonString = $"amp ({source})";
	}

	public void GetPackedParams8(out float rInner, out float rOuter, out float amp, out float a, out float b, out float c, out float reserved0, out float reserved1)
	{
		ResolvedFieldParams resolved = ResolveEffectiveParams(out _);
		rInner = resolved.rInner;
		rOuter = resolved.rOuter;
		amp = resolved.amp;
		a = resolved.a;
		b = resolved.b;
		c = resolved.c;
		reserved0 = 0f;
		reserved1 = 0f;
	}

	public Aabb GetLocalInfluenceAabb()
	{
		ResolvedFieldParams resolved = ResolveEffectiveParams(out _);
		if (resolved.shapeType == FieldShapeType.BoxVolume)
		{
			Vector3 size = BoxExtents * 2.0f;
			return new Aabb(-BoxExtents, size);
		}

		float outer = Mathf.Max(0f, resolved.rOuter);
		Vector3 half = new Vector3(outer, outer, outer);
		return new Aabb(-half, half * 2.0f);
	}

	public Aabb GetWorldInfluenceAabbConservative()
	{
		Aabb local = GetLocalInfluenceAabb();
		Vector3 min = Vector3.Zero;
		Vector3 max = Vector3.Zero;
		bool initialized = false;

		for (int z = 0; z <= 1; z++)
		{
			for (int y = 0; y <= 1; y++)
			{
				for (int x = 0; x <= 1; x++)
				{
					Vector3 corner = new Vector3(
						x == 0 ? local.Position.X : local.Position.X + local.Size.X,
						y == 0 ? local.Position.Y : local.Position.Y + local.Size.Y,
						z == 0 ? local.Position.Z : local.Position.Z + local.Size.Z);
					Vector3 world = GlobalTransform * corner;

					if (!initialized)
					{
						min = world;
						max = world;
						initialized = true;
					}
					else
					{
						min = new Vector3(
							Mathf.Min(min.X, world.X),
							Mathf.Min(min.Y, world.Y),
							Mathf.Min(min.Z, world.Z));
						max = new Vector3(
							Mathf.Max(max.X, world.X),
							Mathf.Max(max.Y, world.Y),
							Mathf.Max(max.Z, world.Z));
					}
				}
			}
		}

		return new Aabb(min, max - min);
	}

	private bool ShouldDrawDebugViz()
	{
		if (!DebugVizEnabled)
		{
			return false;
		}

		if (Engine.IsEditorHint())
		{
			return true;
		}

		return DebugVizInGame;
	}

	private void WarnIfCanonicalAndLegacyBothSet()
	{
		if (_warnedCanonicalLegacyConflict || IsCanonicalUnset() || !HasLegacyNonDefaultOverrides())
		{
			return;
		}

		ResolvedFieldParams canonical = BuildCanonicalParams();
		ResolvedFieldParams legacyMapped = ResolveLegacyCanonicalParams();
		if (!AreMateriallyDifferent(canonical, legacyMapped))
		{
			return;
		}

		_warnedCanonicalLegacyConflict = true;
		GD.PushWarning("[FieldSource3D][Warn] canonical+legacy both set; using canonical. (legacy ignored)");
	}

	private bool HasLegacyNonDefaultOverrides()
	{
		return !Attract
			|| !Mathf.IsEqualApprox(Strength, 1.0f)
			|| !Mathf.IsEqualApprox(MinRadius, 0.0f)
			|| !Mathf.IsEqualApprox(MaxRadius, 0.0f)
			|| Profile != ProfileType.Power
			|| !OverrideGamma
			|| !Mathf.IsEqualApprox(Gamma, 1.0f)
			|| !OverrideBetaScale
			|| !Mathf.IsEqualApprox(BetaScale, 0.0010f)
			|| !Mathf.IsEqualApprox(InnerRadius, 3.0f)
			|| !Mathf.IsEqualApprox(OuterRadius, 6.0f)
			|| !Mathf.IsEqualApprox(EdgeSoftness, 0.5f);
	}

	private void ApplyLegacyCompatibilityShim()
	{
		if (!IsCanonicalUnset() || !HasMeaningfulLegacyParams(out string legacyReason))
		{
			_usedLegacyMigration = false;
			return;
		}

		ResolvedFieldParams migrated = ResolveLegacyCanonicalParams();
		ShapeType = migrated.shapeType;
		CurveType = migrated.curveType;
		ModeFlags = migrated.modeFlags;
		RInner = migrated.rInner;
		ROuter = migrated.rOuter;
		Amp = migrated.amp;
		CurveA = migrated.a;
		CurveB = migrated.b;
		CurveC = migrated.c;
		Softening = migrated.softening;
		Sigma = migrated.sigma;
		_usedLegacyMigration = true;
		MaybeLogLegacyMigration(legacyReason);
	}

	private bool HasMeaningfulLegacyParams(out string reason)
	{
		if (Mathf.Abs(Strength) > ResolveEps)
		{
			reason = "legacy Strength";
			return true;
		}
		if (Mathf.Abs(BetaScale) > ResolveEps || OverrideBetaScale)
		{
			reason = "legacy BetaScale";
			return true;
		}
		if (Mathf.Abs(Gamma) > ResolveEps || OverrideGamma)
		{
			reason = "legacy Gamma";
			return true;
		}
		if (Mathf.Abs(InnerRadius) > ResolveEps || Mathf.Abs(OuterRadius) > ResolveEps)
		{
			reason = "legacy Inner/Outer radius";
			return true;
		}
		if (Mathf.Abs(MinRadius) > ResolveEps || Mathf.Abs(MaxRadius) > ResolveEps)
		{
			reason = "legacy Min/Max radius";
			return true;
		}
		if (Profile != ProfileType.Power || !Attract)
		{
			reason = "legacy Profile/Attract";
			return true;
		}

		reason = "none";
		return false;
	}

	private ResolvedFieldParams BuildCanonicalParams()
	{
		float inner = Mathf.Max(0f, RInner);
		float outer = Mathf.Max(0f, ROuter);
		if (outer > 0f && outer < inner)
		{
			outer = inner;
		}

		return new ResolvedFieldParams
		{
			enabled = Enabled,
			modeFlags = ModeFlags,
			shapeType = ShapeType,
			curveType = CurveType,
			amp = Amp,
			a = CurveA,
			b = CurveB,
			c = CurveC,
			rInner = inner,
			rOuter = outer,
			softening = Mathf.Max(0f, Softening),
			sigma = Mathf.Max(0f, Sigma)
		};
	}

	private ResolvedFieldParams ResolveLegacyCanonicalParams()
	{
		FieldCurveType curveType = FieldCurveType.Power;
		float a = 1f;
		float b = 0f;
		float c = 0f;

		switch (Profile)
		{
			case ProfileType.Power:
				curveType = FieldCurveType.Power;
				a = OverrideGamma ? Gamma : 1f;
				break;
			case ProfileType.InversePower:
				curveType = FieldCurveType.Power;
				a = -(OverrideGamma ? Mathf.Abs(Gamma) : 1f);
				break;
			case ProfileType.Gaussian:
				curveType = FieldCurveType.Exponential;
				a = Mathf.Max(ResolveEps, 1f / Mathf.Max(ResolveEps, Sigma));
				break;
			case ProfileType.Shell:
				curveType = FieldCurveType.Polynomial;
				a = 1f;
				b = 0f;
				c = 0f;
				break;
		}

		if (!TryResolveLegacyRadii(out float inner, out float outer))
		{
			inner = Mathf.Max(0f, MinRadius);
			outer = Mathf.Max(inner, MaxRadius);
		}

		uint modeFlags = ModeFlags;
		if (!Attract)
		{
			modeFlags |= ModeFlagInvertSign;
		}

		return new ResolvedFieldParams
		{
			enabled = Enabled,
			modeFlags = modeFlags,
			shapeType = FieldShapeType.SphereRadial,
			curveType = curveType,
			amp = Strength,
			a = a,
			b = b,
			c = c,
			rInner = inner,
			rOuter = outer,
			softening = Mathf.Max(0f, Softening),
			sigma = Mathf.Max(0f, Sigma)
		};
	}

	private bool TryResolveLegacyRadii(out float inner, out float outer)
	{
		inner = 0f;
		outer = 0f;

		float shellInner = Mathf.Max(0f, InnerRadius);
		float shellOuter = Mathf.Max(0f, OuterRadius);
		if (shellOuter > ResolveEps)
		{
			inner = Mathf.Min(shellInner, shellOuter);
			outer = Mathf.Max(shellInner, shellOuter);
			return true;
		}

		if (MaxRadius > ResolveEps)
		{
			inner = Mathf.Max(0f, MinRadius);
			outer = Mathf.Max(inner, MaxRadius);
			return true;
		}

		return false;
	}

	private static bool AreMateriallyDifferent(ResolvedFieldParams a, ResolvedFieldParams b)
	{
		if (a.enabled != b.enabled || a.modeFlags != b.modeFlags || a.shapeType != b.shapeType || a.curveType != b.curveType)
		{
			return true;
		}

		return Mathf.Abs(a.amp - b.amp) > 1e-4f
			|| Mathf.Abs(a.a - b.a) > 1e-4f
			|| Mathf.Abs(a.b - b.b) > 1e-4f
			|| Mathf.Abs(a.c - b.c) > 1e-4f
			|| Mathf.Abs(a.rInner - b.rInner) > 1e-4f
			|| Mathf.Abs(a.rOuter - b.rOuter) > 1e-4f
			|| Mathf.Abs(a.softening - b.softening) > 1e-4f
			|| Mathf.Abs(a.sigma - b.sigma) > 1e-4f;
	}

	private string BuildEffectiveSummary(ResolvedFieldParams resolved, string source)
	{
		return $"shape={resolved.shapeType} curve={resolved.curveType} amp={resolved.amp:0.###} a={resolved.a:0.###} b={resolved.b:0.###} c={resolved.c:0.###} r=[{resolved.rInner:0.###},{resolved.rOuter:0.###}] sigma={resolved.sigma:0.###} source={source}";
	}

	private void RefreshEquationPreviews()
	{
		ResolvedFieldParams resolved = ResolveEffectiveParams(out string source);
		string metricSign = MetricModel == MetricModel.GordonMetric ? "-" : "+";
		string curveCore = BuildCoreCurveEquation(resolved);
		EffectiveEquationCore =
			$"source={source}\n" +
			"core: r=|p_local|, u=clamp((r-rInner)/max(eps,rOuter-rInner),0,1)\n" +
			$"f(u)={curveCore}; a_local={metricSign}normalize(p_local)*(amp*f(u))";

		bool invertSign = (resolved.modeFlags & ModeFlagInvertSign) != 0u;
		string dirExpr = invertSign ? "+rvec/r" : "-rvec/r";
		string profileExpr = BuildIntegratedProfileEquation(resolved, out float gamma, out float sigma);
		EffectiveEquationIntegrated =
			"integration: r=sqrt(|p-c|^2+soft^2), beta_eff=(|beta_g|>eps?beta_g*amp:amp)\n" +
			$"A=beta_eff*BendScale*FieldStrength; dir={dirExpr}\n" +
			$"profile={profileExpr} (gamma={gamma:0.###}, sigma={sigma:0.###}); a=dir*(A*profile)";
	}

	private string BuildCoreCurveEquation(ResolvedFieldParams resolved)
	{
		return resolved.curveType switch
		{
			FieldCurveType.Linear => "1-u",
			FieldCurveType.Power => $"(1-u)^{resolved.a:0.###}",
			FieldCurveType.Polynomial => $"{resolved.a:0.###}+({resolved.b:0.###}*u)+({resolved.c:0.###}*u^2)",
			FieldCurveType.Exponential => $"exp(-{resolved.a:0.###}*u)",
			_ => "1-u"
		};
	}

	private string BuildIntegratedProfileEquation(ResolvedFieldParams resolved, out float gamma, out float sigma)
	{
		gamma = 1f;
		sigma = Mathf.Max(0f, resolved.sigma);

		switch (resolved.curveType)
		{
			case FieldCurveType.Linear:
				gamma = 0f;
				return "r^0";
			case FieldCurveType.Power:
				gamma = resolved.a;
				return $"r^{gamma:0.###}";
			case FieldCurveType.Polynomial:
				gamma = resolved.a;
				return $"r^{gamma:0.###}  (poly mapped to power in integrated path)";
			case FieldCurveType.Exponential:
				if (sigma <= ResolveEps)
				{
					sigma = resolved.a > ResolveEps ? (1f / resolved.a) : 0.0001f;
				}
				return $"exp(-(r/{sigma:0.###})^2)";
			default:
				return "r^1";
		}
	}

	private void MaybeLogLegacyMigration(string reason)
	{
		if (_loggedLegacyMigration)
		{
			return;
		}

		_loggedLegacyMigration = true;
		GD.Print($"[FieldSource3D] migrated legacy params to canonical (reason={reason})");
	}

	private void ValidateAndClamp()
	{
		bool warned = false;

		if (RInner < 0f)
		{
			RInner = 0f;
			warned = true;
		}

		if (ROuter < 0f)
		{
			ROuter = 0f;
			warned = true;
		}

		if (ROuter < RInner)
		{
			ROuter = RInner;
			warned = true;
		}

		if (ShapeType == FieldShapeType.BoxVolume)
		{
			Vector3 clamped = new Vector3(
				Mathf.Max(0f, BoxExtents.X),
				Mathf.Max(0f, BoxExtents.Y),
				Mathf.Max(0f, BoxExtents.Z));
			if (clamped != BoxExtents)
			{
				BoxExtents = clamped;
				warned = true;
			}
		}

		DebugVizRingSegments = Mathf.Max(8, DebugVizRingSegments);
		DebugVizLineWidth = Mathf.Max(0.25f, DebugVizLineWidth);
		DebugVizDensityVectorCount = Mathf.Clamp(DebugVizDensityVectorCount, 4, 96);
		DebugVizDensityVectorScale = Mathf.Max(0.05f, DebugVizDensityVectorScale);
		DebugVizDensityZoneCount = Mathf.Clamp(DebugVizDensityZoneCount, 2, 6);
		_debugVizPlanes &= (int)DebugVizPlaneFlags.All;
		Softening = Mathf.Max(0f, Softening);
		Sigma = Mathf.Max(0f, Sigma);
		MaxRadius = Mathf.Max(0f, MaxRadius);
		MinRadius = Mathf.Clamp(MinRadius, 0f, MaxRadius > 0f ? MaxRadius : float.MaxValue);
		OuterRadius = Mathf.Max(0f, OuterRadius);
		InnerRadius = Mathf.Clamp(InnerRadius, 0f, OuterRadius > 0f ? OuterRadius : float.MaxValue);

		if (warned)
		{
			GD.PushWarning($"{Name}: FieldSource3D parameters were clamped to safe ranges.");
		}
	}

	private DebugVizState GetDebugVizState()
	{
		bool hasInnerOuter = ResolveAcademicRadii(out float inner, out float outer, out string reason);
		ResolvedFieldParams resolved = ResolveEffectiveParams(out _);
		UpdateDebugSummary(hasInnerOuter, inner, outer, reason);

		if (!hasInnerOuter && !_warnedNoRadii)
		{
			GD.Print($"[FieldViz] no radii available node={GetPath()}");
			_warnedNoRadii = true;
		}

		return new DebugVizState
		{
			Enabled = resolved.enabled,
			HasInnerOuter = hasInnerOuter,
			InnerRadius = Mathf.Max(0f, inner),
			OuterRadius = Mathf.Max(0f, outer),
			ShowInnerOuter = DebugVizShowInnerOuter,
			ShowSigma = DebugVizShowSigma && resolved.sigma > 0f,
			SigmaRadius = Mathf.Max(0f, resolved.sigma),
			ShowDensityVectors = DebugVizShowDensityVectors && hasInnerOuter,
			DensityVectorCount = DebugVizDensityVectorCount,
			DensityVectorScale = DebugVizDensityVectorScale,
			ShowDensityZones = DebugVizShowDensityZones && hasInnerOuter,
			DensityZoneCount = DebugVizDensityZoneCount,
			ModeFlags = resolved.modeFlags,
			CurveType = resolved.curveType,
			CurveA = resolved.a,
			CurveB = resolved.b,
			CurveC = resolved.c,
			Sigma = resolved.sigma,
			Planes = DebugVizPlaneMask,
			OpacityMode = DebugVizOpacityMode,
			Segments = Mathf.Max(8, DebugVizRingSegments),
			LineWidth = Mathf.Max(0.25f, DebugVizLineWidth),
			AlwaysOnTop = DebugVizAlwaysOnTop,
			InnerColor = DebugVizColorInner,
			OuterColor = DebugVizColorOuter,
			SigmaColor = DebugVizColorSigma,
			Transform = GlobalTransform
		};
	}

	private void UpdateDebugSummary(bool hasInnerOuter, float inner, float outer, string reason)
	{
		if (hasInnerOuter)
		{
			DebugVizSummary = $"{EffectiveSummary} inner={inner:0.###} outer={outer:0.###} reason={reason}";
		}
		else
		{
			DebugVizSummary = $"{EffectiveSummary} inner=n/a outer=n/a reason={reason}";
		}
	}

	private void EnsureDebugDraw()
	{
		if (_debugMeshInstance != null)
		{
			return;
		}

		_debugMesh = new ImmediateMesh();
		_debugMeshInstance = new MeshInstance3D
		{
			Name = "_FieldSourceAcademicViz",
			Mesh = _debugMesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		AddChild(_debugMeshInstance);
	}

	private void HideDebugDraw()
	{
		if (_debugMeshInstance != null)
		{
			_debugMeshInstance.Visible = false;
		}
	}

	private void ClearDebugDraw()
	{
		if (_debugMeshInstance == null)
		{
			return;
		}

		_debugMeshInstance.QueueFree();
		_debugMeshInstance = null;
		_debugMesh = null;
		_debugVizStateValid = false;
	}

	private void RebuildDebugMesh(DebugVizState state)
	{
		if (_debugMesh == null || _debugMeshInstance == null)
		{
			return;
		}

		_debugMesh.ClearSurfaces();
		_debugMeshInstance.Visible = true;

		TrackRebuildFrequency();

		if (state.OpacityMode != DebugVizOpacityModeKind.Wireframe)
		{
			if (state.ShowInnerOuter && state.HasInnerOuter)
			{
				AddFilledRingPlanes(state.InnerRadius, state.InnerColor, state);
				AddFilledRingPlanes(state.OuterRadius, state.OuterColor, state);
			}

			if (state.ShowSigma)
			{
				AddFilledRingPlanes(state.SigmaRadius, state.SigmaColor, state);
			}
		}

		if (state.ShowInnerOuter && state.HasInnerOuter)
		{
			AddWireRingPlanes(state.InnerRadius, state.InnerColor, state, dashed: false);
			AddWireRingPlanes(state.OuterRadius, state.OuterColor, state, dashed: false);
		}

		if (state.ShowSigma)
		{
			AddWireRingPlanes(state.SigmaRadius, state.SigmaColor, state, dashed: true);
		}

		AddDensityZoneRings(state);
		AddDensityVectors(state);

		// Keep a small center marker for orientation.
		AddCenterMarker(state);
	}

	private void AddCenterMarker(DebugVizState state)
	{
		float m = Mathf.Max(0.025f, 0.05f * state.LineWidth);
		Color markerColor = state.Enabled
			? new Color(0.95f, 0.95f, 0.95f, 0.95f)
			: new Color(0.6f, 0.6f, 0.6f, 0.4f);

		AddLineSurface(GetLineColorForMode(markerColor, state), state, () =>
		{
			AddLine(Vector3.Left * m, Vector3.Right * m);
			AddLine(Vector3.Down * m, Vector3.Up * m);
			AddLine(Vector3.Back * m, Vector3.Forward * m);
		});
	}

	private void AddWireRingPlanes(float radius, Color baseColor, DebugVizState state, bool dashed)
	{
		if (radius <= 0f)
		{
			return;
		}

		Color color = GetLineColorForMode(baseColor, state);
		AddLineSurface(color, state, () =>
		{
			if ((state.Planes & DebugVizPlaneFlags.XY) != 0)
			{
				AddCircle(radius, state.Segments, Vector3.Right, Vector3.Up, dashed);
			}
			if ((state.Planes & DebugVizPlaneFlags.XZ) != 0)
			{
				AddCircle(radius, state.Segments, Vector3.Right, Vector3.Forward, dashed);
			}
			if ((state.Planes & DebugVizPlaneFlags.YZ) != 0)
			{
				AddCircle(radius, state.Segments, Vector3.Up, Vector3.Forward, dashed);
			}
		});
	}

	private void AddFilledRingPlanes(float radius, Color baseColor, DebugVizState state)
	{
		if (radius <= 0f)
		{
			return;
		}

		Color fillColor = GetFillColorForMode(baseColor, state);
		float thickness = Mathf.Max(0.01f, 0.01f * state.LineWidth);

		AddTriangleSurface(fillColor, state, () =>
		{
			if ((state.Planes & DebugVizPlaneFlags.XY) != 0)
			{
				AddFilledRing(radius, thickness, state.Segments, Vector3.Right, Vector3.Up);
			}
			if ((state.Planes & DebugVizPlaneFlags.XZ) != 0)
			{
				AddFilledRing(radius, thickness, state.Segments, Vector3.Right, Vector3.Forward);
			}
			if ((state.Planes & DebugVizPlaneFlags.YZ) != 0)
			{
				AddFilledRing(radius, thickness, state.Segments, Vector3.Up, Vector3.Forward);
			}
		});
	}

	private void AddDensityZoneRings(DebugVizState state)
	{
		if (!state.ShowDensityZones || !state.HasInnerOuter)
		{
			return;
		}

		int zoneCount = Mathf.Clamp(state.DensityZoneCount, 2, 6);
		if (zoneCount <= 0)
		{
			return;
		}

		for (int i = 0; i < zoneCount; i++)
		{
			float t = (i + 1f) / (zoneCount + 1f);
			float radius = Mathf.Lerp(state.InnerRadius, state.OuterRadius, t);
			float strength = EvaluateDensityStrengthAtT(t, state);
			Color zoneColor = GetDensityZoneColor(strength);
			bool dashed = (i & 1) != 0;
			AddWireRingPlanes(radius, zoneColor, state, dashed);
		}
	}

	private void AddDensityVectors(DebugVizState state)
	{
		if (!state.ShowDensityVectors || !state.HasInnerOuter)
		{
			return;
		}

		int count = Mathf.Clamp(state.DensityVectorCount, 4, 96);
		float sampleRadius = Mathf.Lerp(state.InnerRadius, state.OuterRadius, 0.72f);
		if (sampleRadius <= 0f)
		{
			return;
		}

		float t = Mathf.Clamp((sampleRadius - state.InnerRadius) / Mathf.Max(ResolveEps, state.OuterRadius - state.InnerRadius), 0f, 1f);
		float strength = Mathf.Max(0.15f, EvaluateDensityStrengthAtT(t, state));
		float length = Mathf.Max(state.OuterRadius * 0.08f, state.DensityVectorScale * Mathf.Lerp(0.3f, 1.0f, strength));
		float head = Mathf.Clamp(length * 0.22f, 0.015f, 0.25f);
		bool invert = (state.ModeFlags & ModeFlagInvertSign) != 0u;
		Color vectorColor = new Color(1.0f, 0.45f, 0.2f, 0.95f);

		AddLineSurface(GetLineColorForMode(vectorColor, state), state, () =>
		{
			if ((state.Planes & DebugVizPlaneFlags.XY) != 0)
			{
				AddVectorRingSet(sampleRadius, count, Vector3.Right, Vector3.Up, Vector3.Forward, invert, length, head);
			}
			if ((state.Planes & DebugVizPlaneFlags.XZ) != 0)
			{
				AddVectorRingSet(sampleRadius, count, Vector3.Right, Vector3.Forward, Vector3.Up, invert, length, head);
			}
			if ((state.Planes & DebugVizPlaneFlags.YZ) != 0)
			{
				AddVectorRingSet(sampleRadius, count, Vector3.Up, Vector3.Forward, Vector3.Right, invert, length, head);
			}
		});
	}

	private void AddVectorRingSet(float radius, int count, Vector3 axisA, Vector3 axisB, Vector3 normal, bool invert, float length, float head)
	{
		int safeCount = Mathf.Max(4, count);
		for (int i = 0; i < safeCount; i++)
		{
			float angle = Mathf.Tau * i / safeCount;
			float cos = Mathf.Cos(angle);
			float sin = Mathf.Sin(angle);

			Vector3 radial = (axisA * cos) + (axisB * sin);
			if (radial.LengthSquared() < ResolveEps)
			{
				continue;
			}

			radial = radial.Normalized();
			Vector3 tangent = ((axisA * -sin) + (axisB * cos)).Normalized();
			Vector3 dir = invert ? radial : -radial;

			Vector3 start = radial * radius + normal * 0.005f;
			Vector3 tip = start + dir * length;
			Vector3 wingA = tip - (dir * head) + (tangent * (head * 0.5f));
			Vector3 wingB = tip - (dir * head) - (tangent * (head * 0.5f));

			AddLine(start, tip);
			AddLine(tip, wingA);
			AddLine(tip, wingB);
		}
	}

	private float EvaluateDensityStrengthAtT(float t, DebugVizState state)
	{
		float u = Mathf.Clamp(t, 0f, 1f);
		switch (state.CurveType)
		{
			case FieldCurveType.Linear:
				return 1f - u;
			case FieldCurveType.Power:
				return Mathf.Pow(Mathf.Max(0f, 1f - u), Mathf.Max(0f, state.CurveA));
			case FieldCurveType.Polynomial:
				return Mathf.Clamp(state.CurveA + state.CurveB * u + state.CurveC * u * u, 0f, 1f);
			case FieldCurveType.Exponential:
			{
				float radius = Mathf.Lerp(state.InnerRadius, state.OuterRadius, u);
				float sigma = state.Sigma > ResolveEps
					? state.Sigma
					: (state.CurveA > ResolveEps ? (1f / state.CurveA) : 1f);
				float x = radius / Mathf.Max(ResolveEps, sigma);
				return Mathf.Clamp(Mathf.Exp(-(x * x)), 0f, 1f);
			}
			default:
				return 1f - u;
		}
	}

	private static Color GetDensityZoneColor(float strength)
	{
		float s = Mathf.Clamp(strength, 0f, 1f);
		Color low = new Color(1.0f, 0.95f, 0.25f, 0.9f);
		Color high = new Color(1.0f, 0.3f, 0.16f, 0.95f);
		return low.Lerp(high, s);
	}

	private void AddLineSurface(Color color, DebugVizState state, Action addGeometry)
	{
		StandardMaterial3D material = CreateMaterial(color, state, transparent: color.A < 1f);
		_debugMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
		addGeometry();
		_debugMesh.SurfaceEnd();
	}

	private void AddTriangleSurface(Color color, DebugVizState state, Action addGeometry)
	{
		StandardMaterial3D material = CreateMaterial(color, state, transparent: color.A < 1f);
		_debugMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
		addGeometry();
		_debugMesh.SurfaceEnd();
	}

	private StandardMaterial3D CreateMaterial(Color color, DebugVizState state, bool transparent)
	{
		var material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = color,
			RenderPriority = state.AlwaysOnTop ? 127 : 0,
			NoDepthTest = state.AlwaysOnTop,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};

		if (transparent)
		{
			material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		}

		return material;
	}

	private Color GetLineColorForMode(Color baseColor, DebugVizState state)
	{
		float alpha = state.OpacityMode switch
		{
			DebugVizOpacityModeKind.Wireframe => Mathf.Clamp(baseColor.A, 0.25f, 1.0f),
			DebugVizOpacityModeKind.Ghosted => Mathf.Clamp(baseColor.A * 0.9f, 0.2f, 0.85f),
			_ => Mathf.Clamp(baseColor.A, 0.75f, 1.0f)
		};
		return new Color(baseColor.R, baseColor.G, baseColor.B, alpha);
	}

	private Color GetFillColorForMode(Color baseColor, DebugVizState state)
	{
		float alpha = state.OpacityMode switch
		{
			DebugVizOpacityModeKind.Ghosted => Mathf.Clamp(baseColor.A * 0.22f, 0.06f, 0.33f),
			DebugVizOpacityModeKind.Solid => Mathf.Clamp(baseColor.A * 0.9f, 0.45f, 0.95f),
			_ => 0f
		};
		return new Color(baseColor.R, baseColor.G, baseColor.B, alpha);
	}

	private void AddCircle(float radius, int segments, Vector3 axisA, Vector3 axisB, bool dashed)
	{
		int safeSegments = Mathf.Max(8, segments);
		int step = dashed ? 2 : 1;

		for (int i = 0; i < safeSegments; i += step)
		{
			float a0 = Mathf.Tau * i / safeSegments;
			float a1 = Mathf.Tau * (i + 1) / safeSegments;
			Vector3 p0 = axisA * (Mathf.Cos(a0) * radius) + axisB * (Mathf.Sin(a0) * radius);
			Vector3 p1 = axisA * (Mathf.Cos(a1) * radius) + axisB * (Mathf.Sin(a1) * radius);
			AddLine(p0, p1);
		}
	}

	private void AddFilledRing(float radius, float thickness, int segments, Vector3 axisA, Vector3 axisB)
	{
		int safeSegments = Mathf.Max(8, segments);
		float halfT = 0.5f * thickness;
		float inner = Mathf.Max(0.0001f, radius - halfT);
		float outer = radius + halfT;

		for (int i = 0; i < safeSegments; i++)
		{
			float a0 = Mathf.Tau * i / safeSegments;
			float a1 = Mathf.Tau * (i + 1) / safeSegments;

			Vector3 in0 = axisA * (Mathf.Cos(a0) * inner) + axisB * (Mathf.Sin(a0) * inner);
			Vector3 out0 = axisA * (Mathf.Cos(a0) * outer) + axisB * (Mathf.Sin(a0) * outer);
			Vector3 in1 = axisA * (Mathf.Cos(a1) * inner) + axisB * (Mathf.Sin(a1) * inner);
			Vector3 out1 = axisA * (Mathf.Cos(a1) * outer) + axisB * (Mathf.Sin(a1) * outer);

			// Two triangles per segment form a thin annulus strip.
			_debugMesh.SurfaceAddVertex(in0);
			_debugMesh.SurfaceAddVertex(out0);
			_debugMesh.SurfaceAddVertex(out1);

			_debugMesh.SurfaceAddVertex(in0);
			_debugMesh.SurfaceAddVertex(out1);
			_debugMesh.SurfaceAddVertex(in1);
		}
	}

	private void AddLine(Vector3 start, Vector3 end)
	{
		_debugMesh.SurfaceAddVertex(start);
		_debugMesh.SurfaceAddVertex(end);
	}

	private void TrackRebuildFrequency()
	{
		double now = Time.GetTicksMsec() * 0.001;
		if (_rebuildWindowStartSec <= 0.0 || (now - _rebuildWindowStartSec) > 1.0)
		{
			_rebuildWindowStartSec = now;
			_rebuildsThisWindow = 0;
		}

		_rebuildsThisWindow++;
		if (_rebuildsThisWindow > 30 && !_warnedFrequentRebuilds)
		{
			_warnedFrequentRebuilds = true;
			GD.Print($"[FieldViz] frequent debug mesh rebuilds node={GetPath()} rate>{_rebuildsThisWindow}/s");
		}
	}

	private void DeferredWarnIfZeroCurvatureAcrossSources()
	{
		if (_warnedZeroCurvatureAllSources || !Enabled)
		{
			return;
		}

		var tree = GetTree();
		if (tree == null)
		{
			return;
		}

		var nodes = tree.GetNodesInGroup("field_sources");
		if (nodes == null || nodes.Count == 0)
		{
			return;
		}

		bool anyEnabled = false;
		bool anyNonZeroCurvature = false;
		const float eps = 1e-6f;

		foreach (Node node in nodes)
		{
			if (node is not FieldSource3D field || !field.Enabled)
			{
				continue;
			}

			anyEnabled = true;
			ResolvedFieldParams resolved = field.ResolveEffectiveParams(out _);
			float effective = Mathf.Abs(resolved.amp);
			if (effective > eps)
			{
				anyNonZeroCurvature = true;
				break;
			}
		}

		if (anyEnabled && !anyNonZeroCurvature)
		{
			_warnedZeroCurvatureAllSources = true;
			GD.PushWarning("[FieldViz] effective curvature is zero across all enabled FieldSource3D nodes.");
		}
	}

	private struct DebugVizState
	{
		public bool Enabled;
		public bool HasInnerOuter;
		public float InnerRadius;
		public float OuterRadius;
		public bool ShowInnerOuter;
		public bool ShowSigma;
		public float SigmaRadius;
		public bool ShowDensityVectors;
		public int DensityVectorCount;
		public float DensityVectorScale;
		public bool ShowDensityZones;
		public int DensityZoneCount;
		public uint ModeFlags;
		public FieldCurveType CurveType;
		public float CurveA;
		public float CurveB;
		public float CurveC;
		public float Sigma;
		public DebugVizPlaneFlags Planes;
		public DebugVizOpacityModeKind OpacityMode;
		public int Segments;
		public float LineWidth;
		public bool AlwaysOnTop;
		public Color InnerColor;
		public Color OuterColor;
		public Color SigmaColor;
		public Transform3D Transform;

		public bool Equals(DebugVizState other)
		{
			return Enabled == other.Enabled
				&& HasInnerOuter == other.HasInnerOuter
				&& Mathf.IsEqualApprox(InnerRadius, other.InnerRadius)
				&& Mathf.IsEqualApprox(OuterRadius, other.OuterRadius)
				&& ShowInnerOuter == other.ShowInnerOuter
				&& ShowSigma == other.ShowSigma
				&& Mathf.IsEqualApprox(SigmaRadius, other.SigmaRadius)
				&& ShowDensityVectors == other.ShowDensityVectors
				&& DensityVectorCount == other.DensityVectorCount
				&& Mathf.IsEqualApprox(DensityVectorScale, other.DensityVectorScale)
				&& ShowDensityZones == other.ShowDensityZones
				&& DensityZoneCount == other.DensityZoneCount
				&& ModeFlags == other.ModeFlags
				&& CurveType == other.CurveType
				&& Mathf.IsEqualApprox(CurveA, other.CurveA)
				&& Mathf.IsEqualApprox(CurveB, other.CurveB)
				&& Mathf.IsEqualApprox(CurveC, other.CurveC)
				&& Mathf.IsEqualApprox(Sigma, other.Sigma)
				&& Planes == other.Planes
				&& OpacityMode == other.OpacityMode
				&& Segments == other.Segments
				&& Mathf.IsEqualApprox(LineWidth, other.LineWidth)
				&& AlwaysOnTop == other.AlwaysOnTop
				&& ColorsEqual(InnerColor, other.InnerColor)
				&& ColorsEqual(OuterColor, other.OuterColor)
				&& ColorsEqual(SigmaColor, other.SigmaColor)
				&& TransformsEqual(Transform, other.Transform);
		}

		private static bool ColorsEqual(Color a, Color b)
		{
			return Mathf.IsEqualApprox(a.R, b.R)
				&& Mathf.IsEqualApprox(a.G, b.G)
				&& Mathf.IsEqualApprox(a.B, b.B)
				&& Mathf.IsEqualApprox(a.A, b.A);
		}

		private static bool TransformsEqual(Transform3D a, Transform3D b)
		{
			return VectorsEqual(a.Origin, b.Origin)
				&& VectorsEqual(a.Basis.X, b.Basis.X)
				&& VectorsEqual(a.Basis.Y, b.Basis.Y)
				&& VectorsEqual(a.Basis.Z, b.Basis.Z);
		}

		private static bool VectorsEqual(Vector3 a, Vector3 b)
		{
			return Mathf.IsEqualApprox(a.X, b.X)
				&& Mathf.IsEqualApprox(a.Y, b.Y)
				&& Mathf.IsEqualApprox(a.Z, b.Z);
		}
	}
}
