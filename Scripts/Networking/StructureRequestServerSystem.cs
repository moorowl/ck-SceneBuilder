using System;
using Pug.UnityExtensions;
using SceneBuilder.Structures;
using SceneBuilder.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace SceneBuilder.Networking {
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
	public partial class StructureRequestServerSystem : PugSimulationSystemBase {
		protected override void OnUpdate() {
			var ecb = CreateCommandBuffer();

			Entities.ForEach((Entity rpcEntity, in StructureRequest rpc, in ReceiveRpcCommandRequest req) => {
				ecb.DestroyEntity(rpcEntity);

				switch (rpc.Command) {
					case StructureCommand.Save:
						StructureFile.Saver.SaveStructure(rpc.String0.ToString(), rpc.Position0, rpc.Position1);
						break;
					case StructureCommand.Place:
						var spawnerEntity = ecb.CreateEntity();
						ecb.AddComponent(spawnerEntity, LocalTransform.FromPosition(rpc.Position0.ToFloat3()));
						ecb.AddComponent(spawnerEntity, new SpawnCustomSceneCD {
							name = new FixedString32Bytes(rpc.String0),
							seed = rpc.Int0
						});
						break;
					case StructureCommand.SetData:
						DataToolUtils.SetDataAt(rpc.Position0, new DataToolUtils.DataEntry {
							InventoryLootTable = (LootTableID) rpc.Position1.x,
							DropLootTable = (LootTableID) rpc.Position1.y,
						});
						break;
					default:
						throw new NotImplementedException();
				}
			})
			.WithoutBurst()
			.Run();

			base.OnUpdate();
		}
	}
}