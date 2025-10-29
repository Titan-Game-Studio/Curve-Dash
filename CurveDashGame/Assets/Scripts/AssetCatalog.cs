using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;

namespace STG.CurveDash
{
    [CreateAssetMenu(menuName = "CurveDash/Asset Catalog")]
    public class AssetCatalog : ScriptableObject
    {
        public List<AssetReferenceGameObject> BallSkins;
        public List<AssetReferenceGameObject> VFXs;
        public List<AssetReferenceGameObject> Characters;
        public List<AssetReferenceGameObject> Obstacles;
    }
}