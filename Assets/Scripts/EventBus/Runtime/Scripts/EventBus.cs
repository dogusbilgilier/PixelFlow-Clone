using System.Collections.Generic;
using JetBrains.Annotations;

namespace Utilities.EventBus
{
    public static class EventBus<T> where T : IEvent
    {
        private static readonly HashSet<IEventBinding<T>> m_Bindings = new HashSet<IEventBinding<T>>(16);
        private static readonly List<IEventBinding<T>> m_TempBindings = new List<IEventBinding<T>>(16);

        public static void Subscribe(EventBinding<T> binding) => m_Bindings.Add(binding);
        public static void Unsubscribe(EventBinding<T> binding) => m_Bindings.Remove(binding);

        /// <summary>
        /// Not supporting thread safety
        /// </summary>
        public static void Fire(T @event)
        {
            m_TempBindings.AddRange(m_Bindings);

            int count = m_TempBindings.Count;
            for (int i = 0; i < count; i++)
            {
                IEventBinding<T> binding = m_TempBindings[i];

                binding.OnEvent.Invoke(@event);
                binding.OnEventNoArgs.Invoke();
            }

            m_TempBindings.Clear();
        }

        /// <summary>
        /// Called implicitly by <see cref="EventBusUtil.ClearAllBuses"/>
        /// </summary>
        [UsedImplicitly]
        private static void Clear()
        {
            m_Bindings.Clear();
            m_TempBindings.Clear();
        }
    }
}