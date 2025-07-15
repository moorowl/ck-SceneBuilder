using Pug.UnityExtensions;
using PugProperties;
using PugTilemap;
using PugWorldGen;
using SceneBuilder.Utilities;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SceneBuilder.Scenes {
	[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	[UpdateBefore(typeof(ApplySpawnedCustomSceneSystem))]
	[BurstCompile]
	public partial class SbApplySpawnedCustomSceneSystem : PugSimulationSystemBase {
		private EntityArchetype _waterSpreaderArchetype;

		protected override void OnCreate() {
			base.OnCreate();

			NeedTileUpdateBuffer();
			NeedLootBank();

			RequireForUpdate<CustomSceneTableCD>();
			RequireForUpdate<PugDatabase.DatabaseBankCD>();

			_waterSpreaderArchetype = EntityManager.CreateArchetype(typeof(WaterSpreaderCD));
		}

		protected override void OnUpdate() {
			base.OnUpdate();

			var ecb = CreateCommandBuffer();
			var tileAccessor = CreateTileAccessor(false);
			var tileUpdateBufferEntity = tileUpdateBufferSingletonEntity;
			var pugDatabase = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>();
			var scenesTable = SystemAPI.GetSingleton<CustomSceneTableCD>().Value;
			var sceneObjectPropertiesTable = SystemAPI.GetSingleton<SceneObjectPropertiesTable>().Value;

			Entities
				.ForEach((Entity entity, in LocalTransform transform, in SpawnCustomSceneCD spawnCustomScene) => {
					if (!TryFindModdedScene(spawnCustomScene, ref scenesTable.Value, ref sceneObjectPropertiesTable.Value, out var sceneIndex))
						return;

					ref var scene = ref scenesTable.Value.scenes[sceneIndex];
					ref var properties = ref sceneObjectPropertiesTable.Value.Scenes[sceneIndex];

					var position = transform.Position.RoundToInt2();
					var rng = new Random(spawnCustomScene.seed);

					var tileAreaIsUninitialized = false;
					var flipDirection = new int2((!scene.canFlipX || !rng.NextBool()) ? 1 : (-1), (!scene.canFlipY || !rng.NextBool()) ? 1 : (-1));
					for (var i = 0; i < scene.tilePositions.Length; i++) {
						if (scene.tiles[i].tileType != TileType.none) {
							var tilePosition = position + flipDirection * (scene.tilePositions[i] - scene.centerPosition);
							if (tileAccessor.IsInitialized(tilePosition)) {
								tileAccessor.Clear(tilePosition);
								continue;
							}

							ecb.AppendToBuffer(tileUpdateBufferEntity, new TileUpdateBuffer {
								command = TileUpdateBuffer.Command.Add,
								position = tilePosition
							});
							tileAreaIsUninitialized = true;
						}
					}

					for (var i = 0; i < scene.tilePositions.Length; i++) {
						if (scene.tiles[i].tileType == TileType.none)
							continue;

						var int3 = position + flipDirection * (scene.tilePositions[i] - scene.centerPosition);
						if (tileAccessor.IsInitialized(int3)) {
							tileAccessor.Set(int3, scene.tiles[i]);
							if (!tileAreaIsUninitialized) {
								var waterSpreaderEntity = ecb.CreateEntity(_waterSpreaderArchetype);
								ecb.SetComponent(waterSpreaderEntity, new WaterSpreaderCD {
									position = int3
								});
							}
						}
					}

					if (tileAreaIsUninitialized)
						return;

					var positionF = position.ToFloat3();
					var center = scene.centerPosition.ToFloat3();
					var flipDirectionF = flipDirection.ToFloat3();

					for (var i = 0; i < scene.prefabPositions.Length; i++) {
						var prefabObjectData = scene.prefabObjectDatas[i];
						var prefabAuthoringEntity = scene.prefabs[i];
						var prefabEntity = ecb.Instantiate(prefabAuthoringEntity);

						Utils.ApplySceneObjectProperties(
							ecb,
							prefabEntity,
							prefabAuthoringEntity,
							prefabObjectData,
							ref properties,
							i,
							flipDirection,
							SystemAPI.GetComponentLookup<DirectionBasedOnVariationCD>(),
							SystemAPI.GetComponentLookup<DirectionCD>(),
							SystemAPI.GetComponentLookup<PaintableObjectCD>(),
							SystemAPI.GetComponentLookup<DropsLootFromLootTableCD>(),
							SystemAPI.GetBufferLookup<DescriptionBuffer>(),
							SystemAPI.GetComponentLookup<ObjectPropertiesCD>()
						);

						InventoryOverrideUtility.ApplyInventoryOverridesIfPresent(
							prefabEntity,
							prefabAuthoringEntity,
							ref scene.prefabInventoryOverrides[i],
							ecb,
							SystemAPI.GetComponentLookup<AddRandomLootCD>(),
							SystemAPI.GetBufferLookup<ContainedObjectsBuffer>(),
							pugDatabase.databaseBankBlob
						);

						var prefabTileOffset = math.min(0, flipDirection) * (scene.prefabSizes[i] + scene.prefabCornerOffsets[i] * 2 - 1);
						var prefabPosition = positionF + flipDirectionF * (scene.prefabPositions[i] - center) + prefabTileOffset.ToFloat3();
						ecb.SetComponent(prefabEntity, LocalTransform.FromPosition(prefabPosition));
						ecb.AddComponent<CustomSceneObjectCD>(prefabEntity);
					}

					EntityManager.DestroyEntity(entity);
				})
				.WithStructuralChanges()
				.Run();
		}

		[BurstCompile]
		private static bool TryFindModdedScene(in SpawnCustomSceneCD spawnCustomScene, ref CustomSceneTableBlob customSceneTable, ref SceneObjectPropertiesTableBlob objectPropertiesTable, out int sceneIndex) {
			sceneIndex = -1;

			for (var i = 0; i < customSceneTable.scenes.Length; i++) {
				var name = customSceneTable.scenes[i].sceneName;
				if (spawnCustomScene.name.Equals(name) && SceneLoader.IsRuntimeName(name)) {
					sceneIndex = i;
					return true;
				}
			}

			return false;
		}
	}
}