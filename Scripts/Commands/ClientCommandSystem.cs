using SceneBuilder.Utilities.DataStructures;
using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;
using EntityArchetype = Unity.Entities.EntityArchetype;
using WorldSystemFilterFlags = Unity.Entities.WorldSystemFilterFlags;

namespace SceneBuilder.Commands {
	[Unity.Entities.UpdateInGroup(typeof(RunSimulationSystemGroup))]
	[Unity.Entities.WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
	public partial class ClientCommandSystem : PugSimulationSystemBase {
		private NativeQueue<StructureRpc> _rpcQueue;
		private EntityArchetype _rpcArchetype;

		protected override void OnCreate() {
			UpdatesInRunGroup();
			_rpcQueue = new NativeQueue<StructureRpc>(Allocator.Persistent);
			_rpcArchetype = EntityManager.CreateArchetype(typeof(StructureRpc), typeof(SendRpcCommandRequest));

			base.OnCreate();
		}
		
		public void SpawnScene(Identifier id, int2 position, uint seed = 0) {
			_rpcQueue.Enqueue(new StructureRpc {
				Name = id.AsSceneName,
				Position = position,
				PlaceInstead = true,
				Seed = seed
			});
		}

		public void SaveStructure(string name, int2 position, int2 size) {
			_rpcQueue.Enqueue(new StructureRpc {
				Name = new FixedString64Bytes(name),
				Position = position,
				Size = size
			});
		}
		
		protected override void OnUpdate() {
			var ecb = CreateCommandBuffer();

			while (_rpcQueue.TryDequeue(out var rpc)) {
				var entity = ecb.CreateEntity(_rpcArchetype);
				ecb.SetComponent(entity, rpc);
			}
		}
	}
}