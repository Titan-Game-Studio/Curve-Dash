using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class DataManager : IInitializable
    {
        private const string SaveKey = "CurveDash_UserData";
        
        public UserData UserData { get; private set; }

        public void Initialize()
        {
            LoadData();
        }

        public void LoadData()
        {
            if (PlayerPrefs.HasKey(SaveKey))
            {
                string json = PlayerPrefs.GetString(SaveKey);
                try
                {
                    UserData = JsonUtility.FromJson<UserData>(json);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DataManager] Error loading save data: {e.Message}");
                }
            }
            
            if (UserData == null)
            {
                UserData = new UserData();
            }
        }

        public void SaveData()
        {
            string json = JsonUtility.ToJson(UserData);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
        }

        public void AddCoin(int amount)
        {
            if (UserData == null) LoadData();
            UserData.TotalCoins += amount;
            SaveData();
        }
    }
}
