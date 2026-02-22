using UnityEngine;

public class LinkVisualizer : MonoBehaviour
{
    public LineRenderer lineRenderer;
    
    private Color normalColor = Color.red; // Görseldeki gibi ağın ana rengi kırmızı
    private Color routeColor = new Color(0.6f, 0.2f, 0.0f); // Rota için kahverengi/kalın kırmızı
    private Color disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.1f); 

    void Awake()
    {
        if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.useWorldSpace = true;
    }

    public void Setup(Node nodeA, Node nodeB)
    {
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, nodeA.transform.position);
        lineRenderer.SetPosition(1, nodeB.transform.position);
    }

    // YENİ: isRoute parametresi eklendi
    public void UpdateVisuals(bool isActive, float load, bool isRoute)
    {
        if (!isActive)
        {
            lineRenderer.startColor = disabledColor;
            lineRenderer.endColor = disabledColor;
            lineRenderer.widthMultiplier = 0.02f; // Pasifleri çok incelt
            lineRenderer.sortingOrder = -1; // Arkaya at
        }
        else if (isRoute)
        {
            // EĞER BU LİNK SEÇİLİ ROTA ÜZERİNDEYSE (Görseldeki gibi kalın yap)
            lineRenderer.startColor = routeColor;
            lineRenderer.endColor = routeColor;
            lineRenderer.widthMultiplier = 0.35f; // Çok Kalın
            lineRenderer.sortingOrder = 1; // En öne al
        }
        else
        {
            // Normal aktif link (Görseldeki gibi ince kırmızı)
            lineRenderer.startColor = normalColor;
            lineRenderer.endColor = normalColor;
            lineRenderer.widthMultiplier = 0.05f + (load * 0.05f); 
            lineRenderer.sortingOrder = 0;
        }
    }
}