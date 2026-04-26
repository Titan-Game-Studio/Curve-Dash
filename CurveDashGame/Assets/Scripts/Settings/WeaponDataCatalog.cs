using System.Collections.Generic;
using UnityEngine;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "WeaponDataCatalog", menuName = "Curve Dash/Catalogs/Weapon Data Catalog")]
    public class WeaponDataCatalog : ScriptableObject
    {
        public List<WeaponData> Weapons;
    }
}
