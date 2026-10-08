// Sumber data health (Enemy maupun Player) untuk nameplate / health bar.
public interface IHealthSource
{
    int CurrentHealth { get; }
    int MaxHealth { get; }
    float HealthPercent { get; }
}

public struct ActionScore
{
    public string name;
    public float value;
}

// Kontrak umum "otak" Enemy (Behavior Tree maupun Utility AI) agar komponen pendukung
// (Nameplate, Animator Driver, HUD) tidak bergantung pada satu jenis controller.
public interface IEnemyBrain : IHealthSource
{
    string CurrentAction { get; }
    float LowHealthPercent { get; }

    // Skor Utility tiap action (null jika brain tidak memakai skor).
    ActionScore[] Scores { get; }

    // Dipicu saat Enemy benar-benar melakukan serangan (dipakai Animator).
    event System.Action OnAttack;

    void TakeDamage(int damage);
    void ResetHealth();
}
