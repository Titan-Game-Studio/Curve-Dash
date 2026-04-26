using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace STG.CurveDash
{
    public enum AudioKey
    {
        BallHitCrystal,
        GameStart,
        BallFall,
        BallTurn,
        NextLevel,
        BGM_Thundershade,
        BGM_Soulforge,
        BGM_Wolfspire
    }
    
    [Serializable]
    public class ShopItemConfig
    {
        public string Name = "New Item";
        public int Price = 100;
        public AssetReferenceGameObject Prefab;
        public AssetReferenceT<Sprite> Icon;

#if UNITY_EDITOR
        [HideInInspector]
        public string _lastPrefabName = "";
#endif
    }

    [Serializable]
    public struct AudioMapping
    {
        public AudioKey Key;
        public AssetReferenceT<AudioClip> Reference;
    }
}
