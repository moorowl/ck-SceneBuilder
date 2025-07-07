using System;
using System.Text;
using Pug.UnityExtensions;
using PugMod;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace SceneBuilder.Utilities {
	public static class DataToolUtils {
		public static DataEntry GetDataAt(int2 tilePosition, bool isServer) {
			var entity = GetExistingDataEntityAt(tilePosition, isServer);
			var world = isServer ? API.Server.World : API.Client.World;
			
			if (entity != Entity.Null && EntityUtility.TryGetBuffer<DescriptionBuffer>(entity, world, out var descriptionBuffer)) {
				var textBuffer = new byte[descriptionBuffer.Length];
				for (var i = 0; i < descriptionBuffer.Length; i++)
					textBuffer[i] = descriptionBuffer[i].Value;
				
				if (DataEntry.TryDecode(Encoding.ASCII.GetString(textBuffer), out var entry))
					return entry;

			}
			
			return new DataEntry();
		}
		
		private static Entity GetExistingDataEntityAt(int2 tilePosition, bool isServer) {
			var dataId = API.Authoring.GetObjectID(Constants.StructureLootToolId);
			
			var ecsWorld = isServer ? API.Server.World : API.Client.World;
			var collisionWorldQuery = isServer
				? API.Server.GetEntityQuery(typeof(PhysicsWorldSingleton))
				: API.Client.GetEntityQuery(typeof(PhysicsWorldSingleton));
			var collisionWorld = collisionWorldQuery.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld.CollisionWorld;

			foreach (var (objectData, _, entity) in Utils.ObjectQuery(collisionWorld, ecsWorld, tilePosition)) {
				if (objectData.objectID != dataId)
					continue;

				return entity;
			}
			
			return Entity.Null;
		}
		
		public static void SetDataAt(int2 tilePosition, DataEntry entry) {
			var dataId = API.Authoring.GetObjectID(Constants.StructureLootToolId);
			
			var ecb = API.Server.World.GetExistingSystemManaged<BeginSimulationEntityCommandBufferSystem>().CreateCommandBuffer();
			var pugDatabaseBank = API.Server.GetEntityQuery(typeof(PugDatabase.DatabaseBankCD)).GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
			var shouldSerialize = entry.ShouldSerialize;

			var entity = GetExistingDataEntityAt(tilePosition, true);
			if (entity == Entity.Null && shouldSerialize)
				entity = EntityUtility.CreateEntity(ecb, tilePosition.ToFloat3(), dataId, 1, pugDatabaseBank);

			if (shouldSerialize && entity != Entity.Null) {
				var encodedData = Encoding.ASCII.GetBytes(entry.Encode());
				var descriptionBuffer = ecb.SetBuffer<DescriptionBuffer>(entity);
				for (var i = 0; i < encodedData.Length; i++) {
					descriptionBuffer.Add(new DescriptionBuffer {
						Value = encodedData[i]
					});
				}
			} else {
				ecb.DestroyEntity(entity);
			}
		}
		
		public struct DataEntry {
			public LootTableID InventoryLootTable;
			public LootTableID DropLootTable;

			public bool ShouldSerialize => InventoryLootTable != LootTableID.Empty || DropLootTable != LootTableID.Empty;
			
			public string Encode() {
				return $"{InventoryLootTable};{DropLootTable}";
			}

			public static bool TryDecode(string data, out DataEntry entry) {
				var parts = data.Split(';');

				if (parts.Length == 2) {
					Enum.TryParse<LootTableID>(parts[0], out var inventoryLootTable);
					Enum.TryParse<LootTableID>(parts[1], out var dropLootTable);
					
					entry = new DataEntry {
						InventoryLootTable = inventoryLootTable,
						DropLootTable = dropLootTable
					};
					return true;
				}

				entry = default;
				return false;
			}
		}
	}
}