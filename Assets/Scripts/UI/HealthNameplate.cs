using UnityEngine;
using UnityEngine.UI;

// Nameplate di atas kepala karakter (Enemy maupun Player): nama, chip Current Action,
// dan health bar dengan damage trail. Digambar di canvas overlay sehingga ukurannya selalu
// konsisten dan tajam, mengikuti posisi karakter di layar.
public class HealthNameplate : MonoBehaviour
{
    [SerializeField] private string displayName = "";
    [SerializeField] private Color accent = Color.white;
    [SerializeField] private float heightAboveTarget = 2.7f;
    [SerializeField] private bool showAction = true;

    private const float PlateWidth = 264f;
    private const float PlateHeight = 62f;

    private IHealthSource health;
    private IEnemyBrain brain;
    private RectTransform plate;
    private RectTransform canvasRect;
    private BarView bar;
    private Chip chip;
    private Camera cam;
    private int lastHealth = -1;

    private void Start()
    {
        health = GetComponent<IHealthSource>();
        brain = health as IEnemyBrain;

        if (health == null)
        {
            Debug.LogError("[HealthNameplate] IHealthSource tidak ditemukan pada " + name);
            return;
        }

        Build();
    }

    private void Build()
    {
        canvasRect = UIKit.SharedRoot;

        string title = string.IsNullOrEmpty(displayName) ? name.ToUpperInvariant() : displayName;

        plate = UIKit.Rect(name + "_Nameplate", canvasRect);
        plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0.5f);
        plate.pivot = new Vector2(0.5f, 0f);
        plate.sizeDelta = new Vector2(PlateWidth, PlateHeight);

        // Penunjuk kecil ke arah karakter.
        Image pointer = UIKit.NewImage(plate, "Pointer", UIKit.PanelColor);
        RectTransform pr = pointer.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0f);
        pr.pivot = new Vector2(0.5f, 0.5f);
        pr.anchoredPosition = Vector2.zero;
        pr.sizeDelta = new Vector2(16f, 16f);
        pr.localRotation = Quaternion.Euler(0f, 0f, 45f);

        Image bg = UIKit.NewImage(plate, "Background", UIKit.PanelColor, 14);
        UIKit.Stretch(bg.rectTransform);

        // Garis aksen warna di sisi kiri atas.
        Image accentBar = UIKit.NewImage(plate, "Accent", accent, 2);
        UIKit.TopLeft(accentBar.rectTransform, 10f, 9f, 4f, 18f);

        Text nameText = UIKit.NewText(plate, "Name", title, 17, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold, true);
        UIKit.TopLeft(nameText.rectTransform, 22f, 6f, 150f, 24f);

        if (showAction && brain != null)
        {
            chip = new Chip(plate, "ActionChip", 12, 9);
            UIKit.TopRight(chip.Root, 10f, 8f, 92f, 20f);
        }

        bar = new BarView(plate, "HealthBar", PlateWidth - 20f, 22f, 12);
        bar.Root.anchoredPosition = new Vector2(10f, -34f);

        if (brain != null)
            bar.SetThreshold(brain.LowHealthPercent);
    }

    private void LateUpdate()
    {
        if (plate == null)
            return;

        if (cam == null)
            cam = Camera.main;

        if (cam == null)
            return;

        Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * heightAboveTarget);
        bool visible = screen.z > 0f;

        if (plate.gameObject.activeSelf != visible)
            plate.gameObject.SetActive(visible);

        if (!visible)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
        plate.anchoredPosition = local + new Vector2(0f, 10f);

        bar.Tick(health.CurrentHealth, health.MaxHealth, Time.deltaTime);

        // Angka damage melayang saat health berkurang.
        int current = health.CurrentHealth;
        if (lastHealth >= 0 && current < lastHealth)
            FloatingLabel.Spawn(plate.anchoredPosition + new Vector2(0f, PlateHeight + 24f),
                "-" + (lastHealth - current), new Color(1f, 0.92f, 0.45f));
        lastHealth = current;

        if (chip != null)
            chip.Set(brain.CurrentAction, UIKit.ActionColor(brain.CurrentAction));
    }

    private void OnDestroy()
    {
        if (plate != null)
            Destroy(plate.gameObject);
    }
}
