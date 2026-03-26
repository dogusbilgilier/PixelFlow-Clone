using System;
using System.Collections.Generic;
using System.Reflection;
using Firebase;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using UnityEngine;

namespace Game
{
    public class RemoteConfigManager : MonoBehaviour
    {
        [SerializeField] private bool _useLocalConfig = false;

        public bool IsInitialized { get; private set; }

        private Action _onCompleted;

        /// <summary>
        /// Initializes Firebase, sets GameConfigs values as defaults, fetches remote values,
        /// and overrides any GameConfigs field whose name matches a remote key.
        /// Always calls onCompleted — even if Firebase is unavailable or fetch fails.
        /// When _useLocalConfig is true, skips Firebase entirely and uses GameConfigs as-is.
        /// </summary>
        public void Initialize(Action onCompleted = null)
        {
            _onCompleted = onCompleted;

            if (_useLocalConfig)
            {
                Debug.Log("[RemoteConfig] Local config mode — skipping Firebase fetch.");
                Complete();
                return;
            }

            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result != DependencyStatus.Available)
                {
                    Debug.LogWarning($"[RemoteConfig] Firebase unavailable ({task.Result}). Using local defaults.");
                    Complete();
                    return;
                }

                SetDefaultsAndFetch();
            });
        }


        private void SetDefaultsAndFetch()
        {
            // Create a runtime copy of the ScriptableObject before doing anything.
            // All reads and writes from this point forward go to the copy,
            // so the original asset file is never modified.
            GameConfigs.CreateRuntimeCopy();

            FirebaseRemoteConfig.DefaultInstance
                .SetDefaultsAsync(BuildDefaults())
                .ContinueWithOnMainThread(_ =>
                {
                    FirebaseRemoteConfig.DefaultInstance
                        .FetchAndActivateAsync()
                        .ContinueWithOnMainThread(fetchTask =>
                        {
                            if (fetchTask.IsFaulted || fetchTask.IsCanceled)
                                Debug.LogWarning("[RemoteConfig] Fetch failed. Using local defaults.");
                            else
                                ApplyToGameConfigs();

                            Complete();
                        });
                });
        }

        private void Complete()
        {
            IsInitialized = true;
            _onCompleted?.Invoke();
            _onCompleted = null;
        }


        private static Dictionary<string, object> BuildDefaults()
        {
            var defaults = new Dictionary<string, object>();

            foreach (var field in GetGameConfigsFields())
            {
                var value = field.GetValue(GameConfigs.Instance);
                if (value != null)
                    defaults[field.Name] = value;
            }

            Debug.Log($"[RemoteConfig] Registered {defaults.Count} default keys from GameConfigs.");
            return defaults;
        }

        private static void ApplyToGameConfigs()
        {
            var rc = FirebaseRemoteConfig.DefaultInstance;
            int overrideCount = 0;

            foreach (var field in GetGameConfigsFields())
            {
                if (!rc.AllValues.TryGetValue(field.Name, out var configValue))
                    continue;

                // Skip values that were never set remotely — keep local default.
                if (configValue.Source != ValueSource.RemoteValue)
                    continue;

                try
                {
                    object newValue = ConvertValue(field.FieldType, configValue);

                    if (newValue == null)
                        continue;

                    object oldValue = field.GetValue(GameConfigs.Instance);
                    field.SetValue(GameConfigs.Instance, newValue);
                    //Debug.Log($"[RemoteConfig] Override — {field.Name}: {oldValue} → {newValue}");
                    overrideCount++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[RemoteConfig] Failed to apply '{field.Name}': {e.Message}");
                }
            }

            Debug.Log($"[RemoteConfig] Applied {overrideCount} remote override(s) to GameConfigs.");
        }

        private static FieldInfo[] GetGameConfigsFields()
        {
            return typeof(GameConfigs).GetFields(BindingFlags.Public | BindingFlags.Instance);
        }
        
        private static object ConvertValue(Type fieldType, ConfigValue configValue)
        {
            if (fieldType == typeof(float)) 
                return (float)configValue.DoubleValue;
            if (fieldType == typeof(double)) 
                return configValue.DoubleValue;
            if (fieldType == typeof(int))    
                return (int)configValue.LongValue;
            if (fieldType == typeof(long))   
                return configValue.LongValue;
            if (fieldType == typeof(bool))  
                return configValue.BooleanValue;
            if (fieldType == typeof(string))
                return configValue.StringValue;

            return null; // Unsupported type 
        }
    }
}
