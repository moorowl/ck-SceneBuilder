using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using EntityArchetype = Unity.Entities.EntityArchetype;
using WorldSystemFilterFlags = Unity.Entities.WorldSystemFilterFlags;

namespace SceneBuilder.Commands {
	[Unity.Entities.UpdateInGroup(typeof(RunSimulationSystemGroup))]
	[Unity.Entities.WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
	public partial class ClientCommandSystem : PugSimulationSystemBase {
		private NativeQueue<SceneDebugRpc> _rpcQueue;
		private EntityArchetype _rpcArchetype;

		protected override void OnCreate() {
			UpdatesInRunGroup();
			_rpcQueue = new NativeQueue<SceneDebugRpc>(Allocator.Persistent);
			_rpcArchetype = EntityManager.CreateArchetype(typeof(SceneDebugRpc), typeof(SendRpcCommandRequest));

			base.OnCreate();
		}
		
		public void SpawnScene(string name, int2 position) {
			_rpcQueue.Enqueue(new SceneDebugRpc {
				SceneName = new FixedString32Bytes(name),
				Position = position
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