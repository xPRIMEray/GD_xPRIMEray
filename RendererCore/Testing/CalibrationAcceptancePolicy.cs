using System;

public enum CalibrationDecision
{
	Accept = 0,
	Reject = 1,
	Defer = 2
}

public struct CalibrationDecisionRecord
{
	public CalibrationDecision decision;
	public string reason;
	public double? overhead_pct_est;
	public bool? shadow_trust;
	public string verdict;
	public ulong canonical_signature_hash64;
	public ulong preset_hash64;
}

public struct CalibrationAcceptanceThresholds
{
	public double max_overhead_pct;
	public bool require_shadow_trust;
	public bool require_verdict_pass;

	public static CalibrationAcceptanceThresholds Default =>
		new CalibrationAcceptanceThresholds
		{
			max_overhead_pct = 3.0,
			require_shadow_trust = true,
			require_verdict_pass = true
		};
}

public struct CalibrationShadowEvalInput
{
	public bool overhead_pct_est_known;
	public double overhead_pct_est;
	public bool shadow_trust_known;
	public bool shadow_trust;
	public string verdict;
	public ulong canonical_signature_hash64;
	public ulong preset_hash64;
}

public struct CalibrationShadowEvalMatrixInput
{
	public int shadow_pair_count;
	public bool any_pair_reject;
	public bool any_pair_defer;
	public bool max_overhead_pct_est_known;
	public double max_overhead_pct_est;
	public ulong canonical_signature_hash64;
	public ulong preset_hash64;
}

public static class CalibrationAcceptancePolicy
{
	public static CalibrationDecisionRecord DecideFromShadowEval(in CalibrationShadowEvalInput shadowEval)
	{
		CalibrationAcceptanceThresholds thresholds = CalibrationAcceptanceThresholds.Default;
		return DecideFromShadowEval(in shadowEval, in thresholds);
	}

	public static CalibrationDecisionRecord DecideFromShadowEval(in CalibrationShadowEvalInput shadowEval, in CalibrationAcceptanceThresholds thresholds)
	{
		CalibrationDecisionRecord record = new CalibrationDecisionRecord
		{
			decision = CalibrationDecision.Defer,
			reason = "insufficient_data",
			overhead_pct_est = (shadowEval.overhead_pct_est_known && double.IsFinite(shadowEval.overhead_pct_est))
				? shadowEval.overhead_pct_est
				: (double?)null,
			shadow_trust = shadowEval.shadow_trust_known ? shadowEval.shadow_trust : (bool?)null,
			verdict = string.IsNullOrWhiteSpace(shadowEval.verdict) ? "na" : shadowEval.verdict,
			canonical_signature_hash64 = shadowEval.canonical_signature_hash64,
			preset_hash64 = shadowEval.preset_hash64
		};

		string verdict = string.IsNullOrWhiteSpace(shadowEval.verdict) ? string.Empty : shadowEval.verdict.Trim();
		bool verdictPass = string.Equals(verdict, "pass", StringComparison.OrdinalIgnoreCase);
		bool verdictFail = string.Equals(verdict, "fail", StringComparison.OrdinalIgnoreCase);
		bool verdictDefer = string.Equals(verdict, "defer", StringComparison.OrdinalIgnoreCase);
		bool overheadKnown = shadowEval.overhead_pct_est_known && double.IsFinite(shadowEval.overhead_pct_est);

		if (thresholds.require_verdict_pass)
		{
			if (verdictPass)
			{
				// continue
			}
			else if (verdictFail)
			{
				record.decision = CalibrationDecision.Reject;
				record.reason = "verdict_fail";
				return record;
			}
			else if (verdictDefer || string.IsNullOrEmpty(verdict))
			{
				record.decision = CalibrationDecision.Defer;
				record.reason = "verdict_defer";
				return record;
			}
			else
			{
				record.decision = CalibrationDecision.Defer;
				record.reason = "verdict_unknown";
				return record;
			}
		}

		if (thresholds.require_shadow_trust)
		{
			if (!shadowEval.shadow_trust_known)
			{
				record.decision = CalibrationDecision.Defer;
				record.reason = "shadow_trust_unknown";
				return record;
			}

			if (!shadowEval.shadow_trust)
			{
				record.decision = CalibrationDecision.Reject;
				record.reason = "shadow_trust_fail";
				return record;
			}
		}

		if (!overheadKnown)
		{
			record.decision = CalibrationDecision.Defer;
			record.reason = "overhead_unknown";
			return record;
		}

		if (shadowEval.overhead_pct_est > thresholds.max_overhead_pct)
		{
			record.decision = CalibrationDecision.Reject;
			record.reason = "overhead_exceeds_max";
			return record;
		}

		record.decision = CalibrationDecision.Accept;
		record.reason = "accept";
		return record;
	}

	public static CalibrationDecisionRecord DecideFromShadowEvalMatrix(in CalibrationShadowEvalMatrixInput matrixEval)
	{
		CalibrationAcceptanceThresholds thresholds = CalibrationAcceptanceThresholds.Default;
		return DecideFromShadowEvalMatrix(in matrixEval, in thresholds);
	}

	public static CalibrationDecisionRecord DecideFromShadowEvalMatrix(in CalibrationShadowEvalMatrixInput matrixEval, in CalibrationAcceptanceThresholds thresholds)
	{
		CalibrationDecisionRecord record = new CalibrationDecisionRecord
		{
			decision = CalibrationDecision.Defer,
			reason = "matrix_insufficient_data",
			overhead_pct_est = (matrixEval.max_overhead_pct_est_known && double.IsFinite(matrixEval.max_overhead_pct_est))
				? matrixEval.max_overhead_pct_est
				: (double?)null,
			shadow_trust = null,
			verdict = "matrix",
			canonical_signature_hash64 = matrixEval.canonical_signature_hash64,
			preset_hash64 = matrixEval.preset_hash64
		};

		if (matrixEval.shadow_pair_count <= 0)
		{
			record.reason = "matrix_no_pairs";
			return record;
		}

		if (matrixEval.any_pair_reject)
		{
			record.decision = CalibrationDecision.Reject;
			record.reason = "matrix_pair_reject";
			return record;
		}

		if (matrixEval.any_pair_defer)
		{
			record.decision = CalibrationDecision.Defer;
			record.reason = "matrix_pair_defer";
			return record;
		}

		if (!matrixEval.max_overhead_pct_est_known || !double.IsFinite(matrixEval.max_overhead_pct_est))
		{
			record.reason = "matrix_overhead_unknown";
			return record;
		}

		if (matrixEval.max_overhead_pct_est > thresholds.max_overhead_pct)
		{
			record.decision = CalibrationDecision.Reject;
			record.reason = "matrix_overhead_exceeds_max";
			return record;
		}

		record.decision = CalibrationDecision.Accept;
		record.reason = "accept_matrix";
		return record;
	}
}
