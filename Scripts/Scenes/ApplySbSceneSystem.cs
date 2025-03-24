using PugTilemap;
using PugWorldGen;
using SceneBuilder.Structures;
using SceneBuilder.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SceneBuilder.Scenes {
	[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	[UpdateBefore(typeof(ApplySpawnedCustomSceneSystem))]
	public partial class ApplySbSceneSystem : PugSimulationSystemBase {
		private EntityArchetype _waterSpreaderArchetype;

		protected override void OnCreate() {
			base.OnCreate();
			
			NeedTileUpdateBuffer();
			NeedLootBank();
			
			RequireForUpdate<CustomSceneTableCD>();
			RequireForUpdate<PugDatabase.DatabaseBankCD>();
		}

		protected override void OnUpdate() {
			base.OnUpdate();
			
			var ecb = CreateCommandBuffer();
			var tileAccessor = CreateTileAccessor(false);
			var tileUpdateBufferEntity = tileUpdateBufferSingletonEntity;
			var pugDatabase = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>();
			var waterSpreadingArchetype = EntityManager.CreateArchetype(typeof(WaterSpreaderCD));
			var scenesTableBlob = SystemAPI.GetSingleton<CustomSceneTableCD>().Value;
			
			Entities
				.ForEach((Entity entity, in LocalTransform transform, in SpawnCustomSceneCD spawnCustomScene) => {
					for (var sceneIndex = 0; sceneIndex < scenesTableBlob.Value.scenes.Length; sceneIndex++) {
						ref var sceneBlob = ref scenesTableBlob.Value.scenes[sceneIndex];
						if (!spawnCustomScene.name.Equals(sceneBlob.sceneName) || !SceneLoader.IsModdedScene(spawnCustomScene.name))
							continue;

						if (!SceneLoader.Instance.TryGetSceneFromRuntimeName(sceneBlob.sceneName, out var sceneFile))
							return;
						
						if (!StructureLoader.Instance.TryGetStructure(sceneFile.Structure, out var structureFile))
							return;

						var position = transform.Position.RoundToInt2();
						var rng = new Random(spawnCustomScene.seed);
						
						var flag = false;
						var flipDirection = new int2((!sceneBlob.canFlipX || !rng.NextBool()) ? 1 : (-1), (!sceneBlob.canFlipY || !rng.NextBool()) ? 1 : (-1));
						for (var i = 0; i < sceneBlob.tilePositions.Length; i++) {
							if (sceneBlob.tiles[i].tileType != 0) {
								var tilePosition = position + flipDirection * (sceneBlob.tilePositions[i] - sceneBlob.centerPosition);
								if (tileAccessor.IsInitialized(tilePosition)) {
									tileAccessor.Clear(tilePosition);
									continue;
								}
								
								ecb.AppendToBuffer(tileUpdateBufferEntity, new TileUpdateBuffer {
									command = TileUpdateBuffer.Command.Add,
									position = tilePosition
								});
								flag = true;
							}
						}
						for (var j = 0; j < sceneBlob.tilePositions.Length; j++) {
							if (sceneBlob.tiles[j].tileType == TileType.none)
								continue;

							var int3 = position + flipDirection * (sceneBlob.tilePositions[j] - sceneBlob.centerPosition);
							if (tileAccessor.IsInitialized(int3)) {
								tileAccessor.Set(int3, sceneBlob.tiles[j]);
								if (!flag) {
									var waterSpreaderEntity = ecb.CreateEntity(waterSpreadingArchetype);
									ecb.SetComponent(waterSpreaderEntity, new WaterSpreaderCD {
										position = int3
									});
								}
							}
						}
						if (flag)
							return;
						
						var positionF = position.ToFloat3();
						var center = sceneBlob.centerPosition.ToFloat3();
						var flipDirectionF = flipDirection.ToFloat3();
						
						for (var k = 0; k < sceneBlob.prefabPositions.Length; k++) {
							var properties = structureFile.Objects[k].Properties;
							
							var prefabObjectData = sceneBlob.prefabObjectDatas[k];
							var prefabAuthoringEntity = sceneBlob.prefabs[k];
							var prefabEntity = ecb.Instantiate(prefabAuthoringEntity);
							
							if (SystemAPI.HasComponent<DirectionBasedOnVariationCD>(prefabAuthoringEntity)) {
								var flippedVariation = DirectionBasedOnVariationCD.GetFlippedVariation(prefabObjectData.variation, flipDirection.x == -1, flipDirection.y == -1);
								if (flippedVariation != prefabObjectData.variation) {
									prefabObjectData.variation = flippedVariation;
									ecb.SetComponent(prefabEntity, prefabObjectData);
								}
							} else if (SystemAPI.HasComponent<DirectionCD>(prefabAuthoringEntity) && properties.Direction != null) {
								ecb.SetComponent(prefabEntity, new DirectionCD {
									direction = properties.Direction.Value * flipDirection.ToInt3()
								});
							}

							if (SystemAPI.HasComponent<PaintableObjectCD>(prefabAuthoringEntity) && properties.Color != null) {
								ecb.SetComponent(prefabEntity, new PaintableObjectCD {
									color = properties.Color.Value
								});
							}
							
							if (SystemAPI.HasComponent<DropsLootFromLootTableCD>(prefabAuthoringEntity) && properties.DropsLootTable != null) {
								ecb.SetComponent(prefabEntity, new DropsLootFromLootTableCD {
									lootTableID = properties.DropsLootTable.Value
								});
							}
							
							InventoryOverrideUtility.ApplyInventoryOverridesIfPresent(
								prefabEntity,
								prefabAuthoringEntity,
								ref sceneBlob.prefabInventoryOverrides[k],
								ecb,
								SystemAPI.GetComponentLookup<AddRandomLootCD>(),
								SystemAPI.GetBufferLookup<ContainedObjectsBuffer>(),
								pugDatabase.databaseBankBlob
							);
							
							var prefabTileOffset = math.min(0, flipDirection) * (sceneBlob.prefabSizes[k] + sceneBlob.prefabCornerOffsets[k] * 2 - 1);
							var prefabPosition = positionF + flipDirectionF * (sceneBlob.prefabPositions[k] - center) + prefabTileOffset.ToFloat3();
							ecb.SetComponent(prefabEntity, LocalTransform.FromPosition(prefabPosition));
							ecb.AddComponent<CustomSceneObjectCD>(prefabEntity);
						}

						EntityManager.DestroyEntity(entity);
					}
				})
				.WithStructuralChanges()
				.WithoutBurst()
				.Run();
		}
	}
}