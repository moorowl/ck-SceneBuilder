using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PugMod;
using SceneBuilder.Structures;
using SceneBuilder.Utilities;
using SceneBuilder.Utilities.DataStructures;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace SceneBuilder.Scenes {
	public class SceneFile {
		[JsonProperty("structure")]
		public Identifier Structure;
		
		[JsonProperty("generation")]
		public GenerationSettings Generation;

		public class GenerationSettings {
			[JsonProperty("canFlip")]
			[JsonConverter(typeof(StringEnumConverter))]
			public FlipDirection CanFlip;
			
			[JsonProperty("random")]
			public RandomGenerationSettings Random;
		}

		public class RandomGenerationSettings {
			[JsonProperty("maxOccurrences")]
			public int MaxOccurrences;
			
			[JsonProperty("biomesToSpawnIn", ItemConverterType = typeof(StringEnumConverter))]
			public List<Biome> BiomesToSpawnIn;
		}
		
		public CustomScenesDataTable.Scene ConvertToDataTableScene(Identifier id, StructureFile structureFile) {
			var maps = GetMaps(structureFile);
				
			var smallestTilePosition = (int2) int.MaxValue;
			var largestTilePosition = (int2) int.MinValue;
			foreach (var map in maps) {
				using var tileIterator = map.mapData.GetTileIterator();
				while (tileIterator.MoveNext()) {
					var position = tileIterator.CurrentPosition.ToInt2() + map.localPosition;
					smallestTilePosition = math.min(smallestTilePosition, position);
					largestTilePosition = math.max(largestTilePosition, position);
				}
			}
			
			var prefabs = new List<GameObject>();
			var prefabPositions = new List<Vector3>();
			var prefabInventoryOverrides = new List<CustomScenesDataTable.InventoryOverride>();
			
			foreach (var objectData in structureFile.Objects) {
				if (!Utils.TryFindMatchingPrefab(objectData.Id, objectData.Variation, out var prefab))
					continue;
				
				prefabs.Add(prefab);
				prefabPositions.Add(objectData.Position.ToFloat3());

				var inventoryOverride = new CustomScenesDataTable.InventoryOverride();
				if (objectData.Properties.Inventory is { Count: > 0 } && prefab.TryGetComponent<InventoryAuthoring>(out var inventoryAuthoring)) {
					inventoryOverride.hasAnyInventoryOverride = true;
					inventoryOverride.hasItemsOverride = true;
					inventoryOverride.itemsOverride = new List<ObjectData>();
					inventoryOverride.itemsToRemove = inventoryAuthoring.itemsInInventory.Count;
					
					var itemsBySlot = objectData.Properties.Inventory.ToDictionary(x => x.Slot, x => x);
					var highestSlotIndex = itemsBySlot.Keys.Max();

					for (var i = 0; i <= highestSlotIndex; i++) {
						if (itemsBySlot.TryGetValue(i, out var inventoryItem)) {
							inventoryOverride.itemsOverride.Add(new ObjectData {
								objectID = inventoryItem.Id,
								variation = inventoryItem.Variation,
								amount = inventoryItem.Amount
							});
						} else {
							inventoryOverride.itemsOverride.Add(new ObjectData());
						}
					}
				}
				if (objectData.Properties.InventoryLootTable != null) {
					inventoryOverride.hasAnyInventoryOverride = true;
					inventoryOverride.hasLootTableOverride = true;
					inventoryOverride.lootTableOverride = objectData.Properties.InventoryLootTable.Value;
				}
				
				prefabInventoryOverrides.Add(inventoryOverride);
			}

			var boundsSize = new int2(math.abs(smallestTilePosition.x) + math.abs(largestTilePosition.x), math.abs(smallestTilePosition.y) + math.abs(largestTilePosition.y));
			return new CustomScenesDataTable.Scene {
				sceneName = id.AsSceneName,
				maxOccurrences = Generation.Random?.MaxOccurrences ?? 0,
				replacedByContentBundle = new OptionalValue<ContentBundleID>(),
				biomesToSpawnIn = new WorldGenerationTypeDependentValue<List<Biome>> {
					classic = new List<Biome>(),
					fullRelease = Generation.Random?.BiomesToSpawnIn ?? new List<Biome>()
				},
				minDistanceFromCoreInClassicWorlds = 0,
				canFlipX = Generation.CanFlip is FlipDirection.Horizontal or FlipDirection.HorizontalAndVertical,
				canFlipY = Generation.CanFlip is FlipDirection.Vertical or FlipDirection.HorizontalAndVertical,
				hasCenter = false,
				center = new int2(),
				boundsSize = boundsSize,
				radius = math.ceil(math.max(boundsSize.x, boundsSize.y) / 2f) + 1f,
				maps = maps,
				prefabs = prefabs,
				prefabPositions = prefabPositions,
				prefabInventoryOverrides = prefabInventoryOverrides
			};
		}
		
		private static List<CustomScenesDataTable.Map> GetMaps(StructureFile structureFile) {
			var mapDataModifier = new PugMapDataModifier(new PugMapData());

			foreach (var entry in structureFile.Tiles) {
				foreach (var position in entry.Positions) {
					mapDataModifier.Set(position.ToVec3Int(), (int) entry.Tileset, entry.TileType);
				}
			}
			
			return new List<CustomScenesDataTable.Map> {
				new() {
					localPosition = new int2(0, 0),
					mapData = mapDataModifier.GetMapData()
				}
			};
		}
	}
}