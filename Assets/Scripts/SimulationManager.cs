using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class SimulationManager : MonoBehaviour
{
    [Header("Ayarlar")]
    public TextAsset jsonFile; // JSON dosyasını buraya sürükleyeceksin
    public GameObject nodePrefab; // Küre prefabı
    public GameObject linkPrefab; // LineRenderer prefabı
    
    [Header("Topoloji Parametreleri")]
    public float radius = 10f; // Yörünge yarıçapı
    public float planeSpacing = 2f; // Düzlemler arası mesafe (Görsellik için)

    [Header("UI Referansları")]
    // Buraya TextMeshPro referansları gelecek (Örn: public TMPro.TMP_Text delayText;)

    // Veriler
    private SimulationData simData;
    private Dictionary<int, Node> nodeMap = new Dictionary<int, Node>();
    private Dictionary<string, LinkVisualizer> linkMap = new Dictionary<string, LinkVisualizer>();
    
    private int currentStepIndex = 0;

    void Start()
    {
        LoadData();
        BuildTopology();
        ShowStep(0); // İlk adımı göster
    }

    void LoadData()
    {
        // JSON dosyasını parse et
        simData = JsonUtility.FromJson<SimulationData>(jsonFile.text);
        Debug.Log($"Veri Yüklendi: {simData.steps.Count} adım var.");
    }

    void BuildTopology()
    {
        // 1. Uyduları (Nodes) Oluştur
        int planes = simData.total_planes;
        int sats = simData.sats_per_plane;

        for (int p = 0; p < planes; p++)
        {
            for (int s = 0; s < sats; s++)
            {
                int id = p * sats + s;
                
                // Basit bir silindirik/küresel dizilim matematiği
                float angle = (360f / sats) * s * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                float y = (p - (planes / 2f)) * planeSpacing; // Düzlemleri Y ekseninde ayır

                GameObject nodeObj = Instantiate(nodePrefab, new Vector3(x, y, z), Quaternion.identity, transform);
                Node nodeScript = nodeObj.GetComponent<Node>();
                nodeScript.Setup(id, p, s);
                
                nodeMap.Add(id, nodeScript);
            }
        }

        // 2. Linkleri (Edges) baştan oluşturmak yerine, ilk adımdaki link listesine göre havuz oluşturuyoruz
        // Not: Gerçek topolojiyi bilmek için tüm olası linkleri baştan oluşturmak daha iyidir ama
        // şimdilik dinamik olarak gelen listedekileri oluşturacağız.
    }

    public void ShowStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= simData.steps.Count) return;
        currentStepIndex = stepIndex;

        StepData step = simData.steps[currentStepIndex];

        // --- A. Linkleri Güncelle ---
        foreach (var linkInfo in step.links)
        {
            // Benzersiz bir key oluştur: "0-1" veya "1-0" aynı linktir
            int min = Mathf.Min(linkInfo.u, linkInfo.v);
            int max = Mathf.Max(linkInfo.u, linkInfo.v);
            string key = $"{min}-{max}";

            LinkVisualizer visualizer;

            // Eğer bu link sahnede yoksa oluştur
            if (!linkMap.TryGetValue(key, out visualizer))
            {
                GameObject linkObj = Instantiate(linkPrefab, transform);
                visualizer = linkObj.GetComponent<LinkVisualizer>();
                visualizer.Setup(nodeMap[min], nodeMap[max]);
                linkMap.Add(key, visualizer);
            }

            // Rengini ve durumunu güncelle
            visualizer.UpdateVisuals(linkInfo.active, linkInfo.load);
        }

        // --- B. UI Güncelle (Metrikler) ---
        Debug.Log($"Step: {step.step_id} | Delay: {step.metrics.avg_delay}");
        // uiText.text = ...

        // --- C. Rotayı Çiz (Opsiyonel) ---
        DrawRoute(step.route);
    }

    void DrawRoute(RouteData route)
    {
        // Burada seçilen path üzerindeki linkleri parlatabilirsin
        // Örn: LinkVisualizer'a "Highlight()" fonksiyonu ekleyip çağırabilirsin.
    }

    // Butonlar için fonksiyonlar
    public void NextStep() => ShowStep(currentStepIndex + 1);
    public void PrevStep() => ShowStep(currentStepIndex - 1);
}