using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "GameObjectRefCatalog", menuName = "Curve-Dash/Catalogs/GameObject Reference Catalog")]
    public class GameObjectReferenceCatalog : ScriptableObject
    {
        public List<AssetReferenceGameObject> References;
    }
}

