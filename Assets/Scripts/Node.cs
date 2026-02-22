using UnityEngine;

public class Node : MonoBehaviour
{
    public int id;
    public void Setup(int _id, int _plane, int _sat)
    {
        id = _id;
        gameObject.name = "Uydu_" + id;
    }
}