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
    public struct AudioMapping
    {
        public AudioKey Key;
        public AssetReferenceT<AudioClip> Reference;
    }

    [CreateAssetMenu(menuName = "CurveDash/Asset Catalog")]
    public class AssetCatalog : ScriptableObject
    {
        public List<AssetReferenceGameObject> BallSkins;
        public List<AssetReferenceGameObject> VFXs;
        public List<AssetReferenceGameObject> Characters;
        public List<AssetReferenceGameObject> Obstacles;
        public List<AudioMapping> AudioAssets;
    }
}