using UnityEngine;
using UnityEngine.UI;

// Teks yang naik dan memudar lalu menghilang sendiri (angka damage).
public class FloatingLabel : MonoBehaviour
{
    private const float Lifetime = 0.9f;
    private const float RiseSpeed = 70f;

    private RectTransform rect;
    private Text text;
    private Color baseColor;
    private float age;

    // anchoredPosition relatif terhadap pusat canvas overlay bersama.
    public static void Spawn(Vector2 anchoredPosition, string content, Color color, int fontSize = 34)
    {
        Text t = UIKit.NewText(UIKit.SharedRoot, "FloatingText", content, fontSize, color,
            TextAnchor.MiddleCenter, FontStyle.Bold, true);

        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = anchoredPosition;
        r.sizeDelta = new Vector2(160f, 50f);

        FloatingLabel label = t.gameObject.AddComponent<FloatingLabel>();
        label.rect = r;
        label.text = t;
        label.baseColor = color;
    }

    // Titik layar -> posisi lokal canvas.
    public static Vector2 ToCanvas(Vector3 screenPoint)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(UIKit.SharedRoot, screenPoint, null, out Vector2 local);
        return local;
    }

    private void Update()
    {
        age += Time.deltaTime;

        rect.anchoredPosition += Vector2.up * (RiseSpeed * Time.deltaTime);

        float t = Mathf.Clamp01(age / Lifetime);
        Color c = baseColor;
        c.a = 1f - t * t;
        text.color = c;

        if (age >= Lifetime)
            Destroy(gameObject);
    }
}
