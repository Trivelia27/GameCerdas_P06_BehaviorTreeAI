using System;

// Leaf node: melakukan tindakan; hasilnya Success / Failure / Running.
public class ActionNode : BTNode
{
    private Func<NodeState> action;

    public ActionNode(Func<NodeState> action)
    {
        this.action = action;
    }

    public override NodeState Tick()
    {
        return action();
    }
}
