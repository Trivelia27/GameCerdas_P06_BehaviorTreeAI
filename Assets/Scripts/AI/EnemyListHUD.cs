using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// HUD untuk scene dengan beberapa Enemy (Personality) atau Enemy Utility AI:
// satu kartu per Enemy berisi nama, Current Action, deskripsi personality, dan (jika ada) bar skor Utility.
// Hotkey: H = damage semua Enemy 25, J = reset HP Enemy, K = reset HP Player, R = restart.
public class EnemyListHUD : MonoBehaviour
{
    [SerializeField] private string title = "ENEMY DEBUGGER";
    [SerializeField] private MonoBehaviour[] enemies;
    [SerializeField] private string[] descriptions;
    [SerializeField] private PlayerHealth playerHealth;

    private class Card
    {
        public IEnemyBrain brain;
        public Chip chip;
        public ScoreBars scoreBars;
        public Text pickText;
    }

    private Card[] cards;

    private void Start()
    {
        Transform root = UIKit.SharedRoot;

        UIKit.TitlePill(root, title);

        cards = new Card[enemies.Length];

        const float cardW = 400f;
        const float pad = 20f;
        float y = 24f;

        for (int i = 0; i < enemies.Length; i++)
        {
            IEnemyBrain brain = enemies[i] as IEnemyBrain;
            bool hasScores = brain != null && brain.Scores != null;
            float height = hasScores ? 270f : 96f;

            RectTransform card = UIKit.Card(root, "EnemyCard_" + enemies[i].name, 24f, y, cardW, height);

            Image accent = UIKit.NewImage(card, "Accent", UIKit.GreenColor, 2);
            UIKit.TopLeft(accent.rectTransform, pad, 19f, 4f, 20f);

            Text name = UIKit.NewText(card, "Name", enemies[i].name, 19, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            UIKit.TopLeft(name.rectTransform, pad + 14f, 14f, 230f, 30f);

            Card c = new Card { brain = brain };
            c.chip = new Chip(card, "ActionChip", 14, 11);
            UIKit.TopRight(c.chip.Root, pad, 16f, 112f, 26f);

            string description = descriptions != null && i < descriptions.Length ? descriptions[i] : "";
            Text desc = UIKit.NewText(card, "Description", description, 12, UIKit.DimTextColor, TextAnchor.UpperLeft);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIKit.TopLeft(desc.rectTransform, pad, 54f, cardW - pad * 2f, 36f);

            if (hasScores)
            {
                c.scoreBars = new ScoreBars(card, new[] { "Attack", "Chase", "Flee", "Search", "Patrol" },
                    pad, 96f, cardW - pad * 2f, 28f);

                c.pickText = UIKit.NewText(card, "Pick", "", 13, UIKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                UIKit.TopLeft(c.pickText.rectTransform, pad, 240f, 340f, 22f);
            }

            cards[i] = c;
            y += height + 14f;
        }

        UIKit.ControlsBar(root,
            new[] { "H", "J", "K", "R", "WASD" },
            new[] { "Damage Enemy", "Heal Enemy", "Heal Player", "Restart", "Gerak" });
    }

    private void Update()
    {
        if (cards == null)
            return;

        HandleHotkeys();

        foreach (Card c in cards)
        {
            if (c.brain == null)
                continue;

            string action = c.brain.CurrentAction;
            c.chip.Set(action, UIKit.ActionColor(action));

            if (c.scoreBars != null)
            {
                c.scoreBars.Set(c.brain.Scores, action);
                c.pickText.text = "Action terpilih:  " + action;
            }
        }
    }

    private void HandleHotkeys()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.hKey.wasPressedThisFrame)
            foreach (Card c in cards) c.brain?.TakeDamage(25);

        if (kb.jKey.wasPressedThisFrame)
            foreach (Card c in cards) c.brain?.ResetHealth();

        if (kb.kKey.wasPressedThisFrame && playerHealth != null)
            playerHealth.ResetHealth();

        if (kb.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
