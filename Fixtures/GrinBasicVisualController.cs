using Godot;

public partial class GrinBasicVisualController : Node3D
{
	[Export] public NodePath FilmCameraPath = new("GrinFilmCamera");
	[Export] public NodePath FieldPath = new("FixtureGrinBasicVisual/FieldSource3D");
	[Export] public string FixtureHudName = "grin_basic_visual";
	[Export] public string SourcePatternMode = "dot_grid";

	public override void _Ready()
	{
		GrinFilmCamera filmCamera = GetNodeOrNull<GrinFilmCamera>(FilmCameraPath);
		FieldSource3D field = GetNodeOrNull<FieldSource3D>(FieldPath);

		if (filmCamera != null)
		{
			filmCamera.SetHudFixtureName(FixtureHudName);
			filmCamera.SetHudSourcePatternMode(SourcePatternMode);
			if (field != null)
			{
				filmCamera.SetHudTransportModel(field.TransportModel.ToString());
			}
		}

		if (field == null)
		{
			GD.PushWarning($"[GrinBasicVisual] missing field path={FieldPath}");
			return;
		}

		FieldSource3D.ResolvedFieldParams resolved = field.ResolveEffectiveParams(out string resolveReason);
		string betaMode = resolved.overrideBetaScale ? "override" : "global";
		GD.Print(
			$"[GrinBasicVisual] fixture={FixtureHudName} enabled={(field.Enabled ? 1 : 0)} resolve={resolveReason} " +
			$"transport={field.TransportModel} curve={resolved.curveType} rInner={resolved.rInner:0.###} " +
			$"rOuter={resolved.rOuter:0.###} amp={resolved.amp:0.###} gamma={resolved.a:0.###} " +
			$"betaMode={betaMode} betaScale={resolved.betaScale:0.###}");
	}
}
