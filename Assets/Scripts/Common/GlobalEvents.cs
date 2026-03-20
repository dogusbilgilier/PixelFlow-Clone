using Utilities.EventBus;

public class GameplayStateChangedEvent : IEvent
{
    public GameplayState NewState;
    public GameplayState OldState;
}

public class ProgressChangedEvent : IEvent
{
    public float Progress;
    public int CurrentCount;
    public int TotalCount;
}