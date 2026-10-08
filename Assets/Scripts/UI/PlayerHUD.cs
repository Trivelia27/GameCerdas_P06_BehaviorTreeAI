using UnityEngine;
using UnityEngine.UI;

// HUD health Player: kartu di tengah-bawah layar dengan health bar besar, efek vignette merah
// saat terkena damage, dan overlay "DEFEATED" saat health habis.
public class PlayerHUD : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;

    private BarView bar;
    private Image vignette;
    private float vignetteAlpha;
    private GameObject defeatedOverlay;

    private void Start()
    {
        if (playerHealth == null)
            return;

        Transform root = UIKit.SharedRoot;

        // Vignette layar penuh (paling belakang agar tidak menutupi panel lain).
        vignette = UIKit.NewImage(root, "DamageVignette", new Color(0.9f, 0.05f, 0.08f, 0f));
        vignette.sprite = UIKit.Vignette();
        UIKit.Stretch(vignette.rectTransform);
        vignette.transform.SetAsFirstSibling();

        // Kartu health.
        Image card = UIKit.NewImage(root, "PlayerCard", UIKit.PanelColor, 18);
        RectTransform cr = card.rectTransform;
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0.5f, 0f);
        cr.anchoredPosition = new Vector2(0f, 24f);
        cr.sizeDelta = new Vector2(600f, 88f);

        Image accent = UIKit.NewImage(cr, "Accent", new Color(0.35f, 0.6f, 1f, 1f), 2);
        UIKit.TopLeft(accent.rectTransform, 20f, 15f, 4f, 16f);

        Text title = UIKit.NewText(cr, "Title", "PLAYER", 16, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold, true);
        UIKit.TopLeft(title.rectTransform, 32f, 12f, 200f, 22f);

        Text hint = UIKit.NewText(cr, "Hint", "WASD / Panah untuk bergerak", 12, UIKit.DimTextColor, TextAnchor.MiddleRight);
        UIKit.TopRight(hint.rectTransform, 20f, 12f, 260f, 22f);

        bar = new BarView(cr, "PlayerBar", 560f, 32f, 16);
        bar.Root.anchoredPosition = new Vector2(20f, -42f);

        // Overlay kalah.
        Image dim = UIKit.NewImage(root, "DefeatedOverlay", new Color(0f, 0f, 0f, 0.6f));
        UIKit.Stretch(dim.rectTransform);
        defeatedOverlay = dim.gameObject;

        Text big = UIKit.NewText(dim.transform, "Title", "PLAYER DEFEATED", 64, UIKit.RedColor, TextAnchor.MiddleCenter, FontStyle.Bold, true);
        UIKit.Stretch(big.rectTransform, 0f, 0f, 0f, 40f);

        Text small = UIKit.NewText(dim.transform, "Hint", "Tekan  K  untuk isi ulang health   •   Tekan  R  untuk restart",
            22, UIKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Normal, true);
        UIKit.Stretch(small.rectTransform, 0f, 100f, 0f, 0f);

        defeatedOverlay.SetActive(false);

        playerHealth.Damaged += OnDamaged;
    }

    private void OnDamaged(int damage)
    {
        vignetteAlpha = Mathf.Clamp(0.20f + damage * 0.01f, 0.20f, 0.5f);

        // Angka damage melayang di atas kepala Player.
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 screen = cam.WorldToScreenPoint(playerHealth.transform.position + Vector3.up * 2.2f);
            if (screen.z > 0f)
                FloatingLabel.Spawn(FloatingLabel.ToCanvas(screen), "-" + damage, new Color(1f, 0.55f, 0.5f), 40);
        }
    }

    private void Update()
    {
        if (bar == null)
            return;

        bar.Tick(playerHealth.CurrentHealth, playerHealth.MaxHealth, Time.deltaTime);

        vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, Time.deltaTime * 1.1f);
        Color c = vignette.color;
        c.a = vignetteAlpha;
        vignette.color = c;

        if (defeatedOverlay.activeSelf != playerHealth.IsDead)
            defeatedOverlay.SetActive(playerHealth.IsDead);
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.Damaged -= OnDamaged;
    }
}
