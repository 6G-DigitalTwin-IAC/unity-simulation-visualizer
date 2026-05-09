using UnityEngine;

/// <summary>
/// Walker constellation orbital mechanics. Pure C#.
/// Converts (plane, sat, time) → Unity world-space Vector3.
/// </summary>
public class OrbitEngine
{
    private readonly NetworkConfig cfg;

    // Scaling: 1 Unity unit = 100 km
    private const float WorldScale = 0.01f;

    public readonly float EarthRadius;   // world units
    public readonly float OrbitRadius;   // world units

    private readonly float incRad;          // inclination in radians
    private readonly float angularVelocity; // rad/sec in sim time

    public OrbitEngine(NetworkConfig config, float simTimeScale = 60f)
    {
        cfg         = config;
        EarthRadius = config.earthRadius * WorldScale;
        OrbitRadius = (config.earthRadius + config.orbitAltitude) * WorldScale;
        incRad      = config.inclination * Mathf.Deg2Rad;

        // Real period converted: simTimeScale makes orbits visible in seconds
        float periodSec  = config.orbitalPeriod * 60f;
        angularVelocity  = (2f * Mathf.PI / periodSec) * simTimeScale;
    }

    /// <summary>Returns positions for all satellites at simTime (seconds).</summary>
    public Vector3[] GetAllPositions(float simTime)
    {
        int P = cfg.totalPlanes, S = cfg.satsPerPlane;
        var pos = new Vector3[P * S];
        for (int p = 0; p < P; p++)
            for (int s = 0; s < S; s++)
                pos[p * S + s] = GetPosition(p, s, simTime);
        return pos;
    }

    public Vector3 GetPositionByID(int id, float simTime) =>
        GetPosition(id / cfg.satsPerPlane, id % cfg.satsPerPlane, simTime);

    public Vector3 GetPosition(int planeIdx, int satIdx, float simTime)
    {
        int P = cfg.totalPlanes, S = cfg.satsPerPlane;

        // Each orbital plane is rotated by RAAN around Earth's polar axis
        float raan = planeIdx * (2f * Mathf.PI / P);

        // Walker: phase offset per plane prevents satellite clustering
        float phaseOffset = planeIdx * (2f * Mathf.PI / (P * S));
        float phase = satIdx * (2f * Mathf.PI / S) + phaseOffset
                    + simTime * angularVelocity;

        // Position in orbital plane (2D circular orbit)
        float xOrb = OrbitRadius * Mathf.Cos(phase);
        float yOrb = OrbitRadius * Mathf.Sin(phase);

        // Rotate by inclination around X
        float xI = xOrb;
        float yI = yOrb * Mathf.Cos(incRad);
        float zI = yOrb * Mathf.Sin(incRad);

        // Rotate by RAAN around Z
        float x =  xI * Mathf.Cos(raan) - yI * Mathf.Sin(raan);
        float y =  xI * Mathf.Sin(raan) + yI * Mathf.Cos(raan);
        float z =  zI;

        // Unity: Y is up, so swap Y↔Z from math convention
        return new Vector3(x, z, y);
    }
}