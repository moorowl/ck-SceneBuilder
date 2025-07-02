using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SceneBuilder.Scenes {
	public struct SceneObjectPropertiesTableBlob {
		public BlobArray<SceneObjectPropertiesBlob> Scenes;
	}
	
	public struct SceneObjectPropertiesBlob {
		public BlobArray<int> PrefabAmounts;
		public BlobArray<int3> PrefabDirections;
		public BlobArray<PaintableColor> PrefabColors;
		public BlobArray<FixedString128Bytes> PrefabDescriptions;
		public BlobArray<LootTableID> PrefabDropsLootTable;
	}
	
	public struct SceneObjectPropertiesTable : IComponentData {
		public BlobAssetReference<SceneObjectPropertiesTableBlob> Value;
	}
}