using Unity.Collections;
using Unity.Entities;

namespace SceneBuilder.Scenes {
	public struct CustomPrefabPropertiesTable {
		public BlobArray<CustomPrefabProperties> Scenes;
	}

	public struct CustomPrefabProperties {
		public BlobArray<int> PrefabVariations;
		public BlobArray<int> PrefabAmounts;
		public BlobArray<FixedString128Bytes> PrefabDescriptions;
		public BlobArray<int> PrefabGrowthStages;
		public BlobArray<LootTableID> PrefabDropsLootTable;
	}

	public struct CustomPrefabPropertiesTableCD : IComponentData {
		public BlobAssetReference<CustomPrefabPropertiesTable> Value;
	}
}