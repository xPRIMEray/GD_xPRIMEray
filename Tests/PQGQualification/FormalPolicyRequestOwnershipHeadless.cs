#nullable enable

using System;
using System.Reflection;
using Godot;

/// <summary>
/// Isolated regression fixture for request-owned Formal policy provenance.
/// It temporarily replaces the two catalog records with synthetic definitions
/// whose FormalPolicyId values differ, then drives the production controller
/// request and seal path. The catalog is restored before this process exits.
/// </summary>
public partial class FormalPolicyRequestOwnershipHeadless : Node
{
	private const int SettleFrames = 8;
	private const int TimeoutFrames = 6000;
	private const string AlphaPolicy = "synthetic.formal.alpha.v1";
	private const string BetaPolicy = "synthetic.formal.beta.v1";

	private enum Stage
	{
		Settling,
		WaitingForAlpha,
		WaitingForBeta,
		Finished
	}

	private Stage _stage = Stage.Settling;
	private int _frames;
	private Node? _workbench;
	private GrinFilmCamera? _film;
	private Node? _controller;
	private ProbeProfileDefinition[]? _catalog;
	private ProbeProfileDefinition[]? _originalProfiles;
	private string _alphaPolicySha256 = string.Empty;

	public override void _Ready()
	{
		PackedScene? scene = GD.Load<PackedScene>("res://ObservatoryWorkbench.tscn");
		if (scene == null)
		{
			Finish(false, "Workbench scene unavailable");
			return;
		}

		_workbench = scene.Instantiate();
		AddChild(_workbench);
	}

	public override void _Process(double delta)
	{
		if (_stage == Stage.Finished)
			return;

		try
		{
			_frames++;
			switch (_stage)
			{
				case Stage.Settling when _frames >= SettleFrames:
					BeginAlphaRequest();
					break;
				case Stage.WaitingForAlpha:
					PollAlphaCompletion();
					break;
				case Stage.WaitingForBeta:
					PollBetaCompletion();
					break;
			}
		}
		catch (Exception exception)
		{
			Finish(false, $"{exception.GetType().Name}: {exception.Message}");
		}
	}

	private void BeginAlphaRequest()
	{
		if (_workbench == null)
			throw new InvalidOperationException("Workbench instance unavailable");

		Node chamber = _workbench.GetNode("PlayableWorld/TransportChamberWorld");
		_film = chamber.GetNode<GrinFilmCamera>("GrinFilmCamera");
		_controller = chamber.GetNode("FilmController");

		FieldInfo? profilesField = typeof(ProbeProfileCatalog).GetField(
			"Profiles",
			BindingFlags.Static | BindingFlags.NonPublic);
		_catalog = profilesField?.GetValue(null) as ProbeProfileDefinition[];
		if (_catalog == null || _catalog.Length != 2)
			throw new InvalidOperationException("Probe profile catalog shape changed");

		_originalProfiles = (ProbeProfileDefinition[])_catalog.Clone();
		_catalog[0] = new ProbeProfileDefinition(
			ProbeProfileId.Cathedral,
			"synthetic-alpha",
			"Synthetic Alpha",
			"accurate",
			AlphaPolicy);
		_catalog[1] = new ProbeProfileDefinition(
			ProbeProfileId.Motion,
			"synthetic-beta",
			"Synthetic Beta",
			"cheap-motion",
			BetaPolicy);

		_film.SetProbeProfileByIndex(0);
		if (!_controller.Call("EnterFormalSnapshotFromInput").AsBool())
			throw new InvalidOperationException("Alpha Formal request rejected");
		if (_film.CathedralRequestedProbeProfileStableId != "synthetic-alpha")
			throw new InvalidOperationException("Alpha request did not freeze selected profile metadata");

		string requestedPolicy = ReadRequestedPolicy();
		if (!string.Equals(requestedPolicy, AlphaPolicy, StringComparison.Ordinal))
			throw new InvalidOperationException($"Alpha request policy mismatch: {requestedPolicy}");

		// This is the production P mutation seam. Selection changes immediately,
		// while the active request must continue to own Alpha provenance.
		_film.SetProbeProfileByIndex(1);
		if (_film.ProbeProfileStableId != "synthetic-beta")
			throw new InvalidOperationException("Synthetic Beta was not selected during Alpha acquisition");
		if (!string.Equals(ReadRequestedPolicy(), AlphaPolicy, StringComparison.Ordinal))
			throw new InvalidOperationException("P relabeled the active Alpha request");

		_frames = 0;
		_stage = Stage.WaitingForAlpha;
	}

	private void PollAlphaCompletion()
	{
		if (_film == null || _controller == null)
			throw new InvalidOperationException("Alpha fixture ownership unavailable");
		if (!_film.ProbeViewAvailable)
		{
			if (_frames >= TimeoutFrames)
				throw new TimeoutException("Alpha acquisition did not seal");
			return;
		}

		if (_film.CathedralSealedProbeProfileStableId != "synthetic-alpha")
			throw new InvalidOperationException("Alpha sealed profile was relabeled by later P selection");
		if (_film.CathedralFormalPolicyId != AlphaPolicy)
			throw new InvalidOperationException("Alpha sealed Formal policy is not request-owned");
		_alphaPolicySha256 = _film.CathedralFormalPolicySha256;
		if (string.IsNullOrWhiteSpace(_alphaPolicySha256))
			throw new InvalidOperationException("Alpha sealed Formal policy hash unavailable");

		if (!_controller.Call("HandleProbeViewInput", false, true).AsBool())
			throw new InvalidOperationException("Beta replacement request rejected");
		if (_film.CathedralRequestedProbeProfileStableId != "synthetic-beta")
			throw new InvalidOperationException("Beta request did not freeze selected profile metadata");
		if (!string.Equals(ReadRequestedPolicy(), BetaPolicy, StringComparison.Ordinal))
			throw new InvalidOperationException("Beta request policy mismatch");

		_frames = 0;
		_stage = Stage.WaitingForBeta;
	}

	private void PollBetaCompletion()
	{
		if (_film == null)
			throw new InvalidOperationException("Beta fixture ownership unavailable");
		if (!_film.ProbeViewAvailable)
		{
			if (_frames >= TimeoutFrames)
				throw new TimeoutException("Beta acquisition did not seal");
			return;
		}

		if (_film.CathedralSealedProbeProfileStableId != "synthetic-beta")
			throw new InvalidOperationException("Beta sealed profile mismatch");
		if (_film.CathedralFormalPolicyId != BetaPolicy)
			throw new InvalidOperationException("Beta sealed Formal policy mismatch");
		if (string.Equals(_alphaPolicySha256, _film.CathedralFormalPolicySha256, StringComparison.Ordinal))
			throw new InvalidOperationException("Distinct Formal policy IDs produced interchangeable policy hashes");

		Finish(
			true,
			$"alpha_policy={AlphaPolicy} alpha_sha={_alphaPolicySha256} " +
			$"beta_policy={BetaPolicy} beta_sha={_film.CathedralFormalPolicySha256}");
	}

	private string ReadRequestedPolicy()
	{
		if (_film == null)
			return string.Empty;
		FieldInfo? field = typeof(GrinFilmCamera).GetField(
			"_requestedSnapshotFormalPolicyId",
			BindingFlags.Instance | BindingFlags.NonPublic);
		return field?.GetValue(_film) as string ?? string.Empty;
	}

	private void Finish(bool pass, string message)
	{
		if (_catalog != null && _originalProfiles != null)
			Array.Copy(_originalProfiles, _catalog, _catalog.Length);
		_stage = Stage.Finished;
		GD.Print($"PQG FORMAL POLICY OWNERSHIP {(pass ? "PASS" : "FAIL")} {message}");
		GetTree().Quit(pass ? 0 : 1);
	}
}

#nullable restore
