using Utilities.EventBus;

public class GameplayStateChangedEvent : IEvent
{
    public GameplayState newState;
    public GameplayState oldState;
}