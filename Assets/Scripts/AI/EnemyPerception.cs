using UnityEngine;
using UnityEngine.AI;

// Perception terpisah dari Decision Making (dipakai EnemyUtilityController):
// Vision Range + Field of View + Raycast Line of Sight + memory posisi terakhir Player.
public class EnemyPerception : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float visionRange = 10f;
    [SerializeField] private float visionAngle = 90f;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private Vector3 targetOffset = Vector3.zero;

    private PlayerHealth playerHealth;
    private int visibilityFrame = -1;
    private bool visibilityCache;

    public Transform Player => player;
    public float VisionRange => visionRange;
    public float VisionAngle => visionAngle;
    public bool HasLastSeenPosition { get; private set; }
    public Vector3 LastSeenPosition { get; private set; }

    public float DistanceToPlayer =>
        player != null ? Vector3.Distance(transform.position, player.position) : float.PositiveInfinity;

    private void Awake()
    {
        if (player != null)
            playerHealth = player.GetComponent<PlayerHealth>();
    }

    public bool CanSeePlayer
    {
        get
        {
            if (visibilityFrame == Time.frameCount)
                return visibilityCache;

            visibilityFrame = Time.frameCount;
            visibilityCache = Evaluate();
            return visibilityCache;
        }
    }

    public void ClearMemory()
    {
        HasLastSeenPosition = false;
    }

    private bool Evaluate()
    {
        if (player == null)
            return false;

        if (playerHealth != null && playerHealth.IsDead)
            return false;

        Vector3 eyePosition = transform.position + eyeOffset;
        Vector3 directionToPlayer = player.position + targetOffset - eyePosition;
        float distanceToPlayer = directionToPlayer.magnitude;

        // 1. Jarak
        if (distanceToPlayer > visionRange)
            return false;

        // 2. Field of View
        Vector3 flat = directionToPlayer;
        flat.y = 0f;

        if (flat.sqrMagnitude > 0.0001f &&
            Vector3.Angle(transform.forward, flat) > visionAngle * 0.5f)
            return false;

        // 3. Obstacle (Line of Sight)
        if (Physics.Raycast(eyePosition, directionToPlayer.normalized, distanceToPlayer, obstacleMask))
            return false;

        Remember();
        return true;
    }

    private void Remember()
    {
        Vector3 position = player.position;

        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            position = hit.position;

        LastSeenPosition = position;
        HasLastSeenPosition = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Vector3 left = Quaternion.Euler(0f, -visionAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, visionAngle * 0.5f, 0f) * transform.forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, left * visionRange);
        Gizmos.DrawRay(transform.position, right * visionRange);

        if (Application.isPlaying && HasLastSeenPosition)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(LastSeenPosition + Vector3.up * 0.1f, 0.4f);
        }
    }
}
