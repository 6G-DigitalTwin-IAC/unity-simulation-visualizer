using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SimulationManager : MonoBehaviour
{
    [Header("Gerekli Dosyalar")]
    public TextAsset jsonFile; // test_data.json buraya
    public GameObject nodePrefab; 
    public GameObject linkPrefab; // Üzerinde LinkVisualizer olan prefab

    [Header("Otomatik Bulunacak UI")]
    public Button nextBtn;
    public Button prevBtn;
    
    // Veriler
    private SimulationData simData;
    private Dictionary<int, Node> nodeMap = new Dictionary<int, Node>();
    private Dictionary<string, LinkVisualizer> linkMap = new Dictionary<string, LinkVisualizer>();
    private int currentStepIndex = 0;

    void Start()
    {
        // Butonları bul
        if(nextBtn == null) 
            if(GameObject.Find("ButtonNext")) nextBtn = GameObject.Find("ButtonNext").GetComponent<Button>();
        if(prevBtn == null) 
            if(GameObject.Find("ButtonPrev")) prevBtn = GameObject.Find("ButtonPrev").GetComponent<Button>();

        if(nextBtn != null) nextBtn.onClick.AddListener(NextStep);
        if(prevBtn != null) prevBtn.onClick.AddListener(PrevStep);

        // İşlemleri Başlat
        LoadData();
        BuildTopology();
        ShowStep(0); 
    }

    void LoadData()
    {
        if(jsonFile == null) { Debug.LogError("JSON DOSYASI ATANMADI!"); return; }
        simData = JsonUtility.FromJson<SimulationData>(jsonFile.text);
    }

    void BuildTopology()
    {
        // 1. Uyduları (Nodes) Diz
        float radius = 10f;
        int planes = simData.total_planes;
        int sats = simData.sats_per_plane;

        for (int p = 0; p < planes; p++)
        {
            for (int s = 0; s < sats; s++)
            {
                int id = p * sats + s;
                
                
                
                // Uyduları uzayda bir küre şeklinde diziyoruz
                float phi = (p * 360f / planes) * Mathf.Deg2Rad; // Düzlem açısı
                float theta = ((s * 180f / sats) - 90f) * Mathf.Deg2Rad; // Yükseklik açısı

                float angle = (360f / planes) * p * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                float y = s * 2.0f; // Yükseklik
                
                

                GameObject nodeObj = Instantiate(nodePrefab, new Vector3(x, y, z), Quaternion.identity, transform);
                Node nodeScript = nodeObj.GetComponent<Node>();
                
                // Node scripti yoksa ekle (güvenlik için)
                if(nodeScript == null) nodeScript = nodeObj.AddComponent<Node>();
                
                nodeScript.Setup(id, p, s);
                nodeMap.Add(id, nodeScript);
            }
        }
        Debug.Log("Uydular oluşturuldu: " + nodeMap.Count);
    }

    public void ShowStep(int stepIndex)
    {
        if (simData == null || stepIndex >= simData.steps.Count) return;
        currentStepIndex = stepIndex;
        StepData step = simData.steps[currentStepIndex];

        // Linkleri çiz veya güncelle
        foreach (var linkInfo in step.links)
        {
            // ID'leri küçükten büyüğe sırala ki "0-1" ile "1-0" aynı olsun
            int min = Mathf.Min(linkInfo.u, linkInfo.v);
            int max = Mathf.Max(linkInfo.u, linkInfo.v);
            string key = min + "-" + max;

            LinkVisualizer visualizer;

            if (!linkMap.TryGetValue(key, out visualizer))
            {
                // Link yoksa oluştur
                if(nodeMap.ContainsKey(min) && nodeMap.ContainsKey(max))
                {
                    GameObject linkObj = Instantiate(linkPrefab, transform);
                    visualizer = linkObj.GetComponent<LinkVisualizer>();
                    if(visualizer == null) visualizer = linkObj.AddComponent<LinkVisualizer>();
                    
                    visualizer.Setup(nodeMap[min], nodeMap[max]);
                    linkMap.Add(key, visualizer);
                }
            }

            // Varsa güncelle
            if (linkMap.TryGetValue(key, out visualizer))
            {
                visualizer.UpdateVisuals(linkInfo.active, linkInfo.load);
            }
        }
        Debug.Log("Step " + stepIndex + " gösteriliyor.");
    }

    public void NextStep() { if(currentStepIndex < simData.steps.Count -1) ShowStep(currentStepIndex + 1); }
    public void PrevStep() { if(currentStepIndex > 0) ShowStep(currentStepIndex - 1); }
}