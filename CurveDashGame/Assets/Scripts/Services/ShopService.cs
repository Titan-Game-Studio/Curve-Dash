using UnityEngine;
using Zenject;

namespace STG.CurveDash
{
    public class ShopService
    {
        private readonly DataManager _dataManager;

        public event System.Action<int> OnBallSkinEquipped;
        public event System.Action<int> OnVFXEquipped;
        public event System.Action<int> OnCharacterEquipped;
        public event System.Action<int> OnBlockPartSkinEquipped;

        public ShopService(DataManager dataManager)
        {
            _dataManager = dataManager;
        }

        #region BALL SKINS

        public bool TryUnlockBallSkin(int index, int cost)
        {
            if (IsBallSkinUnlocked(index)) return false;

            if (_dataManager.UserData.TotalCoins >= cost)
            {
                _dataManager.UserData.TotalCoins -= cost;
                _dataManager.UserData.UnlockedBallSkins.Add(index);
                _dataManager.SaveData();
                return true;
            }
            return false;
        }

        public bool IsBallSkinUnlocked(int index)
        {
            return _dataManager.UserData.UnlockedBallSkins.Contains(index);
        }

        public void EquipBallSkin(int index)
        {
            if (IsBallSkinUnlocked(index))
            {
                _dataManager.UserData.CurrentBallSkin = index;
                _dataManager.SaveData();
                OnBallSkinEquipped?.Invoke(index);
            }
        }

        #endregion

        #region BLOCK PART SKINS

        public bool TryUnlockBlockPartSkin(int index, int cost)
        {
            if (IsBlockPartSkinUnlocked(index)) return false;

            if (_dataManager.UserData.TotalCoins >= cost)
            {
                _dataManager.UserData.TotalCoins -= cost;
                _dataManager.UserData.UnlockedBlockPartSkins.Add(index);
                _dataManager.SaveData();
                return true;
            }
            return false;
        }

        public bool IsBlockPartSkinUnlocked(int index)
        {
            return _dataManager.UserData.UnlockedBlockPartSkins.Contains(index);
        }

        public void EquipBlockPartSkin(int index)
        {
            if (IsBlockPartSkinUnlocked(index))
            {
                _dataManager.UserData.CurrentBlockPartSkin = index;
                _dataManager.SaveData();
                OnBlockPartSkinEquipped?.Invoke(index);
            }
        }

        #endregion

        #region VFX

        public bool TryUnlockVFX(int index, int cost)
        {
            if (IsVFXUnlocked(index)) return false;

            if (_dataManager.UserData.TotalCoins >= cost)
            {
                _dataManager.UserData.TotalCoins -= cost;
                _dataManager.UserData.UnlockedVFXs.Add(index);
                _dataManager.SaveData();
                return true;
            }
            return false;
        }

        public bool IsVFXUnlocked(int index)
        {
            return _dataManager.UserData.UnlockedVFXs.Contains(index);
        }

        public void EquipVFX(int index)
        {
            if (IsVFXUnlocked(index))
            {
                _dataManager.UserData.CurrentVFX = index;
                _dataManager.SaveData();
                OnVFXEquipped?.Invoke(index);
            }
        }

        #endregion

        #region CHARACTERS

        public bool TryUnlockCharacter(int index, int cost)
        {
            if (IsCharacterUnlocked(index)) return false;

            if (_dataManager.UserData.TotalCoins >= cost)
            {
                _dataManager.UserData.TotalCoins -= cost;
                _dataManager.UserData.UnlockedCharacters.Add(index);
                _dataManager.SaveData();
                return true;
            }
            return false;
        }

        public bool IsCharacterUnlocked(int index)
        {
            return _dataManager.UserData.UnlockedCharacters.Contains(index);
        }

        public void EquipCharacter(int index)
        {
            if (IsCharacterUnlocked(index))
            {
                _dataManager.UserData.CurrentCharacter = index;
                _dataManager.SaveData();
                OnCharacterEquipped?.Invoke(index);
            }
        }

        #endregion
    }
}
