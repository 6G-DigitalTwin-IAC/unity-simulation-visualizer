using UnityEngine;

public class LinkVisualizer : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public int startNodeID;
    public int endNodeID;

    // Renk skalası: Yeşil (Boş) -> Sarı -> Kırmızı (Dolu)
    public Color emptyColor = Color.green;
    public Color fullColor = Color.red;
    public Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.2f); // Gri ve şeffaf

    public void Setup(Node nodeA, Node nodeB)
    {
        startNodeID = nodeA.id;
        endNodeID = nodeB.id;
        
        // Çizgiyi iki node arasında başlat
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, nodeA.transform.position);
        lineRenderer.SetPosition(1, nodeB.transform.position);
    }

    public void UpdateVisuals(bool isActive, float load)
    {
        if (!isActive)
        {
            // Link koptuysa gri yap veya incelt
            lineRenderer.startColor = disabledColor;
            lineRenderer.endColor = disabledColor;
            lineRenderer.widthMultiplier = 0.05f; 
        }
        else
        {
            // Yüke göre renk değiştir (Lerp)
            Color targetColor = Color.Lerp(emptyColor, fullColor, load);
            lineRenderer.startColor = targetColor;
            lineRenderer.endColor = targetColor;
            lineRenderer.widthMultiplier = 0.1f + (load * 0.1f); // Yük arttıkça kalınlaşsın
        }
    }
}