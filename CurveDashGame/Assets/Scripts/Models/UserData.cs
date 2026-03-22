using System;
using System.Collections.Generic;

namespace STG.CurveDash
{
    [Serializable]
    public class UserData
    {
        public int TotalCoins = 0;
        
        public int CurrentBallSkin = 0;
        public int CurrentBlockPartSkin = 0;
        public int CurrentVFX = 0;
        public int CurrentCharacter = 0;
        
        public List<int> UnlockedBallSkins = new List<int> { 0 };
        public List<int> UnlockedBlockPartSkins = new List<int> { 0 };
        public List<int> UnlockedVFXs = new List<int> { 0 };
        public List<int> UnlockedCharacters = new List<int> { 0 };

        public long LastUpdated;
    }
}
