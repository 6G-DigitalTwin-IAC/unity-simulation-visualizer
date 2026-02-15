using System.Collections.Generic;

[System.Serializable]
public class SimulationData
{
    public int total_planes;
    public int sats_per_plane;
    public List<StepData> steps;
}

[System.Serializable]
public class StepData
{
    public int step_id;
    public StepMetrics metrics;
    public List<LinkData> links;
    public RouteData route;
}

[System.Serializable]
public class StepMetrics
{
    public float avg_delay;
    public int reroute_count;
    public float stability_score;
}

[System.Serializable]
public class LinkData
{
    public int u; // Başlangıç Node ID
    public int v; // Bitiş Node ID
    public bool active;
    public float load; // 0.0 ile 1.0 arası
}

[System.Serializable]
public class RouteData
{
    public int source;
    public int target;
    public List<int> path; // İzlenen yolun node ID'leri: [2, 3, 9, 15]
    public bool is_adaptive;
}