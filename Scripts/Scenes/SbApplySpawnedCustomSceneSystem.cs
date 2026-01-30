using Pug.Properties;
using Pug.UnityExtensions;
using PugTilemap;
using PugWorldGen;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SceneBuilder.Scenes {
	[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	[UpdateBefore(typeof(ApplyCustomPrefabPropertiesSystem))]
	public partial class SbApplySpawnedCustomSceneSystem : PugSimulationSystemBase {
		private EntityArchetype _waterSpreadingArchetype;
		private CustomScenePrefabUtility.Lookups _customScenePrefabLookups;

		protected override void OnCreate() {
			RequireForUpdate<CustomSceneTableCD>();
			RequireForUpdate<ActivatedContentBundlesBuffer>();
			NeedTileUpdateBuffer();

			_waterSpreadingArchetype = EntityManager.CreateArchetype(typeof(WaterSpreaderCD));
			_customScenePrefabLookups = new CustomScenePrefabUtility.Lookups(ref CheckedStateRef);

			base.OnCreate();
		}

		protected override void OnUpdate() {
			var ecb = CreateCommandBuffer();
			var tileAccessor = CreateTileAccessor(readOnly: false);

			_customScenePrefabLookups.Update(ref CheckedStateRef);

			var directionBasedOnVariationLookup = SystemAPI.GetComponentLookup<DirectionBasedOnVariationCD>();
			var directionLookup = SystemAPI.GetComponentLookup<DirectionCD>();
			var objectPropertiesLookup = SystemAPI.GetComponentLookup<ObjectPropertiesCD>();

			var customSceneTable = SystemAPI.GetSingleton<CustomSceneTableCD>().Value;
			var tileUpdateBufferSingletonEntityLocal = tileUpdateBufferSingletonEntity;
			var waterSpreadingArchetypeLocal = _waterSpreadingArchetype;
			var customScenePrefabLookupsLocal = _customScenePrefabLookups;

			var customSceneData = new CustomScenePrefabUtility.Data {
				ActiveContentBundles = SystemAPI.GetSingletonBuffer<ActivatedContentBundlesBuffer>().AsNativeArray().Reinterpret<DataBlockAddress>(),
				Database = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob
			};

			Entities
				.ForEach((Entity entity, in LocalTransform transform, in SpawnCustomSceneCD spawnCustomSceneCD) => {
					for (var i = 0; i < customSceneTable.Value.scenes.Length; i++) {
						ref var customSceneBlob = ref customSceneTable.Value.scenes[i];
						if (!spawnCustomSceneCD.name.Equals(customSceneBlob.sceneName))
							continue;

						if (ApplyCustomScene(transform.Position.RoundToInt2(), ref customSceneBlob, tileAccessor, ecb, tileUpdateBufferSingletonEntityLocal, new Unity.Mathematics.Random(spawnCustomSceneCD.seed), waterSpreadingArchetypeLocal, directionBasedOnVariationLookup, directionLookup, objectPropertiesLookup, customScenePrefabLookupsLocal, customSceneData))
							break;

						return;
					}

					ecb.DestroyEntity(entity);
				})
				.Run();

			base.OnUpdate();
		}

		private static bool ApplyCustomScene(int2 position, ref CustomSceneBlob sceneBlob, TileAccessor tileAccessor, EntityCommandBuffer ecb, Entity tileUpdateBufferSingletonLocal, Unity.Mathematics.Random rng, EntityArchetype waterSpreadingArchetypeLocal, ComponentLookup<DirectionBasedOnVariationCD> directionFromVariationLookup, ComponentLookup<DirectionCD> directionLookup, ComponentLookup<ObjectPropertiesCD> propertiesLookup, CustomScenePrefabUtility.Lookups customSceneLookups, CustomScenePrefabUtility.Data customSceneData) {
			var areaIsUnloaded = false;
			var flipDirection = new int2(!sceneBlob.canFlipX || !rng.NextBool() ? 1 : -1, !sceneBlob.canFlipY || !rng.NextBool() ? 1 : -1);
			for (var i = 0; i < sceneBlob.tilePositions.Length; i++) {
				if (sceneBlob.tiles[i].tileType == TileType.none)
					continue;

				var tilePosition = position + flipDirection * (sceneBlob.tilePositions[i] - sceneBlob.centerPosition);
				if (tileAccessor.IsInitialized(tilePosition)) {
					tileAccessor.Clear(tilePosition);
					continue;
				}

				ecb.AppendToBuffer(tileUpdateBufferSingletonLocal, new TileUpdateBuffer {
					command = TileUpdateBuffer.Command.Add,
					position = tilePosition
				});
				areaIsUnloaded = true;
			}

			for (var j = 0; j < sceneBlob.tilePositions.Length; j++) {
				if (sceneBlob.tiles[j].tileType == TileType.none)
					continue;

				var tilePosition = position + flipDirection * (sceneBlob.tilePositions[j] - sceneBlob.centerPosition);
				if (!tileAccessor.IsInitialized(tilePosition))
					continue;
				
				tileAccessor.Set(tilePosition, sceneBlob.tiles[j]);
				if (areaIsUnloaded)
					continue;

				var waterSpreaderEntity = ecb.CreateEntity(waterSpreadingArchetypeLocal);
				ecb.SetComponent(waterSpreaderEntity, new WaterSpreaderCD {
					position = tilePosition
				});
			}

			if (areaIsUnloaded)
				return false;

			var positionF = position.ToFloat3();
			var center = sceneBlob.centerPosition.ToFloat3();
			var flipDirectionF = flipDirection.ToFloat3();
			
			for (var i = 0; i < sceneBlob.prefabPositions.Length; i++) {
				var entity = CustomScenePrefabUtility.CreateWithOverrides(ecb, i, ref sceneBlob, customSceneLookups, customSceneData);
				var prefabEntity = sceneBlob.prefabs[i];
				var prefabObjectData = sceneBlob.prefabObjectDatas[i];
				
				var float4 = (math.min(0, flipDirection) * (sceneBlob.prefabSizes[i] + sceneBlob.prefabCornerOffsets[i] * 2 - 1)).ToFloat3();
				var prefabPosition = positionF + flipDirectionF * (sceneBlob.prefabPositions[i] - center) + float4;
				ecb.SetComponent(entity, LocalTransform.FromPosition(prefabPosition));

				if (directionFromVariationLookup.HasComponent(prefabEntity)) {
					var flippedVariation = DirectionBasedOnVariationCD.GetFlippedVariation(prefabObjectData.variation, flipDirection.x == -1, flipDirection.y == -1);
					if (flippedVariation != prefabObjectData.variation) {
						prefabObjectData.variation = flippedVariation;
						ecb.SetComponent(entity, prefabObjectData);
					}
				} else if (!TryFlipWallMountedObjects(ecb, prefabEntity, entity, prefabObjectData, flipDirection, prefabPosition, propertiesLookup) && directionLookup.TryGetComponent(prefabEntity, out var directionCD)) {
					if (sceneBlob.prefabDirections[i].TryGetValue(out var output))
						directionCD.direction = output;

					directionCD.direction *= flipDirectionF;
					ecb.SetComponent(entity, directionCD);
				}
			}

			return true;
		}

		private static bool TryFlipWallMountedObjects(EntityCommandBuffer ecb, Entity prefabEntity, Entity entity, ObjectDataCD objectData, int2 flip, float3 position, ComponentLookup<ObjectPropertiesCD> objectPropertiesLookup) {
			if (!objectPropertiesLookup.TryGetComponent(prefabEntity, out var objectPropertiesCD))
				return false;

			if (!objectPropertiesCD.Has(PropertyID.PlaceableObject.hasVariationsThatCanBePlacedOnWalls))
				return false;

			var variation = objectData.variation;
			var wallSideVariationStartsOnIndex1 = objectPropertiesCD.Has(PropertyID.PlaceableObject.wallSideVariationStartsOnIndex1);
			if (wallSideVariationStartsOnIndex1) {
				if (variation == 0)
					return false;

				variation--;
			}

			if ((flip.x == -1 && variation % 2 == 1) || (flip.y == -1 && variation % 2 == 0))
				variation = (variation + 2) % 4;

			if (wallSideVariationStartsOnIndex1)
				variation++;

			if (variation != objectData.variation) {
				objectData.variation = variation;
				ecb.SetComponent(entity, objectData);
			}

			return true;
		}
	}
}