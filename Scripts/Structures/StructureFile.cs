using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using PugMod;
using PugTilemap;
using SceneBuilder.Utilities;
using SceneBuilder.Utilities.DataStructures;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace SceneBuilder.Structures {
	public class StructureFile {
		private static readonly HashSet<ObjectID> ObjectsToRemove = new() {
			ObjectID.DroppedItem,
			ObjectID.TheCore
		};
		private static readonly Dictionary<ObjectID, ObjectID> ObjectsToReplace = new() {
			{ ObjectID.PlayerGrave, ObjectID.Gravestone }
		};
		private static readonly HashSet<TileType> TileTypesToRemove = new() {
			TileType.__illegal__,
			TileType.__max__,
			TileType.roof,
			TileType.immune
		};
		private static readonly Dictionary<TileType, Tileset> TilesetsToEnforce = new() {
			{ TileType.pit, Tileset.Dirt },
			{ TileType.roofHole, Tileset.Dirt }
		};
		
		[JsonProperty("objects")]
		public List<ObjectData> Objects { get; set; }
		
		[JsonProperty("tiles")]
		public List<TileAndPositions> Tiles { get; set; }

		public class ObjectData {
			[JsonProperty("id")]
			[JsonConverter(typeof(StringEnumConverter))]
			public ObjectID Id { get; set; }
			
			[JsonProperty("variation")]
			public int Variation { get; set; }
			
			[JsonProperty("position")]
			[JsonConverter(typeof(Converters.Float2))]
			public float2 Position { get; set; }
			
			[JsonProperty("properties")]
			public ObjectProperties Properties { get; set; }
		}

		public class ObjectProperties {
			[JsonProperty("direction")]
			[JsonConverter(typeof(Converters.Int3))]
			public int3? Direction { get; set; }
			
			[JsonProperty("color")]
			[JsonConverter(typeof(StringEnumConverter))]
			public PaintableColor? Color { get; set; }
			
			[JsonProperty("inventory")]
			public List<InventoryItem> Inventory { get; set; }
			
			[JsonProperty("inventoryLootTable")]
			[JsonConverter(typeof(StringEnumConverter))]
			public LootTableID? InventoryLootTable { get; set; }
			
			[JsonProperty("drops")]
			public List<InventoryItem> Drops { get; set; }
			
			[JsonProperty("dropsLootTable")]
			[JsonConverter(typeof(StringEnumConverter))]
			public LootTableID? DropsLootTable { get; set; }
		}

		public class InventoryItem {
			[JsonProperty("slot")]
			public int Slot { get; set; }
			
			[JsonProperty("id")]
			[JsonConverter(typeof(StringEnumConverter))]
			public ObjectID Id { get; set; }
			
			[JsonProperty("variation")]
			public int Variation { get; set; }
			
			[JsonProperty("amount")]
			public int Amount { get; set; }
		}
		
		public class TileAndPositions {
			[JsonProperty("tileset")]
			[JsonConverter(typeof(StringEnumConverter))]
			public Tileset Tileset { get; set; }
			
			[JsonProperty("tileType")]
			[JsonConverter(typeof(StringEnumConverter))]
			public TileType TileType { get; set; }
			
			[JsonProperty("positions", ItemConverterType = typeof(Converters.Int2))]
			public List<int2> Positions { get; set; }
		}

		public void Validate() {
			Objects?.RemoveAll(objectData => ObjectsToRemove.Contains(objectData.Id) || !Utils.TryFindMatchingPrefab(objectData.Id, objectData.Variation, out _));
			Objects?.ForEach(objectData => {
				objectData.Id = ObjectsToReplace.GetValueOrDefault(objectData.Id, objectData.Id);
			});
			
			Tiles?.RemoveAll(tile => TileTypesToRemove.Contains(tile.TileType) || tile.Tileset < Tileset.Dirt || tile.Tileset >= Tileset.MAX_VALUE);
			Tiles?.ForEach(tile => {
				tile.Tileset = TilesetsToEnforce.GetValueOrDefault(tile.TileType, tile.Tileset);
			});
		}
		
		public static class Saver {
			public static void SaveStructure(string name, int2 position, int2 size) {
				var objects = GetObjects(position, size, out var structureVoidPositions);
				var tiles = GetTiles(position, size, structureVoidPositions);
				
				var file = new StructureFile {
					Tiles = tiles,
					Objects = objects
				};
				file.Validate();
				
				var serialized = JsonConvert.SerializeObject(file, new JsonSerializerSettings {
					Formatting = Formatting.Indented,
					NullValueHandling = NullValueHandling.Ignore,
					ContractResolver = new CamelCasePropertyNamesContractResolver()
				}).Replace("  ", "\t");

				var path = $"{Main.InternalName}/Saved/{name}.json";
				if (API.ConfigFilesystem.FileExists(path))
					API.ConfigFilesystem.Delete(path);
				API.ConfigFilesystem.Write(path, Encoding.UTF8.GetBytes(serialized));
			}
			
			private static List<TileAndPositions> GetTiles(int2 position, int2 size, HashSet<int2> structureVoidPositions) {
				var tileAccessor = new TileAccessor(API.Server.World.GetExistingSystemManaged<PugQuerySystem>());
				
				var tiles = new List<TileAndPositions>();
				for (var x = position.x; x < position.x + size.x; x++) {
					for (var y = position.y; y < position.y + size.y; y++) {
						var localPosition = new int2(x, y) - position;
						var tilesAtPosition = tileAccessor.Get(new int2(x, y), Allocator.Temp);

						if (structureVoidPositions.Contains(localPosition))
							continue;

						foreach (var tileAtPosition in tilesAtPosition) {
							var foundExisting = false;
							foreach (var entry in tiles) {
								// try to find an existing entry first
								if (entry.TileType == tileAtPosition.tileType && (int) entry.Tileset == tileAtPosition.tileset) {
									entry.Positions.Add(localPosition);
									foundExisting = true;
									break;
								}
							}

							if (!foundExisting) {
								tiles.Add(new TileAndPositions {
									TileType = tileAtPosition.tileType,
									Tileset = (Tileset) tileAtPosition.tileset,
									Positions = new List<int2> {
										localPosition
									}
								});
							}
						}
					}
				}

				return tiles;
			}

			private static List<ObjectData> GetObjects(int2 position, int2 size, out HashSet<int2> structureVoidPositions) {
				structureVoidPositions = new HashSet<int2>();
				var objects = new List<ObjectData>();
				
				var collisionWorld = API.Server.GetEntityQuery(typeof(PhysicsWorldSingleton)).GetSingleton<PhysicsWorldSingleton>().PhysicsWorld.CollisionWorld;
				var outHits = new NativeList<DistanceHit>(Allocator.Temp);
				var center = position.ToFloat3() + (new float2(size.x - 1f, size.y - 1f).ToFloat3() / 2f);
				collisionWorld.OverlapBox(center, quaternion.identity, new float3((size.x - 1.5f) / 2f, 10f, (size.y - 1.5f) / 2f), ref outHits, new CollisionFilter {
					BelongsTo = PhysicsLayerID.Everything,
					CollidesWith = PhysicsLayerID.Everything
				});

				var structureVoidId = API.Authoring.GetObjectID("SceneBuilder:StructureVoid");
				var entitiesAdded = new HashSet<Entity>();
				
				foreach (var hit in outHits) {
					var entity = hit.Entity;
					if (entitiesAdded.Contains(entity))
						continue;
					
					if (!EntityUtility.TryGetComponentData<ObjectDataCD>(entity, API.Server.World, out var objectData))
						continue;
					
					if (!EntityUtility.TryGetComponentData<LocalTransform>(entity, API.Server.World, out var transform))
						continue;

					if (objectData.objectID != structureVoidId && !EntityUtility.HasComponentData<DontSerializeCD>(entity, API.Server.World)) {
						objects.Add(new ObjectData {
							Id = objectData.objectID,
							Variation = objectData.variation,
							Position = transform.Position.ToFloat2() - position,
							Properties = GetObjectProperties(entity)
						});
					} else {
						structureVoidPositions.Add(transform.Position.RoundToInt2() - position);
					}

					entitiesAdded.Add(entity);
				}
				
				outHits.Dispose();
				return objects;
			}
			
			private static ObjectProperties GetObjectProperties(Entity entity) {
				var world = API.Server.World;
				var properties = new ObjectProperties();
			
				if (EntityUtility.TryGetComponentData<DirectionCD>(entity, world, out var directionData))
					properties.Direction = directionData.direction.RoundToInt3();
			
				if (EntityUtility.TryGetComponentData<PaintableObjectCD>(entity, world, out var paintableData))
					properties.Color = paintableData.color;

				if (EntityUtility.TryGetBuffer<ContainedObjectsBuffer>(entity, world, out var containedObjects)) {
					properties.Inventory = new List<InventoryItem>();

					for (var i = 0; i < containedObjects.Length; i++) {
						var containedObject = containedObjects[i];
						
						if (!Utils.IsContainedObjectEmpty(containedObject)) {
							properties.Inventory.Add(new InventoryItem {
								Slot = i,
								Id = containedObject.objectID,
								Variation = containedObject.variation,
								Amount = containedObject.amount
							});
						}
					}
				}

				return properties;
			}
		}
	}
}