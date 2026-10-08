using System;

// Leaf node: memeriksa kondisi. true -> Success, false -> Failure.
public class ConditionNode : BTNode
{
    private Func<bool> condition;

    public ConditionNode(Func<bool> condition)
    {
        this.condition = condition;
    }

    public override NodeState Tick()
    {
        return condition()
            ? NodeState.Success
            : NodeState.Failure;
    }
}
