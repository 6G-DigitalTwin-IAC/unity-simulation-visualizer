using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MonoBehaviour hub. Owns SatelliteNetwork + OrbitEngine.
/// Drives simulation steps, generates random events, exposes read-only state.
/// Attach to a single GameObject (e.g. "SimulationManager").
/// </summary>
public class SimulationDriver : MonoBehaviour
{
    [Header("Constellation & Gate Config")]
    public NetworkConfig config = new NetworkConfig();

    [Header("Timing")]
    [Tooltip("Seconds between network simulation ticks")]
    [Range(0.05f, 3f)]
    public float stepInterval = 0.4f;

    [Tooltip("How fast satellites visually orbit (multiplier on real speed)")]
    [Range(1f, 200f)]
    public float simTimeScale = 80f;

    [Header("Random Events")]
    [Range(0f, 0.1f)]  public float linkFailureProbability  = 0.02f;
    [Range(0f, 0.15f)] public float congestionProbability   = 0.04f;
    [Range(0f, 1f)]    public float congestionSpikeAmount   = 0.35f;

    [Header("Playback")]
    public bool isPlaying = true;

    // ─── State (read from Visualizer and UI) ──────────────────────────────────

    public NetworkSnapshot CurrentSnapshot   { get; private set; }
    public Vector3[]       SatellitePositions { get; private set; }
    public float           SimTime            { get; private set; }
    public int             TotalSats          => config.totalPlanes * config.satsPerPlane;

    public float EarthRadius   => orbitEngine?.EarthRadius ?? 1f;
    public float OrbitRadius   => orbitEngine?.OrbitRadius ?? 2f;

    // ─── Events ───────────────────────────────────────────────────────────────

    public event Action<NetworkSnapshot> OnStepComplete;

    // ─── Internals ────────────────────────────────────────────────────────────

    private SatelliteNetwork network;
    private OrbitEngine      orbitEngine;
    private float            stepTimer;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Start()
    {
        Debug.Log($"[Driver] Config: {config.totalPlanes} planes x {config.satsPerPlane} sats = {TotalSats} total (Walker Delta {config.inclination}°)");
        Debug.Log($"[Driver] Flow: Source={config.sourceNode}, Target={config.targetNode}, gate mode={config.gateMode}");

        network     = new SatelliteNetwork(config);
        orbitEngine = new OrbitEngine(config, simTimeScale);

        SatellitePositions = orbitEngine.GetAllPositions(0f);
        CurrentSnapshot     = network.GetSnapshot();

        Debug.Log($"[Driver] Network created with {network.GetLinks().Count} ISLs (expect {TotalSats * 2} for degree-4 Walker Delta)");
    }

    void Update()
    {
        if (!isPlaying) return;

        SimTime   += Time.deltaTime;
        stepTimer += Time.deltaTime;

        // Orbital positions update every frame → smooth motion
        SatellitePositions = orbitEngine.GetAllPositions(SimTime);

        // Network ticks at fixed intervals
        if (stepTimer >= stepInterval)
        {
            float dt = stepTimer;
            stepTimer = 0f;
            Tick(dt);
        }
    }

    void Tick(float dt)
    {
        TryGenerateRandomEvent();
        CurrentSnapshot = network.Step(dt);
        OnStepComplete?.Invoke(CurrentSnapshot);
    }

    void TryGenerateRandomEvent()
    {
        if (UnityEngine.Random.value < linkFailureProbability)
        {
            var activeLinks = GetActiveLinks();
            if (activeLinks.Count > 0)
            {
                var link = RandomElement(activeLinks);
                float dur = UnityEngine.Random.Range(config.failureDurationMin, config.failureDurationMax);
                network.InjectLinkFailure(link.u, link.v, dur);
            }
        }

        if (UnityEngine.Random.value < congestionProbability)
        {
            var candidates = GetActiveLinks().FindAll(l => l.load < 0.7f);
            if (candidates.Count > 0)
            {
                var link = RandomElement(candidates);
                network.InjectCongestion(link.u, link.v, congestionSpikeAmount);
            }
        }
    }

    List<SatelliteLink> GetActiveLinks() => network.GetLinks().FindAll(l => l.active);

    static T RandomElement<T>(List<T> list) => list[UnityEngine.Random.Range(0, list.Count)];

    // ─── Public Controls ──────────────────────────────────────────────────────

    public void SetPlaying(bool play)     => isPlaying = play;
    public void TogglePlaying()           => isPlaying = !isPlaying;

    /// <param name="speed">Ticks per second (e.g. 2 = two network steps/sec)</param>
    public void SetSpeed(float speed)     => stepInterval = 1f / Mathf.Max(0.1f, speed);

    public void SetGateMode(GateMode mode)         => config.gateMode = mode;
    public void SetTrafficRegime(TrafficRegime r)  => config.trafficRegime = r;
    public void SetTelemetryDelay(float seconds)   => config.telemetryDelaySeconds = seconds;
    public void SetLoadSigma(float sigma)          => config.loadSigma = sigma;

    /// Force a failure on the first link of the currently visualized route to demo rerouting.
    public void ForceReroute()
    {
        var path = network?.GetActivePath(config.gateMode);
        if (path != null && path.Count >= 2)
            network.InjectLinkFailure(path[0], path[1],
                UnityEngine.Random.Range(config.failureDurationMin, config.failureDurationMax));
    }

    public void InjectFailure(int u, int v) =>
        network.InjectLinkFailure(u, v,
            UnityEngine.Random.Range(config.failureDurationMin, config.failureDurationMax));

    // Expose for extensions (e.g. clicking on a satellite in-scene)
    public SatelliteNetwork Network => network;
}
