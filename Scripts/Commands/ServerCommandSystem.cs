using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace SceneBuilder.Commands {
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
	public partial class ServerCommandSystem : PugSimulationSystemBase {
		protected override void OnUpdate() {
            var ecb = CreateCommandBuffer();

            Entities.ForEach((Entity rpcEntity, in SceneDebugRpc rpc, in ReceiveRpcCommandRequest req) => {
                ecb.DestroyEntity(rpcEntity);
                
                var spawnerEntity = ecb.CreateEntity();
                ecb.AddComponent(spawnerEntity, LocalTransform.FromPosition(rpc.Position.ToFloat3()));
                ecb.AddComponent(spawnerEntity, new SpawnCustomSceneCD {
	                name = rpc.SceneName,
	                seed = 1
                });
            })
            .WithoutBurst()
            .Run();

            base.OnUpdate();
        }
	}
}