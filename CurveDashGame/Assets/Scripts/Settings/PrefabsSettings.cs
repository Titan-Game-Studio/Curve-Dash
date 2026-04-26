using System;
using UnityEngine;

namespace STG.CurveDash
{
    [CreateAssetMenu(fileName = "GamePrefabs", menuName = "Curve Dash/Prefabs Settings")]
    public class PrefabsSettings : ScriptableObject
    {
        [Header("Player & Environment")]
        public GameObject PlayerPrefab;
        public GameObject BlockPartPrefab;
        public GameObject BlockPrefab;
        
        [Header("Pickups & Items")]
        public GameObject CrystalPrefab;
        public GameObject ShieldPrefab;
        public GameObject WeaponPickupPrefab;
        public GameObject MountPickupPrefab;
        public GameObject AuraPickupPrefab;

        [Header("Enemies & Obstacles")]
        public GameObject ObstaclePrefab;

        [Header("Decorations")]
        public GameObject CloudPrefab;

        [Header("Data")]
        public GameAssetCatalog GameAssetCatalog;
    }
}