using System.Linq;
using HarmonyLib;
using PugConversion;
using SceneBuilder.Structures;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace SceneBuilder.Scenes {
	public class SceneObjectPropertiesTableConverter : PugPostConverter {
		public override bool CanRunInStagingWorld => false;

		public override void PostConvert(GameObject authoring) {
			if (!TryGetActiveComponent<CustomSceneAuthoring>(authoring, out var customSceneAuthoring))
				return;

			var customScenesDataTable = customSceneAuthoring.CustomScenesDataTable;
			if (customScenesDataTable == null)
				return;

			var scenes = customScenesDataTable.scenes
				.Where(scene => SceneLoader.IsModdedScene(scene.sceneName))
				.ToDictionary(scene => scene.sceneName, scene => {
					SceneLoader.Instance.TryGetSceneFromRuntimeName(scene.sceneName, out var sceneFile);
					StructureLoader.Instance.TryGetStructure(sceneFile.Structure, out var structureFile);
					return structureFile;
				});
			
			using var builder = new BlobBuilder(Allocator.Temp);
			var objectProperties = builder.Allocate(ref builder.ConstructRoot<SceneObjectPropertiesTableBlob>().Scenes, scenes.Count);

			var sceneIndex = 0;
			foreach (var (sceneName, sceneFile) in scenes) {
				objectProperties[sceneIndex].SceneName = sceneName;
				
				var prefabDirections = builder.Allocate(ref objectProperties[sceneIndex].PrefabDirections, sceneFile.Objects.Count);
				var prefabColors = builder.Allocate(ref objectProperties[sceneIndex].PrefabColors, sceneFile.Objects.Count);
				var prefabDropsLootTable = builder.Allocate(ref objectProperties[sceneIndex].PrefabDropsLootTable, sceneFile.Objects.Count);
				
				for (var i = 0; i < sceneFile.Objects.Count; i++) {
					var properties = sceneFile.Objects[i].Properties;
					
					if (properties.Direction != null)
						prefabDirections[i] = properties.Direction.Value;
					
					if (properties.Color != null)
						prefabColors[i] = properties.Color.Value;
					
					prefabDropsLootTable[i] = properties.DropsLootTable.GetValueOrDefault((LootTableID) (-1));
				}

				sceneIndex++;
			}
			
			var tableReference = builder.CreateBlobAssetReference<SceneObjectPropertiesTableBlob>(Allocator.Persistent);
			BlobAssetStore.TryAdd(ref tableReference);
			EntityManager.AddComponentData(GetEntity(authoring), new SceneObjectPropertiesTable {
				Value = tableReference
			});
		}

		[HarmonyPatch]
		public static class Patches {
			[HarmonyPatch(typeof(ECSManager), nameof(ECSManager.ConfigurePostConverters))]
			[HarmonyPostfix]
			public static void ConfigurePostConverters(ConversionManager conversionManager) {
				conversionManager.AddPostConverter(new SceneObjectPropertiesTableConverter());
			}
		}
	}
}