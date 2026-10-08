using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Enemy AI berbasis Behavior Tree.
// Prioritas: Flee > Attack > Chase > Patrol
public class EnemyBTController : MonoBehaviour, IEnemyBrain
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private Transform safePoint;

    [Header("Perception")]
    [SerializeField] private float visionRange = 10f;
    [SerializeField] private float visionAngle = 90f;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private Vector3 targetOffset = Vector3.zero;

    [Header("Combat")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private int attackDamage = 10;

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int lowHealthThreshold = 30;

    [Header("Recovery (istirahat di SafePoint)")]
    [SerializeField] private float healthRegenPerSecond = 8f;
    [SerializeField, Range(0f, 1f)] private float recoverHealthPercent = 0.8f;

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float fleeSpeed = 5f;

    [Header("Search Last Seen Position")]
    [SerializeField] private float searchSpeed = 3.5f;
    [SerializeField] private float searchDuration = 3f;
    [SerializeField] private float searchLookSpeed = 120f;

    [Header("Debug")]
    [SerializeField] private string currentAction = "None";

    private NavMeshAgent agent;
    private PlayerHealth playerHealth;
    private BTNode rootNode;

    private int currentHealth;
    private int currentPatrolIndex;
    private bool recovering;
    private float regenAccumulator;

    // Cache hasil perception per frame (CanSeePlayer dipanggil oleh beberapa node).
    private int visibilityFrame = -1;
    private bool visibilityCache;

    // Memory: posisi terakhir Player terlihat.
    private bool hasLastSeenPosition;
    private Vector3 lastSeenPosition;
    private bool searchArrived;
    private float searchTimer;

    // ---------- Data untuk debugger / HUD ----------
    public string CurrentAction => currentAction;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float HealthPercent => (float)currentHealth / maxHealth;
    public float LowHealthPercent => (float)lowHealthThreshold / maxHealth;
    public float AttackRange => attackRange;
    public float VisionRange => visionRange;
    public float AttackCooldown => attackCooldown;
    public bool PlayerVisible => CanSeePlayer();
    public bool HealthLow => IsHealthLow();
    public bool PlayerInAttackRange => IsPlayerInAttackRange();
    public bool HasLastSeenPosition => hasLastSeenPosition;
    public ActionScore[] Scores => null;

    public event System.Action OnAttack;

    public float DistanceToPlayer =>
        player != null ? Vector3.Distance(transform.position, player.position) : float.PositiveInfinity;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        currentHealth = maxHealth;

        if (player != null)
            playerHealth = player.GetComponent<PlayerHealth>();
    }

    private void Start()
    {
        BuildBehaviorTree();
    }

    private void Update()
    {
        if (rootNode == null)
            return;

        rootNode.Tick();

        // Selama cooldown, Attack tidak dieksekusi ulang; tetap hadap Player.
        if (currentAction == "ATTACK")
            FacePlayer();
    }

    private void BuildBehaviorTree()
    {
        // ------------------------------
        // FLEE
        // ------------------------------
        BTNode fleeSequence =
            new SequenceNode(
                new List<BTNode>
                {
                    new ConditionNode(IsHealthLow),
                    new ActionNode(Flee)
                }
            );

        // ------------------------------
        // ATTACK
        // ------------------------------
        BTNode attackWithCooldown =
            new CooldownDecorator(
                new ActionNode(AttackPlayer),
                attackCooldown
            );

        BTNode attackSequence =
            new SequenceNode(
                new List<BTNode>
                {
                    new ConditionNode(CanSeePlayer),
                    new ConditionNode(IsPlayerInAttackRange),
                    attackWithCooldown
                }
            );

        // ------------------------------
        // CHASE
        // ------------------------------
        BTNode chaseSequence =
            new SequenceNode(
                new List<BTNode>
                {
                    new ConditionNode(CanSeePlayer),
                    new ActionNode(ChasePlayer)
                }
            );

        // ------------------------------
        // SEARCH LAST SEEN POSITION
        // ------------------------------
        BTNode searchSequence =
            new SequenceNode(
                new List<BTNode>
                {
                    new ConditionNode(HasLastSeenPositionMemory),
                    new ActionNode(SearchLastSeenPosition)
                }
            );

        // ------------------------------
        // PATROL
        // ------------------------------
        BTNode patrolAction = new ActionNode(Patrol);

        // ------------------------------
        // ROOT SELECTOR (urutan = prioritas)
        // ------------------------------
        rootNode =
            new SelectorNode(
                new List<BTNode>
                {
                    fleeSequence,
                    attackSequence,
                    chaseSequence,
                    searchSequence,
                    patrolAction
                }
            );
    }

    // ==================================================
    // CONDITIONS
    // ==================================================

    // Health dianggap "rendah" begitu <= threshold, dan tetap rendah (Enemy tetap Flee/istirahat)
    // sampai health pulih ke recoverHealthPercent. Tanpa jeda ini Enemy akan kembali menyerang
    // dengan health 31 lalu langsung kabur lagi.
    private bool IsHealthLow()
    {
        if (recovering)
        {
            if (currentHealth >= RecoverTarget)
                recovering = false;
        }
        else if (currentHealth <= lowHealthThreshold)
        {
            recovering = true;
        }

        return recovering;
    }

    private int RecoverTarget =>
        Mathf.Min(maxHealth, Mathf.Max(lowHealthThreshold + 1, Mathf.RoundToInt(maxHealth * recoverHealthPercent)));

    private bool IsPlayerInAttackRange()
    {
        if (player == null)
            return false;

        return Vector3.Distance(transform.position, player.position) <= attackRange;
    }

    private bool HasLastSeenPositionMemory()
    {
        return hasLastSeenPosition;
    }

    // Dipanggil setiap kali Player terlihat: perbarui memory.
    private void RememberPlayerPosition()
    {
        Vector3 position = player.position;

        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            position = hit.position;

        lastSeenPosition = position;
        hasLastSeenPosition = true;
        searchArrived = false;
        searchTimer = 0f;
    }

    private bool CanSeePlayer()
    {
        if (visibilityFrame == Time.frameCount)
            return visibilityCache;

        visibilityFrame = Time.frameCount;
        visibilityCache = EvaluateVisibility();
        return visibilityCache;
    }

    private bool EvaluateVisibility()
    {
        if (player == null)
            return false;

        if (playerHealth != null && playerHealth.IsDead)
            return false;

        Vector3 eyePosition = transform.position + eyeOffset;
        Vector3 targetPosition = player.position + targetOffset;
        Vector3 directionToPlayer = targetPosition - eyePosition;
        float distanceToPlayer = directionToPlayer.magnitude;

        // 1. Jarak
        if (distanceToPlayer > visionRange)
            return false;

        // 2. Field of View (sudut diukur pada bidang horizontal)
        Vector3 flatDirection = directionToPlayer;
        flatDirection.y = 0f;

        if (flatDirection.sqrMagnitude > 0.0001f)
        {
            float angle = Vector3.Angle(transform.forward, flatDirection);

            if (angle > visionAngle * 0.5f)
                return false;
        }

        // 3. Obstacle (Line of Sight)
        bool blocked =
            Physics.Raycast(
                eyePosition,
                directionToPlayer.normalized,
                distanceToPlayer,
                obstacleMask
            );

        if (blocked)
            return false;

        RememberPlayerPosition();
        return true;
    }

    // ==================================================
    // ACTIONS
    // ==================================================

    private NodeState Patrol()
    {
        currentAction = "PATROL";

        if (patrolPoints == null || patrolPoints.Length == 0)
            return NodeState.Failure;

        agent.isStopped = false;
        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0.2f;

        agent.SetDestination(patrolPoints[currentPatrolIndex].position);

        if (!agent.pathPending && agent.remainingDistance <= 0.5f)
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;

        return NodeState.Running;
    }

    private NodeState ChasePlayer()
    {
        if (player == null)
            return NodeState.Failure;

        currentAction = "CHASE";

        agent.isStopped = false;
        agent.speed = chaseSpeed;
        agent.stoppingDistance = attackRange * 0.8f;

        agent.SetDestination(player.position);

        return NodeState.Running;
    }

    // Menuju posisi terakhir Player terlihat, melihat sekeliling sebentar,
    // lalu melupakan memory. Failure di akhir membuat Selector lanjut ke Patrol.
    private NodeState SearchLastSeenPosition()
    {
        currentAction = "SEARCH";

        if (!searchArrived)
        {
            agent.isStopped = false;
            agent.speed = searchSpeed;
            agent.stoppingDistance = 0.2f;
            agent.SetDestination(lastSeenPosition);

            if (!agent.pathPending && agent.remainingDistance <= 0.5f)
                searchArrived = true;

            return NodeState.Running;
        }

        agent.isStopped = true;
        transform.Rotate(0f, searchLookSpeed * Time.deltaTime, 0f);

        searchTimer += Time.deltaTime;

        if (searchTimer >= searchDuration)
        {
            hasLastSeenPosition = false;
            searchArrived = false;
            searchTimer = 0f;
            return NodeState.Failure;
        }

        return NodeState.Running;
    }

    private NodeState AttackPlayer()
    {
        if (player == null)
            return NodeState.Failure;

        currentAction = "ATTACK";

        agent.isStopped = true;
        agent.ResetPath();

        FacePlayer();

        Debug.Log(name + " attacks Player! Damage = " + attackDamage);

        if (playerHealth != null)
            playerHealth.TakeDamage(attackDamage);

        OnAttack?.Invoke();

        return NodeState.Success;
    }

    private NodeState Flee()
    {
        if (safePoint == null)
            return NodeState.Failure;

        currentAction = "FLEE";

        agent.isStopped = false;
        agent.speed = fleeSpeed;
        agent.stoppingDistance = 0.5f;

        agent.SetDestination(safePoint.position);

        if (!agent.pathPending && agent.remainingDistance <= 0.7f)
        {
            agent.isStopped = true;
            RestAtSafePoint();
            return NodeState.Success;
        }

        return NodeState.Running;
    }

    // Di SafePoint Enemy beristirahat: health pulih perlahan (hanya saat sudah sampai, bukan saat berlari).
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

    private void FacePlayer()
    {
        if (player == null)
            return;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation =
            Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
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
        Debug.Log(name + " Health = " + currentHealth);
    }

    [ContextMenu("Test Damage 25")]
    private void TestDamage25()
    {
        TakeDamage(25);
    }

    [ContextMenu("Reset Health")]
    private void ContextResetHealth()
    {
        ResetHealth();
    }

    // ==================================================
    // GIZMOS
    // ==================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Vector3 leftDirection =
            Quaternion.Euler(0f, -visionAngle * 0.5f, 0f) * transform.forward;
        Vector3 rightDirection =
            Quaternion.Euler(0f, visionAngle * 0.5f, 0f) * transform.forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, leftDirection * visionRange);
        Gizmos.DrawRay(transform.position, rightDirection * visionRange);

        if (Application.isPlaying && hasLastSeenPosition)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lastSeenPosition + Vector3.up * 0.1f, 0.4f);
            Gizmos.DrawLine(transform.position, lastSeenPosition);
        }

        if (player != null)
        {
            Gizmos.color = Application.isPlaying && CanSeePlayer() ? Color.green : Color.gray;
            Gizmos.DrawLine(transform.position + eyeOffset, player.position + targetOffset);
        }
    }
}
