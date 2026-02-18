using System.Collections.Generic;

// Bu dosya sadece veri tiplerini tanimlar. Sahneye atilmaz!

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
    public int u; // Start Node
    public int v; // End Node
    public bool active;
    public float load;
}

[System.Serializable]
public class RouteData
{
    public int source;
    public int target;
    public List<int> path;
    public bool is_adaptive;
}