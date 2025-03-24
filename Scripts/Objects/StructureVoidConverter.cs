using System.Linq;
using HarmonyLib;
using Pug.ECS.Hybrid;
using PugConversion;
using PugMod;
using Unity.Mathematics;
using UnityEngine;

namespace SceneBuilder.Utilities {
	public class StructureVoidConverter : PugConverter {
		public override void Convert(GameObject authoring) {
			if (IsServer || !authoring.TryGetComponent<ObjectAuthoring>(out var objectAuthoring) || objectAuthoring.objectName != "SceneBuilder:StructureVoid")
				return;
			
			var objectInfo = objectAuthoring.ObjectInfo;
			if (objectInfo.prefabInfos.Count <= 0 || objectInfo.prefabInfos[0].prefab == null)
				return;

			var entity = CreateAdditionalEntity();
			var prefabComponent = objectInfo.prefabInfos[0].prefab;
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
			Utils.Log("converted mod graphical object");
		}

		[HarmonyPatch]
		public static class Patches {
			[HarmonyPatch(typeof(GraphicalObjectConversion), "Convert")]
			[HarmonyPrefix]
			public static bool Convert(GraphicalObjectConversion __instance, GameObject authoring) {
				if (authoring.TryGetComponent<ObjectAuthoring>(out var objectAuthoring) && objectAuthoring.objectName == "SceneBuilder:StructureVoid")
					return false;
				
				return true;
			}
		}
	}
}