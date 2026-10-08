using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Toolkit UI kecil: palet warna, sprite rounded 9-slice (dibuat prosedural), canvas bersama,
// serta helper pembuat Image/Text. Semua UI Praktikum 06 dibangun lewat kode memakai ini.
public static class UIKit
{
    // ---------- Palet ----------
    public static readonly Color PanelColor = new Color(0.055f, 0.07f, 0.11f, 0.90f);
    public static readonly Color RowColor = new Color(1f, 1f, 1f, 0.06f);
    public static readonly Color TrackColor = new Color(0f, 0f, 0f, 0.50f);
    public static readonly Color TextColor = new Color(0.94f, 0.96f, 1f, 1f);
    public static readonly Color DimTextColor = new Color(0.62f, 0.69f, 0.80f, 1f);
    public static readonly Color DarkTextColor = new Color(0.06f, 0.07f, 0.11f, 1f);

    public static readonly Color GreenColor = new Color(0.30f, 0.85f, 0.45f, 1f);
    public static readonly Color YellowColor = new Color(1f, 0.85f, 0.25f, 1f);
    public static readonly Color RedColor = new Color(0.95f, 0.28f, 0.30f, 1f);

    public static Color ActionColor(string action)
    {
        switch (action)
        {
            case "PATROL": return GreenColor;
            case "CHASE": return new Color(1f, 0.62f, 0.20f, 1f);
            case "ATTACK": return RedColor;
            case "FLEE": return new Color(1f, 0.90f, 0.30f, 1f);
            case "SEARCH": return new Color(0.72f, 0.45f, 1f, 1f);
            default: return new Color(0.55f, 0.60f, 0.70f, 1f);
        }
    }

    public static Color HealthColor(float percent)
    {
        percent = Mathf.Clamp01(percent);
        return percent > 0.5f
            ? Color.Lerp(YellowColor, GreenColor, (percent - 0.5f) * 2f)
            : Color.Lerp(RedColor, YellowColor, percent * 2f);
    }

    // ---------- Resource cache ----------
    private static readonly Dictionary<int, Sprite> roundedCache = new Dictionary<int, Sprite>();
    private static Sprite vignette;
    private static Font font;
    private static Canvas sharedCanvas;

    public static Font Font
    {
        get
        {
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    // Sprite rounded rect untuk Image.Type.Sliced (border = radius).
    public static Sprite Rounded(int radius)
    {
        if (radius <= 0)
            return null;

        if (roundedCache.TryGetValue(radius, out Sprite cached) && cached != null)
            return cached;

        int size = radius * 4;
        float half = size * 0.5f;
        float inner = half - radius;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - half) - inner;
                float dy = Mathf.Abs(y + 0.5f - half) - inner;
                float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f))
                                + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;

                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - outside));
            }
        }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.SetPixels(pixels);
        tex.Apply();

        Sprite sprite = Sprite.Create(
            tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        sprite.hideFlags = HideFlags.HideAndDontSave;

        roundedCache[radius] = sprite;
        return sprite;
    }

    // Gradien radial transparan di tengah -> merah di tepi (efek kena damage).
    public static Sprite Vignette()
    {
        if (vignette != null)
            return vignette;

        const int size = 128;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float a = Mathf.SmoothStep(0.65f, 1.45f, d);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.SetPixels(pixels);
        tex.Apply();

        vignette = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        vignette.hideFlags = HideFlags.HideAndDontSave;
        return vignette;
    }

    // ---------- Canvas ----------
    public static Canvas SharedCanvas
    {
        get
        {
            if (sharedCanvas == null)
                sharedCanvas = CreateCanvas("UI_Overlay", 0);
            return sharedCanvas;
        }
    }

    public static RectTransform SharedRoot => (RectTransform)SharedCanvas.transform;

    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        GameObject go = new GameObject(name);

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    // ---------- Elemen ----------
    public static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static Image NewImage(Transform parent, string name, Color color, int radius = 0)
    {
        RectTransform rect = Rect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        if (radius > 0)
        {
            image.sprite = Rounded(radius);
            image.type = Image.Type.Sliced;
        }

        return image;
    }

    public static Text NewText(
        Transform parent, string name, string content, int size, Color color,
        TextAnchor align, FontStyle style = FontStyle.Normal, bool shadow = false)
    {
        RectTransform rect = Rect(name, parent);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = Font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.supportRichText = false;

        if (shadow)
        {
            Shadow s = rect.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.65f);
            s.effectDistance = new Vector2(1f, -1f);
        }

        return text;
    }

    // Anchor di pojok kiri-atas parent; x ke kanan, y ke bawah.
    public static void TopLeft(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
    }

    // Anchor di pojok kanan-atas parent; x ke kiri, y ke bawah.
    public static void TopRight(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-x, -y);
        r.sizeDelta = new Vector2(w, h);
    }

    public static void Stretch(RectTransform r, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(left, bottom);
        r.offsetMax = new Vector2(-right, -top);
    }

    public static RectTransform Card(Transform parent, string name, float x, float y, float w, float h, int radius = 16)
    {
        Image image = NewImage(parent, name, PanelColor, radius);
        TopLeft(image.rectTransform, x, y, w, h);
        return image.rectTransform;
    }

    // Pill judul di tengah-atas layar.
    public static void TitlePill(Transform parent, string content)
    {
        float width = Mathf.Max(260f, 36f + content.Length * 7.2f);

        Image bg = NewImage(parent, "TitlePill", PanelColor, 18);
        RectTransform r = bg.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(0f, -22f);
        r.sizeDelta = new Vector2(width, 36f);

        Text t = NewText(r, "Text", content, 16, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold, true);
        Stretch(t.rectTransform);
    }

    // Baris keycap kontrol di kanan-atas layar.
    public static void ControlsBar(Transform parent, string[] keys, string[] labels)
    {
        float[] widths = new float[keys.Length];
        float total = 20f;

        for (int i = 0; i < keys.Length; i++)
        {
            float capW = Mathf.Max(30f, 14f + keys[i].Length * 11f);
            widths[i] = capW + 8f + labels[i].Length * 8f + 18f;
            total += widths[i];
        }

        Image bg = NewImage(parent, "ControlsBar", PanelColor, 16);
        RectTransform root = bg.rectTransform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = new Vector2(-24f, -24f);
        root.sizeDelta = new Vector2(total, 48f);

        float x = 14f;

        for (int i = 0; i < keys.Length; i++)
        {
            float capW = Mathf.Max(30f, 14f + keys[i].Length * 11f);

            Image cap = NewImage(root, "Key_" + keys[i], new Color(1f, 1f, 1f, 0.16f), 7);
            TopLeft(cap.rectTransform, x, 9f, capW, 30f);

            Text keyText = NewText(cap.rectTransform, "Text", keys[i], 15, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(keyText.rectTransform);

            Text label = NewText(root, "Label_" + keys[i], labels[i], 14, DimTextColor, TextAnchor.MiddleLeft);
            TopLeft(label.rectTransform, x + capW + 8f, 9f, labels[i].Length * 8f + 10f, 30f);

            x += widths[i];
        }
    }
}

// Bar dengan lebar terisi yang bisa di-clip tanpa merusak sudut rounded (RectMask2D).
public class FillBar
{
    public readonly RectTransform Mask;
    public readonly Image Fill;

    public FillBar(Transform parent, string name, float width, int radius, Color color)
    {
        Mask = UIKit.Rect(name + "Mask", parent);
        Mask.gameObject.AddComponent<RectMask2D>();
        Mask.anchorMin = Vector2.zero;
        Mask.anchorMax = Vector2.one;
        Mask.offsetMin = Mask.offsetMax = Vector2.zero;

        Fill = UIKit.NewImage(Mask, name, color, radius);
        RectTransform r = Fill.rectTransform;
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(width, 0f);
    }

    public void Set(float percent)
    {
        percent = Mathf.Clamp01(percent);

        bool visible = percent > 0.001f;
        if (Mask.gameObject.activeSelf != visible)
            Mask.gameObject.SetActive(visible);

        Mask.anchorMax = new Vector2(percent, 1f);
    }
}

// Health bar: warna gradien, jejak putih (damage trail), flash saat terkena damage, teks "cur / max".
public class BarView
{
    public readonly RectTransform Root;

    private readonly RectTransform inner;
    private readonly float innerWidth;
    private readonly FillBar fill;
    private readonly FillBar ghost;
    private readonly Text valueText;

    private float shown = 1f;
    private float ghostValue = 1f;
    private float hold;
    private float flash;
    private int lastCurrent = -1;
    private int lastMax = -1;

    public BarView(Transform parent, string name, float width, float height, int fontSize)
    {
        Root = UIKit.Rect(name, parent);
        Root.anchorMin = Root.anchorMax = Root.pivot = new Vector2(0f, 1f);
        Root.sizeDelta = new Vector2(width, height);

        int frameRadius = Mathf.RoundToInt(height * 0.5f);
        Image frame = UIKit.NewImage(Root, "Frame", UIKit.TrackColor, frameRadius);
        UIKit.Stretch(frame.rectTransform);

        inner = UIKit.Rect("Inner", Root);
        UIKit.Stretch(inner, 2f, 2f, 2f, 2f);

        innerWidth = width - 4f;
        float innerHeight = height - 4f;
        int radius = Mathf.Max(2, Mathf.RoundToInt(innerHeight * 0.5f));

        ghost = new FillBar(inner, "Ghost", innerWidth, radius, new Color(1f, 1f, 1f, 0.85f));
        fill = new FillBar(inner, "Fill", innerWidth, radius, UIKit.GreenColor);

        // Kilau tipis di bagian atas bar.
        Image gloss = UIKit.NewImage(fill.Fill.transform, "Gloss", new Color(1f, 1f, 1f, 0.18f), Mathf.Max(2, radius - 1));
        RectTransform g = gloss.rectTransform;
        g.anchorMin = new Vector2(0f, 0.5f);
        g.anchorMax = new Vector2(1f, 1f);
        g.offsetMin = new Vector2(3f, 0f);
        g.offsetMax = new Vector2(-3f, -2f);

        valueText = UIKit.NewText(Root, "Value", "", fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
        UIKit.Stretch(valueText.rectTransform);
    }

    // Garis penanda (mis. Low Health Threshold).
    public void SetThreshold(float percent)
    {
        Image marker = UIKit.NewImage(inner, "Threshold", new Color(1f, 1f, 1f, 0.9f));
        RectTransform r = marker.rectTransform;
        r.anchorMin = new Vector2(percent, 0f);
        r.anchorMax = new Vector2(percent, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(2f, 0f);
    }

    public void Tick(int current, int max, float dt)
    {
        float target = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

        if (target < shown - 0.001f)
        {
            // Terkena damage: bar langsung turun, jejak putih menyusul pelan.
            shown = target;
            flash = 1f;
            hold = 0.45f;
        }
        else
        {
            shown = Mathf.MoveTowards(shown, target, dt * 1.2f);
        }

        if (ghostValue < shown)
        {
            ghostValue = shown;
        }
        else if (hold > 0f)
        {
            hold -= dt;
        }
        else
        {
            ghostValue = Mathf.MoveTowards(ghostValue, shown, dt * 0.5f);
        }

        flash = Mathf.Max(0f, flash - dt * 3.5f);

        fill.Set(shown);
        ghost.Set(ghostValue);
        fill.Fill.color = Color.Lerp(UIKit.HealthColor(shown), Color.white, flash * 0.7f);

        if (current != lastCurrent || max != lastMax)
        {
            lastCurrent = current;
            lastMax = max;
            valueText.text = current + " / " + max;
        }
    }
}

// Label kecil berwarna (mis. "CHASE").
public class Chip
{
    public readonly RectTransform Root;
    private readonly Image bg;
    private readonly Text text;
    private string lastText;

    public Chip(Transform parent, string name, int fontSize, int radius)
    {
        bg = UIKit.NewImage(parent, name, UIKit.GreenColor, radius);
        Root = bg.rectTransform;

        text = UIKit.NewText(Root, "Text", "", fontSize, UIKit.DarkTextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        UIKit.Stretch(text.rectTransform);
    }

    public void Set(string content, Color color)
    {
        if (content != lastText)
        {
            lastText = content;
            text.text = content;
        }

        bg.color = color;
    }
}

// Deretan bar skor (Utility AI). Baris dengan skor yang dipilih disorot.
public class ScoreBars
{
    private readonly string[] keys;
    private readonly Image[] rowBg;
    private readonly Text[] nameText;
    private readonly Text[] valueText;
    private readonly FillBar[] bars;
    private readonly Color[] colors;

    public ScoreBars(Transform parent, string[] labels, float x, float y, float width, float rowHeight)
    {
        int n = labels.Length;
        keys = new string[n];
        rowBg = new Image[n];
        nameText = new Text[n];
        valueText = new Text[n];
        bars = new FillBar[n];
        colors = new Color[n];

        float trackWidth = width - 86f - 54f;
        float h = rowHeight - 4f;

        for (int i = 0; i < n; i++)
        {
            float ry = y + i * rowHeight;
            keys[i] = labels[i].ToUpperInvariant();
            colors[i] = UIKit.ActionColor(keys[i]);

            rowBg[i] = UIKit.NewImage(parent, "ScoreRow_" + labels[i], UIKit.RowColor, 8);
            UIKit.TopLeft(rowBg[i].rectTransform, x, ry, width, h);

            nameText[i] = UIKit.NewText(parent, "Name", labels[i], 13, UIKit.DimTextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.TopLeft(nameText[i].rectTransform, x + 12f, ry, 70f, h);

            Image track = UIKit.NewImage(parent, "Track", UIKit.TrackColor, 4);
            UIKit.TopLeft(track.rectTransform, x + 86f, ry + (h - 8f) * 0.5f, trackWidth, 8f);
            bars[i] = new FillBar(track.transform, "Fill", trackWidth, 4, colors[i]);

            valueText[i] = UIKit.NewText(parent, "Value", "0.00", 13, UIKit.DimTextColor, TextAnchor.MiddleRight, FontStyle.Bold);
            UIKit.TopLeft(valueText[i].rectTransform, x + width - 56f, ry, 46f, h);
        }
    }

    public void Set(ActionScore[] scores, string activeAction)
    {
        for (int i = 0; i < keys.Length && i < scores.Length; i++)
        {
            bool active = keys[i] == activeAction;
            Color c = colors[i];

            bars[i].Set(scores[i].value);
            bars[i].Fill.color = active ? c : new Color(c.r, c.g, c.b, 0.55f);

            nameText[i].color = active ? UIKit.TextColor : UIKit.DimTextColor;
            valueText[i].color = active ? UIKit.TextColor : UIKit.DimTextColor;
            valueText[i].text = scores[i].value.ToString("F2");

            rowBg[i].color = active ? new Color(c.r, c.g, c.b, 0.22f) : UIKit.RowColor;
        }
    }
}
