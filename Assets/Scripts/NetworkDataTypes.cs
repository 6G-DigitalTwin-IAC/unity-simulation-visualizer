using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NetworkConfig
{
    [Header("Constellation Shape")]
    [Tooltip("Number of orbital planes around Earth")]
    public int totalPlanes    = 8;       // Increased from 5 to 8 for more satellites

    [Tooltip("Number of satellites in each plane")]
    public int satsPerPlane   = 10;      // Increased from 8 to 10 for denser coverage

    public float orbitAltitude = 550f;   // km above surface
    public float earthRadius   = 6371f;  // km
    public float inclination   = 80f;    // degrees
    public float orbitalPeriod = 95f;    // minutes (LEO ~95 min)

    [Header("Routing")]
    [Tooltip("Starting satellite ID (0-based)")]
    public int sourceNode = 0;

    [Tooltip("Destination satellite ID. For longer routes, choose satellites on opposite sides of Earth. Total satellites = totalPlanes × satsPerPlane")]
    public int targetNode = 10;  // With 8×10=80 satellites, node 45 is on opposite side

    public float rerouteThreshold = 0.85f; // trigger reroute above this load

    [Header("Dynamics")]
    public float congestionDecayRate  = 0.04f; // load drops per step off-route
    public float routeLoadIncrease    = 0.08f; // load rises per step on-route
    public float failureDurationMin   = 3f;
    public float failureDurationMax   = 10f;
}

// ─── Link ────────────────────────────────────────────────────────────────────

public class SatelliteLink
{
    public int   u, v;
    public bool  active    = true;
    public float load      = 0f;    // 0=empty, 1=saturated
    public float failTimer = 0f;    // steps remaining until recovery

    public SatelliteLink(int u, int v) { this.u = u; this.v = v; }

    public string Key => LinkKey(u, v);
    public static string LinkKey(int a, int b) => a < b ? $"{a}-{b}" : $"{b}-{a}";
}

// ─── Snapshot (read-only view of one tick) ───────────────────────────────────

public class NetworkSnapshot
{
    public int              stepId;
    public List<SatelliteLink> links;   // shallow copies — read only
    public RouteInfo        route;
    public NetworkMetrics   metrics;
}

public struct RouteInfo
{
    public List<int> path;       // node IDs in order: [src, ..., dst]
    public bool      isAdaptive; // true if last route was a forced reroute
    public bool      isActive;   // false when no path exists
}

public struct NetworkMetrics
{
    public float avgDelay;       // ms (simplified model)
    public int   rerouteCount;
    public float stabilityScore; // 0-100
    public int   activeLinks;
    public int   totalLinks;
    public float avgLoad;        // 0-1 across active links
}