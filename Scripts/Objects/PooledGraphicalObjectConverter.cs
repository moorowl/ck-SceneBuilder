using System.Collections.Generic;
using HarmonyLib;
using Interaction;
using Pug.ECS.Hybrid;
using PugConversion;
using Unity.Mathematics;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace SceneBuilder.Objects {
	public class PooledGraphicalObjectConverter : PugConverter {
		private static readonly List<PoolablePrefabBank.PoolablePrefab> PoolablePrefabs = new();

		public static void Register(PooledGraphicalObject pooledGraphicalObject) {
			PoolablePrefabs.Add(pooledGraphicalObject.GetPoolablePrefab());
		}

		public override void Convert(GameObject authoring) {
			if (IsServer || !authoring.TryGetComponent<ObjectAuthoring>(out var objectAuthoring))
				return;

			var objectInfo = objectAuthoring.ObjectInfo;
			if (!TryGetGraphicalObjectComponent(authoring, out var prefabComponent) || prefabComponent.gameObject.GetComponent<PooledGraphicalObject>() == null)
				return;

			var entity = CreateAdditionalEntity();
			var prefabSize = (float2) (Vector2) objectInfo.prefabTileSize;
			var prefabOffset = (float2) (Vector2) objectInfo.prefabCornerOffset - 0.5f;
			var renderBounds = new float4(prefabOffset, prefabOffset + prefabSize);

			AddComponentData(entity, new GraphicalObjectPrefabCD {
				RenderBounds = renderBounds,
				PrefabComponent = prefabComponent,
				Prefab = null
			});
			AddComponentData(entity, new GraphicalObjectPrefabEntityCD {
				Value = PrimaryEntity
			});
			EnsureHasComponent<EntityMonoBehaviourCD>(PrimaryEntity);
		}

		private static bool TryGetGraphicalObjectComponent(GameObject authoring, out MonoBehaviour component) {
			if (authoring.TryGetComponent<ObjectAuthoring>(out var objectAuthoring) && objectAuthoring.graphicalPrefab != null) {
				component = (MonoBehaviour) objectAuthoring.graphicalPrefab.GetComponent(typeof(MonoBehaviour));
				return true;
			}

			component = null;
			return false;
		}

		[HarmonyPatch]
		public static class Patches {
			[HarmonyPatch(typeof(MemoryManager), nameof(MemoryManager.Init))]
			[HarmonyPrefix]
			public static void InjectPoolablePrefabs(MemoryManager __instance) {
				var bank = ScriptableObject.CreateInstance<PooledGraphicalObjectBank>();
				bank.poolInitializers = PoolablePrefabs;
				bank.poolablePlatformScaling = new List<PoolablePrefabBank.PlatformObjectPoolScaling>();

				__instance.poolablePrefabBanks?.Add(bank);
			}

			[HarmonyPatch(typeof(InteractablePostConverter), nameof(InteractablePostConverter.PostConvert))]
			[HarmonyPrefix]
			public static void PostConvertPre(InteractablePostConverter __instance, GameObject authoring) {
				if (TryGetGraphicalObjectComponent(authoring, out var component) && component.gameObject.GetComponent<PooledGraphicalObject>() != null) {
					var entityMonoBehaviourData = authoring.AddComponent<EntityMonoBehaviourData>();
					entityMonoBehaviourData.objectInfo = authoring.GetComponent<ObjectAuthoring>().ObjectInfo;
				}
			}

			[HarmonyPatch(typeof(InteractablePostConverter), nameof(InteractablePostConverter.PostConvert))]
			[HarmonyPostfix]
			public static void PostConvertPost(InteractablePostConverter __instance, GameObject authoring) {
				if (TryGetGraphicalObjectComponent(authoring, out var component) && authoring.TryGetComponent<EntityMonoBehaviourData>(out var entityMonoBehaviourData) && component.gameObject.GetComponent<PooledGraphicalObject>() != null)
					Object.DestroyImmediate(entityMonoBehaviourData);
			}

			[HarmonyPatch(typeof(GraphicalObjectConversion), nameof(GraphicalObjectConversion.Convert))]
			[HarmonyPrefix]
			public static bool Convert(GraphicalObjectConversion __instance, GameObject authoring) {
				if (TryGetGraphicalObjectComponent(authoring, out var component) && component.gameObject.GetComponent<PooledGraphicalObject>() != null)
					return false;

				return true;
			}
		}
	}
}