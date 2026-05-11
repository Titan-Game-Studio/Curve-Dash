using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "ShopItemCatalog", menuName = "Curve-Dash/Catalogs/Shop Item Catalog")]
    public class ShopItemCatalog : ScriptableObject
    {
        public List<ShopItemConfig> Items;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Items == null) return;
            
            foreach (var item in Items)
            {
                if (item != null && item.Prefab != null && item.Prefab.editorAsset != null)
                {
                    string currentPrefabName = item.Prefab.editorAsset.name;

                    // Nếu người dùng vừa kéo 1 Asset khác vào ô Prefab
                    if (item._lastPrefabName != currentPrefabName)
                    {
                        item.Name = currentPrefabName;
                        item._lastPrefabName = currentPrefabName;
                    }
                }
                else if (item != null)
                {
                    // Nếu người dùng xóa asset khỏi ô Prefab
                    item._lastPrefabName = "";
                }
            }
        }
#endif
    }
}

