using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Pug.UnityExtensions;
using PugTilemap;
using SceneBuilder.Utilities;
using SceneBuilder.Utilities.DataStructures;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using API = PugMod.API;

#pragma warning disable CS0618 // disable TileType.roof is obsolete

namespace SceneBuilder.Structures {
	public class StructureFile {
		private static readonly HashSet<string> ObjectsToRemove = new() {
			nameof(ObjectID.DroppedItem),
			nameof(ObjectID.TheCore)
		};

		private static readonly Dictionary<string, string> ObjectsToReplace = new() {
			{ nameof(ObjectID.PlayerGrave), nameof(ObjectID.Gravestone) }
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

		[JsonProperty("version")] public int Version { get; set; } = 0;

		[JsonProperty("objects")] public List<ObjectData> Objects { get; set; } = new();

		[JsonProperty("tiles")] public List<TileAndPositions> Tiles { get; set; } = new();

		public class ObjectData {
			[JsonProperty("id")] public string Id { get; set; }

			[JsonProperty("variation")] public int Variation { get; set; }

			[JsonProperty("position")]
			[JsonConverter(typeof(Converters.Float2))]
			public float2 Position { get; set; }

			[JsonProperty("properties")] public ObjectProperties Properties { get; set; } = new();
		}

		public class ObjectProperties {
			[JsonProperty("amount")] public int? Amount { get; set; }

			[JsonProperty("direction")]
			[JsonConverter(typeof(Converters.Int3))]
			public int3? Direction { get; set; }

			[JsonProperty("color")]
			[JsonConverter(typeof(StringEnumConverter))]
			public PaintableColor? Color { get; set; }

			[JsonProperty("description")] public string Description { get; set; }

			[JsonProperty("inventory")] public List<InventoryItem> Inventory { get; set; }

			[JsonProperty("inventoryLootTable")]
			[JsonConverter(typeof(StringEnumConverter))]
			public LootTableID? InventoryLootTable { get; set; }

			[JsonProperty("drops")] public List<InventoryItem> Drops { get; set; }

			[JsonProperty("dropsLootTable")]
			[JsonConverter(typeof(StringEnumConverter))]
			public LootTableID? DropsLootTable { get; set; }
		}

		public class InventoryItem {
			[JsonProperty("slot")] public int Slot { get; set; }

			[JsonProperty("id")] public string Id { get; set; }

			[JsonProperty("variation")] public int Variation { get; set; }

			[JsonProperty("amount")] public int Amount { get; set; }
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

		public static class Converter {
			public static void Validate(StructureFile structureFile) {
				structureFile.Objects.RemoveAll(objectData => {
					if (ObjectsToRemove.Contains(objectData.Id) || !Utils.TryFindMatchingPrefab(objectData.Id, objectData.Variation, out _)) {
						Utils.Log($"(Validation) Removing invalid object {objectData.Id}");
						return true;
					}

					return false;
				});
				structureFile.Objects.ForEach(objectData => {
					var id = objectData.Id;
					var replacementId = ObjectsToReplace.GetValueOrDefault(id, id);

					if (id != replacementId)
						Utils.Log($"(Validation) Replacing object {id} with {replacementId}");

					objectData.Id = replacementId;
				});

				structureFile.Tiles.RemoveAll(tile => {
					if (TileTypesToRemove.Contains(tile.TileType) || tile.Tileset < Tileset.Dirt || tile.Tileset >= Tileset.MAX_VALUE) {
						Utils.Log($"(Validation) Removing invalid tile type {tile.TileType}");
						return true;
					}

					if (tile.Tileset < Tileset.Dirt || tile.Tileset >= Tileset.MAX_VALUE) {
						Utils.Log($"(Validation) Removing invalid tileset {tile.Tileset}");
						return true;
					}

					return false;
				});
				structureFile.Tiles.ForEach(tile => {
					var replacementTileset = TilesetsToEnforce.GetValueOrDefault(tile.TileType, tile.Tileset);

					if (tile.Tileset != replacementTileset)
						Utils.Log($"(Validation) Replacing tileset {tile.Tileset} with {replacementTileset}");

					tile.Tileset = replacementTileset;
				});

				var tileTypesAtPosition = new Dictionary<int2, HashSet<TileType>>();
				foreach (var tile in structureFile.Tiles) {
					foreach (var position in tile.Positions) {
						if (!tileTypesAtPosition.ContainsKey(position))
							tileTypesAtPosition[position] = new HashSet<TileType>();

						tileTypesAtPosition[position].Add(tile.TileType);
					}
				}

				// Remove tiles that are missing a required tile type (e.g. walls without ground)
				var neededTileTypes = new NativeList<TileType>(4, Allocator.Temp);
				foreach (var tile in structureFile.Tiles) {
					foreach (var position in tile.Positions) {
						if (!tileTypesAtPosition.ContainsKey(position))
							tileTypesAtPosition[position] = new HashSet<TileType>();

						tileTypesAtPosition[position].Add(tile.TileType);
					}
				}

				foreach (var tile in structureFile.Tiles) {
					neededTileTypes.Clear();
					tile.TileType.GetNeededTile(ref neededTileTypes);

					if (neededTileTypes.Length > 0) {
						tile.Positions.RemoveAll(position => {
							var availableTileTypes = tileTypesAtPosition[position];

							for (var i = 0; i < neededTileTypes.Length; i++) {
								if (availableTileTypes.Contains(neededTileTypes[i])) {
									return false;
								}
							}

							Utils.Log($"(Validation) Removing {tile.Tileset}:{tile.TileType} at {position.x}, {position.y} because it is missing a required tile type at the same position");
							return true;
						});
					}
				}

				neededTileTypes.Dispose();
			}

			public static void GetPrefabs(StructureFile structureFile, int sceneIndex, out List<GameObject> prefabs, out List<Vector3> prefabPositions, out List<CustomScenesDataTable.InventoryOverride> prefabInventoryOverrides) {
				prefabs = new List<GameObject>();
				prefabPositions = new List<Vector3>();
				prefabInventoryOverrides = new List<CustomScenesDataTable.InventoryOverride>();

				foreach (var objectData in structureFile.Objects) {
					if (!Utils.TryFindMatchingPrefab(objectData.Id, objectData.Variation, out var prefab))
						continue;

					prefabs.Add(prefab);
					prefabPositions.Add(objectData.Position.ToFloat3());

					var inventoryOverride = new CustomScenesDataTable.InventoryOverride();
					if (objectData.Properties.Inventory is { Count: > 0 } && prefab.TryGetComponent<InventoryAuthoring>(out var inventoryAuthoring)) {
						inventoryOverride.itemsOverride = new List<global::ObjectData>();
						inventoryOverride.itemsToRemove = inventoryAuthoring.itemsInInventory.Count;

						var itemsBySlot = objectData.Properties.Inventory
							.GroupBy(x => x.Slot)
							.Select(group => group.First())
							.ToDictionary(x => x.Slot, x => x);
						var highestSlotIndex = itemsBySlot.Keys.Max();

						for (var i = 0; i <= highestSlotIndex; i++) {
							if (itemsBySlot.TryGetValue(i, out var inventoryItem)) {
								inventoryOverride.itemsOverride.Add(new global::ObjectData {
									objectID = API.Authoring.GetObjectID(inventoryItem.Id),
									variation = inventoryItem.Variation,
									amount = inventoryItem.Amount
								});
							} else {
								inventoryOverride.itemsOverride.Add(new global::ObjectData());
							}
						}
					}

					if (objectData.Properties.InventoryLootTable != null) {
						inventoryOverride.hasLootTableOverride = true;
						inventoryOverride.lootTableOverride = objectData.Properties.InventoryLootTable.Value;
					}

					inventoryOverride.hasAnyInventoryOverride = true;
					inventoryOverride.hasItemsOverride = true;
					inventoryOverride.itemsOverride ??= new List<global::ObjectData>();

					prefabInventoryOverrides.Add(inventoryOverride);
				}
			}

			public static void GetMaps(StructureFile structureFile, out List<CustomScenesDataTable.Map> maps, out int2 smallestTilePosition, out int2 largestTilePosition) {
				var mapDataModifier = new PugMapDataModifier(new PugMapData());
				smallestTilePosition = int.MaxValue;
				largestTilePosition = int.MinValue;

				foreach (var entry in structureFile.Tiles) {
					foreach (var position in entry.Positions) {
						mapDataModifier.Set(position.ToVec3Int(), (int) entry.Tileset, entry.TileType);

						smallestTilePosition = math.min(smallestTilePosition, position);
						largestTilePosition = math.max(largestTilePosition, position);
					}
				}

				maps = new List<CustomScenesDataTable.Map> {
					new() {
						localPosition = new int2(0, 0),
						mapData = mapDataModifier.GetMapData()
					}
				};
			}
		}

		public static class Saver {
			public static void SaveStructure(string name, int2 position, int2 size) {
				var objects = GetObjects(position, size, out var structureVoidPositions);
				var tiles = GetTiles(position, size, structureVoidPositions);

				var file = new StructureFile {
					Tiles = tiles,
					Objects = objects
				};
				Converter.Validate(file);

				var serialized = JsonConvert.SerializeObject(file, new JsonSerializerSettings {
					Formatting = Formatting.Indented,
					NullValueHandling = NullValueHandling.Ignore,
					ContractResolver = new CamelCasePropertyNamesContractResolver()
				}).Replace("  ", "\t");

				var path = $"{Constants.InternalName}/Saved/{name}.json";
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
				var structureVoidId = API.Authoring.GetObjectID(Constants.StructureVoidId);
				var lootToolId = API.Authoring.GetObjectID(Constants.StructureLootToolId);

				foreach (var (objectData, transform, entity) in Utils.ObjectQuery(collisionWorld, API.Server.World, position, size)) {
					if (EntityUtility.HasComponentData<DontSerializeCD>(entity, API.Server.World) || objectData.objectID == lootToolId)
						continue;

					DataToolUtils.DataEntry? optionalDataEntry = null;

					var objectType = EntityUtility.GetComponentData<ObjectTypeCD>(entity, API.Server.World).Value;
					var isCreature = objectType is ObjectType.Creature or ObjectType.Critter;

					if (!isCreature && (EntityUtility.HasComponentData<ContainedObjectsBuffer>(entity, API.Server.World) || EntityUtility.HasComponentData<DropsLootFromLootTableCD>(entity, API.Server.World)))
						optionalDataEntry = DataToolUtils.GetDataAt(transform.RoundToInt2(), true);

					if (objectData.objectID != structureVoidId) {
						objects.Add(new ObjectData {
							Id = Utils.GetObjectIdName(objectData.objectID),
							Variation = objectData.variation,
							Position = transform.ToFloat2() - position,
							Properties = GetObjectProperties(objectData, entity, optionalDataEntry)
						});
					} else {
						structureVoidPositions.Add(transform.RoundToInt2() - position);
					}
				}

				return objects;
			}

			private static ObjectProperties GetObjectProperties(ObjectDataCD objectData, Entity entity, DataToolUtils.DataEntry? optionalDataEntry) {
				var world = API.Server.World;
				var properties = new ObjectProperties();

				if (objectData.amount != PugDatabase.GetObjectInfo(objectData.objectID, objectData.variation).initialAmount)
					properties.Amount = objectData.amount;

				if (EntityUtility.TryGetComponentData<DirectionCD>(entity, world, out var directionData))
					properties.Direction = directionData.direction.RoundToInt3();

				if (EntityUtility.TryGetComponentData<PaintableObjectCD>(entity, world, out var paintableData))
					properties.Color = paintableData.color;

				if (EntityUtility.TryGetBuffer<DescriptionBuffer>(entity, world, out var descriptionBuffer)) {
					var textBuffer = new byte[descriptionBuffer.Length];
					for (var i = 0; i < descriptionBuffer.Length; i++)
						textBuffer[i] = descriptionBuffer[i].Value;

					var text = Encoding.UTF8.GetString(textBuffer);
					if (!string.IsNullOrWhiteSpace(text))
						properties.Description = text;
				}

				if (EntityUtility.TryGetBuffer<ContainedObjectsBuffer>(entity, world, out var containedObjects)) {
					properties.Inventory = new List<InventoryItem>();

					for (var i = 0; i < containedObjects.Length; i++) {
						var containedObject = containedObjects[i];

						if (!Utils.IsContainedObjectEmpty(containedObject)) {
							properties.Inventory.Add(new InventoryItem {
								Slot = i,
								Id = Utils.GetObjectIdName(containedObject.objectID),
								Variation = containedObject.variation,
								Amount = containedObject.amount
							});
						}
					}

					var inventoryLootTable = optionalDataEntry?.InventoryLootTable ?? LootTableID.Empty;
					if (inventoryLootTable != LootTableID.Empty)
						properties.InventoryLootTable = inventoryLootTable;
				}

				// Only save if the default entity doesn't have DropsLootFromLootTableCD, or the loot table is different
				if (EntityUtility.TryGetComponentData<DropsLootFromLootTableCD>(entity, world, out var dropsLootFromLootTableData) && (!PugDatabase.TryGetComponent<DropsLootFromLootTableCD>(objectData, out var defaultDropsLootFromLootTableData) || dropsLootFromLootTableData.lootTableID != defaultDropsLootFromLootTableData.lootTableID))
					properties.DropsLootTable = dropsLootFromLootTableData.lootTableID;

				if (!EntityUtility.HasComponentData<ContainedObjectsBuffer>(entity, world)) {
					var dropLootTable = optionalDataEntry?.DropLootTable ?? LootTableID.Empty;
					if (dropLootTable != LootTableID.Empty)
						properties.DropsLootTable = dropLootTable;
				}

				return properties;
			}
		}
	}
}