using System;
using UnityEngine;
using System.Collections.Generic;
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
    }

    [Serializable]
    public struct AudioMapping
    {
        public AudioKey Key;
        public AssetReferenceT<AudioClip> Reference;
    }

    [CreateAssetMenu(menuName = "CurveDash/Asset Catalog")]
    public class AssetCatalog : ScriptableObject
    {
        public List<ShopItemConfig> BallSkins;
        public List<ShopItemConfig> BlockPartSkins;
        public List<ShopItemConfig> VFXs;
        public List<ShopItemConfig> Characters;

        public List<AssetReferenceGameObject> Obstacles;
        public List<AssetReferenceGameObject> Clouds;
        public List<AudioMapping> AudioAssets;
    }
}