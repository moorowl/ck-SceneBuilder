using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;

namespace SceneBuilder.Networking {
	public struct StructureRequest : IRpcCommand {
		public StructureCommand Command;
		public FixedString128Bytes String0;
		public int2 Position0;
		public int2 Position1;
		public uint Int0;
	}
}