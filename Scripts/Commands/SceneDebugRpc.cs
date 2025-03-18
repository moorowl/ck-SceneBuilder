using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;

namespace SceneBuilder.Commands {
	public struct SceneDebugRpc : IRpcCommand {
		public FixedString32Bytes SceneName;
		public int2 Position;
	}
}