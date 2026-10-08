using UnityEngine;
using UnityEngine.AI;

// Animator Integration (bonus): menghubungkan keputusan AI + gerakan NavMeshAgent ke Animator.
//   Speed      (float)   <- kecepatan NavMeshAgent  -> Idle / Walk / Run
//   Attack     (trigger) <- IEnemyBrain.OnAttack     -> Attack
//   IsFleeing  (bool)    <- Current Action = FLEE    -> Flee
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAnimatorDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int AttackId = Animator.StringToHash("Attack");
    private static readonly int IsFleeingId = Animator.StringToHash("IsFleeing");

    private NavMeshAgent agent;
    private IEnemyBrain brain;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        brain = GetComponent<IEnemyBrain>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (brain != null)
            brain.OnAttack += HandleAttack;
    }

    private void OnDestroy()
    {
        if (brain != null)
            brain.OnAttack -= HandleAttack;
    }

    private void HandleAttack()
    {
        if (animator != null)
            animator.SetTrigger(AttackId);
    }

    private void Update()
    {
        if (animator == null || agent == null || brain == null)
            return;

        float speed = agent.velocity.magnitude;
        animator.SetFloat(SpeedId, speed, 0.1f, Time.deltaTime);

        // Flee hanya dianimasikan saat benar-benar berlari; setelah sampai SafePoint kembali Idle.
        bool fleeing = brain.CurrentAction == "FLEE" && speed > 0.3f;
        animator.SetBool(IsFleeingId, fleeing);
    }
}
