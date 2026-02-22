using System.Collections.Generic;

[System.Serializable]
public class UnityExportData
{
    public float earth_radius;
    public float[] ground_A;
    public float[] ground_B;
    public List<FrameData> frames;
}

[System.Serializable]
public class FrameData
{
    public int frame_id;
    public List<float[]> sats; // Index = Sat ID, array[0,1,2] = XYZ
    public List<LinkData> links;
    public List<string> path; // Örn: ["A", "14", "25", "B"]
}

[System.Serializable]
public class LinkData
{
    public string u; // Node 1 (Sayi veya "A"/"B")
    public string v; // Node 2
    public float load; // Tıkanıklık
}