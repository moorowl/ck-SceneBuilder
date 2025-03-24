using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;

namespace SceneBuilder.Commands {
	public struct StructureRpc : IRpcCommand {
		public FixedString64Bytes Name;
		public int2 Position;
		public int2 Size;
		public bool PlaceInstead;
		public uint Seed;
	}
}