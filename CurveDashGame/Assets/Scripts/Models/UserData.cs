using System;
using System.Collections.Generic;

namespace STG.CurveDash
{
    [Serializable]
    public class UserData
    {
        public int TotalCoins = 0;
        public string CurrentCharacterId = "Knight_Default";

        
        // Cấu trúc Equipment mới (PoE Style)
        // Key là EquipmentSlot, Value là ID của ScriptableObject (hoặc index)
        public Dictionary<EquipmentSlot, string> EquippedItems = new Dictionary<EquipmentSlot, string>();
        
        // Shop/Unlock system
        public int CurrentMountSkin = 0;
        public int CurrentBlockPartSkin = 0;
        public int CurrentVFX = 0;
        
        public List<int> UnlockedMountSkins = new List<int> { 0 };
        public List<int> UnlockedBlockPartSkins = new List<int> { 0 };
        public List<int> UnlockedVFXs = new List<int> { 0 };
        
        // List các Item ID đã sở hữu
        public List<string> OwnedItems = new List<string>();

        public long LastUpdated;
    }
}
