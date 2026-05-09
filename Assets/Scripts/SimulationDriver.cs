using System;
using UnityEngine;

/// <summary>
/// MonoBehaviour hub. Owns SatelliteNetwork + OrbitEngine.
/// Drives simulation steps, generates random events, exposes read-only state.
/// Attach to a single GameObject (e.g. "SimulationManager").
/// </summary>
public class SimulationDriver : MonoBehaviour
{
    [Header("Constellation Config")]
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
        Debug.Log($"[Driver] ===== SIMULATION DRIVER START =====");
        Debug.Log($"[Driver] Config: {config.totalPlanes} planes × {config.satsPerPlane} sats = {TotalSats} total");
        Debug.Log($"[Driver] Route: Source={config.sourceNode}, Target={config.targetNode}");
        Debug.Log($"[Driver] Orbit: altitude={config.orbitAltitude}km, inclination={config.inclination}°");

        network     = new SatelliteNetwork(config);
        orbitEngine = new OrbitEngine(config, simTimeScale);

        SatellitePositions = orbitEngine.GetAllPositions(0f);
        CurrentSnapshot    = network.GetSnapshot();

        Debug.Log($"[Driver] ✓ Network created with {network.GetLinks().Count} ISLs");
        Debug.Log($"[Driver] ✓ Initial route path: {string.Join("→", CurrentSnapshot.route.path)}");
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
            stepTimer -= stepInterval;
            Tick();
        }
    }

    void Tick()
    {
        TryGenerateRandomEvent();
        CurrentSnapshot = network.Step();
        OnStepComplete?.Invoke(CurrentSnapshot);
    }

    void TryGenerateRandomEvent()
    {
        // Random link failure
        if (UnityEngine.Random.value < linkFailureProbability)
        {
            var activeLinks = GetActiveLinks();
            if (activeLinks.Count > 0)
            {
                var link = RandomElement(activeLinks);
                float dur = UnityEngine.Random.Range(
                    config.failureDurationMin, config.failureDurationMax);
                network.InjectLinkFailure(link.u, link.v, dur);
            }
        }

        // Random congestion spike
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

    System.Collections.Generic.List<SatelliteLink> GetActiveLinks()
    {
        var all = new System.Collections.Generic.List<SatelliteLink>(
            network.GetLinks().Values);
        return all.FindAll(l => l.active);
    }

    static T RandomElement<T>(System.Collections.Generic.List<T> list) =>
        list[UnityEngine.Random.Range(0, list.Count)];

    // ─── Public Controls ──────────────────────────────────────────────────────

    public void SetPlaying(bool play)     => isPlaying = play;
    public void TogglePlaying()           => isPlaying = !isPlaying;

    /// <param name="speed">Ticks per second (e.g. 2 = two network steps/sec)</param>
    public void SetSpeed(float speed)     => stepInterval = 1f / Mathf.Max(0.1f, speed);

    /// Force a failure on the first link of the current route to demo rerouting.
    public void ForceReroute()
    {
        var path = CurrentSnapshot?.route.path;
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

// Needed because NetworkConfig is a plain class, not ScriptableObject
// This adds a public GetSnapshot() on the network (already defined above)
public static class SatelliteNetworkExtensions
{
    public static NetworkSnapshot GetSnapshot(this SatelliteNetwork net)
    {
        // Delegates to the internal BuildSnapshot — already returned by Step().
        // Call net.Step() if you need a fresh snapshot without advancing the sim.
        return net.Step(); // Note: only use for initialization
    }
}