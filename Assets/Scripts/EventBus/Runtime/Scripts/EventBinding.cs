using System;

namespace Utilities.EventBus
{
    public class EventBinding<T> : IEventBinding<T> where T : IEvent
    {
        private Action<T> _onEvent = _ => { };
        private Action _onEventNoArgs = () => { };

        Action<T> IEventBinding<T>.OnEvent => _onEvent;
        Action IEventBinding<T>.OnEventNoArgs => _onEventNoArgs;

        public EventBinding(Action<T> onEvent) => this._onEvent = onEvent;
        public EventBinding(Action onEventNoArgs) => this._onEventNoArgs = onEventNoArgs;

        public void Add(Action<T> onEvent) => this._onEvent += onEvent;
        public void Remove(Action<T> onEvent) => this._onEvent -= onEvent;

        public void Add(Action onEventNoArgs) => this._onEventNoArgs += onEventNoArgs;
        public void Remove(Action onEventNoArgs) => this._onEventNoArgs -= onEventNoArgs;
    }

    public interface IEvent
    {
    }

    internal interface IEventBinding<T>
    {
        public Action<T> OnEvent { get; }
        public Action OnEventNoArgs { get; }
    }
}