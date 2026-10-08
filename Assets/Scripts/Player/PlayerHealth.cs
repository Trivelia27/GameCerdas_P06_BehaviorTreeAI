using UnityEngine;

public class PlayerHealth : MonoBehaviour, IHealthSource
{
    [SerializeField] private int maxHealth = 100;

    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float HealthPercent => (float)currentHealth / maxHealth;
    public bool IsDead => currentHealth <= 0;

    // Dipicu setiap Player terkena damage (dipakai efek layar merah).
    public event System.Action<int> Damaged;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (IsDead)
            return;

        currentHealth = Mathf.Clamp(currentHealth - damage, 0, maxHealth);

        Debug.Log("Player Health = " + currentHealth);

        Damaged?.Invoke(damage);

        if (currentHealth <= 0)
            Debug.Log("Player Dead");
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }
}
