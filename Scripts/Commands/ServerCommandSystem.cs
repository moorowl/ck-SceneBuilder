using SceneBuilder.Structures;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace SceneBuilder.Commands {
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
	public partial class ServerCommandSystem : PugSimulationSystemBase {
		protected override void OnUpdate() {
			var ecb = CreateCommandBuffer();

			Entities.ForEach((Entity rpcEntity, in StructureRpc rpc, in ReceiveRpcCommandRequest req) => {
				ecb.DestroyEntity(rpcEntity);

				if (rpc.PlaceInstead) {
					var spawnerEntity = ecb.CreateEntity();
					ecb.AddComponent(spawnerEntity, LocalTransform.FromPosition(rpc.Position.ToFloat3()));
					ecb.AddComponent(spawnerEntity, new SpawnCustomSceneCD {
						name = new FixedString32Bytes(rpc.Name),
						seed = rpc.Seed
					});
				} else {
					StructureFile.Saver.SaveStructure(rpc.Name.ToString(), rpc.Position, rpc.Size);
				}
			}).WithoutBurst().Run();

			base.OnUpdate();
		}
	}
}