using UnityEngine;

// Decorator: child hanya boleh dijalankan setelah cooldown berakhir.
// Selama cooldown mengembalikan Running agar branch Attack tetap aktif
// (Selector tidak jatuh ke Chase/Patrol saat menunggu serangan berikutnya).
public class CooldownDecorator : BTNode
{
    private BTNode child;
    private float cooldown;
    private float nextAllowedTime;

    public CooldownDecorator(BTNode child, float cooldown)
    {
        this.child = child;
        this.cooldown = cooldown;
        nextAllowedTime = 0f;
    }

    public override NodeState Tick()
    {
        if (Time.time < nextAllowedTime)
            return NodeState.Running;

        NodeState state = child.Tick();

        if (state == NodeState.Success)
            nextAllowedTime = Time.time + cooldown;

        return state;
    }
}
