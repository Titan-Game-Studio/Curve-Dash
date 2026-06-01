using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DevionGames.InventorySystem{
	[System.Serializable]
	public class ItemDatabase : ScriptableObject {
		public List<Item> items = new List<Item>();
        public List<Currency> currencies = new List<Currency>();
        public List<Rarity> raritys = new List<Rarity>();
		public List<Category> categories = new List<Category>();
		public List<EquipmentRegion> equipments = new List<EquipmentRegion>();
        public List<ItemGroup> itemGroups = new List<ItemGroup>();
        public List<Configuration.Settings> settings = new List<Configuration.Settings>();

		public List<Item> allItems {
			get {
				List<Item> all = new List<Item>(items);
				all.AddRange(currencies);
				return all;
			}
		}

		public GameObject GetItemPrefab(string name){
			for (int i=0; i< items.Count; i++) {
				Item item=items[i];
				if(item != null && item.Prefab != null && item.Prefab.name == name){
					return item.Prefab;
				}
			}
			return null;
		}

		public ItemGroup GetItemGroup(string name) {
			return itemGroups.First(x => x.Name == name);
		}

		public void Merge(ItemDatabase database) {
			items.AddRange(database.items.Where(y => y != null && !items.Any(z => z != null && z.Name == y.Name)));
			currencies.AddRange(database.currencies.Where(y => y != null && !currencies.Any(z => z != null && z.Name == y.Name)));
			raritys.AddRange(database.raritys.Where(y => y != null && !raritys.Any(z => z != null && z.Name == y.Name)));
			categories.AddRange(database.categories.Where(y => y != null && !categories.Any(z => z != null && z.Name == y.Name)));
			equipments.AddRange(database.equipments.Where(y => y != null && !equipments.Any(z => z != null && z.Name == y.Name)));
			itemGroups.AddRange(database.itemGroups.Where(y => y != null && !itemGroups.Any(z => z != null && z.Name == y.Name)));
		}
	}
}