using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class CloudSaveManager : IInitializable, IDisposable
    {
        private readonly DataManager _dataManager;

        public CloudSaveManager(DataManager dataManager)
        {
            _dataManager = dataManager;
            _dataManager.OnLocalDataSaved += SaveToCloud;
        }

        public async void Initialize()
        {
            try
            {
                await UnityServices.InitializeAsync();
                
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                Debug.Log($"Signed in to Unity Services. PlayerId: {AuthenticationService.Instance.PlayerId}");
                
                // Fetch cloud data
                await LoadFromCloud();
            }
            catch (Exception e)
            {
                Debug.LogError($"Cloud Save Initialization Error: {e.Message}");
            }
        }

        public void Dispose()
        {
            if (_dataManager != null)
                _dataManager.OnLocalDataSaved -= SaveToCloud;
        }

        public async Task LoadFromCloud()
        {
            try
            {
                var savedData = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { TGS.Core.TGSIntegrations.CLOUD_SAVE_KEY });
                
                if (savedData.TryGetValue(TGS.Core.TGSIntegrations.CLOUD_SAVE_KEY, out var item))
                {
                    string json = item.Value.GetAs<string>();
                    UserData cloudData = JsonUtility.FromJson<UserData>(json);
                    
                    if (cloudData != null)
                    {
                        // Compare with local data
                        if (cloudData.LastUpdated > _dataManager.UserData.LastUpdated)
                        {
                            _dataManager.UpdateDataFromCloud(cloudData);
                            Debug.Log("Cloud data was newer. Local data updated.");
                        }
                        else if (cloudData.LastUpdated < _dataManager.UserData.LastUpdated)
                        {
                            // Local is newer, push to cloud
                            SaveToCloud();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not load from cloud: {e.Message}");
            }
        }

        public async void SaveToCloud()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized || !AuthenticationService.Instance.IsSignedIn)
            {
                return; // Wait until ready
            }

            try
            {
                string json = JsonUtility.ToJson(_dataManager.UserData);
                var data = new Dictionary<string, object>
            {
                { TGS.Core.TGSIntegrations.CLOUD_SAVE_KEY, json }
            };    await CloudSaveService.Instance.Data.Player.SaveAsync(data);
                // Debug.Log("Data saved to cloud."); // Uncomment to spam logs
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to save to cloud: {e.Message}");
            }
        }
    }
}
