using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Reads state from SimulationDriver every frame and renders the constellation.
/// Attach to the same GameObject as SimulationDriver.
/// </summary>
[RequireComponent(typeof(SimulationDriver))]
public class ConstellationVisualizer : MonoBehaviour
{
    [Header("Prefabs — assign in Inspector")]
    public GameObject satPrefab;
    public GameObject earthPrefab;

    [Header("Debug")]
    [Tooltip("Force use of primitive cubes instead of prefabs (for testing)")]
    public bool forceUseFallbackCubes = false;

    [Header("Line Pool")]
    [Tooltip("Must be >= number of ISLs. ~800 needed for 8x10 constellation (3 links per sat × 80 sats / 2).")]
    public int linePoolSize = 800;

    [Header("Scales")]
    public float earthScale = 0.15f;     // Multiplier for Earth size (smaller to see constellation better)
    public float satScale = 2.0f;        // Satellite size in world units (larger for visibility)

    [Header("Colors - Links")]
    public Color colorIdle   = new Color(0.15f, 0.9f,  0.3f,  0.55f); // green
    public Color colorBusy   = new Color(1f,    0.75f, 0f,    0.75f); // yellow
    public Color colorCrit   = new Color(1f,    0.15f, 0.15f, 0.95f); // red
    public Color colorRoute  = new Color(1f,    0.55f, 0f,    1f);    // orange
    public Color colorDead   = new Color(0.35f, 0.35f, 0.35f, 0.12f); // gray

    [Header("Colors - Special Satellites")]
    public Color colorSource = new Color(0f,    1f,    0f,    1f);    // bright green
    public Color colorTarget = new Color(1f,    0f,    0f,    1f);    // bright red
    public bool highlightEndpoints = true;

    [Header("Line Widths")]
    public float widthNormal = 0.08f;
    public float widthRoute  = 0.2f;
    public float widthDead   = 0.02f;

    [Header("Rendering")]
    [Tooltip("Enable to render lines behind satellites")]
    public bool linesAlwaysBehind = true;

    // ─── Internal ─────────────────────────────────────────────────────────────

    private SimulationDriver  driver;
    private GameObject[]      satObjects;
    private List<LineRenderer> linePool = new();
    private Material          lineMat;
    private Material          routeMat;   // scrolling UV for animated route
    private float             routeScroll;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Start()
    {
        driver = GetComponent<SimulationDriver>();
        if (driver == null)
        {
            Debug.LogError("[Visualizer] SimulationDriver not found! Attach this script to the same GameObject as SimulationDriver.");
            enabled = false;
            return;
        }
        // Defer build so driver.Start() runs first
        Invoke(nameof(Build), 0.05f);
    }

    void Build()
    {
        Debug.Log($"[Visualizer] ===== BUILD STARTED =====");
        Debug.Log($"[Visualizer] Total satellites to create: {driver.TotalSats}");
        Debug.Log($"[Visualizer] Earth radius: {driver.EarthRadius:F2}, Orbit radius: {driver.OrbitRadius:F2}");

        // Earth - apply scale multiplier
        float earthDiameter = driver.EarthRadius * 2f * earthScale;
        if (earthPrefab)
        {
            var earth = Instantiate(earthPrefab, Vector3.zero, Quaternion.identity);
            earth.name = "Earth";
            earth.transform.localScale = Vector3.one * earthDiameter;
            Debug.Log($"[Visualizer] ✓ Earth instantiated (radius={driver.EarthRadius:F2}, diameter={earthDiameter:F2})");
        }
        else
        {
            Debug.LogWarning("[Visualizer] Earth prefab not assigned! Creating fallback sphere.");
            // Create fallback Earth
            var earth = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            earth.name = "Earth (Fallback)";
            earth.transform.position = Vector3.zero;
            earth.transform.localScale = Vector3.one * earthDiameter;
            var renderer = earth.GetComponent<Renderer>();
            if (renderer) renderer.material.color = new Color(0.2f, 0.4f, 0.8f);
        }

        // Satellites
        int total  = driver.TotalSats;
        satObjects = new GameObject[total];

        if (!satPrefab)
        {
            Debug.LogWarning("[Visualizer] Satellite prefab not assigned! Creating fallback cubes.");
        }
        else
        {
            Debug.Log($"[Visualizer] Using satellite prefab: {satPrefab.name}");
        }

        int rendererCount = 0;
        bool usingFallback = !satPrefab || forceUseFallbackCubes;

        if (forceUseFallbackCubes)
            Debug.LogWarning($"[Visualizer] ⚠ forceUseFallbackCubes is TRUE - using primitive cubes instead of prefab!");

        for (int i = 0; i < total; i++)
        {
            if (satPrefab && !forceUseFallbackCubes)
            {
                satObjects[i] = Instantiate(satPrefab, Vector3.zero, Quaternion.identity, transform);
                satObjects[i].SetActive(true); // Ensure it's active

                // Count renderers (only for first satellite)
                if (i == 0)
                {
                    var renderers = satObjects[i].GetComponentsInChildren<Renderer>();
                    rendererCount = renderers.Length;
                    Debug.Log($"[Visualizer] First satellite instantiated at {satObjects[i].transform.position}, has {rendererCount} renderer(s)");

                    if (rendererCount == 0)
                        Debug.LogError($"[Visualizer] ⚠⚠⚠ SATELLITE PREFAB HAS NO RENDERERS! Satellites will be invisible!");
                    else
                    {
                        foreach (var r in renderers)
                        {
                            Debug.Log($"[Visualizer]   - Renderer: {r.name}, enabled: {r.enabled}, material: {r.sharedMaterial?.name ?? "NULL"}");
                        }
                    }
                }
            }
            else
            {
                // Fallback: create simple cube
                satObjects[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                satObjects[i].transform.parent = transform;
                var renderer = satObjects[i].GetComponent<Renderer>();
                if (renderer)
                {
                    renderer.material.color = new Color(0.9f, 0.9f, 0.2f);
                    renderer.enabled = true;
                }

                if (i == 0)
                {
                    Debug.Log($"[Visualizer] Created fallback CUBE for first satellite");
                    Debug.Log($"[Visualizer]   - Position: {satObjects[i].transform.position}");
                    Debug.Log($"[Visualizer]   - Renderer enabled: {renderer.enabled}");
                    Debug.Log($"[Visualizer]   - Material: {renderer.material.name}");
                }
            }
            satObjects[i].name = $"Sat_{i}";
            satObjects[i].transform.localScale = Vector3.one * satScale;
            satObjects[i].layer = 0; // Default layer
        }

        string prefabInfo = usingFallback ? "FALLBACK CUBES" : $"prefab '{satPrefab.name}'";
        Debug.Log($"[Visualizer] ✓ Created {total} satellites using {prefabInfo} with scale {satScale} (renderers: {rendererCount})");

        // Highlight source and destination satellites
        if (highlightEndpoints)
        {
            int src = driver.Network.GetSnapshot().route.path.Count > 0 ? driver.Network.GetSnapshot().route.path[0] : -1;
            int dst = driver.Network.GetSnapshot().route.path.Count > 0 ? driver.Network.GetSnapshot().route.path[^1] : -1;

            if (src >= 0 && src < satObjects.Length && satObjects[src] != null)
            {
                var renderers = satObjects[src].GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                    r.material.color = colorSource;
                Debug.Log($"[Visualizer] Source satellite (ID {src}) highlighted in GREEN");
            }

            if (dst >= 0 && dst < satObjects.Length && satObjects[dst] != null)
            {
                var renderers = satObjects[dst].GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                    r.material.color = colorTarget;
                Debug.Log($"[Visualizer] Target satellite (ID {dst}) highlighted in RED");
            }
        }

        // Verify satObjects array
        int nullCount = 0;
        for (int i = 0; i < satObjects.Length; i++)
            if (satObjects[i] == null) nullCount++;

        if (nullCount > 0)
            Debug.LogError($"[Visualizer] ⚠⚠⚠ {nullCount} satellites are NULL in the array!");
        else
            Debug.Log($"[Visualizer] ✓ All {satObjects.Length} satellite GameObjects are valid (not null)");

        // Materials - try multiple shaders for compatibility
        Shader lineShader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default")
                         ?? Shader.Find("Unlit/Color");

        if (lineShader == null)
        {
            Debug.LogWarning("[Visualizer] No suitable shader found, creating basic unlit shader");
            lineShader = Shader.Find("Hidden/InternalErrorShader");
        }

        lineMat  = new Material(lineShader);
        routeMat = new Material(lineShader); // separate instance so UV scroll is independent

        // LineRenderer pool
        for (int i = 0; i < linePoolSize; i++)
        {
            var go = new GameObject($"Link_{i}") { transform = { parent = transform } };
            var lr = go.AddComponent<LineRenderer>();
            lr.material       = lineMat;
            lr.positionCount  = 2;
            lr.useWorldSpace  = true;
            lr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.allowOcclusionWhenDynamic = false; // Prevent lines from being culled

            // Render queue: Lower values render first (behind)
            if (linesAlwaysBehind)
            {
                lr.sortingOrder = -10; // Render behind other objects
            }

            lr.enabled        = false;
            linePool.Add(lr);
        }

        Debug.Log($"[Visualizer] Created {linePoolSize} line renderers");
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    void Update()
    {
        if (satObjects == null) return;

        // Animate route line (scrolling dashes illusion)
        routeScroll = (routeScroll + Time.deltaTime * 0.35f) % 1f;
        routeMat.mainTextureOffset = new Vector2(routeScroll, 0f);

        MoveSatellites();
        DrawLinks();
    }

    private bool positionLoggedOnce = false;
    private int updateCount = 0;

    void MoveSatellites()
    {
        var pos = driver.SatellitePositions;

        if (pos == null)
        {
            Debug.LogWarning($"[Visualizer] MoveSatellites: driver.SatellitePositions is NULL!");
            return;
        }

        if (satObjects == null)
        {
            Debug.LogWarning($"[Visualizer] MoveSatellites: satObjects array is NULL!");
            return;
        }

        // Debug log first frame positions
        if (!positionLoggedOnce && pos.Length > 0)
        {
            Debug.Log($"[Visualizer] ===== FIRST UPDATE =====");
            Debug.Log($"[Visualizer] Position array length: {pos.Length}, satObjects length: {satObjects.Length}");
            Debug.Log($"[Visualizer] Sat[0] position: {pos[0]}, magnitude: {pos[0].magnitude:F2}");
            if (pos.Length > 1)
                Debug.Log($"[Visualizer] Sat[1] position: {pos[1]}, magnitude: {pos[1].magnitude:F2}");
            Debug.Log($"[Visualizer] OrbitRadius: {driver.OrbitRadius:F2}");

            if (satObjects[0] != null)
            {
                Debug.Log($"[Visualizer] Sat[0] GameObject: {satObjects[0].name}, active: {satObjects[0].activeSelf}, activeInHierarchy: {satObjects[0].activeInHierarchy}");
                Debug.Log($"[Visualizer] Sat[0] scale: {satObjects[0].transform.localScale}");
            }

            positionLoggedOnce = true;
        }

        for (int i = 0; i < satObjects.Length && i < pos.Length; i++)
        {
            if (satObjects[i] != null)
            {
                satObjects[i].transform.position = pos[i];
                satObjects[i].transform.LookAt(Vector3.zero); // always face Earth
            }
            else
            {
                Debug.LogError($"[Visualizer] Satellite {i} is null during MoveSatellites!");
            }
        }

        updateCount++;
        if (updateCount == 10) // Log again after 10 updates
        {
            Debug.Log($"[Visualizer] After 10 updates - Sat[0] is at position: {satObjects[0]?.transform.position}");
        }
    }

    void DrawLinks()
    {
        var snap = driver.CurrentSnapshot;
        if (snap == null) return;

        var pos        = driver.SatellitePositions;
        var routeEdges = BuildEdgeSet(snap.route.path);
        int poolIdx    = 0;

        foreach (var link in snap.links)
        {
            if (poolIdx >= linePool.Count) break;
            if (link.u >= pos.Length || link.v >= pos.Length) continue;

            var lr     = linePool[poolIdx++];
            lr.enabled = true;
            lr.SetPosition(0, pos[link.u]);
            lr.SetPosition(1, pos[link.v]);

            bool isRoute = routeEdges.Contains(link.Key);
            StyleLink(lr, link, isRoute);
        }

        // Hide unused pool entries
        for (int i = poolIdx; i < linePool.Count; i++)
            linePool[i].enabled = false;
    }

    void StyleLink(LineRenderer lr, SatelliteLink link, bool isRoute)
    {
        if (!link.active)
        {
            lr.startColor = lr.endColor = colorDead;
            lr.widthMultiplier = widthDead;
            lr.material    = lineMat;
            lr.sortingOrder = linesAlwaysBehind ? -10 : -1;
        }
        else if (isRoute)
        {
            lr.startColor = lr.endColor = colorRoute;
            lr.widthMultiplier = widthRoute;
            lr.material    = routeMat;
            lr.sortingOrder = linesAlwaysBehind ? -8 : 2; // Route slightly in front of normal lines
        }
        else
        {
            Color c = LoadColor(link.load);
            lr.startColor = lr.endColor = c;
            lr.widthMultiplier = widthNormal * (1f + link.load * 0.8f);
            lr.material    = lineMat;
            lr.sortingOrder = linesAlwaysBehind ? -9 : 0;
        }
    }

    Color LoadColor(float t)
    {
        if (t < 0.5f) return Color.Lerp(colorIdle, colorBusy, t * 2f);
        return Color.Lerp(colorBusy, colorCrit, (t - 0.5f) * 2f);
    }

    static HashSet<string> BuildEdgeSet(List<int> path)
    {
        var set = new HashSet<string>();
        if (path == null) return set;
        for (int i = 0; i < path.Count - 1; i++)
            set.Add(SatelliteLink.LinkKey(path[i], path[i + 1]));
        return set;
    }
}