using UnityEngine;
using UnityEngine.AI;

// Full Utility AI Controller (bonus).
// Setiap frame semua action diberi skor 0..1, action dengan skor tertinggi dipilih.
// Dua teknik anti "rapid action switching":
//   - Action Commitment : action aktif dipertahankan minimal minimumActionDuration detik
//   - Hysteresis        : action baru harus mengungguli action aktif sebesar switchMargin
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyPerception))]
public class EnemyUtilityController : MonoBehaviour, IEnemyBrain
{
    private enum Act { Attack = 0, Chase = 1, Flee = 2, Search = 3, Patrol = 4 }

    private static readonly string[] ActNames = { "ATTACK", "CHASE", "FLEE", "SEARCH", "PATROL" };

    [Header("References")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private Transform safePoint;

    [Header("Combat")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private int attackDamage = 10;

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float healthRegenPerSecond = 8f;
    [SerializeField, Range(0f, 1f)] private float recoverHealthPercent = 0.8f;

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float fleeSpeed = 5f;
    [SerializeField] private float searchSpeed = 3.5f;
    [SerializeField] private float searchDuration = 3f;
    [SerializeField] private float searchLookSpeed = 120f;

    [Header("Utility Tuning")]
    [SerializeField, Range(0f, 1f)] private float patrolScore = 0.1f;
    [SerializeField, Range(0f, 1f)] private float searchScore = 0.4f;
    [SerializeField] private float minimumActionDuration = 1f;
    [SerializeField, Range(0f, 0.5f)] private float switchMargin = 0.15f;

    [Header("Debug")]
    [SerializeField] private string currentAction = "None";

    private NavMeshAgent agent;
    private EnemyPerception perception;
    private PlayerHealth playerHealth;

    private readonly float[] scores = new float[5];
    private readonly ActionScore[] scoreView = new ActionScore[5];
    private Act current = Act.Patrol;
    private float actionStartTime = float.NegativeInfinity;

    private int currentHealth;
    private int currentPatrolIndex;
    private float regenAccumulator;
    private bool recovering;
    private float nextAttackTime;
    private bool searchArrived;
    private float searchTimer;

    // ---------- IEnemyBrain ----------
    public string CurrentAction => currentAction;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float HealthPercent => (float)currentHealth / maxHealth;
    public float LowHealthPercent => 0.3f; // penanda visual saja; Utility AI tidak memakai threshold keras

    public ActionScore[] Scores
    {
        get
        {
            for (int i = 0; i < scores.Length; i++)
            {
                scoreView[i].name = ActNames[i];
                scoreView[i].value = scores[i];
            }

            return scoreView;
        }
    }

    public event System.Action OnAttack;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        perception = GetComponent<EnemyPerception>();
        currentHealth = maxHealth;
    }

    private void Start()
    {
        if (perception.Player != null)
            playerHealth = perception.Player.GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        ComputeScores();
        ChooseAction();
        Execute();
    }

    // ==================================================
    // SCORING
    // ==================================================

    private void ComputeScores()
    {
        float distance = perception.DistanceToPlayer;
        float visibility = perception.CanSeePlayer ? 1f : 0f;

        // Attack: hanya jika Player terlihat dan dalam jangkauan; makin dekat makin tinggi (0.5 .. 1.0).
        scores[(int)Act.Attack] = distance <= attackRange
            ? visibility * (0.5f + 0.5f * (1f - distance / attackRange))
            : 0f;

        // Chase: Player terlihat; makin jauh makin perlu dikejar.
        scores[(int)Act.Chase] = visibility *
            Mathf.Clamp01(0.2f + 0.6f * (distance / perception.VisionRange));

        // Flee: makin sedikit health makin tinggi; lebih mendesak jika Player terlihat.
        float lowHealthScore = 1f - HealthPercent;
        float threatScore = visibility > 0f ? 1f : 0.5f;
        float fleeScore = lowHealthScore * threatScore;

        // Setelah mulai kabur, Enemy tetap beristirahat sampai health pulih (mencegah bolak-balik
        // Flee <-> Search/Chase di tengah pemulihan).
        if (recovering)
        {
            if (HealthPercent >= recoverHealthPercent)
                recovering = false;
            else
                fleeScore = Mathf.Max(fleeScore, 0.95f);
        }

        scores[(int)Act.Flee] = safePoint != null ? fleeScore : 0f;

        // Search: ada memory posisi terakhir tetapi Player tidak terlihat.
        scores[(int)Act.Search] = perception.HasLastSeenPosition && visibility <= 0f ? searchScore : 0f;

        // Patrol: default action (selalu ada pilihan).
        scores[(int)Act.Patrol] = patrolScore;
    }

    private void ChooseAction()
    {
        Act best = Act.Patrol;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < scores.Length; i++)
        {
            if (scores[i] > bestScore)
            {
                bestScore = scores[i];
                best = (Act)i;
            }
        }

        if (best != current)
        {
            float currentScore = scores[(int)current];
            bool stillValid = currentScore > 0.001f;

            bool committed = stillValid &&
                Time.time - actionStartTime < minimumActionDuration;

            bool beatsHysteresis = bestScore > currentScore + switchMargin;

            if (!stillValid || (!committed && beatsHysteresis))
                SwitchTo(best);
        }

        currentAction = ActNames[(int)current];
    }

    private void SwitchTo(Act next)
    {
        current = next;
        actionStartTime = Time.time;

        if (next == Act.Flee)
            recovering = true;

        searchArrived = false;
        searchTimer = 0f;
    }

    // ==================================================
    // EXECUTION
    // ==================================================

    private void Execute()
    {
        switch (current)
        {
            case Act.Attack: DoAttack(); break;
            case Act.Chase: DoChase(); break;
            case Act.Flee: DoFlee(); break;
            case Act.Search: DoSearch(); break;
            default: DoPatrol(); break;
        }
    }

    private void DoPatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        agent.isStopped = false;
        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0.2f;
        agent.SetDestination(patrolPoints[currentPatrolIndex].position);

        if (!agent.pathPending && agent.remainingDistance <= 0.5f)
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
    }

    private void DoChase()
    {
        agent.isStopped = false;
        agent.speed = chaseSpeed;
        agent.stoppingDistance = attackRange * 0.8f;
        agent.SetDestination(perception.Player.position);
    }

    private void DoAttack()
    {
        agent.isStopped = true;
        agent.ResetPath();
        FacePlayer();

        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;

        Debug.Log(name + " attacks Player! Damage = " + attackDamage);

        if (playerHealth != null)
            playerHealth.TakeDamage(attackDamage);

        OnAttack?.Invoke();
    }

    private void DoFlee()
    {
        agent.isStopped = false;
        agent.speed = fleeSpeed;
        agent.stoppingDistance = 0.5f;
        agent.SetDestination(safePoint.position);

        if (!agent.pathPending && agent.remainingDistance <= 0.7f)
        {
            agent.isStopped = true;
            RestAtSafePoint();
        }
    }

    // Di SafePoint Enemy beristirahat: health pulih perlahan. Skor Flee turun seiring health naik,
    // sehingga Enemy kembali Patrol dengan sendirinya setelah pulih.
    private void RestAtSafePoint()
    {
        if (healthRegenPerSecond <= 0f || currentHealth >= maxHealth)
            return;

        regenAccumulator += healthRegenPerSecond * Time.deltaTime;

        int gained = Mathf.FloorToInt(regenAccumulator);
        if (gained > 0)
        {
            regenAccumulator -= gained;
            currentHealth = Mathf.Min(maxHealth, currentHealth + gained);
        }
    }

    private void DoSearch()
    {
        if (!searchArrived)
        {
            agent.isStopped = false;
            agent.speed = searchSpeed;
            agent.stoppingDistance = 0.2f;
            agent.SetDestination(perception.LastSeenPosition);

            if (!agent.pathPending && agent.remainingDistance <= 0.5f)
                searchArrived = true;

            return;
        }

        agent.isStopped = true;
        transform.Rotate(0f, searchLookSpeed * Time.deltaTime, 0f);
        searchTimer += Time.deltaTime;

        // Selesai mencari: lupakan memory -> skor Search jadi 0 -> kembali Patrol.
        if (searchTimer >= searchDuration)
            perception.ClearMemory();
    }

    private void FacePlayer()
    {
        Vector3 direction = perception.Player.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction),
            10f * Time.deltaTime);
    }

    // ==================================================
    // HEALTH
    // ==================================================

    public void TakeDamage(int damage)
    {
        currentHealth = Mathf.Clamp(currentHealth - damage, 0, maxHealth);
        Debug.Log(name + " Health = " + currentHealth);
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }

    [ContextMenu("Test Damage 25")]
    private void TestDamage25()
    {
        TakeDamage(25);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
