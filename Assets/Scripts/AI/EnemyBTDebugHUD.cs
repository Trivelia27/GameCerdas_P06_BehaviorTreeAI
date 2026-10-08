using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// On-screen Behavior Tree Debugger (bonus):
//  - Kartu "Current Action" + jarak ke Player
//  - Visualisasi pohon keputusan: cabang aktif disorot, tiap condition tampil sebagai pill (hijau = terpenuhi)
//  - Eksperimen Utility AI: skor Attack/Chase/Flee/Search/Patrol vs pilihan Behavior Tree
//  - Warna tubuh Enemy mengikuti Current Action
//  - Hotkey demo: H = damage Enemy 25, J = reset HP Enemy, K = reset HP Player, R = restart scene
public class EnemyBTDebugHUD : MonoBehaviour
{
    [SerializeField] private EnemyBTController enemy;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private float patrolBaseScore = 0.1f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // Definisi baris pohon: nama, kunci action, daftar condition (pill).
    private static readonly string[] RowTitles = { "Flee", "Attack", "Chase", "Search", "Patrol" };
    private static readonly string[] RowKeys = { "FLEE", "ATTACK", "CHASE", "SEARCH", "PATROL" };
    private static readonly string[][] RowPills =
    {
        new[] { "Health Low?" },
        new[] { "Can See?", "In Range?" },
        new[] { "Can See?" },
        new[] { "Has Memory?" },
        new string[0]
    };

    private class Pill
    {
        public Image bg;
        public Text text;
    }

    private MaterialPropertyBlock block;

    // UI
    private Chip actionChip;
    private Text distanceText;
    private Image[] rowBg;
    private Image[] rowStrip;
    private Text[] rowName;
    private Pill[][] rowPills;
    private ScoreBars scoreBars;
    private Text pickText;
    private Chip matchChip;

    // Utility (eksperimen)
    private readonly ActionScore[] utility = new ActionScore[5];
    private string utilityChoice = "-";

    private void Awake()
    {
        block = new MaterialPropertyBlock();

        string[] names = { "ATTACK", "CHASE", "FLEE", "SEARCH", "PATROL" };
        for (int i = 0; i < utility.Length; i++)
            utility[i].name = names[i];
    }

    private void Start()
    {
        BuildUI();
    }

    private void Update()
    {
        if (enemy == null)
            return;

        HandleHotkeys();
        ComputeUtilityScores();
        ApplyBodyColor();
        RefreshUI();
    }

    // ------------------------------------------------------------------
    // Input
    // ------------------------------------------------------------------

    private void HandleHotkeys()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.hKey.wasPressedThisFrame) enemy.TakeDamage(25);
        if (kb.jKey.wasPressedThisFrame) enemy.ResetHealth();
        if (kb.kKey.wasPressedThisFrame && playerHealth != null) playerHealth.ResetHealth();
        if (kb.rKey.wasPressedThisFrame) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ------------------------------------------------------------------
    // Eksperimen Utility AI: semua skor 0..1, dipilih yang tertinggi.
    // ------------------------------------------------------------------

    private void ComputeUtilityScores()
    {
        float distance = enemy.DistanceToPlayer;
        float visibility = enemy.PlayerVisible ? 1f : 0f;

        // Attack: makin dekat makin tinggi, wajib terlihat.
        float distanceScore = Mathf.Clamp01(1f - distance / (enemy.AttackRange * 2f));
        float attack = distanceScore * visibility;

        // Chase: makin jauh (tapi masih terlihat) makin perlu mengejar.
        float chase = visibility * Mathf.Clamp01(0.2f + 0.6f * (distance / enemy.VisionRange));

        // Flee: makin sedikit health makin tinggi; lebih mendesak jika Player terlihat.
        float flee = (1f - enemy.HealthPercent) * (visibility > 0f ? 1f : 0.5f);

        // Search: ada memory posisi terakhir tetapi Player sedang tidak terlihat.
        float search = enemy.HasLastSeenPosition && visibility <= 0f ? 0.4f : 0f;

        utility[0].value = attack;
        utility[1].value = chase;
        utility[2].value = flee;
        utility[3].value = search;
        utility[4].value = patrolBaseScore;

        float best = float.NegativeInfinity;
        for (int i = 0; i < utility.Length; i++)
        {
            if (utility[i].value > best)
            {
                best = utility[i].value;
                utilityChoice = utility[i].name;
            }
        }
    }

    private void ApplyBodyColor()
    {
        if (bodyRenderer == null)
            return;

        Color color = UIKit.ActionColor(enemy.CurrentAction);
        bodyRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        bodyRenderer.SetPropertyBlock(block);
    }

    // ------------------------------------------------------------------
    // UI
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        Transform root = UIKit.SharedRoot;

        UIKit.TitlePill(root, "PRIORITAS:  Flee  >  Attack  >  Chase  >  Search  >  Patrol");

        const float cardW = 420f;
        const float pad = 20f;
        const float innerW = cardW - pad * 2f;

        RectTransform card = UIKit.Card(root, "BTCard", 24f, 24f, cardW, 626f);

        // Header
        Image dot = UIKit.NewImage(card, "HeaderAccent", UIKit.GreenColor, 2);
        UIKit.TopLeft(dot.rectTransform, pad, 19f, 4f, 16f);
        Text title = UIKit.NewText(card, "Title", "BEHAVIOR TREE DEBUGGER", 15, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold, true);
        UIKit.TopLeft(title.rectTransform, pad + 12f, 14f, 300f, 26f);

        // Current action
        Text caption = UIKit.NewText(card, "ActionCaption", "CURRENT ACTION", 11, UIKit.DimTextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIKit.TopLeft(caption.rectTransform, pad, 52f, 200f, 16f);

        distanceText = UIKit.NewText(card, "Distance", "", 12, UIKit.DimTextColor, TextAnchor.MiddleRight);
        UIKit.TopLeft(distanceText.rectTransform, cardW - pad - 200f, 52f, 200f, 16f);

        actionChip = new Chip(card, "ActionChip", 26, 14);
        UIKit.TopLeft(actionChip.Root, pad, 74f, innerW, 50f);

        // Tree
        Text treeCaption = UIKit.NewText(card, "TreeCaption", "DECISION TREE  (Selector, prioritas dari atas)", 11, UIKit.DimTextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIKit.TopLeft(treeCaption.rectTransform, pad, 140f, innerW, 16f);

        int rows = RowTitles.Length;
        rowBg = new Image[rows];
        rowStrip = new Image[rows];
        rowName = new Text[rows];
        rowPills = new Pill[rows][];

        for (int i = 0; i < rows; i++)
        {
            float y = 162f + i * 46f;

            rowBg[i] = UIKit.NewImage(card, "Row_" + RowTitles[i], UIKit.RowColor, 10);
            UIKit.TopLeft(rowBg[i].rectTransform, pad, y, innerW, 40f);

            rowStrip[i] = UIKit.NewImage(rowBg[i].transform, "Strip", UIKit.ActionColor(RowKeys[i]), 2);
            UIKit.TopLeft(rowStrip[i].rectTransform, 9f, 9f, 4f, 22f);

            Text number = UIKit.NewText(rowBg[i].transform, "Priority", (i + 1).ToString(), 13, UIKit.DimTextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.TopLeft(number.rectTransform, 20f, 0f, 18f, 40f);

            rowName[i] = UIKit.NewText(rowBg[i].transform, "Name", RowTitles[i], 17, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.TopLeft(rowName[i].rectTransform, 44f, 0f, 100f, 40f);

            string[] pills = RowPills[i];
            rowPills[i] = new Pill[pills.Length];

            float right = 10f;
            for (int p = pills.Length - 1; p >= 0; p--)
            {
                float pw = 22f + pills[p].Length * 6.6f;

                Pill pill = new Pill();
                pill.bg = UIKit.NewImage(rowBg[i].transform, "Pill_" + pills[p], UIKit.RowColor, 9);
                UIKit.TopRight(pill.bg.rectTransform, right, 10f, pw, 20f);

                pill.text = UIKit.NewText(pill.bg.transform, "Text", pills[p], 11, UIKit.DimTextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(pill.text.rectTransform);

                rowPills[i][p] = pill;
                right += pw + 6f;
            }

            if (pills.Length == 0)
            {
                Text def = UIKit.NewText(rowBg[i].transform, "Default", "default (selalu jalan)", 11, UIKit.DimTextColor, TextAnchor.MiddleRight);
                UIKit.TopRight(def.rectTransform, 12f, 0f, 160f, 40f);
            }
        }

        // Utility section
        Image divider = UIKit.NewImage(card, "Divider", new Color(1f, 1f, 1f, 0.10f));
        UIKit.TopLeft(divider.rectTransform, pad, 402f, innerW, 1f);

        Text utilCaption = UIKit.NewText(card, "UtilityCaption", "UTILITY AI  (eksperimen skor)", 11, UIKit.DimTextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIKit.TopLeft(utilCaption.rectTransform, pad, 414f, innerW, 16f);

        scoreBars = new ScoreBars(card, new[] { "Attack", "Chase", "Flee", "Search", "Patrol" }, pad, 436f, innerW, 28f);

        pickText = UIKit.NewText(card, "Pick", "", 13, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIKit.TopLeft(pickText.rectTransform, pad, 586f, 300f, 22f);

        matchChip = new Chip(card, "MatchChip", 11, 9);
        UIKit.TopLeft(matchChip.Root, cardW - pad - 70f, 587f, 70f, 20f);

        // Kontrol
        UIKit.ControlsBar(root,
            new[] { "H", "J", "K", "R", "WASD" },
            new[] { "Damage Enemy", "Heal Enemy", "Heal Player", "Restart", "Gerak" });
    }

    private void RefreshUI()
    {
        if (actionChip == null)
            return;

        string action = enemy.CurrentAction;
        Color actionColor = UIKit.ActionColor(action);

        actionChip.Set(action, actionColor);

        float d = enemy.DistanceToPlayer;
        distanceText.text = float.IsInfinity(d) ? "" : "jarak ke Player  " + d.ToString("F1") + " m";

        bool[][] conditions =
        {
            new[] { enemy.HealthLow },
            new[] { enemy.PlayerVisible, enemy.PlayerInAttackRange },
            new[] { enemy.PlayerVisible },
            new[] { enemy.HasLastSeenPosition },
            new bool[0]
        };

        for (int i = 0; i < RowKeys.Length; i++)
        {
            bool active = RowKeys[i] == action;
            Color c = UIKit.ActionColor(RowKeys[i]);

            rowBg[i].color = active ? new Color(c.r, c.g, c.b, 0.24f) : UIKit.RowColor;
            rowStrip[i].color = active ? c : new Color(c.r, c.g, c.b, 0.30f);
            rowName[i].color = active ? UIKit.TextColor : UIKit.DimTextColor;

            for (int p = 0; p < rowPills[i].Length; p++)
            {
                bool ok = conditions[i][p];
                rowPills[i][p].bg.color = ok ? new Color(0.30f, 0.85f, 0.45f, 0.85f) : new Color(1f, 1f, 1f, 0.07f);
                rowPills[i][p].text.color = ok ? UIKit.DarkTextColor : UIKit.DimTextColor;
            }
        }

        scoreBars.Set(utility, utilityChoice);

        pickText.text = "Utility: " + utilityChoice + "    Behavior Tree: " + action;

        bool same = utilityChoice == action;
        matchChip.Set(same ? "SAMA" : "BEDA",
            same ? new Color(0.30f, 0.85f, 0.45f, 1f) : new Color(1f, 0.62f, 0.20f, 1f));
    }
}
