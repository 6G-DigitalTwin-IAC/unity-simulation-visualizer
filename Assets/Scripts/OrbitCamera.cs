using UnityEngine;
using UnityEngine.InputSystem; // YENİ İNPUT SİSTEMİ İÇİN EKLENDİ

public class OrbitCamera : MonoBehaviour
{
    [Header("Hedef ve Mesafe")]
    public Transform target; // Etrafında döneceğimiz obje

    [Tooltip("Camera distance from Earth. Adjust this to zoom in/out. Recommended: 100-200 for full constellation view")]
    public float distance = 150.0f; // Başlangıç uzaklığı (increased to see full constellation)

    public float minDistance = 10.0f; // En fazla ne kadar yakınlaşabilir
    public float maxDistance = 500.0f; // En fazla ne kadar uzaklaşabilir

    [Header("Dönüş Hızı")]
    public float xSpeed = 10.0f; // Yeni sistemde raw pixel geldiği için hassasiyet ayarlandı
    public float ySpeed = 10.0f;

    [Header("Açı Sınırları")]
    public float yMinLimit = -80f; // Kameranın altına inme sınırı
    public float yMaxLimit = 80f;  // Kameranın üstüne çıkma sınırı

    private float x = 0.0f;
    private float y = 0.0f;
    private bool targetFoundLogged = false;

    private float currentX, currentY, currentDistance;
    private float xVelocity, yVelocity, distVelocity;
    public float smoothTime = 0.12f;
    
    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;
        currentX = x;
        currentY = y;
        currentDistance = distance;
    }

    void LateUpdate()
    {
        // 1. EĞER HEDEF YOKSA SÜREKLİ ARAMAYA DEVAM ET
        if (target == null)
        {
            // Önce Clone'u ara, yoksa normalini ara, fallback'i de ara
            GameObject earthObj = GameObject.Find("Earth(Clone)");
            if (earthObj == null) earthObj = GameObject.Find("Earth");
            if (earthObj == null) earthObj = GameObject.Find("Earth (Fallback)");
            
            if (earthObj != null)
            {
                target = earthObj.transform;
            }
            else
            {
                return; // Hedef yoksa kodu aşağı doğru çalıştırma
            }
        }

        // Hedef bulunduğunda (sadece 1 kere) konsola yazdır
        if (target != null && !targetFoundLogged)
        {
            Debug.Log("✅ Kamera hedefe kilitlendi: " + target.name);
            targetFoundLogged = true;
        }

        // 2. HEDEF BULUNDUYSA KAMERAYI ÇEVİR
        if (target != null)
        {
            // Yeni sistemde önce farenin bağlı olup olmadığını kontrol ediyoruz
            if (Mouse.current != null)
            {
                // Sadece SAĞ fare tuşuna basılı tutuluyorsa dön (sol tuş kaldırıldı)
                if (Mouse.current.rightButton.isPressed)
                {
                    // Yeni sistem "delta" değeri ile farenin piksellerdeki değişimini verir
                    x += Mouse.current.delta.x.ReadValue() * xSpeed * 0.02f;
                    y -= Mouse.current.delta.y.ReadValue() * ySpeed * 0.02f;
                    y = ClampAngle(y, yMinLimit, yMaxLimit);

                    currentX        = Mathf.SmoothDamp(currentX, x, ref xVelocity, smoothTime);
                    currentY        = Mathf.SmoothDamp(currentY, y, ref yVelocity, smoothTime);
                    currentDistance = Mathf.SmoothDamp(currentDistance, distance, ref distVelocity, smoothTime);

                }

                // Fare tekerleği ile Zoom In/Out
                float scroll = Mouse.current.scroll.y.ReadValue();
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    // Yeni input sisteminde scroll değerleri -120 ile +120 civarında gelir.
                    // Bu yüzden hızı 120'ye bölerek yumuşattık.
                    distance -= (scroll / 120f) * 10f; 
                    distance = Mathf.Clamp(distance, minDistance, maxDistance);
                }
            }

            // Yeni pozisyon ve açıyı hesapla ve uygula
            Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
            Vector3 position    = rotation * new Vector3(0f, 0f, -currentDistance) + target.position;
            transform.rotation  = rotation;
            transform.position  = position;
        }
    }

    // Açıyı limitleyen matematiksel yardımcı fonksiyon
    public static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F) angle += 360F;
        if (angle > 360F) angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }

    /// <summary>
    /// Set camera distance from Earth. Call this to programmatically adjust zoom.
    /// </summary>
    public void SetDistance(float newDistance)
    {
        distance = Mathf.Clamp(newDistance, minDistance, maxDistance);
        currentDistance = distance;
    }
}
