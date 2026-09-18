using System.Collections.Generic;
using UnityEngine;

public enum GateMode { Direct, Ungated, Fixed, Adaptive }
public enum TrafficRegime { Calm, Bursty }

[System.Serializable]
public class NetworkConfig
{
    [Header("Constellation — Walker Delta 53.0 (264 sats, 12 planes, phase factor 1)")]
    public int totalPlanes    = 12;
    public int satsPerPlane   = 22;      // 12 x 22 = 264, matches the article's constellation

    public float orbitAltitude = 550f;   // km above surface
    public float earthRadius   = 6371f;  // km
    public float inclination   = 53f;    // degrees — first operational Starlink shell
    public float orbitalPeriod = 95f;    // minutes (LEO ~95 min)

    [Header("Single Flow / Single Ground-Station Pair")]
    [Tooltip("Uplink satellite ID for the one simulated flow")]
    public int sourceNode = 0;

    [Tooltip("Downlink satellite ID, chosen on the far side of the constellation")]
    public int targetNode = 143;

    [Header("Digital Twin & Trust Gate")]
    [Tooltip("Which of the four approaches drives the visualized route. All four are still simulated and compared live.")]
    public GateMode gateMode = GateMode.Adaptive;

    [Tooltip("Telemetry lag between ground truth and the twin's view, seconds. Article default 15s; hard operating point 30s.")]
    [Range(1f, 30f)]
    public float telemetryDelaySeconds = 15f;

    [Tooltip("Background load variability (sigma). Article's hard operating point uses 0.7.")]
    [Range(0.05f, 0.7f)]
    public float loadSigma = 0.35f;

    public TrafficRegime trafficRegime = TrafficRegime.Calm;

    [Tooltip("Fixed-gate threshold tau (Section IV.A of the article)")]
    [Range(0.5f, 0.95f)]
    public float fixedThreshold = 0.80f;

    [Header("Reconfiguration Energy Model (Section III.D)")]
    [Tooltip("FLOPs per forwarding-table recomputation. Unsourced in the article, swept 1e6-1e12; kept fixed here for a concrete on-screen joule figure.")]
    public double fConfigFlops = 1e9;

    [Tooltip("Joules per FLOP on space-qualified hardware (article: 5e-9 J)")]
    public double eFlopJoules = 5e-9;

    [Header("Bufferbloat Traffic Model (Section III.B)")]
    [Tooltip("One-hop delay at zero load, ms (article: ~33ms, Starlink-calibrated)")]
    public float bufferbloatBaselineMs = 33f;

    [Tooltip("One-hop delay at full saturation, ms (article: 400-500ms)")]
    public float bufferbloatSaturatedMs = 450f;

    [Header("Link Failure Events")]
    public float failureDurationMin = 3f;
    public float failureDurationMax = 10f;
}

// ─── Link ────────────────────────────────────────────────────────────────────

public class SatelliteLink
{
    public int   u, v;
    public bool  active    = true;
    public float load      = 0f;    // 0=empty, 1=saturated — ground-truth background congestion
    public float failTimer = 0f;    // seconds remaining until recovery

    public SatelliteLink(int u, int v) { this.u = u; this.v = v; }

    public string Key => LinkKey(u, v);
    public static string LinkKey(int a, int b) => a < b ? $"{a}-{b}" : $"{b}-{a}";
}

// ─── Per-mode trust-gate state (Direct / Ungated / Fixed / Adaptive) ─────────

public class GateModeStats
{
    public List<int> path = new();
    public int    routeChanges;
    public double energyJoules;
    public float  currentThreshold; // tau actually applied this tick (0 for Ungated, n/a for Direct)
    public float  lastDelta;        // twin's last normalized predicted improvement
    public bool   lastProposed;     // twin proposed a different path than the one currently active
    public bool   lastAccepted;     // the proposal cleared the gate and was adopted
    public float  trueDelayMs;      // current path's delay under ground-truth load
}

// ─── Snapshot (read-only view of one tick) ───────────────────────────────────

public class NetworkSnapshot
{
    public int                          stepId;
    public List<SatelliteLink>          links;   // ground-truth link state — shallow copies, read only
    public Dictionary<GateMode, GateModeStats> modes;
    public GateMode                     visualizedMode;
    public float                        twinDifficulty; // 0..1 flavor readout of the twin's own operating-point sigma
    public int                          activeLinks;
    public int                          totalLinks;
    public float                        avgBackgroundLoad;

    public GateModeStats Visualized => modes[visualizedMode];
}
