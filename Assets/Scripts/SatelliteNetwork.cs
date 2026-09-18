using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Satellite ISL network + digital twin + trust gate. No Unity MonoBehaviour — purely a data engine.
/// Reproduces the article's system model: Walker Delta topology (Section III.A), a bufferbloat
/// traffic model (Section III.B), a delayed-telemetry digital twin (Section III.C), a reconfiguration
/// energy model (Section III.D) and the trust gate itself (Section IV) in all four compared forms
/// (Direct/oracle, Ungated, Fixed, Adaptive — Section V).
///
/// All four gate modes are simulated every tick regardless of which one is currently visualized,
/// so the UI can show a live Table-1-style comparison.
/// </summary>
public class SatelliteNetwork
{
    private static readonly GateMode[] AllModes =
        { GateMode.Direct, GateMode.Ungated, GateMode.Fixed, GateMode.Adaptive };

    private readonly NetworkConfig cfg;
    private readonly AdaptiveThresholdSelector selector = new();

    // Graph storage — index-aligned
    private readonly List<SatelliteLink>       linksList  = new();
    private readonly Dictionary<string, int>   keyToIndex = new();
    private readonly Dictionary<int, List<int>> adjacency = new();
    private float[] bgMean; // per-link baseline the background load reverts toward

    // Telemetry history for the digital twin — (simClockSeconds, load snapshot)
    private readonly List<(float time, float[] loads)> history = new();
    private const float MaxHistorySeconds = 35f; // covers the article's 1-30s delay range plus margin

    private readonly Dictionary<GateMode, ModeState> modeStates = new();
    private class ModeState
    {
        public List<int> path = new();
        public int    routeChanges;
        public double energyJoules;
        public float  currentThreshold;
        public float  lastDelta;
        public bool   lastProposed;
        public bool   lastAccepted;
        public float  trueDelayMs;
    }

    private float simClockSeconds = 0f;
    private int   stepId = 0;

    public int TotalSats => cfg.totalPlanes * cfg.satsPerPlane;

    // ─── Construction ─────────────────────────────────────────────────────────

    public SatelliteNetwork(NetworkConfig config)
    {
        cfg = config;
        BuildTopology();
        InitBackgroundLoad();
        RecordHistory();

        var initialPath = Dijkstra(cfg.sourceNode, cfg.targetNode, CurrentLoadArray()) ?? new List<int>();
        foreach (var mode in AllModes)
            modeStates[mode] = new ModeState { path = new List<int>(initialPath) };
    }

    void BuildTopology()
    {
        int P = cfg.totalPlanes, S = cfg.satsPerPlane;

        // Walker Delta: planes spread across the full 360 degrees, so the same-index
        // inter-plane link wraps uniformly through every plane pair — no seam anywhere,
        // unlike a Walker Star. Each satellite ends up with exactly 4 ISLs: 2 intra-plane
        // ring neighbours + 2 same-index inter-plane neighbours (Section III.A).
        for (int p = 0; p < P; p++)
        {
            for (int s = 0; s < S; s++)
            {
                int id = p * S + s;
                AddLink(id, p * S + (s + 1) % S);       // intra-plane ring neighbour
                AddLink(id, ((p + 1) % P) * S + s);      // inter-plane same-index neighbour
            }
        }
    }

    void AddLink(int u, int v)
    {
        string key = SatelliteLink.LinkKey(u, v);
        if (keyToIndex.ContainsKey(key)) return;

        int idx = linksList.Count;
        linksList.Add(new SatelliteLink(u, v));
        keyToIndex[key] = idx;

        if (!adjacency.ContainsKey(u)) adjacency[u] = new List<int>();
        if (!adjacency.ContainsKey(v)) adjacency[v] = new List<int>();
        adjacency[u].Add(idx);
        adjacency[v].Add(idx);
    }

    void InitBackgroundLoad()
    {
        bgMean = new float[linksList.Count];
        var rng = new System.Random(12345); // deterministic per-link baseline variety
        for (int i = 0; i < linksList.Count; i++)
        {
            bgMean[i] = 0.15f + (float)rng.NextDouble() * 0.15f; // 0.15 - 0.30 baseline
            linksList[i].load = bgMean[i];
        }
    }

    // ─── Simulation Tick ──────────────────────────────────────────────────────

    public NetworkSnapshot Step(float dt)
    {
        stepId++;
        simClockSeconds += dt;

        RecoverFailedLinks(dt);
        UpdateBackgroundLoad(dt);
        RecordHistory();

        float[] groundTruthLoad = CurrentLoadArray();
        float[] twinLoad        = GetLaggedLoad(cfg.telemetryDelaySeconds);
        var candidatePath        = Dijkstra(cfg.sourceNode, cfg.targetNode, twinLoad);

        foreach (var mode in AllModes)
            StepMode(mode, groundTruthLoad, twinLoad, candidatePath);

        return BuildSnapshot();
    }

    void StepMode(GateMode mode, float[] groundTruthLoad, float[] twinLoad, List<int> candidatePath)
    {
        var ms = modeStates[mode];
        List<int> newPath = ms.path;
        float delta = 0f, tau = 0f;
        bool proposed = false, accepted = false;

        if (mode == GateMode.Direct)
        {
            // Oracle: always sees ground truth, immune to staleness by construction (Section V).
            var oraclePath = Dijkstra(cfg.sourceNode, cfg.targetNode, groundTruthLoad) ?? ms.path;
            proposed = !PathsEqual(oraclePath, ms.path);
            accepted = proposed;
            newPath  = oraclePath;
        }
        else
        {
            var candidate = candidatePath ?? ms.path;
            proposed = !PathsEqual(candidate, ms.path);

            float costCurrent   = PathCost(ms.path, twinLoad);
            float costCandidate = PathCost(candidate, twinLoad);
            bool currentIsBroken = float.IsPositiveInfinity(costCurrent);

            delta = (!currentIsBroken && costCurrent > 0.0001f)
                ? Mathf.Clamp01((costCurrent - costCandidate) / costCurrent)
                : 0f;

            if (currentIsBroken)
            {
                // The active path no longer exists in the twin's own view (e.g. a link failed).
                // No amount of gating can justify staying on a dead path.
                accepted = proposed;
            }
            else
            {
                switch (mode)
                {
                    case GateMode.Ungated:
                        tau = 0f;
                        accepted = proposed;
                        break;
                    case GateMode.Fixed:
                        tau = cfg.fixedThreshold;
                        accepted = proposed && delta >= tau;
                        break;
                    case GateMode.Adaptive:
                        tau = selector.GetThreshold(cfg.telemetryDelaySeconds, cfg.loadSigma, cfg.trafficRegime);
                        accepted = proposed && delta >= tau;
                        break;
                }
            }
            newPath = accepted ? candidate : ms.path;
        }

        if (accepted && !PathsEqual(newPath, ms.path))
        {
            int nChanged = CountForwardingChanges(ms.path, newPath);
            ms.energyJoules += nChanged * cfg.fConfigFlops * cfg.eFlopJoules;
            ms.routeChanges++;
            ms.path = newPath;
        }

        ms.lastDelta         = delta;
        ms.currentThreshold  = tau;
        ms.lastProposed      = proposed;
        ms.lastAccepted      = accepted;
        ms.trueDelayMs       = PathCost(ms.path, groundTruthLoad);
    }

    void RecoverFailedLinks(float dt)
    {
        for (int i = 0; i < linksList.Count; i++)
        {
            var link = linksList[i];
            if (link.active || link.failTimer <= 0) continue;
            link.failTimer -= dt;
            if (link.failTimer <= 0)
            {
                link.active = true;
                link.load   = bgMean[i];
            }
        }
    }

    void UpdateBackgroundLoad(float dt)
    {
        // Slowly-drifting background load, correlation timescale anchored to the 15-60s
        // window from Section II/III.B; the bursty regime reverts faster and gets spikes on top.
        float tau   = cfg.trafficRegime == TrafficRegime.Calm ? 45f : 20f;
        float theta = Mathf.Clamp01(dt / tau);

        for (int i = 0; i < linksList.Count; i++)
        {
            var link = linksList[i];
            if (!link.active) continue;
            float noise = GaussianRandom() * cfg.loadSigma * Mathf.Sqrt(dt);
            link.load = Mathf.Clamp01(link.load + (bgMean[i] - link.load) * theta + noise);
        }

        if (cfg.trafficRegime == TrafficRegime.Bursty)
        {
            for (int i = 0; i < linksList.Count; i++)
            {
                if (!linksList[i].active) continue;
                if (Random.value < 0.12f * dt)
                    linksList[i].load = Mathf.Clamp01(linksList[i].load + Random.Range(0.2f, 0.5f));
            }
        }
    }

    static float GaussianRandom()
    {
        float u1 = 1f - Random.value;
        float u2 = Random.value;
        return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
    }

    // ─── Digital twin telemetry history ──────────────────────────────────────

    void RecordHistory()
    {
        var snap = new float[linksList.Count];
        for (int i = 0; i < linksList.Count; i++) snap[i] = linksList[i].load;
        history.Add((simClockSeconds, snap));

        while (history.Count > 1 && simClockSeconds - history[0].time > MaxHistorySeconds)
            history.RemoveAt(0);
    }

    float[] GetLaggedLoad(float delaySeconds)
    {
        if (history.Count == 0) return CurrentLoadArray();
        float target = simClockSeconds - delaySeconds;
        for (int i = history.Count - 1; i >= 0; i--)
            if (history[i].time <= target) return history[i].loads;
        return history[0].loads; // not enough history yet — use the oldest we have
    }

    float[] CurrentLoadArray()
    {
        var arr = new float[linksList.Count];
        for (int i = 0; i < linksList.Count; i++) arr[i] = linksList[i].load;
        return arr;
    }

    // ─── Bufferbloat delay model (Section III.B) ─────────────────────────────

    float BufferbloatDelayMs(float load) =>
        cfg.bufferbloatBaselineMs +
        (cfg.bufferbloatSaturatedMs - cfg.bufferbloatBaselineMs) * Mathf.Pow(Mathf.Clamp01(load), 3f);

    // ─── Dijkstra over a given load view (ground truth or the twin's lagged view) ─

    public List<int> Dijkstra(int src, int dst, float[] loadArr)
    {
        int n = TotalSats;
        var dist    = new float[n];
        var prev    = new int[n];
        var visited = new bool[n];
        for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
        dist[src] = 0f;

        var queue = new SortedSet<(float, int)>(
            Comparer<(float, int)>.Create((a, b) =>
                a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : a.Item2.CompareTo(b.Item2)));
        queue.Add((0f, src));

        while (queue.Count > 0)
        {
            var (cost, u) = queue.Min;
            queue.Remove((cost, u));
            if (visited[u]) continue;
            visited[u] = true;
            if (u == dst) break;

            if (!adjacency.TryGetValue(u, out var linkIdxs)) continue;
            foreach (var idx in linkIdxs)
            {
                var link = linksList[idx];
                if (!link.active) continue;
                int v = link.u == u ? link.v : link.u;
                if (visited[v]) continue;

                float w  = BufferbloatDelayMs(loadArr[idx]);
                float nd = dist[u] + w;
                if (nd < dist[v])
                {
                    dist[v] = nd;
                    prev[v] = u;
                    queue.Add((nd, v));
                }
            }
        }

        if (dist[dst] == float.MaxValue) return null;
        var path = new List<int>();
        for (int c = dst; c != -1; c = prev[c]) path.Insert(0, c);
        return path;
    }

    float PathCost(List<int> path, float[] loadArr)
    {
        if (path == null || path.Count < 2) return float.PositiveInfinity;
        float total = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            if (!keyToIndex.TryGetValue(SatelliteLink.LinkKey(path[i], path[i + 1]), out int idx))
                return float.PositiveInfinity;
            var link = linksList[idx];
            if (!link.active) return float.PositiveInfinity;
            total += BufferbloatDelayMs(loadArr[idx]);
        }
        return total;
    }

    static bool PathsEqual(List<int> a, List<int> b)
    {
        if (a == null || b == null) return a == b;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }

    /// <summary>
    /// N_changed: satellites whose actual next hop changes, not every satellite that merely
    /// lies somewhere along a path that changed (Section III.D — the article calls out the
    /// stricter accounting explicitly, after finding the naive count too generous).
    /// </summary>
    static int CountForwardingChanges(List<int> oldPath, List<int> newPath)
    {
        var oldMap = ForwardMap(oldPath);
        var newMap = ForwardMap(newPath);
        var keys = new HashSet<int>(oldMap.Keys);
        keys.UnionWith(newMap.Keys);

        int count = 0;
        foreach (var k in keys)
        {
            bool hadOld = oldMap.TryGetValue(k, out int a);
            bool hasNew = newMap.TryGetValue(k, out int b);
            if (hadOld != hasNew || a != b) count++;
        }
        return count;
    }

    static Dictionary<int, int> ForwardMap(List<int> path)
    {
        var d = new Dictionary<int, int>();
        if (path == null) return d;
        for (int i = 0; i < path.Count - 1; i++) d[path[i]] = path[i + 1];
        return d;
    }

    // ─── Event Injection (called from SimulationDriver) ───────────────────────

    public void InjectLinkFailure(int u, int v, float duration)
    {
        if (!keyToIndex.TryGetValue(SatelliteLink.LinkKey(u, v), out int idx)) return;
        var link = linksList[idx];
        link.active    = false;
        link.load      = 0f;
        link.failTimer = duration;
    }

    public void InjectCongestion(int u, int v, float amount)
    {
        if (!keyToIndex.TryGetValue(SatelliteLink.LinkKey(u, v), out int idx)) return;
        var link = linksList[idx];
        if (!link.active) return;
        link.load = Mathf.Min(1f, link.load + amount);
    }

    // ─── State Export ─────────────────────────────────────────────────────────

    public List<SatelliteLink> GetLinks() => linksList;
    public List<int> GetActivePath(GateMode mode) => modeStates[mode].path;
    public NetworkSnapshot GetSnapshot() => BuildSnapshot();

    NetworkSnapshot BuildSnapshot()
    {
        var linkCopies = new List<SatelliteLink>(linksList.Count);
        int activeCount = 0; float totalLoad = 0f;
        foreach (var l in linksList)
        {
            linkCopies.Add(new SatelliteLink(l.u, l.v) { active = l.active, load = l.load, failTimer = l.failTimer });
            if (l.active) { totalLoad += l.load; activeCount++; }
        }

        var modes = new Dictionary<GateMode, GateModeStats>();
        foreach (var kv in modeStates)
        {
            var s = kv.Value;
            modes[kv.Key] = new GateModeStats
            {
                path             = new List<int>(s.path),
                routeChanges     = s.routeChanges,
                energyJoules     = s.energyJoules,
                currentThreshold = s.currentThreshold,
                lastDelta        = s.lastDelta,
                lastProposed     = s.lastProposed,
                lastAccepted     = s.lastAccepted,
                trueDelayMs      = s.trueDelayMs,
            };
        }

        return new NetworkSnapshot
        {
            stepId            = stepId,
            links             = linkCopies,
            modes             = modes,
            visualizedMode    = cfg.gateMode,
            twinDifficulty    = Mathf.InverseLerp(0.05f, 0.7f, cfg.loadSigma),
            activeLinks       = activeCount,
            totalLinks        = linksList.Count,
            avgBackgroundLoad = activeCount > 0 ? totalLoad / activeCount : 0f,
        };
    }
}
