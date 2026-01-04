using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Utilities.EventBus
{
    public static class EventBusUtil
    {
        private static IReadOnlyList<Type> _eventBusTypes;

#if UNITY_EDITOR
        /// <summary>
        /// Initializes the Unity Editor related components of the EventBusUtil.
        /// The [InitializeOnLoadMethod] attribute causes this method to be called every time a script
        /// is loaded or when the game enters Play Mode in the Editor. This is useful to initialize
        /// fields or states of the class that are necessary during the editing state that also apply
        /// when the game enters Play Mode.
        /// The method sets up a subscriber to the playModeStateChanged event to allow
        /// actions to be performed when the Editor's play mode changes.
        /// </summary>    
        [UnityEditor.InitializeOnLoadMethod]
        public static void InitializeEditor()
        {
            // To make sure that we are not registered that event more than once
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                ClearAllBuses();
            }
        }
#endif

        /// <summary>
        /// Initializes the EventBusUtil class at runtime before the loading of any scene.
        /// The [RuntimeInitializeOnLoadMethod] attribute instructs Unity to execute this method after
        /// the game has been loaded but before any scene has been loaded, in both Play Mode and after
        /// a Build is run. This guarantees that necessary initialization of bus-related types and events is
        /// done before any game objects, scripts or components have started.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            _eventBusTypes = InitializeAllBuses(PredefinedAssemblyUtil.GetTypes(typeof(IEvent)));
        }

        private static List<Type> InitializeAllBuses(List<Type> eventTypes)
        {
            List<Type> eventBusTypes = new List<Type>();

            var typedef = typeof(EventBus<>);

            foreach (var eventType in eventTypes)
            {
                var busType = typedef.MakeGenericType(eventType);
                eventBusTypes.Add(busType);
                // Debug.Log($"Initialized EventBus<{eventType.Name}>");
            }

            return eventBusTypes;
        }

        /// <summary>
        /// Clears (removes all listeners from) all event buses in the application.
        /// </summary>
        private static void ClearAllBuses()
        {
            Debug.Log("Clearing all buses...");

            for (int i = 0; i < _eventBusTypes.Count; i++)
            {
                var busType = _eventBusTypes[i];
                var clearMethod = busType.GetMethod("Clear", BindingFlags.Static | BindingFlags.NonPublic);
                clearMethod?.Invoke(null, null);
            }
        }
    }
}