﻿using HarmonyLib;
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

			var scenes = customScenesDataTable.scenes;
			
			using var builder = new BlobBuilder(Allocator.Temp);
			var objectProperties = builder.Allocate(ref builder.ConstructRoot<SceneObjectPropertiesTableBlob>().Scenes, scenes.Count);
			
			for (var sceneIndex = 0; sceneIndex < scenes.Count; sceneIndex++) {
				var scene = scenes[sceneIndex];

				if (!SceneLoader.Instance.TryGetFromRuntimeName(scene.sceneName, out var sceneFile))
					continue;
				if (!StructureLoader.Instance.TryGetStructure(sceneFile.Structure, out var structureFile))
					continue;
				
				var prefabAmounts = builder.Allocate(ref objectProperties[sceneIndex].PrefabAmounts, structureFile.Objects.Count);
				var prefabDirections = builder.Allocate(ref objectProperties[sceneIndex].PrefabDirections, structureFile.Objects.Count);
				var prefabColors = builder.Allocate(ref objectProperties[sceneIndex].PrefabColors, structureFile.Objects.Count);
				var prefabDescriptions = builder.Allocate(ref objectProperties[sceneIndex].PrefabDescriptions, structureFile.Objects.Count);
				var prefabDropsLootTable = builder.Allocate(ref objectProperties[sceneIndex].PrefabDropsLootTable, structureFile.Objects.Count);
				
				for (var i = 0; i < structureFile.Objects.Count; i++) {
					var objectData = structureFile.Objects[i];
					var properties = objectData.Properties;
						
					prefabAmounts[i] = properties.Amount ?? -1;
					
					if (properties.Direction != null)
						prefabDirections[i] = properties.Direction.Value;
					
					if (properties.Color != null)
						prefabColors[i] = properties.Color.Value;
						
					if (properties.Description != null)
						prefabDescriptions[i] = properties.Description;
					
					prefabDropsLootTable[i] = properties.DropsLootTable.GetValueOrDefault((LootTableID) (-1));
				}
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