import json
import os
import re
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_GODOT_EXE = (
    r"C:\Users\wmbro\Downloads\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64"
    r"\Godot_v4.5.1-stable_mono_win64_console.exe"
)
LOG_DIR = ROOT / "logs" / "blackhole_core_sweep"

CASES = [
    {"id": "baseline", "parameter": "baseline", "value": None, "args": []},
    {"id": "rinner_0p06", "parameter": "RInner", "value": 0.06, "args": ["--blackhole-mass-r-inner=0.06"]},
    {"id": "rinner_0p24", "parameter": "RInner", "value": 0.24, "args": ["--blackhole-mass-r-inner=0.24"]},
    {"id": "router_3p5", "parameter": "ROuter", "value": 3.5, "args": ["--blackhole-mass-r-outer=3.5"]},
    {"id": "router_6p0", "parameter": "ROuter", "value": 6.0, "args": ["--blackhole-mass-r-outer=6.0"]},
    {"id": "amp_0p075", "parameter": "Amp", "value": 0.075, "args": ["--blackhole-mass-amp=0.075"]},
    {"id": "amp_0p30", "parameter": "Amp", "value": 0.30, "args": ["--blackhole-mass-amp=0.30"]},
]

TRANSPORTS = [
    {"id": "grin", "arg": "grin"},
    {"id": "metric", "arg": "metric"},
]

RESOLVED_MASS_RE = re.compile(
    r"\[BlackHoleFixture\]\[ResolvedMass\].*?rInner=(?P<rinner>-?\d+(?:\.\d+)?) "
    r"rOuter=(?P<router>-?\d+(?:\.\d+)?) amp=(?P<amp>-?\d+(?:\.\d+)?) gamma=(?P<gamma>-?\d+(?:\.\d+)?)"
)
COMPARE_RE = re.compile(r"\[BlackHoleFixture\]\[ComparisonSummary\]\s+(?P<body>.*)")
STEERING_RE = re.compile(r"(\[TransportSteering\].*)")
FINGERPRINT_RE = re.compile(r"\[FixtureCompare\].*?fingerprint=(?P<fingerprint>[0-9a-fA-F]+)")
INVALID_RE = re.compile(r"\[FixtureInvalid\]|\[BlackHoleFixture\]\[ERROR\]|curvature not engaged", re.IGNORECASE)
KV_RE = re.compile(r"([A-Za-z][A-Za-z0-9_]*)=([^\s]+)")


def require_godot_exe() -> str:
    candidate = os.environ.get("GODOT_EXE", DEFAULT_GODOT_EXE)
    if not Path(candidate).exists():
        raise FileNotFoundError(
            f"GODOT_EXE not found at '{candidate}'. Set GODOT_EXE to a valid Godot console executable."
        )
    return candidate


def parse_log(text: str) -> dict:
    result = {
        "transportModel": None,
        "RInner": None,
        "ROuter": None,
        "Amp": None,
        "CanonicalGamma": None,
        "absorbCount": None,
        "absorbRate": None,
        "silhouetteRadiusMean": None,
        "silhouetteRadiusStdDev": None,
        "silhouetteRadiusMin": None,
        "silhouetteRadiusMax": None,
        "shadowAreaFraction": None,
        "shadowAngularCoverage": None,
        "shadowRadiusAnisotropy": None,
        "probeN": None,
        "silhouetteHistogram": None,
        "absorbedAngularHistogram": None,
        "hitRate": None,
        "steeringSummary": None,
        "fingerprintHash": None,
        "invalid": bool(INVALID_RE.search(text)),
    }

    for line in text.splitlines():
        if result["steeringSummary"] is None:
            steering_match = STEERING_RE.search(line)
            if steering_match:
                result["steeringSummary"] = steering_match.group(1).strip()

        if result["fingerprintHash"] is None:
            fingerprint_match = FINGERPRINT_RE.search(line)
            if fingerprint_match:
                result["fingerprintHash"] = fingerprint_match.group("fingerprint")

        if result["transportModel"] is None:
            compare_match = COMPARE_RE.search(line)
            if compare_match:
                fields = {key: value for key, value in KV_RE.findall(compare_match.group("body"))}
                result["transportModel"] = fields.get("transportModel")
                result["absorbCount"] = parse_int(fields.get("absorbCount"))
                result["absorbRate"] = parse_float(fields.get("absorbRate"))
                result["hitRate"] = parse_float(fields.get("hitRate"))
                result["silhouetteRadiusMean"] = parse_float(fields.get("silhouetteRadiusMean"))
                result["silhouetteRadiusStdDev"] = parse_float(fields.get("silhouetteRadiusStdDev"))
                result["silhouetteRadiusMin"] = parse_float(fields.get("silhouetteRadiusMin"))
                result["silhouetteRadiusMax"] = parse_float(fields.get("silhouetteRadiusMax"))
                result["shadowAreaFraction"] = parse_float(fields.get("shadowAreaFraction"))
                result["shadowAngularCoverage"] = parse_float(fields.get("shadowAngularCoverage"))
                result["shadowRadiusAnisotropy"] = parse_float(fields.get("shadowRadiusAnisotropy"))
                result["probeN"] = parse_int(fields.get("probeN"))
                result["silhouetteHistogram"] = extract_bracket_value(line, "silhouetteHistogram")
                result["absorbedAngularHistogram"] = extract_bracket_value(line, "absorbedAngularHistogram")

        if result["RInner"] is None:
            resolved_mass_match = RESOLVED_MASS_RE.search(line)
            if resolved_mass_match:
                result["RInner"] = float(resolved_mass_match.group("rinner"))
                result["ROuter"] = float(resolved_mass_match.group("router"))
                result["Amp"] = float(resolved_mass_match.group("amp"))
                result["CanonicalGamma"] = float(resolved_mass_match.group("gamma"))

    return result


def parse_float(value: str | None) -> float | None:
    if value in (None, "", "na"):
        return None
    try:
        return float(value)
    except ValueError:
        return None


def parse_int(value: str | None) -> int | None:
    if value in (None, "", "na"):
        return None
    try:
        return int(value)
    except ValueError:
        return None


def extract_bracket_value(line: str, key: str) -> str | None:
    marker = f"{key}=["
    start = line.find(marker)
    if start < 0:
        return None
    start += len(marker)
    end = line.find("]", start)
    if end < 0:
        return None
    return line[start:end]


def run_case(godot_exe: str, transport: dict, case: dict) -> dict:
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    log_path = LOG_DIR / f"{transport['id']}_{case['id']}.log"
    cmd = [
        godot_exe,
        "--path",
        ".",
        "--",
        "--render-test",
        "--render-test-fixture=blackhole_minimal",
        f"--blackhole-transport-model={transport['arg']}",
        "--lifecycle-stress=0",
        "--smartscale=0",
        *case["args"],
    ]
    completed = subprocess.run(
        cmd,
        cwd=ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=240,
        check=False,
    )
    combined = completed.stdout + ("\n" + completed.stderr if completed.stderr else "")
    log_path.write_text(combined, encoding="utf-8")
    parsed = parse_log(combined)
    parsed.update(
        {
            "caseId": case["id"],
            "parameter": case["parameter"],
            "sweptValue": case["value"],
            "requestedTransport": transport["id"],
            "exitCode": completed.returncode,
            "logFile": str(log_path),
        }
    )
    return parsed


def main() -> int:
    godot_exe = require_godot_exe()
    results = []
    for transport in TRANSPORTS:
        for case in CASES:
            print(f"RUN {transport['id']} {case['id']}", flush=True)
            result = run_case(godot_exe, transport, case)
            results.append(result)
            print(
                json.dumps(
                    {
                        "transport": result["transportModel"] or transport["id"],
                        "case": result["caseId"],
                        "RInner": result["RInner"],
                        "ROuter": result["ROuter"],
                        "Amp": result["Amp"],
                        "CanonicalGamma": result["CanonicalGamma"],
                        "absorbCount": result["absorbCount"],
                        "absorbRate": result["absorbRate"],
                        "silhouetteRadiusMean": result["silhouetteRadiusMean"],
                        "silhouetteRadiusStdDev": result["silhouetteRadiusStdDev"],
                        "silhouetteRadiusMin": result["silhouetteRadiusMin"],
                        "silhouetteRadiusMax": result["silhouetteRadiusMax"],
                        "shadowAreaFraction": result["shadowAreaFraction"],
                        "shadowAngularCoverage": result["shadowAngularCoverage"],
                        "shadowRadiusAnisotropy": result["shadowRadiusAnisotropy"],
                        "probeN": result["probeN"],
                        "silhouetteHistogram": result["silhouetteHistogram"],
                        "absorbedAngularHistogram": result["absorbedAngularHistogram"],
                        "hitRate": result["hitRate"],
                        "fingerprintHash": result["fingerprintHash"],
                        "invalid": result["invalid"],
                        "exitCode": result["exitCode"],
                    }
                ),
                flush=True,
            )

    summary_path = LOG_DIR / "summary.json"
    summary_path.write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(f"WROTE {summary_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
