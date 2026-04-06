using System;
using System.Reflection;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace Game
{
    public struct UserAttributes { }
    public struct AppAttributes { }

    public class RemoteConfigManager : MonoBehaviour
    {
        [SerializeField] private bool _useLocalConfig = false;

        public bool IsInitialized { get; private set; }

        private Action _onCompleted;

        /// <summary>
        /// Initializes Unity Gaming Services, fetches remote config,
        /// and overrides any GameConfigs field whose name matches a remote key.
        /// Always calls onCompleted — even if UGS is unavailable or fetch fails.
        /// When _useLocalConfig is true, skips fetch entirely and uses GameConfigs as-is.
        /// </summary>
        public void Initialize(Action onCompleted = null)
        {
            _onCompleted = onCompleted;

            if (_useLocalConfig)
            {
                Debug.Log("[RemoteConfig] Local config mode — skipping UGS fetch.");
                Complete();
                return;
            }

            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                GameConfigs.CreateRuntimeCopy();

                RemoteConfigService.Instance.FetchCompleted += OnFetchCompleted;
                await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RemoteConfig] Init/fetch failed: {e.Message}. Using local defaults.");
                RemoteConfigService.Instance.FetchCompleted -= OnFetchCompleted;
                Complete();
            }
        }

        private void OnFetchCompleted(ConfigResponse response)
        {
            RemoteConfigService.Instance.FetchCompleted -= OnFetchCompleted;

            if (response.requestOrigin == ConfigOrigin.Remote)
                ApplyToGameConfigs();
            else
                Debug.LogWarning($"[RemoteConfig] Config came from {response.requestOrigin}. Using local defaults.");

            Complete();
        }

        private void Complete()
        {
            IsInitialized = true;
            _onCompleted?.Invoke();
            _onCompleted = null;
        }

        private static void ApplyToGameConfigs()
        {
            var config = RemoteConfigService.Instance.appConfig;
            int overrideCount = 0;

            foreach (var field in GetGameConfigsFields())
            {
                if (!config.HasKey(field.Name))
                    continue;

                try
                {
                    object newValue = GetRemoteValue(field.FieldType, field.Name, config);
                    if (newValue == null)
                        continue;

                    field.SetValue(GameConfigs.Instance, newValue);
                    overrideCount++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[RemoteConfig] Failed to apply '{field.Name}': {e.Message}");
                }
            }

            Debug.Log($"[RemoteConfig] Applied {overrideCount} remote override(s) to GameConfigs.");
        }

        private static object GetRemoteValue(Type fieldType, string key, RuntimeConfig config)
        {
            if (fieldType == typeof(float))  return config.GetFloat(key);
            if (fieldType == typeof(double)) return (double)config.GetFloat(key);
            if (fieldType == typeof(int))    return config.GetInt(key);
            if (fieldType == typeof(long))   return (long)config.GetInt(key);
            if (fieldType == typeof(bool))   return config.GetBool(key);
            if (fieldType == typeof(string)) return config.GetString(key);

            return null; // Unsupported type
        }

        private static FieldInfo[] GetGameConfigsFields()
        {
            return typeof(GameConfigs).GetFields(BindingFlags.Public | BindingFlags.Instance);
        }
    }
}
