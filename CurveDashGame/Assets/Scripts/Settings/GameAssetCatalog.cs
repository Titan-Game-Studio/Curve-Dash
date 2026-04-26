using UnityEngine;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "GameAssetCatalog", menuName = "Curve Dash/Catalogs/Master Game Asset Catalog")]
    public class GameAssetCatalog : ScriptableObject
    {
        [Header("Shop Items")]
        public ShopItemCatalog MountSkins; // Formerly MountSkins
        public ShopItemCatalog BlockPartSkins;
        public ShopItemCatalog Auras;
        public ShopItemCatalog Characters;

        [Header("Weapons")]
        public WeaponDataCatalog Weapons;

        [Header("Environment")]
        public GameObjectReferenceCatalog Obstacles;
        public GameObjectReferenceCatalog Clouds;

        [Header("Audio")]
        public AudioMappingCatalog AudioCatalog;
    }
}

