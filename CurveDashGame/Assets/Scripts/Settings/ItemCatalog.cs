using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "Curve Dash/Catalogs/Item Catalog")]
    public class ItemCatalog : ScriptableObject
    {
        public List<ItemData> Items;
        
        public ItemData GetItem(string id)
        {
            return Items.Find(i => i.name == id);
        }
    }
}
