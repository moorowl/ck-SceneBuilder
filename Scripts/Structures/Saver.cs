using PugMod;
using Unity.Mathematics;

namespace SceneBuilder.Structures {
	public class Saver {
		public static StructureFile SaveToStructureFile(int2 position, int2 height) {
			var querySystem = API.Server.World.GetExistingSystemManaged<PugQuerySystem>();
			var tileAccessor = new TileAccessor(querySystem);

			return new StructureFile();
		}
	}
}