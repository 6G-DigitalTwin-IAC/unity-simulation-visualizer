using UnityEngine;

public class Node : MonoBehaviour
{
    public int id;
    public int planeIndex;
    public int satIndex;

    // Node üzerine ID'sini yazdırmak istersen TextMeshPro kullanabilirsin
    // public TMPro.TextMeshPro label; 

    public void Setup(int _id, int _plane, int _sat)
    {
        id = _id;
        planeIndex = _plane;
        satIndex = _sat;
        gameObject.name = $"Sat_{id}";
    }
}