using SceneBuilder.Scenes;
using SceneBuilder.Utilities.DataStructures;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using EntityArchetype = Unity.Entities.EntityArchetype;
using WorldSystemFilterFlags = Unity.Entities.WorldSystemFilterFlags;

namespace SceneBuilder.Networking {
	[UpdateInGroup(typeof(RunSimulationSystemGroup))]
	[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
	public partial class StructureRequestClientSystem : PugSimulationSystemBase {
		private NativeQueue<StructureRequest> _rpcQueue;
		private EntityArchetype _rpcArchetype;

		protected override void OnCreate() {
			UpdatesInRunGroup();
			_rpcQueue = new NativeQueue<StructureRequest>(Allocator.Persistent);
			_rpcArchetype = EntityManager.CreateArchetype(typeof(StructureRequest), typeof(SendRpcCommandRequest));

			base.OnCreate();
		}
		
		public void PlaceScene(Identifier id, int2 position, uint seed = 0) {
			_rpcQueue.Enqueue(new StructureRequest {
				Command = StructureCommand.Place,
				String0 = SceneLoader.GetRuntimeName(id),
				Position0 = position,
				Int0 = seed
			});
		}

		public void SaveStructure(string name, int2 position, int2 size) {
			_rpcQueue.Enqueue(new StructureRequest {
				String0 = new FixedString64Bytes(name),
				Position0 = position,
				Position1 = size
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