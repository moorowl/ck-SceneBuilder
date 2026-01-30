using HarmonyLib;
using Pug.Properties;
using PugWorldGen;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace SceneBuilder.Scenes {
	[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	[UpdateBefore(typeof(ApplySpawnedCustomSceneSystem))]
	[UpdateBefore(typeof(DungeonApplySpawnedObjectsSystem))]
	public partial struct ApplyCustomPrefabPropertiesSystem : ISystem {
		public static BlobAssetReference<CustomPrefabPropertiesTable> CustomPrefabPropertiesTable;
		public static ComponentLookup<ObjectPropertiesCD> ObjectPropertiesLookup;
		public static ComponentLookup<GrowingCD> GrowingLookup;
		public static ComponentLookup<DropsLootFromLootTableCD> DropsLootFromLootTableLookup;
		public static BufferLookup<DescriptionBuffer> DescriptionLookup;
		
		public static NativeHashMap<FixedString64Bytes, int> SceneNameToIndex;
		
		private bool _hasSetupSceneNameToIndex;
		
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<CustomPrefabPropertiesTableCD>();
			state.RequireForUpdate<CustomSceneTableCD>();

			ObjectPropertiesLookup = state.GetComponentLookup<ObjectPropertiesCD>();
			GrowingLookup = state.GetComponentLookup<GrowingCD>();
			DropsLootFromLootTableLookup = state.GetComponentLookup<DropsLootFromLootTableCD>();
			DescriptionLookup = state.GetBufferLookup<DescriptionBuffer>();
		}

		public void OnDestroy(ref SystemState state) {
			SceneNameToIndex.Dispose();
			_hasSetupSceneNameToIndex = false;
		}

		public void OnUpdate(ref SystemState state) {
			CustomPrefabPropertiesTable = SystemAPI.GetSingleton<CustomPrefabPropertiesTableCD>().Value;
			
			ObjectPropertiesLookup.Update(ref state);
			GrowingLookup.Update(ref state);
			DropsLootFromLootTableLookup.Update(ref state);
			DescriptionLookup.Update(ref state);

			if (!_hasSetupSceneNameToIndex) {
				var customScenesTable = SystemAPI.GetSingleton<CustomSceneTableCD>().Value;
				ref var scenes = ref customScenesTable.Value.scenes;

				SceneNameToIndex = new NativeHashMap<FixedString64Bytes, int>(scenes.Length, Allocator.Persistent);
				
				for (var i = 0; i < scenes.Length; i++) {
					ref var scene = ref scenes[i];
					SceneNameToIndex.Add(scene.sceneName, i);
				}

				_hasSetupSceneNameToIndex = true;
			}
		}

		[HarmonyPatch]
		public static class Patches {
			[HarmonyPatch(typeof(CustomScenePrefabUtility), "ApplyOverrides")]
			[HarmonyPostfix]
			public static void CustomScenePrefabUtility_ApplyOverrides(EntityCommandBuffer ecb, Entity entity, int scenePrefabIndex, ref CustomSceneBlob blob, CustomScenePrefabUtility.Lookups lookups, CustomScenePrefabUtility.Data data) {
				if (!SceneNameToIndex.TryGetValue(blob.sceneName, out var sceneIndex) || !SceneLoader.IsRuntimeName(blob.sceneName))
					return;

				ref var properties = ref CustomPrefabPropertiesTable.Value.Scenes[sceneIndex];

				ref var prefabVariation = ref properties.PrefabVariations[scenePrefabIndex];
				ref var prefabAmount = ref properties.PrefabAmounts[scenePrefabIndex];
				ref var prefabDescription = ref properties.PrefabDescriptions[scenePrefabIndex];
				ref var prefabGrowthStage = ref properties.PrefabGrowthStages[scenePrefabIndex];
				ref var prefabDropsLootTable = ref properties.PrefabDropsLootTable[scenePrefabIndex];

				var authoringEntity = blob.prefabs[scenePrefabIndex];
				var authoringObjectData = blob.prefabObjectDatas[scenePrefabIndex];

				if (!ObjectPropertiesLookup.TryGetComponent(authoringEntity, out var objectPropertiesCD))
					return;

				var variationOverride = prefabVariation;
				if (variationOverride != authoringObjectData.variation || (prefabAmount >= 0 && prefabAmount != authoringObjectData.amount)) {
					authoringObjectData.variation = variationOverride;
					authoringObjectData.amount = prefabAmount;
					ecb.SetComponent(entity, authoringObjectData);
				}

				if (DescriptionLookup.HasBuffer(authoringEntity)) {
					var buffer = ecb.SetBuffer<DescriptionBuffer>(entity);
					for (var i = 0; i < prefabDescription.Length; i++) {
						buffer.Add(new DescriptionBuffer {
							Value = prefabDescription[i]
						});
					}
				}

				if (GrowingLookup.TryGetComponent(authoringEntity, out var existingGrowingCD)) {
					// Default to highest stage if one isn't specified
					existingGrowingCD.currentStage = prefabGrowthStage == -1 && objectPropertiesCD.TryGet<int>(PropertyID.Growing.highestStage, out var highestStage)
						? highestStage
						: prefabGrowthStage;
					ecb.SetComponent(entity, existingGrowingCD);
				}

				if (DropsLootFromLootTableLookup.HasComponent(authoringEntity) && (int) prefabDropsLootTable > -1) {
					ecb.SetComponent(entity, new DropsLootFromLootTableCD {
						lootTableID = prefabDropsLootTable
					});
				}
			}
		}
	}
}