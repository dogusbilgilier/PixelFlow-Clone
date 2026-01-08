using Utilities.EventBus;

public class GameplayStateChangedEvent : IEvent
{
    public GameplayState _newState;
    public GameplayState _oldState;
}