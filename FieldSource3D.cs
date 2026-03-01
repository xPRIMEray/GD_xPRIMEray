using Godot;
using System;
using System.Collections.Generic;
using RendererCore.Fields;

/// <summary>
/// Authoring node for local field sources plus editor/runtime academic debug visualization.
/// This node is intentionally debug-heavy and does not modify renderer hot-loop behavior.
/// PR: Academic canonical radial model: u-clamped shell with profile f(u).
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
		public bool overrideBetaScale;
		public float betaScale;
		public float edgeSoftness;
		public Curve customCurve;
	}

	private const uint ModeFlagInvertSign = FieldMath.ModeFlagInvertSign;
	private const float ResolveEps = 1e-6f;

	// Primary: academic baseline controls.
	[ExportGroup("Primary (Academic Baseline)")]
	[Export] public bool Enabled = true;
	[Export] public MetricModel MetricModel { get; set; } = MetricModel.GRIN;
	[Export] public FieldShapeType ShapeType { get; set; } = FieldShapeType.SphereRadial;
	[Export] public float ROuter { get; set; } = 0f;
	[Export] public bool ApplyAcademicDefaults
	{
		get => false;
		set
		{
			if (!value)
			{
				return;
			}

			ResetToAcademicDefaults();
			if (Engine.IsEditorHint())
			{
				NotifyPropertyListChanged();
			}
		}
	}
	[Export] public FieldCurveType CurveType { get; set; } = FieldCurveType.Linear;

	[ExportSubgroup("Power Curve")]
	[Export] public float CanonicalGamma { get; set; } = 1.0f;

	[ExportSubgroup("Gaussian Curve")] 
	[Export] public float Sigma = 1.0f;

	[ExportSubgroup("Advanced Coefficients")]
	[Export] public float CurveA { get; set; } = 1.0f;
	[Export] public float CurveB { get; set; } = 0f;
	[Export] public float CurveC { get; set; } = 0f;
	[Export] public Curve CustomCurve { get; set; }

	[ExportSubgroup("In-Game Ray-Tracing only Scalars")]
	[Export] public float Amp { get; set; } = 1.00f;
	[Export] public float CanonicalBetaScale { get; set; } = 1.00f;

	// Advanced: secondary controls and expert overrides.
	[ExportGroup("Advanced")]
	[ExportSubgroup("Shape")]
	[Export] public Vector3 BoxExtents { get; set; } = new Vector3(10f, 10f, 10f);

	[ExportSubgroup("Radii")]
	[Export] public bool CanonicalEnableInnerRadius { get; set; } = false;
	[Export] public float RInner { get; set; } = 0f;

	[ExportSubgroup("Stability & Boundaries")]
	[Export] public float Softening = 0.05f;
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float CanonicalEdgeSoftness { get; set; } = 0f;

	[ExportSubgroup("Overrides / Expert")]
	[Export] public bool CanonicalOverrideBetaScale { get; set; } = true;
	[Export] public uint ModeFlags { get; set; } = 0;

	[ExportGroup("Legacy (Deprecated)")]
	[Export] public bool ShowLegacyControls { get; set; } = false;
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

	// Debug Viz: academic visual diagnostics.
	[ExportGroup("Debug Viz (Academic)")]
	[Export] public bool DebugVizEnabled { get; set; } = true;
	[Export] public DebugVizOpacityModeKind DebugVizOpacityMode { get; set; } = DebugVizOpacityModeKind.Wireframe;
	[Export(PropertyHint.Flags, "XY,XZ,YZ")]
	public int DebugVizPlanes
	{
		get => _debugVizPlanes;
		set => _debugVizPlanes = value & (int)DebugVizPlaneFlags.All;
	}

	public DebugVizPlaneFlags DebugVizPlaneMask => (DebugVizPlaneFlags)_debugVizPlanes;
	[ExportSubgroup("Core")]
	[Export] public bool DebugVizShowInnerOuter { get; set; } = true;
	[Export] public bool DebugVizShowSigma { get; set; } = false;
	[Export(PropertyHint.Range, "0.25,8.0,0.05")] public float DebugVizLineWidth { get; set; } = 2.0f;
	[Export] public Color DebugVizColorInner { get; set; } = new Color(0.1f, 0.9f, 0.9f, 1.0f);
	[Export] public Color DebugVizColorOuter { get; set; } = new Color(0.15f, 0.95f, 0.35f, 1.0f);
	[Export] public Color DebugVizColorSigma { get; set; } = new Color(1.0f, 0.85f, 0.25f, 1.0f);

	[ExportSubgroup("Density Zones")]
	[Export] public bool DebugVizShowDensityZones { get; set; } = false;
	[Export(PropertyHint.Range, "2,6,1")] public int DebugVizDensityZoneCount { get; set; } = 3;
	[Export(PropertyHint.Range, "8,256,1")] public int DebugVizRingSegments { get; set; } = 64;
	[Export(PropertyHint.Range, "0.0,0.25,0.001")] public float DebugVizZeroInnerRadiusEpsilonFraction { get; set; } = 0.01f;
	[Export] public Color DebugVizDensityZoneColorMin { get; set; } = new Color(0.0f, 0.90f, 1.00f, 0.85f);
	[Export] public Color DebugVizDensityZoneColorMax { get; set; } = new Color(1.0f, 0, 0.10f, 0.95f);

	[ExportSubgroup("Density Vectors")]
	[Export] public bool DebugVizShowDensityVectors { get; set; } = false;
	[Export(PropertyHint.Range, "2,16,1")] public int DebugVizDensityVectorLayers { get; set; } = 6;
	[Export(PropertyHint.Range, "4,96,1")] public int DebugVizDensityVectorCount { get; set; } = 16;
	[Export(PropertyHint.Range, "0.05,5.0,0.01")] public float DebugVizDensityVectorScale { get; set; } = 0.75f;
	[Export(PropertyHint.Range, "0.01,2.0,0.01")] public float DebugVizDensityArrowMinLength { get; set; } = 0.12f;
	[Export(PropertyHint.Range, "0.01,4.0,0.01")] public float DebugVizDensityArrowMaxLength { get; set; } = 0.45f;
	[Export(PropertyHint.Range, "0.005,1.0,0.005")] public float DebugVizDensityArrowMinHeadSize { get; set; } = 0.03f;
	[Export(PropertyHint.Range, "0.005,2.0,0.005")] public float DebugVizDensityArrowMaxHeadSize { get; set; } = 0.16f;
	[Export(PropertyHint.Range, "1,6,1")] public int DebugVizDensityArrowMinThicknessBands { get; set; } = 1;
	[Export(PropertyHint.Range, "1,10,1")] public int DebugVizDensityArrowMaxThicknessBands { get; set; } = 4;
	[Export(PropertyHint.Range, "0.2,3.0,0.05")] public float DebugVizDensityArrowThicknessIntensity { get; set; } = 1.0f;
	[Export] public bool DebugVizDensityVectorTipAtOrigin { get; set; } = true;
	[Export] public Color DebugVizDensityVectorColorMin { get; set; } = new Color(0.0f, 0.90f, 1.00f, 0.85f);
	[Export] public Color DebugVizDensityVectorColorMax { get; set; } = new Color(1.0f, 0, 0.10f, 0.95f);

	[ExportSubgroup("Render")]
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float DebugVizGlobalOpacity { get; set; } = 0.90f;
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

	// Academic reference: read-only canonical equations and conventions.
	[ExportGroup("Academic Reference (Read Only)")]
	[Export(PropertyHint.MultilineText)]
	public string AcademicReference
	{
		get => _academicReference;
		private set
		{
			if (_academicReference == value)
			{
				return;
			}

			_academicReference = value;
			if (Engine.IsEditorHint())
			{
				NotifyPropertyListChanged();
			}
		}
	}

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
	private string _academicReference = "r=|p-c| (or softened)\nu=clamp((r-rInner)/(rOuter-rInner),0,1)\nf(u)=by CurveType\na = dir * beta_eff * Amp * f(u) * edgeRamp(u)\nunits: scene units";
	private string _effectiveEquationCore = "core: u-clamped shell with profile f(u)";
	private string _effectiveEquationIntegrated = "integrated: a = dir * (beta_eff * amp * f(u))";
	private int _debugVizPlanes = (int)DebugVizPlaneFlags.All;
	private bool _inspectorStateInitialized;
	private FieldCurveType _inspectorCurveType = (FieldCurveType)(-1);
	private FieldShapeType _inspectorShapeType = (FieldShapeType)(-1);
	private bool _inspectorInnerRadiusEnabled;
	private bool _inspectorLegacyOverrideBeta;
	private bool _inspectorShowLegacyControls;
	private bool _inspectorShowDensityZones;
	private bool _inspectorShowDensityVectors;
	private bool _usedLegacyMigration;
	private bool _loggedLegacyMigration;
	private bool _warnedCanonicalLegacyConflict;
	private bool _warnedNoRadii;
	private static bool _warnedZeroCurvatureAllSources;
	private double _rebuildWindowStartSec;
	private int _rebuildsThisWindow;
	private bool _warnedFrequentRebuilds;

	/// <summary>
	/// Inspector-callable baseline preset for academic demos/tests.
	/// </summary>
	public void ResetToAcademicDefaults()
	{
		RInner = 0f;
		ROuter = 10f;
		Amp = 1.00f;
		CanonicalOverrideBetaScale = true;
		CanonicalBetaScale = 1.00f;
		CurveType = FieldCurveType.Power;
		CanonicalGamma = 1.00f;
		Softening = 0.05f;
		CanonicalEdgeSoftness = 0.10f;
		CanonicalEnableInnerRadius = true;

		ValidateAndClamp();
		ResolveEffectiveParams(out _);
		RefreshEquationPreviews();
		RefreshInspectorVisibility();
	}

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
		RefreshInspectorVisibility();
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
		RefreshInspectorVisibility();

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

	public override void _ValidateProperty(Godot.Collections.Dictionary property)
	{
		try
		{
			if (!property.ContainsKey("name") || !property.ContainsKey("usage"))
			{
				return;
			}

			string propertyName = property["name"].ToString() ?? string.Empty;
			bool visible = true;

			switch (propertyName)
			{
				case nameof(MetricModel):
					property["hint"] = (int)PropertyHint.Enum;
					property["hint_string"] = "GRIN:0,GordonMetric (Experimental):1";
					break;

				case nameof(RInner):
					visible = CanonicalEnableInnerRadius;
					break;

				case nameof(CurveType):
					property["hint"] = (int)PropertyHint.Enum;
					property["hint_string"] = "Linear:0,Power:1,Polynomial:2,Gaussian:3,CustomCurve:4";
					break;

				case nameof(CanonicalGamma):
					visible = CurveType == FieldCurveType.Power;
					break;

				case nameof(CurveA):
				case nameof(CurveB):
				case nameof(CurveC):
					visible = CurveType == FieldCurveType.Polynomial;
					break;

				case nameof(Sigma):
					visible = CurveType == FieldCurveType.Exponential;
					break;

				case nameof(CustomCurve):
					visible = CurveType == FieldCurveType.CustomCurve;
					break;

				case nameof(BoxExtents):
					visible = ShapeType == FieldShapeType.BoxVolume;
					break;

				case nameof(EffectiveEquationCore):
				case nameof(EffectiveEquationIntegrated):
					visible = false;
					break;

				case nameof(Softening):
					property["tooltip"] = "CoreSoftening: core stability softening term for near-center robustness.";
					break;

				case nameof(CanonicalEdgeSoftness):
					property["tooltip"] = "EdgeFeather: boundary fade width in normalized u-space.";
					break;

				case nameof(CanonicalBetaScale):
					property["tooltip"] = "Coupling (Beta): response gain scaling (canonical beta).";
					break;

				case nameof(Amp):
					property["tooltip"] = "Strength: source amplitude.";
					break;

				case nameof(ApplyAcademicDefaults):
					property["tooltip"] = "One-click preset: rInner=0, rOuter=10, strength=0.02, coupling=0.001, power gamma=1";
					break;

				case nameof(DebugVizShowDensityZones):
					property["tooltip"] = "Enable interpolated density zone rings.";
					break;

				case nameof(DebugVizDensityZoneCount):
				case nameof(DebugVizRingSegments):
				case nameof(DebugVizZeroInnerRadiusEpsilonFraction):
				case nameof(DebugVizDensityZoneColorMin):
				case nameof(DebugVizDensityZoneColorMax):
					visible = DebugVizShowDensityZones || DebugVizShowDensityVectors;
					if (propertyName == nameof(DebugVizDensityZoneColorMin))
					{
						property["tooltip"] = "Density zone color at minimum profile strength.";
					}
					else if (propertyName == nameof(DebugVizDensityZoneColorMax))
					{
						property["tooltip"] = "Density zone color at maximum profile strength.";
					}
					else if (propertyName == nameof(DebugVizZeroInnerRadiusEpsilonFraction))
					{
						property["tooltip"] = "When inner radius is zero, use this fraction of outer radius as a sampling offset.";
					}
					break;

				case nameof(DebugVizShowDensityVectors):
					property["tooltip"] = "Enable density vector glyph overlays.";
					break;

				case nameof(DebugVizDensityVectorLayers):
				case nameof(DebugVizDensityVectorCount):
				case nameof(DebugVizDensityVectorScale):
				case nameof(DebugVizDensityArrowMinLength):
				case nameof(DebugVizDensityArrowMaxLength):
				case nameof(DebugVizDensityArrowMinHeadSize):
				case nameof(DebugVizDensityArrowMaxHeadSize):
				case nameof(DebugVizDensityArrowMinThicknessBands):
				case nameof(DebugVizDensityArrowMaxThicknessBands):
				case nameof(DebugVizDensityArrowThicknessIntensity):
				case nameof(DebugVizDensityVectorTipAtOrigin):
				case nameof(DebugVizDensityVectorColorMin):
				case nameof(DebugVizDensityVectorColorMax):
					visible = DebugVizShowDensityVectors;
					if (propertyName == nameof(DebugVizDensityVectorTipAtOrigin))
					{
						property["tooltip"] = "When enabled, arrow tip is anchored at the sample point and tail extends backward.";
					}
					else if (propertyName == nameof(DebugVizDensityVectorColorMin))
					{
						property["tooltip"] = "Density vector color at minimum profile strength.";
					}
					else if (propertyName == nameof(DebugVizDensityVectorColorMax))
					{
						property["tooltip"] = "Density vector color at maximum profile strength.";
					}
					break;

				case nameof(ShowLegacyControls):
					property["tooltip"] = "Off by default to keep deprecated controls collapsed.";
					break;

				case nameof(Attract):
				case nameof(Strength):
				case nameof(MinRadius):
				case nameof(MaxRadius):
				case nameof(Profile):
				case nameof(OverrideGamma):
				case nameof(Gamma):
				case nameof(OverrideBetaScale):
				case nameof(BetaScale):
				case nameof(InnerRadius):
				case nameof(OuterRadius):
				case nameof(EdgeSoftness):
					visible = ShowLegacyControls;
					if (!IsCanonicalUnset())
					{
						property["tooltip"] = "Ignored: canonical fields are set.";
					}
					break;
			}

			SetPropertyVisibility(property, visible);
		}
		catch (Exception ex)
		{
			GD.PushWarning($"[FieldSource3D] _ValidateProperty failed: {ex.Message}");
		}
	}

	private static void SetPropertyVisibility(Godot.Collections.Dictionary property, bool visible)
	{
		long usageRaw = ReadUsageValue(property["usage"]);
		PropertyUsageFlags usage = (PropertyUsageFlags)usageRaw;

		if (visible)
		{
			usage &= ~PropertyUsageFlags.NoEditor;
		}
		else
		{
			usage |= PropertyUsageFlags.NoEditor;
		}

		property["usage"] = (long)usage;
	}

	private static long ReadUsageValue(Variant value)
	{
		return value.VariantType switch
		{
			Variant.Type.Int => (long)(int)value,
			Variant.Type.Float => (long)(float)value,
			_ => (long)PropertyUsageFlags.Default
		};
	}

	private void RefreshInspectorVisibility()
	{
		if (!Engine.IsEditorHint())
		{
			return;
		}

		bool changed = !_inspectorStateInitialized
			|| _inspectorCurveType != CurveType
			|| _inspectorShapeType != ShapeType
			|| _inspectorInnerRadiusEnabled != CanonicalEnableInnerRadius
			|| _inspectorLegacyOverrideBeta != CanonicalOverrideBetaScale
			|| _inspectorShowLegacyControls != ShowLegacyControls
			|| _inspectorShowDensityZones != DebugVizShowDensityZones
			|| _inspectorShowDensityVectors != DebugVizShowDensityVectors;

		if (!changed)
		{
			return;
		}

		_inspectorStateInitialized = true;
		_inspectorCurveType = CurveType;
		_inspectorShapeType = ShapeType;
		_inspectorInnerRadiusEnabled = CanonicalEnableInnerRadius;
		_inspectorLegacyOverrideBeta = CanonicalOverrideBetaScale;
		_inspectorShowLegacyControls = ShowLegacyControls;
		_inspectorShowDensityZones = DebugVizShowDensityZones;
		_inspectorShowDensityVectors = DebugVizShowDensityVectors;
		NotifyPropertyListChanged();
	}

	public bool IsCanonicalUnset()
	{
		float effectiveInner = GetEffectiveCanonicalInnerRadius();
		return Mathf.Abs(Amp) <= ResolveEps
			&& Mathf.Abs(CurveA) <= ResolveEps
			&& Mathf.Abs(CurveB) <= ResolveEps
			&& Mathf.Abs(CurveC) <= ResolveEps
			&& Mathf.Abs(effectiveInner) <= ResolveEps
			&& Mathf.Abs(ROuter) <= ResolveEps
			&& CustomCurve == null
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
		beta = FieldMath.ResolveBetaEff(globalBeta, resolved.overrideBetaScale, resolved.betaScale);
		reasonString = $"beta_eff ({source})";
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
		CanonicalGamma = migrated.curveType == FieldCurveType.Power ? migrated.a : CanonicalGamma;
		CurveA = migrated.a;
		CurveB = migrated.b;
		CurveC = migrated.c;
		Softening = migrated.softening;
		Sigma = migrated.sigma;
		CanonicalOverrideBetaScale = migrated.overrideBetaScale;
		CanonicalBetaScale = migrated.betaScale;
		CanonicalEdgeSoftness = migrated.edgeSoftness;
		CustomCurve = migrated.customCurve;
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
		float inner = Mathf.Max(0f, GetEffectiveCanonicalInnerRadius());
		float outer = Mathf.Max(0f, ROuter);
		if (outer > 0f && outer < inner)
		{
			outer = inner;
		}

		float profileA = 0f;
		float profileB = 0f;
		float profileC = 0f;
		float profileSigma = 0f;
		Curve profileCustomCurve = null;

		switch (CurveType)
		{
			case FieldCurveType.Power:
				profileA = CanonicalGamma;
				break;
			case FieldCurveType.Polynomial:
				profileA = CurveA;
				profileB = CurveB;
				profileC = CurveC;
				break;
			case FieldCurveType.Exponential:
				profileSigma = Mathf.Max(0f, Sigma);
				break;
			case FieldCurveType.CustomCurve:
				profileCustomCurve = CustomCurve;
				break;
		}

		return new ResolvedFieldParams
		{
			enabled = Enabled,
			modeFlags = ModeFlags,
			shapeType = ShapeType,
			curveType = CurveType,
			amp = Amp,
			a = profileA,
			b = profileB,
			c = profileC,
			rInner = inner,
			rOuter = outer,
			softening = Mathf.Max(0f, Softening),
			sigma = profileSigma,
			overrideBetaScale = CanonicalOverrideBetaScale,
			betaScale = CanonicalBetaScale,
			edgeSoftness = Mathf.Clamp(CanonicalEdgeSoftness, 0f, 1f),
			customCurve = profileCustomCurve
		};
	}

	private float GetEffectiveCanonicalInnerRadius()
	{
		return CanonicalEnableInnerRadius ? RInner : 0f;
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
			sigma = Mathf.Max(0f, Sigma),
			overrideBetaScale = OverrideBetaScale,
			betaScale = BetaScale,
			edgeSoftness = Mathf.Clamp(EdgeSoftness, 0f, 1f),
			customCurve = null
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
			|| Mathf.Abs(a.sigma - b.sigma) > 1e-4f
			|| a.overrideBetaScale != b.overrideBetaScale
			|| Mathf.Abs(a.betaScale - b.betaScale) > 1e-4f
			|| Mathf.Abs(a.edgeSoftness - b.edgeSoftness) > 1e-4f
			|| a.customCurve != b.customCurve;
	}

	private string BuildEffectiveSummary(ResolvedFieldParams resolved, string source)
	{
		string betaMode = resolved.overrideBetaScale
			? $"override(beta_scale={resolved.betaScale:0.###})"
			: "global";
		string curveExtras = resolved.curveType == FieldCurveType.CustomCurve
			? (resolved.customCurve != null ? "custom=resource" : "custom=missing")
			: "custom=n/a";
		return $"shape={resolved.shapeType} curve={resolved.curveType} amp={resolved.amp:0.###} a={resolved.a:0.###} b={resolved.b:0.###} c={resolved.c:0.###} r=[{resolved.rInner:0.###},{resolved.rOuter:0.###}] sigma={resolved.sigma:0.###} edge={resolved.edgeSoftness:0.###} beta={betaMode} {curveExtras} source={source}";
	}

	private void RefreshEquationPreviews()
	{
		ResolvedFieldParams resolved = ResolveEffectiveParams(out string source);
		string curveCore = BuildCoreCurveEquation(resolved);
		string curveReference = resolved.curveType switch
		{
			FieldCurveType.Linear => "Linear: f(u) = 1-u",
			FieldCurveType.Power => $"Power: f(u) = (1-u)^gamma (gamma={resolved.a:0.###})",
			FieldCurveType.Polynomial => $"Polynomial: f(u) = A + B*u + C*u^2 (A={resolved.a:0.###}, B={resolved.b:0.###}, C={resolved.c:0.###})",
			FieldCurveType.Exponential => $"Gaussian: f(u) = exp(-(u/sigma)^2) (sigma={Mathf.Max(ResolveEps, resolved.sigma):0.###})",
			FieldCurveType.CustomCurve => resolved.customCurve != null
				? "CustomCurve: f(u) = Curve.Sample(u)"
				: "CustomCurve: missing Curve resource (f(u)=0)",
			_ => "Linear: f(u) = 1-u"
		};
		string betaExpr = resolved.overrideBetaScale
			? $"(|beta_g|>eps ? beta_g*{resolved.betaScale:0.###} : {resolved.betaScale:0.###})"
			: "beta_g";
		string edgeExpr = resolved.edgeSoftness > ResolveEps
			? $"edge_ramp=smoothstep(0,{resolved.edgeSoftness:0.###},u) * (1-smoothstep({1f - resolved.edgeSoftness:0.###},1,u))"
			: "edge_ramp=1 (disabled)";
		AcademicReference =
			"Canonical baseline (read only)\n" +
			"r = |p-c| (or softened)\n" +
			"u = clamp((r-rInner)/(rOuter-rInner),0,1)\n" +
			$"{curveReference}\n" +
			"a = dir * beta_eff * Amp * f(u) * edgeRamp(u)\n" +
			"Amp = source strength, beta_eff/coupling = response gain\n" +
			"CoreSoftening = core stability, EdgeFeather = boundary fade\n" +
			"Units: scene units";
		EffectiveEquationCore =
			$"source={source}\n" +
			"r=|p-c|\n" +
			"u=clamp((r-rInner)/max(eps,rOuter-rInner),0,1)\n" +
			$"f(u)={curveCore}\n" +
			$"{edgeExpr}";

		bool invertSign = (resolved.modeFlags & ModeFlagInvertSign) != 0u;
		string dirExpr = invertSign ? "+normalize(p-c)" : "-normalize(p-c)";
		string paramNote = resolved.curveType switch
		{
			FieldCurveType.Power => $"gamma={resolved.a:0.###}",
			FieldCurveType.Polynomial => $"A={resolved.a:0.###}, B={resolved.b:0.###}, C={resolved.c:0.###}",
			FieldCurveType.Exponential => $"sigma={Mathf.Max(ResolveEps, resolved.sigma):0.###}",
			FieldCurveType.CustomCurve => resolved.customCurve != null ? "custom_curve=resource" : "custom_curve=missing",
			_ => "no extra curve params"
		};
		EffectiveEquationIntegrated =
			$"dir={dirExpr} (ModeFlagInvertSign={(invertSign ? 1 : 0)})\n" +
			$"beta_eff={betaExpr}\n" +
			$"mag=beta_eff*amp*f(u)*edge_ramp\n" +
			$"{paramNote}\n" +
			"a=dir*mag";
	}

	private string BuildCoreCurveEquation(ResolvedFieldParams resolved)
	{
		return resolved.curveType switch
		{
			FieldCurveType.Linear => "1-u",
			FieldCurveType.Power => $"(1-u)^{resolved.a:0.###}",
			FieldCurveType.Polynomial => $"{resolved.a:0.###}+({resolved.b:0.###}*u)+({resolved.c:0.###}*u^2)",
			FieldCurveType.Exponential => $"exp(-pow(u/max(eps,{Mathf.Max(ResolveEps, resolved.sigma):0.###}),2))",
			FieldCurveType.CustomCurve => resolved.customCurve != null
				? "Curve.SampleBaked(u)"
				: "0 (missing Curve resource)",
			_ => "1-u"
		};
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

		float effectiveInner = Mathf.Max(0f, GetEffectiveCanonicalInnerRadius());
		if (ROuter < effectiveInner)
		{
			ROuter = effectiveInner;
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
		DebugVizDensityVectorLayers = Mathf.Clamp(DebugVizDensityVectorLayers, 2, 16);
		DebugVizDensityVectorCount = Mathf.Clamp(DebugVizDensityVectorCount, 4, 96);
		DebugVizDensityVectorScale = Mathf.Max(0.05f, DebugVizDensityVectorScale);
		DebugVizDensityArrowMinLength = Mathf.Max(0.01f, DebugVizDensityArrowMinLength);
		DebugVizDensityArrowMaxLength = Mathf.Max(0.01f, DebugVizDensityArrowMaxLength);
		if (DebugVizDensityArrowMaxLength < DebugVizDensityArrowMinLength)
		{
			DebugVizDensityArrowMaxLength = DebugVizDensityArrowMinLength;
		}

		DebugVizDensityArrowMinHeadSize = Mathf.Max(0.005f, DebugVizDensityArrowMinHeadSize);
		DebugVizDensityArrowMaxHeadSize = Mathf.Max(0.005f, DebugVizDensityArrowMaxHeadSize);
		if (DebugVizDensityArrowMaxHeadSize < DebugVizDensityArrowMinHeadSize)
		{
			DebugVizDensityArrowMaxHeadSize = DebugVizDensityArrowMinHeadSize;
		}

		DebugVizDensityArrowMinThicknessBands = Mathf.Clamp(DebugVizDensityArrowMinThicknessBands, 1, 6);
		DebugVizDensityArrowMaxThicknessBands = Mathf.Clamp(DebugVizDensityArrowMaxThicknessBands, 1, 10);
		if (DebugVizDensityArrowMaxThicknessBands < DebugVizDensityArrowMinThicknessBands)
		{
			DebugVizDensityArrowMaxThicknessBands = DebugVizDensityArrowMinThicknessBands;
		}

		DebugVizDensityArrowThicknessIntensity = Mathf.Clamp(DebugVizDensityArrowThicknessIntensity, 0.2f, 3.0f);
		DebugVizDensityZoneCount = Mathf.Clamp(DebugVizDensityZoneCount, 2, 6);
		DebugVizZeroInnerRadiusEpsilonFraction = Mathf.Clamp(DebugVizZeroInnerRadiusEpsilonFraction, 0f, 0.25f);
		DebugVizGlobalOpacity = Mathf.Clamp(DebugVizGlobalOpacity, 0f, 1f);
		_debugVizPlanes &= (int)DebugVizPlaneFlags.All;
		Softening = Mathf.Max(0f, Softening);
		Sigma = Mathf.Max(0f, Sigma);
		CanonicalEdgeSoftness = Mathf.Clamp(CanonicalEdgeSoftness, 0f, 1f);
		if (!float.IsFinite(CanonicalBetaScale))
		{
			CanonicalBetaScale = 0f;
			warned = true;
		}
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
			DensitySampleInnerRadius = ComputeDensitySampleInnerRadius(Mathf.Max(0f, inner), Mathf.Max(0f, outer)),
			ShowInnerOuter = DebugVizShowInnerOuter,
			ShowSigma = DebugVizShowSigma && FieldMath.IsSigmaMeaningful(resolved.curveType) && resolved.sigma > 0f,
			SigmaRadius = Mathf.Max(0f, resolved.sigma),
			ShowDensityVectors = DebugVizShowDensityVectors && hasInnerOuter,
			DensityVectorLayers = DebugVizDensityVectorLayers,
			DensityVectorCount = DebugVizDensityVectorCount,
			DensityVectorScale = DebugVizDensityVectorScale,
			DensityArrowMinLength = DebugVizDensityArrowMinLength,
			DensityArrowMaxLength = DebugVizDensityArrowMaxLength,
			DensityArrowMinHeadSize = DebugVizDensityArrowMinHeadSize,
			DensityArrowMaxHeadSize = DebugVizDensityArrowMaxHeadSize,
			DensityArrowMinThicknessBands = DebugVizDensityArrowMinThicknessBands,
			DensityArrowMaxThicknessBands = DebugVizDensityArrowMaxThicknessBands,
			DensityArrowThicknessIntensity = DebugVizDensityArrowThicknessIntensity,
			DensityVectorTipAtOrigin = DebugVizDensityVectorTipAtOrigin,
			ShowDensityZones = DebugVizShowDensityZones && hasInnerOuter,
			DensityZoneCount = DebugVizDensityZoneCount,
			DensityZoneColorMin = DebugVizDensityZoneColorMin,
			DensityZoneColorMax = DebugVizDensityZoneColorMax,
			ModeFlags = resolved.modeFlags,
			CurveType = resolved.curveType,
			CurveA = resolved.a,
			CurveB = resolved.b,
			CurveC = resolved.c,
			Sigma = resolved.sigma,
			EdgeSoftness = resolved.edgeSoftness,
			CustomCurve = resolved.customCurve,
			Planes = DebugVizPlaneMask,
			OpacityMode = DebugVizOpacityMode,
			Segments = Mathf.Max(8, DebugVizRingSegments),
			LineWidth = Mathf.Max(0.25f, DebugVizLineWidth),
			GlobalOpacity = Mathf.Clamp(DebugVizGlobalOpacity, 0f, 1f),
			AlwaysOnTop = DebugVizAlwaysOnTop,
			InnerColor = DebugVizColorInner,
			OuterColor = DebugVizColorOuter,
			SigmaColor = DebugVizColorSigma,
			DensityVectorColorMin = DebugVizDensityVectorColorMin,
			DensityVectorColorMax = DebugVizDensityVectorColorMax,
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
			float radius = Mathf.Lerp(state.DensitySampleInnerRadius, state.OuterRadius, t);
			float u = ComputeDensityUFromRadius(state, radius);
			float strength = EvaluateDensityStrengthAtT(u, state);
			Color zoneColor = InterpolateDensityColor(strength, state.DensityZoneColorMin, state.DensityZoneColorMax);
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
		DensityRingSample[] rings = BuildDensityVectorRingSamples(state);
		if (rings.Length == 0)
		{
			return;
		}

		bool invert = (state.ModeFlags & ModeFlagInvertSign) != 0u;
		for (int r = 0; r < rings.Length; r++)
		{
			DensityRingSample ring = rings[r];
			float sizeT = Mathf.Clamp(ring.Strength, 0f, 1f);
			Color vectorColor = InterpolateDensityColor(sizeT, state.DensityVectorColorMin, state.DensityVectorColorMax);

			AddLineSurface(GetLineColorForMode(vectorColor, state), state, () =>
			{
				float thicknessT = Mathf.Clamp(Mathf.Pow(sizeT, state.DensityArrowThicknessIntensity), 0f, 1f);
				float length = Mathf.Lerp(state.DensityArrowMinLength, state.DensityArrowMaxLength, sizeT) * state.DensityVectorScale;
				float head = Mathf.Lerp(state.DensityArrowMinHeadSize, state.DensityArrowMaxHeadSize, sizeT);
				int strokeBands = Mathf.Clamp(
					Mathf.RoundToInt(Mathf.Lerp(state.DensityArrowMinThicknessBands, state.DensityArrowMaxThicknessBands, thicknessT)),
					1,
					10);
				float strokeOffset = Mathf.Max(
					0.0015f,
					0.0025f * state.LineWidth * Mathf.Lerp(0.4f, 2.0f, thicknessT));

				if ((state.Planes & DebugVizPlaneFlags.XY) != 0)
				{
					AddVectorRingSet(ring.Radius, count, Vector3.Right, Vector3.Up, Vector3.Forward, invert, length, head, strokeBands, strokeOffset, state.DensityVectorTipAtOrigin);
				}
				if ((state.Planes & DebugVizPlaneFlags.XZ) != 0)
				{
					AddVectorRingSet(ring.Radius, count, Vector3.Right, Vector3.Forward, Vector3.Up, invert, length, head, strokeBands, strokeOffset, state.DensityVectorTipAtOrigin);
				}
				if ((state.Planes & DebugVizPlaneFlags.YZ) != 0)
				{
					AddVectorRingSet(ring.Radius, count, Vector3.Up, Vector3.Forward, Vector3.Right, invert, length, head, strokeBands, strokeOffset, state.DensityVectorTipAtOrigin);
				}
			});
		}
	}

	private DensityRingSample[] BuildDensityVectorRingSamples(DebugVizState state)
	{
		float inner = Mathf.Max(0f, state.DensitySampleInnerRadius);
		float outer = Mathf.Max(inner, state.OuterRadius);
		if (outer <= ResolveEps)
		{
			return Array.Empty<DensityRingSample>();
		}

		int layers = Mathf.Clamp(state.DensityVectorLayers, 2, 16);
		if (Mathf.IsEqualApprox(inner, outer))
		{
			float u = ComputeDensityUFromRadius(state, outer);
			float clampedStrength = Mathf.Max(0.15f, EvaluateDensityStrengthAtT(u, state));
			return new DensityRingSample[] { new DensityRingSample(outer, clampedStrength) };
		}

		var samples = new List<DensityRingSample>(layers);
		for (int i = 0; i < layers; i++)
		{
			float t = layers == 1 ? 1f : (float)i / (layers - 1);
			float radius = Mathf.Lerp(inner, outer, t);
			float u = ComputeDensityUFromRadius(state, radius);
			float strength = Mathf.Max(0.15f, EvaluateDensityStrengthAtT(u, state));
			samples.Add(new DensityRingSample(radius, strength));
		}

		return samples.ToArray();
	}

	private void AddVectorRingSet(
		float radius,
		int count,
		Vector3 axisA,
		Vector3 axisB,
		Vector3 normal,
		bool invert,
		float length,
		float head,
		int strokeBands,
		float strokeOffset,
		bool tipAtOrigin)
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

			Vector3 origin = radial * radius + normal * 0.005f;
			Vector3 start = tipAtOrigin ? origin - (dir * length) : origin;
			Vector3 tip = tipAtOrigin ? origin : start + (dir * length);
			Vector3 wingA = tip - (dir * head) + (tangent * (head * 0.5f));
			Vector3 wingB = tip - (dir * head) - (tangent * (head * 0.5f));

			AddWeightedVectorGlyph(start, tip, wingA, wingB, tangent, normal, strokeBands, strokeOffset);
		}
	}

	private void AddWeightedVectorGlyph(
		Vector3 start,
		Vector3 tip,
		Vector3 wingA,
		Vector3 wingB,
		Vector3 tangent,
		Vector3 normal,
		int strokeBands,
		float strokeOffset)
	{
		AddVectorGlyph(start, tip, wingA, wingB);

		int safeBands = Mathf.Clamp(strokeBands, 1, 10);
		for (int band = 1; band < safeBands; band++)
		{
			float offsetMag = strokeOffset * band;
			Vector3 tangentOffset = tangent * offsetMag;
			Vector3 normalOffset = normal * (offsetMag * 0.7f);

			AddVectorGlyph(start + tangentOffset, tip + tangentOffset, wingA + tangentOffset, wingB + tangentOffset);
			AddVectorGlyph(start - tangentOffset, tip - tangentOffset, wingA - tangentOffset, wingB - tangentOffset);

			if (band >= 2)
			{
				AddVectorGlyph(start + normalOffset, tip + normalOffset, wingA + normalOffset, wingB + normalOffset);
				AddVectorGlyph(start - normalOffset, tip - normalOffset, wingA - normalOffset, wingB - normalOffset);
			}
		}
	}

	private void AddVectorGlyph(Vector3 start, Vector3 tip, Vector3 wingA, Vector3 wingB)
	{
		AddLine(start, tip);
		AddLine(tip, wingA);
		AddLine(tip, wingB);
	}

	private float EvaluateDensityStrengthAtT(float t, DebugVizState state)
	{
		float u = Mathf.Clamp(t, 0f, 1f);
		float profile = FieldMath.EvaluateProfileAtU(
			state.CurveType,
			u,
			state.CurveA,
			state.Sigma,
			state.CurveA,
			state.CurveB,
			state.CurveC,
			state.CustomCurve);
		float edgeRamp = FieldMath.EvaluateEdgeRamp(u, state.EdgeSoftness);
		return Mathf.Clamp(profile * edgeRamp, 0f, 1f);
	}

	private float ComputeDensitySampleInnerRadius(float inner, float outer)
	{
		float safeInner = Mathf.Max(0f, inner);
		float safeOuter = Mathf.Max(safeInner, outer);
		if (safeOuter <= ResolveEps || safeInner > ResolveEps)
		{
			return safeInner;
		}

		float frac = Mathf.Clamp(DebugVizZeroInnerRadiusEpsilonFraction, 0f, 0.25f);
		float offset = safeOuter * frac;
		return Mathf.Clamp(Mathf.Max(ResolveEps, offset), 0f, safeOuter);
	}

	private static float ComputeDensityUFromRadius(DebugVizState state, float radius)
	{
		float inner = Mathf.Max(0f, state.InnerRadius);
		float outer = Mathf.Max(inner, state.OuterRadius);
		float span = Mathf.Max(ResolveEps, outer - inner);
		return Mathf.Clamp((radius - inner) / span, 0f, 1f);
	}

	private static Color InterpolateDensityColor(float strength, Color minColor, Color maxColor)
	{
		float s = Mathf.Clamp(strength, 0f, 1f);
		return minColor.Lerp(maxColor, s);
	}

	private readonly struct DensityRingSample
	{
		public readonly float Radius;
		public readonly float Strength;

		public DensityRingSample(float radius, float strength)
		{
			Radius = radius;
			Strength = strength;
		}
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
		alpha = Mathf.Clamp(alpha * state.GlobalOpacity, 0f, 1f);
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
		alpha = Mathf.Clamp(alpha * state.GlobalOpacity, 0f, 1f);
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
		public float DensitySampleInnerRadius;
		public bool ShowInnerOuter;
		public bool ShowSigma;
		public float SigmaRadius;
		public bool ShowDensityVectors;
		public int DensityVectorLayers;
		public int DensityVectorCount;
		public float DensityVectorScale;
		public float DensityArrowMinLength;
		public float DensityArrowMaxLength;
		public float DensityArrowMinHeadSize;
		public float DensityArrowMaxHeadSize;
		public int DensityArrowMinThicknessBands;
		public int DensityArrowMaxThicknessBands;
		public float DensityArrowThicknessIntensity;
		public bool DensityVectorTipAtOrigin;
		public bool ShowDensityZones;
		public int DensityZoneCount;
		public Color DensityZoneColorMin;
		public Color DensityZoneColorMax;
		public uint ModeFlags;
		public FieldCurveType CurveType;
		public float CurveA;
		public float CurveB;
		public float CurveC;
		public float Sigma;
		public float EdgeSoftness;
		public Curve CustomCurve;
		public DebugVizPlaneFlags Planes;
		public DebugVizOpacityModeKind OpacityMode;
		public int Segments;
		public float LineWidth;
		public float GlobalOpacity;
		public bool AlwaysOnTop;
		public Color InnerColor;
		public Color OuterColor;
		public Color SigmaColor;
		public Color DensityVectorColorMin;
		public Color DensityVectorColorMax;
		public Transform3D Transform;

		public bool Equals(DebugVizState other)
		{
			return Enabled == other.Enabled
				&& HasInnerOuter == other.HasInnerOuter
				&& Mathf.IsEqualApprox(InnerRadius, other.InnerRadius)
				&& Mathf.IsEqualApprox(OuterRadius, other.OuterRadius)
				&& Mathf.IsEqualApprox(DensitySampleInnerRadius, other.DensitySampleInnerRadius)
				&& ShowInnerOuter == other.ShowInnerOuter
				&& ShowSigma == other.ShowSigma
				&& Mathf.IsEqualApprox(SigmaRadius, other.SigmaRadius)
				&& ShowDensityVectors == other.ShowDensityVectors
				&& DensityVectorLayers == other.DensityVectorLayers
				&& DensityVectorCount == other.DensityVectorCount
				&& Mathf.IsEqualApprox(DensityVectorScale, other.DensityVectorScale)
				&& Mathf.IsEqualApprox(DensityArrowMinLength, other.DensityArrowMinLength)
				&& Mathf.IsEqualApprox(DensityArrowMaxLength, other.DensityArrowMaxLength)
				&& Mathf.IsEqualApprox(DensityArrowMinHeadSize, other.DensityArrowMinHeadSize)
				&& Mathf.IsEqualApprox(DensityArrowMaxHeadSize, other.DensityArrowMaxHeadSize)
				&& DensityArrowMinThicknessBands == other.DensityArrowMinThicknessBands
				&& DensityArrowMaxThicknessBands == other.DensityArrowMaxThicknessBands
				&& Mathf.IsEqualApprox(DensityArrowThicknessIntensity, other.DensityArrowThicknessIntensity)
				&& DensityVectorTipAtOrigin == other.DensityVectorTipAtOrigin
				&& ShowDensityZones == other.ShowDensityZones
				&& DensityZoneCount == other.DensityZoneCount
				&& ColorsEqual(DensityZoneColorMin, other.DensityZoneColorMin)
				&& ColorsEqual(DensityZoneColorMax, other.DensityZoneColorMax)
				&& ModeFlags == other.ModeFlags
				&& CurveType == other.CurveType
				&& Mathf.IsEqualApprox(CurveA, other.CurveA)
				&& Mathf.IsEqualApprox(CurveB, other.CurveB)
				&& Mathf.IsEqualApprox(CurveC, other.CurveC)
				&& Mathf.IsEqualApprox(Sigma, other.Sigma)
				&& Mathf.IsEqualApprox(EdgeSoftness, other.EdgeSoftness)
				&& CustomCurve == other.CustomCurve
				&& Planes == other.Planes
				&& OpacityMode == other.OpacityMode
				&& Segments == other.Segments
				&& Mathf.IsEqualApprox(LineWidth, other.LineWidth)
				&& Mathf.IsEqualApprox(GlobalOpacity, other.GlobalOpacity)
				&& AlwaysOnTop == other.AlwaysOnTop
				&& ColorsEqual(InnerColor, other.InnerColor)
				&& ColorsEqual(OuterColor, other.OuterColor)
				&& ColorsEqual(SigmaColor, other.SigmaColor)
				&& ColorsEqual(DensityVectorColorMin, other.DensityVectorColorMin)
				&& ColorsEqual(DensityVectorColorMax, other.DensityVectorColorMax)
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
