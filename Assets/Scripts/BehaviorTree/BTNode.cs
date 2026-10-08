public enum NodeState
{
    Success,
    Failure,
    Running
}

// Base class seluruh node Behavior Tree.
public abstract class BTNode
{
    public abstract NodeState Tick();
}
