using Pug.UnityExtensions;
using PugTilemap;
using PugWorldGen;
using SceneBuilder.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace SceneBuilder.Scenes {
	[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	[UpdateAfter(typeof(DungeonGenerateRoomsSystem))]
	public partial struct SbDungeonApplySpawnedObjectsSystem : ISystem {
		// Complete reimplementation of DungeonApplySpawnedObjectsSystem from vanilla
		private partial struct ReplaceObjectsJob : IJobEntity {
			public BlobAssetReference<PugDatabase.PugDatabaseBank> Database;

			public void Execute(in DungeonAreaCD dungeon, in DynamicBuffer<DungeonReplaceObjectsBuffer> replacementRules, ref DynamicBuffer<DungeonSpawnedObjectBuffer> spawnObjects) {
				var random = new Random(dungeon.seed ^ 0xCD86609Cu);

				foreach (var replacementRule in replacementRules) {
					var randomIndex = GetRandomIndex(ref random, replacementRule.accumulatedVariationProbability);
					var replaceWithVariations = replacementRule.replaceWithVariations;
					var variation = replaceWithVariations.Value[randomIndex];
					var primaryPrefabEntity = PugDatabase.GetPrimaryPrefabEntity(replacementRule.replaceWithID, Database, variation);

					for (var i = 0; i < spawnObjects.Length; i++) {
						ref var spawnObject = ref spawnObjects.ElementAt(i);
						if (Matches(spawnObject.objectData, replacementRule)) {
							spawnObject.objectData.objectID = replacementRule.replaceWithID;
							spawnObject.objectData.variation = variation;
							if (spawnObject.prefabEntity != Entity.Null)
								spawnObject.prefabEntity = primaryPrefabEntity;
						}
					}
				}
			}

			private bool Matches(ObjectData objectData, DungeonReplaceObjectsBuffer replacementRule) {
				if (objectData.objectID != replacementRule.replaceID) {
					return false;
				}

				if (replacementRule.replaceVariation.hasValue && objectData.variation != replacementRule.replaceVariation.value) {
					return false;
				}

				return true;
			}

			private static int GetRandomIndex(ref Random rng, BlobAssetReference<BlobArray<float>> normalizedWeights) {
				ref var weights = ref normalizedWeights.Value;
				var num = rng.NextFloat();

				for (var i = 0; i < normalizedWeights.Value.Length; i++) {
					if (num < weights[i]) {
						return i;
					}
				}

				return normalizedWeights.Value.Length - 1;
			}
		}

		private partial struct RemoveNullObjectsJob : IJobEntity {
			public void Execute(ref DynamicBuffer<DungeonSpawnedObjectBuffer> spawnObjects) {
				for (var i = spawnObjects.Length - 1; i >= 0; i--) {
					if (spawnObjects[i].objectData.objectID == ObjectID.None) {
						spawnObjects.RemoveAtSwapBack(i);
					}
				}
			}
		}

		private partial struct InitializeSubMapsJob : IJobEntity {
			public EntityCommandBuffer Ecb;
			public Entity TileUpdateBufferSingleton;
			[ReadOnly] public TileAccessor TileAccessor;

			public void Execute(in DynamicBuffer<DungeonSpawnedObjectBuffer> spawnObjects) {
				foreach (var spawnObject in spawnObjects) {
					if (!TileAccessor.IsInitialized(spawnObject.position)) {
						Ecb.AppendToBuffer(TileUpdateBufferSingleton, new TileUpdateBuffer {
							command = TileUpdateBuffer.Command.Add,
							position = spawnObject.position
						});
					}
				}
			}
		}

		private partial struct EnableAreaJob : IJobEntity {
			public EntityCommandBuffer Ecb;

			public void Execute(Entity entity, in LocalTransform transform, in DungeonAreaCD dungeon) {
				Ecb.AddComponent(entity, new EnableEntitiesInCircleCD {
					Center = transform.Position.ToFloat2(),
					Radius = dungeon.placementRadius + 8
				});
			}
		}

		private partial struct ClearAreaJob : IJobEntity {
			public EntityCommandBuffer Ecb;
			public TileAccessor TileAccessor;
			[ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
			[ReadOnly] public CollisionWorld CollisionWorld;

			public void Execute(Entity entity, in DungeonAreaCD dungeon, in DynamicBuffer<DungeonSpawnedObjectBuffer> dungeonObjects) {
				var occupiedPositions = new NativeList<int2>(dungeonObjects.Length, Allocator.Temp);
				foreach (var dungeonObject in dungeonObjects) {
					occupiedPositions.Add(in dungeonObject.position);
					TileAccessor.Clear(dungeonObject.position);
				}

				var collisionFilter = new CollisionFilter {
					BelongsTo = uint.MaxValue,
					CollidesWith = 131921u
				};
				var hits = new NativeList<DistanceHit>(1000, Allocator.Temp);
				if (!CollisionWorld.OverlapSphere(TransformLookup[entity].Position, dungeon.placementRadius + 8, ref hits, collisionFilter))
					return;

				foreach (var hit in hits) {
					var tilePosition = TransformLookup[hit.Entity].Position.RoundToInt2();
					if (occupiedPositions.Contains(tilePosition))
						Ecb.DestroyEntity(hit.Entity);
				}
			}
		}

		private partial struct SpawnObjectsJob : IJobEntity {
			public EntityCommandBuffer Ecb;
			public TileAccessor TileAccessor;
			public EntityArchetype WaterSpreadingArchetype;
			public BlobAssetReference<CustomSceneTableBlob> CustomSceneTable;
			public BlobAssetReference<SceneObjectPropertiesTableBlob> SceneObjectPropertiesTable;
			public BlobAssetReference<PugDatabase.PugDatabaseBank> Database;
			public BlobAssetReference<LootTableBankBlob> LootBank;
			[ReadOnly] public ComponentLookup<AddRandomLootCD> AddRandomLootLookup;
			[ReadOnly] public BufferLookup<ContainedObjectsBuffer> ContainedBufferLookup;
			[ReadOnly] public ComponentLookup<TileCD> TileLookup;
			[ReadOnly] public BiomeLookup BiomeLookup;
			public ComponentLookup<DirectionBasedOnVariationCD> DirectionBasedOnVariationLookup;
			public ComponentLookup<DirectionCD> DirectionLookup;
			public ComponentLookup<PaintableObjectCD> PaintableObjectLookup;
			public ComponentLookup<DropsLootFromLootTableCD> DropsLootFromLootTableLookup;
			public BufferLookup<DescriptionBuffer> DescriptionBufferLookup;

			public void Execute(in DungeonAreaCD dungeon, ref DynamicBuffer<DungeonSpawnedObjectBuffer> dungeonObjects) {
				var random = new Random(dungeon.seed ^ 0xA74A613Bu);

				foreach (var dungeonObject in dungeonObjects) {
					var objectData = dungeonObject.objectData;
					if (dungeonObject.prefabEntity != Entity.Null) {
						var entity = Ecb.Instantiate(dungeonObject.prefabEntity);
						Ecb.SetComponent(entity, LocalTransform.FromPosition(dungeonObject.prefabEntityPosition.ToFloat3()));
						Ecb.SetComponent(entity, new ObjectDataCD {
							objectID = objectData.objectID,
							amount = objectData.amount,
							variation = objectData.variation
						});
						Ecb.AddComponent<CustomSceneObjectCD>(entity);

						if (dungeonObject.optionalSceneIndex > -1 && dungeonObject.optionalInventoryOverrideIndex > -1) {
							if (SceneLoader.IsRuntimeName(CustomSceneTable.Value.scenes[dungeonObject.optionalSceneIndex].sceneName)) {
								ref var properties = ref SceneObjectPropertiesTable.Value.Scenes[dungeonObject.optionalSceneIndex];
								Utils.ApplySceneObjectProperties(
									Ecb,
									entity,
									dungeonObject.prefabEntity,
									objectData,
									ref properties,
									dungeonObject.optionalInventoryOverrideIndex,
									new int2(1, 1),
									DirectionBasedOnVariationLookup,
									DirectionLookup,
									PaintableObjectLookup,
									DropsLootFromLootTableLookup,
									DescriptionBufferLookup
								);
							}

							ref var inventoryOverride = ref CustomSceneTable.Value.scenes[dungeonObject.optionalSceneIndex].prefabInventoryOverrides[dungeonObject.optionalInventoryOverrideIndex];
							InventoryOverrideUtility.ApplyInventoryOverridesIfPresent(entity, dungeonObject.prefabEntity, ref inventoryOverride, Ecb, AddRandomLootLookup, ContainedBufferLookup, Database);
						}

						continue;
					}

					ref var entityObjectInfo = ref PugDatabase.GetEntityObjectInfo(objectData.objectID, Database, objectData.variation);
					if (entityObjectInfo.prefabEntities.Length > 0 && !TileLookup.HasComponent(entityObjectInfo.prefabEntities[0])) {
						var biome = BiomeLookup.GetBiome(dungeonObject.position);
						EntityUtility.CreateEntityWithLoot(Ecb, dungeonObject.position.ToFloat3(), objectData.objectID, objectData.amount, dungeonObject.loot, ref random, Database, LootBank, ContainedBufferLookup, biome, objectData.variation);
					} else if (entityObjectInfo.tileType != 0) {
						TileAccessor.Set(dungeonObject.position, new TileCD {
							tileset = entityObjectInfo.tileset,
							tileType = entityObjectInfo.tileType
						});
						if (entityObjectInfo.tileType == TileType.water) {
							var waterSpreadingEntity = Ecb.CreateEntity(WaterSpreadingArchetype);
							Ecb.SetComponent(waterSpreadingEntity, new WaterSpreaderCD {
								position = dungeonObject.position
							});
						}
					}
				}

				dungeonObjects.Clear();
			}
		}

		private bool _hasLazyInitialized;
		private EntityArchetype _waterSpreadingArchetype;
		private BiomeLookup _biomeLookup;
		private TileAccessor _tileAccessor;

		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<SceneObjectPropertiesTable>();
			state.RequireForUpdate<PhysicsWorldSingleton>();
			state.RequireForUpdate<LootTableBankCD>();
			state.RequireForUpdate<CustomSceneTableCD>();
			state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
			state.RequireForUpdate<PugDatabase.DatabaseBankCD>();
			state.RequireForUpdate<TileUpdateBuffer>();
			state.RequireForUpdate<SubMapRegistry>();
			state.RequireForUpdate(SystemAPI.QueryBuilder().WithAny<BiomeRangesCD, BiomeSamplesCD>().Build());
			state.RequireForUpdate<DungeonAreaCD>();

			_waterSpreadingArchetype = state.EntityManager.CreateArchetype(typeof(WaterSpreaderCD));
		}

		public void OnStartRunning(ref SystemState state) {
			if (!_hasLazyInitialized) {
				_hasLazyInitialized = true;
				_biomeLookup = (SystemAPI.HasSingleton<BiomeSamplesCD>() ? new BiomeLookup(SystemAPI.GetSingleton<BiomeSamplesCD>()) : new BiomeLookup(SystemAPI.GetSingleton<BiomeRangesCD>().Value, Allocator.Persistent));
				_tileAccessor = new TileAccessor(ref state, isReadOnly: false);
			}
		}

		public void OnDestroy(ref SystemState state) {
			if (_hasLazyInitialized)
				_biomeLookup.Dispose();
		}

		public void OnUpdate(ref SystemState state) {
			var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
			_tileAccessor.Update(ref state);

			var _query_548494060_ = SystemAPI.QueryBuilder().WithAll<DungeonApplySpawnedObjectsSystem.Trigger, DungeonAreaCD, LocalTransform, DungeonSpawnedObjectBuffer>().WithNone<EnableEntitiesInCircleCD>().Build();
			var _query_548494060_2 = SystemAPI.QueryBuilder().WithAll<DungeonApplySpawnedObjectsSystem.Trigger, DungeonAreaCD, LocalTransform, DungeonReplaceObjectsBuffer, DungeonSpawnedObjectBuffer>().WithNone<EnableEntitiesInCircleCD>().Build();
			var _query_548494060_3 = SystemAPI.QueryBuilder().WithAll<DungeonApplySpawnedObjectsSystem.Trigger, DungeonAreaCD, PositionHasBeenEnabled, DungeonSpawnedObjectBuffer, LocalTransform>().Build();

			ecb.DestroyEntity(SystemAPI.QueryBuilder().WithAll<DungeonApplySpawnedObjectsSystem.Trigger, DungeonAreaCD, PositionHasBeenEnabled, DungeonSpawnedObjectBuffer, LocalTransform>().Build(), EntityQueryCaptureMode.AtRecord);

			var replaceObjectsJob = new ReplaceObjectsJob {
				Database = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob
			};
			state.Dependency = replaceObjectsJob.Schedule(_query_548494060_2, state.Dependency);

			var removeNullObjectsJob = new RemoveNullObjectsJob();
			state.Dependency = removeNullObjectsJob.Schedule(_query_548494060_, state.Dependency);

			var initializeSubMapsJob = new InitializeSubMapsJob {
				Ecb = ecb,
				TileAccessor = _tileAccessor,
				TileUpdateBufferSingleton = SystemAPI.GetSingletonEntity<TileUpdateBuffer>()
			};
			state.Dependency = initializeSubMapsJob.Schedule(_query_548494060_, state.Dependency);

			var enableAreaJob = new EnableAreaJob {
				Ecb = ecb
			};
			state.Dependency = enableAreaJob.Schedule(_query_548494060_, state.Dependency);

			var clearAreaJob = new ClearAreaJob {
				Ecb = ecb,
				TileAccessor = _tileAccessor,
				TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(),
				CollisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld
			};
			state.Dependency = clearAreaJob.Schedule(_query_548494060_3, state.Dependency);

			var spawnObjectsJob = new SpawnObjectsJob {
				Ecb = ecb,
				TileAccessor = _tileAccessor,
				WaterSpreadingArchetype = _waterSpreadingArchetype,
				CustomSceneTable = SystemAPI.GetSingleton<CustomSceneTableCD>().Value,
				SceneObjectPropertiesTable = SystemAPI.GetSingleton<SceneObjectPropertiesTable>().Value,
				Database = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob,
				LootBank = SystemAPI.GetSingleton<LootTableBankCD>().Value,
				AddRandomLootLookup = SystemAPI.GetComponentLookup<AddRandomLootCD>(),
				ContainedBufferLookup = SystemAPI.GetBufferLookup<ContainedObjectsBuffer>(),
				TileLookup = SystemAPI.GetComponentLookup<TileCD>(),
				BiomeLookup = _biomeLookup,
				DirectionBasedOnVariationLookup = SystemAPI.GetComponentLookup<DirectionBasedOnVariationCD>(),
				DirectionLookup = SystemAPI.GetComponentLookup<DirectionCD>(),
				PaintableObjectLookup = SystemAPI.GetComponentLookup<PaintableObjectCD>(),
				DropsLootFromLootTableLookup = SystemAPI.GetComponentLookup<DropsLootFromLootTableCD>(),
				DescriptionBufferLookup = SystemAPI.GetBufferLookup<DescriptionBuffer>()
			};
			state.Dependency = spawnObjectsJob.Schedule(_query_548494060_3, state.Dependency);
		}
	}
}