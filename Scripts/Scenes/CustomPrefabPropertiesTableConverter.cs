using HarmonyLib;
using Pug.Conversion;
using SceneBuilder.Structures;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SceneBuilder.Scenes {
	public class CustomPrefabPropertiesTableConverter : PostConverter {
		public override bool CanRunInStagingWorld => false;

		public override void PostConvert(GameObject authoring) {
			if (!TryGetActiveComponent<CustomSceneAuthoring>(authoring, out var customSceneAuthoring))
				return;

			var customScenesDataTable = customSceneAuthoring.CustomScenesDataTable;
			if (customScenesDataTable == null)
				return;

			var scenes = customScenesDataTable.scenes;

			using var builder = new BlobBuilder(Allocator.Temp);
			var customPrefabProperties = builder.Allocate(ref builder.ConstructRoot<CustomPrefabPropertiesTable>().Scenes, scenes.Count);

			for (var sceneIndex = 0; sceneIndex < scenes.Count; sceneIndex++) {
				var scene = scenes[sceneIndex];

				if (!SceneLoader.Instance.TryGetFromRuntimeName(scene.sceneName, out var sceneFile))
					continue;
				if (!StructureLoader.Instance.TryGetStructure(sceneFile.Structure, out var structureFile))
					continue;
				
				var prefabVariations = builder.Allocate(ref customPrefabProperties[sceneIndex].PrefabVariations, structureFile.Objects.Count);
				var prefabAmounts = builder.Allocate(ref customPrefabProperties[sceneIndex].PrefabAmounts, structureFile.Objects.Count);
				var prefabDescriptions = builder.Allocate(ref customPrefabProperties[sceneIndex].PrefabDescriptions, structureFile.Objects.Count);
				var prefabGrowthStages = builder.Allocate(ref customPrefabProperties[sceneIndex].PrefabGrowthStages, structureFile.Objects.Count);
				var prefabDropsLootTable = builder.Allocate(ref customPrefabProperties[sceneIndex].PrefabDropsLootTable, structureFile.Objects.Count);

				for (var i = 0; i < structureFile.Objects.Count; i++) {
					var objectData = structureFile.Objects[i];
					var properties = objectData.Properties;

					prefabVariations[i] = objectData.Variation;
					prefabAmounts[i] = properties.Amount ?? -1;
					prefabDescriptions[i] = properties.Description ?? string.Empty;
					prefabGrowthStages[i] = properties.GrowthStage ?? -1;
					prefabDropsLootTable[i] = properties.DropsLootTable.GetValueOrDefault((LootTableID) (-1));
				}
			}

			var tableReference = builder.CreateBlobAssetReference<CustomPrefabPropertiesTable>(Allocator.Persistent);
			BlobAssetStore.TryAdd(ref tableReference);
			EntityManager.AddComponentData(GetEntity(authoring), new CustomPrefabPropertiesTableCD {
				Value = tableReference
			});
		}

		[HarmonyPatch]
		public static class Patches {
			[HarmonyPatch(typeof(ECSManager), nameof(ECSManager.ConfigurePostConverters))]
			[HarmonyPostfix]
			public static void ConfigurePostConverters(ConversionManager conversionManager) {
				conversionManager.AddPostConverter(new CustomPrefabPropertiesTableConverter());
			}
		}
	}
}