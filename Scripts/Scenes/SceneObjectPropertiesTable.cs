using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SceneBuilder.Scenes {
	public struct SceneObjectPropertiesTableBlob {
		public BlobArray<SceneObjectPropertiesBlob> Scenes;
	}
	
	public struct SceneObjectPropertiesBlob {
		public FixedString64Bytes SceneName;
		public BlobArray<int3> PrefabDirections;
		public BlobArray<PaintableColor> PrefabColors;
		public BlobArray<LootTableID> PrefabDropsLootTable;
	}
	
	public struct SceneObjectPropertiesTable : IComponentData {
		public BlobAssetReference<SceneObjectPropertiesTableBlob> Value;
	}
}