using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Satellite ISL network graph. No Unity MonoBehaviour — purely a data engine.
/// Call Step() each simulation tick. Inject events anytime.
/// </summary>
public class SatelliteNetwork
{
    private readonly NetworkConfig cfg;

    // Graph storage
    private readonly Dictionary<string, SatelliteLink>   links      = new();
    private readonly Dictionary<int, List<string>>        adjacency  = new();

    // Routing state
    private List<int> currentPath = new();
    private bool      isAdaptive  = false;
    private int       rerouteCount = 0;
    private int       stepId       = 0;

    public int TotalSats => cfg.totalPlanes * cfg.satsPerPlane;

    // ─── Construction ─────────────────────────────────────────────────────────

    public SatelliteNetwork(NetworkConfig config)
    {
        cfg = config;
        BuildTopology();
        // Find initial route
        currentPath = Dijkstra(cfg.sourceNode, cfg.targetNode) ?? new List<int>();
    }

    void BuildTopology()
    {
        int P = cfg.totalPlanes;
        int S = cfg.satsPerPlane;

        for (int p = 0; p < P; p++)
        {
            for (int s = 0; s < S; s++)
            {
                int id = p * S + s;

                // Intra-plane ring (clockwise neighbor)
                AddLink(id, p * S + (s + 1) % S);

                // Inter-plane: same-index in next plane
                AddLink(id, ((p + 1) % P) * S + s);

                // Inter-plane diagonal: next sat in next plane (richer routing)
                AddLink(id, ((p + 1) % P) * S + (s + 1) % S);
            }
        }
    }

    void AddLink(int u, int v)
    {
        string key = SatelliteLink.LinkKey(u, v);
        if (links.ContainsKey(key)) return;

        links[key] = new SatelliteLink(u, v);

        if (!adjacency.ContainsKey(u)) adjacency[u] = new List<string>();
        if (!adjacency.ContainsKey(v)) adjacency[v] = new List<string>();
        adjacency[u].Add(key);
        adjacency[v].Add(key);
    }

    // ─── Simulation Tick ──────────────────────────────────────────────────────

    public NetworkSnapshot Step()
    {
        stepId++;
        RecoverFailedLinks();
        UpdateLoads();
        CheckAndReroute();
        return BuildSnapshot();
    }

    void RecoverFailedLinks()
    {
        foreach (var link in links.Values)
        {
            if (link.active || link.failTimer <= 0) continue;
            link.failTimer--;
            if (link.failTimer <= 0)
            {
                link.active = true;
                link.load   = 0f;
                Debug.Log($"[Net] Link {link.u}↔{link.v} recovered.");
            }
        }
    }

    void UpdateLoads()
    {
        var routeEdges = EdgeSetFromPath(currentPath);

        foreach (var link in links.Values)
        {
            if (!link.active) continue;
            if (routeEdges.Contains(link.Key))
                link.load = Mathf.Min(1f, link.load + cfg.routeLoadIncrease);
            else
                link.load = Mathf.Max(0f, link.load - cfg.congestionDecayRate);
        }
    }

    void CheckAndReroute()
    {
        if (!PathIsValid(currentPath))
        {
            bool hadPath = currentPath.Count > 1;
            currentPath = Dijkstra(cfg.sourceNode, cfg.targetNode) ?? new List<int>();

            if (hadPath)
            {
                isAdaptive = true;
                rerouteCount++;
                string pathStr = currentPath.Count > 0
                    ? string.Join("→", currentPath)
                    : "NONE";
                Debug.Log($"[Net] Rerouted! Path: {pathStr}");
            }
        }
    }

    bool PathIsValid(List<int> path)
    {
        if (path == null || path.Count < 2) return false;
        for (int i = 0; i < path.Count - 1; i++)
        {
            string key = SatelliteLink.LinkKey(path[i], path[i + 1]);
            if (!links.TryGetValue(key, out var link)) return false;
            if (!link.active || link.load >= cfg.rerouteThreshold) return false;
        }
        return true;
    }

    // ─── Dijkstra ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Weighted shortest path. Weight = 1 + load*8.
    /// Inactive links are excluded (treated as infinite cost).
    /// </summary>
    public List<int> Dijkstra(int src, int dst)
    {
        var dist = new Dictionary<int, float>();
        var prev = new Dictionary<int, int>();

        // SortedSet as a min-heap (cost, nodeId)
        var queue = new SortedSet<(float, int)>(
            Comparer<(float, int)>.Create((a, b) =>
                a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1)
                                   : a.Item2.CompareTo(b.Item2)));

        for (int n = 0; n < TotalSats; n++) { dist[n] = float.MaxValue; prev[n] = -1; }
        dist[src] = 0f;
        queue.Add((0f, src));

        while (queue.Count > 0)
        {
            var (cost, u) = queue.Min;
            queue.Remove((cost, u));
            if (u == dst) break;
            if (cost > dist[u]) continue;

            if (!adjacency.TryGetValue(u, out var neighbors)) continue;
            foreach (var key in neighbors)
            {
                if (!links.TryGetValue(key, out var link) || !link.active) continue;
                int v = link.u == u ? link.v : link.u;

                float w       = 1f + link.load * 8f;
                float newDist = dist[u] + w;
                if (newDist >= dist[v]) continue;

                queue.Remove((dist[v], v));
                dist[v] = newDist;
                prev[v] = u;
                queue.Add((newDist, v));
            }
        }

        if (dist[dst] == float.MaxValue) return null; // no path

        var path = new List<int>();
        for (int c = dst; c != -1; c = prev[c]) path.Insert(0, c);
        return path;
    }

    // ─── Event Injection (called from SimulationDriver) ───────────────────────

    public void InjectLinkFailure(int u, int v, float duration)
    {
        string key = SatelliteLink.LinkKey(u, v);
        if (!links.TryGetValue(key, out var link)) return;
        link.active    = false;
        link.load      = 0f;
        link.failTimer = duration;
        Debug.Log($"[Net] FAILURE: {u}↔{v} for {duration} steps.");
    }

    public void InjectCongestion(int u, int v, float amount)
    {
        string key = SatelliteLink.LinkKey(u, v);
        if (!links.TryGetValue(key, out var link) || !link.active) return;
        link.load = Mathf.Min(1f, link.load + amount);
        Debug.Log($"[Net] CONGESTION: {u}↔{v} → {link.load:F2}");
    }

    // ─── State Export ─────────────────────────────────────────────────────────

    public Dictionary<string, SatelliteLink> GetLinks()     => links;
    public Dictionary<int, List<string>>     GetAdjacency() => adjacency;

    NetworkSnapshot BuildSnapshot()
    {
        float totalLoad = 0f; int activeCount = 0;
        var linkCopies = new List<SatelliteLink>(links.Count);

        foreach (var l in links.Values)
        {
            linkCopies.Add(new SatelliteLink(l.u, l.v)
                { active = l.active, load = l.load, failTimer = l.failTimer });
            if (l.active) { totalLoad += l.load; activeCount++; }
        }

        float avgLoad  = activeCount > 0 ? totalLoad / activeCount : 0f;
        float avgDelay = 1f + avgLoad * 80f; // simplified: 1ms baseline, 80ms under full load
        float stability = Mathf.Clamp01(1f - rerouteCount * 0.08f) * 100f;

        return new NetworkSnapshot
        {
            stepId = stepId,
            links  = linkCopies,
            route  = new RouteInfo
            {
                path       = new List<int>(currentPath),
                isAdaptive = isAdaptive,
                isActive   = currentPath.Count > 1
            },
            metrics = new NetworkMetrics
            {
                avgDelay       = avgDelay,
                rerouteCount   = rerouteCount,
                stabilityScore = stability,
                activeLinks    = activeCount,
                totalLinks     = links.Count,
                avgLoad        = avgLoad
            }
        };
    }

    static HashSet<string> EdgeSetFromPath(List<int> path)
    {
        var set = new HashSet<string>();
        if (path == null) return set;
        for (int i = 0; i < path.Count - 1; i++)
            set.Add(SatelliteLink.LinkKey(path[i], path[i + 1]));
        return set;
    }
}