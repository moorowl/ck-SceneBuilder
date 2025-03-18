using System.Collections.Generic;
using System.Linq;
using SceneBuilder.Structures;
using SceneBuilder.Utilities;
using SceneBuilder.Utilities.DataStructures;
using Unity.Mathematics;
using UnityEngine;

namespace SceneBuilder.Scenes {
	public static class Converter {
		public static void ConvertAndInjectScene(Identifier id, SceneFile sceneFile, StructureFile structureFile) {
			var serverCustomSceneAuthoring = Manager.ecs.serverAuthoringPrefab.GetComponentInChildren<CustomSceneAuthoring>();
			var sceneForDataTable = ConvertSceneToDataTable(id, sceneFile, structureFile);
			
			serverCustomSceneAuthoring.CustomScenesDataTable.scenes.Add(sceneForDataTable);
			
			// todo guaranteed generation
			// create a gameobject with serverAuthoringPrefab as its parent, add PugWorldGenAuthoring
		}
		
		private static CustomScenesDataTable.Scene ConvertSceneToDataTable(Identifier id, SceneFile sceneFile, StructureFile structureFile) {
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
				var matchingPrefab = Manager.ecs.pugDatabase.prefabList.FirstOrDefault(prefab => {
					if (!prefab.gameObject.TryGetComponent<EntityMonoBehaviourData>(out var entityMonoBehaviourData))
						return false;
					
					return entityMonoBehaviourData.objectInfo.objectID == objectData.Id && entityMonoBehaviourData.objectInfo.variation == objectData.Variation;
				});
				if (matchingPrefab == null)
					continue;
				
				prefabs.Add(matchingPrefab.gameObject);
				prefabPositions.Add(objectData.Position.ToFloat3());
				prefabInventoryOverrides.Add(new CustomScenesDataTable.InventoryOverride());
			}

			return new CustomScenesDataTable.Scene {
				sceneName = id.AsSceneName,
				maxOccurrences = sceneFile.Generation.Random?.MaxOccurrences ?? 0,
				replacedByContentBundle = new OptionalValue<ContentBundleID>(),
				biomesToSpawnIn = new WorldGenerationTypeDependentValue<List<Biome>> {
					classic = new List<Biome>(),
					fullRelease = sceneFile.Generation.Random?.BiomesToSpawnIn ?? new List<Biome>()
				},
				minDistanceFromCoreInClassicWorlds = 0,
				canFlipX = sceneFile.Generation.CanFlip is FlipDirection.Horizontal or FlipDirection.HorizontalAndVertical,
				canFlipY = sceneFile.Generation.CanFlip is FlipDirection.Vertical or FlipDirection.HorizontalAndVertical,
				hasCenter = false,
				center = new int2(),
				boundsSize = new int2(math.abs(smallestTilePosition.x) + math.abs(largestTilePosition.x), math.abs(smallestTilePosition.y) + math.abs(largestTilePosition.y)),
				radius = math.lengthsq(smallestTilePosition + largestTilePosition),
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