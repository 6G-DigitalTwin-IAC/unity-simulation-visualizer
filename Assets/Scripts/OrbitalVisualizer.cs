using UnityEngine;
using System.Collections.Generic;

public class OrbitalVisualizer : MonoBehaviour
{
    [Header("Veri")]
    public TextAsset jsonFile;

    [Header("Prefablar")]
    public GameObject satPrefab;
    public GameObject groundStationPrefab;
    public GameObject linkPrefab; // İçinde sadece LineRenderer olsun, ekstra script'e gerek yok
    public GameObject earthPrefab; // Yarı saydam mavi küre

    [Header("Görsel Ayarlar")]
    public Color normalLineColor = new Color(1f, 0f, 0f, 0.3f); // Matplotlib'deki şeffaf kırmızı
    public Color pathLineColor = new Color(0.8f, 0f, 0f, 1f); // Kalın yol
    public Color groundColor = Color.green;

    private UnityExportData data;
    private int currentFrame = 0;
    private float timer = 0f;
    public float frameRate = 0.1f; // Saniyede 10 kare (Hızı buradan ayarla)
    public bool isPlaying = true;

    // Sahne Objeleri
    private GameObject[] satObjects;
    private GameObject objA;
    private GameObject objB;
    
    // Obje Havuzu (Performans için her saniye yüzlerce obje yaratıp silmek yerine, havuz kullanacağız)
    private List<LineRenderer> linePool = new List<LineRenderer>();

    void Start()
    {
        LoadAndBuild();
    }

    void LoadAndBuild()
    {
        if(jsonFile == null) { Debug.LogError("JSON yok!"); return; }
        
        // JSON'ı parse et (Newtonsoft Json kullanman önerilir ama Unity'nin kendi JsonUtility'si de basit listeleri çözer)
        data = Newtonsoft.Json.JsonConvert.DeserializeObject<UnityExportData>(jsonFile.text);
        
        if(data == null || data.frames.Count == 0) return;

        // 1. Dünyayı Yarat
        if (earthPrefab)
        {
            GameObject earth = Instantiate(earthPrefab, Vector3.zero, Quaternion.identity);
            earth.transform.localScale = Vector3.one * (data.earth_radius * 2f);
        }

        // 2. Yer İstasyonlarını Yarat (A ve B)
        Vector3 posA = new Vector3(data.ground_A[0], data.ground_A[1], data.ground_A[2]);
        Vector3 posB = new Vector3(data.ground_B[0], data.ground_B[1], data.ground_B[2]);
        
        objA = Instantiate(groundStationPrefab, posA, Quaternion.identity);
        objA.name = "A";
        objB = Instantiate(groundStationPrefab, posB, Quaternion.identity);
        objB.name = "B";

        // 3. Uyduları Yarat (İlk frame koordinatlarında)
        int numSats = data.frames[0].sats.Count;
        satObjects = new GameObject[numSats];
        
        for (int i = 0; i < numSats; i++)
        {
            float[] pos = data.frames[0].sats[i];
            satObjects[i] = Instantiate(satPrefab, new Vector3(pos[0], pos[1], pos[2]), Quaternion.identity, transform);
            satObjects[i].name = i.ToString();
        }

        // 4. Havuzu doldur (Tahmini 2000 çizgi lazım olabilir, fazlası göz çıkarmaz)
        for (int i = 0; i < 1500; i++)
        {
            GameObject lineObj = Instantiate(linkPrefab, transform);
            LineRenderer lr = lineObj.GetComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.enabled = false;
            linePool.Add(lr);
        }
    }

    void Update()
    {
        if (!isPlaying || data == null) return;

        timer += Time.deltaTime;
        if (timer >= frameRate)
        {
            timer = 0f;
            currentFrame++;
            if (currentFrame >= data.frames.Count) currentFrame = 0; // Başa sar
            
            RenderFrame(data.frames[currentFrame]);
        }
    }

    void RenderFrame(FrameData frame)
    {
        // 1. Uyduların Yerini Güncelle (Yörüngede dönüyorlar)
        for (int i = 0; i < satObjects.Length; i++)
        {
            float[] pos = frame.sats[i];
            satObjects[i].transform.position = new Vector3(pos[0], pos[1], pos[2]);
        }

        // Rotayı hızlı aramak için Hashset'e al
        HashSet<string> routeEdges = new HashSet<string>();
        if (frame.path != null && frame.path.Count > 1)
        {
            for (int i = 0; i < frame.path.Count - 1; i++)
            {
                // Örn: "A-15" veya "15-A"
                string u = frame.path[i];
                string v = frame.path[i+1];
                routeEdges.Add(u+"-"+v);
                routeEdges.Add(v+"-"+u);
            }
        }

        // 2. Çizgileri Çiz (LineRenderer Havuzundan kullan)
        int lineIndex = 0;
        foreach (var link in frame.links)
        {
            if (lineIndex >= linePool.Count) break; // Havuz dolduysa çizme

            Vector3 startPos = GetNodePosition(link.u);
            Vector3 endPos = GetNodePosition(link.v);

            LineRenderer lr = linePool[lineIndex];
            lr.enabled = true;
            lr.SetPosition(0, startPos);
            lr.SetPosition(1, endPos);

            bool isRoute = routeEdges.Contains(link.u + "-" + link.v);

            if (isRoute)
            {
                // Kalın Kırmızı Rota (Resimdeki gibi)
                lr.startColor = pathLineColor;
                lr.endColor = pathLineColor;
                lr.startWidth = 0.15f;
                lr.endWidth = 0.15f;
                lr.sortingOrder = 1; // Önde çıksın
            }
            else
            {
                // Normal Ağ (Şeffaf Kırmızı)
                lr.startColor = normalLineColor;
                lr.endColor = normalLineColor;
                // Yüke göre kalınlık ver
                lr.startWidth = 0.02f + (link.load * 0.05f);
                lr.endWidth = 0.02f + (link.load * 0.05f);
                lr.sortingOrder = 0;
            }

            lineIndex++;
        }

        // 3. Kullanılmayan eski çizgileri gizle
        for (int i = lineIndex; i < linePool.Count; i++)
        {
            linePool[i].enabled = false;
        }
    }

    Vector3 GetNodePosition(string nodeID)
    {
        if (nodeID == "A") return objA.transform.position;
        if (nodeID == "B") return objB.transform.position;
        
        if (int.TryParse(nodeID, out int id) && id >= 0 && id < satObjects.Length)
        {
            return satObjects[id].transform.position;
        }
        return Vector3.zero;
    }
}