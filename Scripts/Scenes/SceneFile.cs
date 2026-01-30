using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Pug.UnityExtensions;
using SceneBuilder.Structures;
using SceneBuilder.Utilities.DataStructures;
using Unity.Mathematics;

namespace SceneBuilder.Scenes {
	public class SceneFile {
		[JsonProperty("version")] public int Version { get; set; } = 0;

		[JsonProperty("structure")] public Identifier Structure { get; set; }

		[JsonProperty("generation")] public GenerationSettings Generation { get; set; }

		public class GenerationSettings {
			[JsonProperty("canFlip")]
			[JsonConverter(typeof(StringEnumConverter))]
			public FlipDirection CanFlip { get; set; }

			[JsonProperty("random")] public RandomGenerationSettings Random { get; set; }

			[JsonProperty("dungeon")] public List<DungeonGenerationSettings> Dungeon { get; set; }
		}

		public class RandomGenerationSettings {
			[JsonProperty("maxOccurrences")] public int MaxOccurrences { get; set; }

			[JsonProperty("biomesToSpawnIn", ItemConverterType = typeof(StringEnumConverter))]
			public List<Biome> BiomesToSpawnIn { get; set; }
		}

		public class GuaranteedGenerationSettings {
			[JsonProperty("distanceFromCoreInBiome")]
			public DistanceFromCoreInBiomeGenerationSettings DistanceFromCoreInBiome { get; set; }

			[JsonProperty("anywhereInBiome")] public AnywhereInBiomeGenerationSettings AnywhereInBiome { get; set; }

			[JsonProperty("exactPosition")] public ExactPositionGenerationSettings ExactPosition { get; set; }
		}

		public class DistanceFromCoreInBiomeGenerationSettings {
			[JsonProperty("targetDistance")] public int TargetDistance { get; set; }

			[JsonProperty("biome")]
			[JsonConverter(typeof(StringEnumConverter))]
			public Biome BiomeToSpawnIn { get; set; }
		}

		public class AnywhereInBiomeGenerationSettings {
			[JsonProperty("biome")]
			[JsonConverter(typeof(StringEnumConverter))]
			public Biome BiomeToSpawnIn { get; set; }
		}

		public class ExactPositionGenerationSettings {
			[JsonProperty("position")]
			[JsonConverter(typeof(Converters.Int2))]
			public int2 Position { get; set; }
		}

		public class DungeonGenerationSettings {
			[JsonProperty("internalName")] public string InternalName { get; set; } = "";

			[JsonProperty("sceneGroupIndex")] public int SceneGroupIndex { get; set; } = -1;
		}

		public CustomScenesDataTable.Scene ConvertToDataTableScene(Identifier id, StructureFile structureFile, int sceneIndex) {
			StructureFile.Converter.GetMaps(structureFile, out var maps, out var smallestTilePosition, out var largestTilePosition);
			StructureFile.Converter.GetPrefabs(structureFile, sceneIndex, out var prefabs, out var prefabPositions, out var prefabInventoryOverrides, out var prefabColors, out var prefabDirections);

			var boundsSize = new int2(math.abs(smallestTilePosition.x) + math.abs(largestTilePosition.x), math.abs(smallestTilePosition.y) + math.abs(largestTilePosition.y));
			var supportsFlip = Generation.Dungeon == null;

			return new CustomScenesDataTable.Scene {
				sceneName = SceneLoader.GetRuntimeName(id),
				maxOccurrences = Generation.Random?.MaxOccurrences ?? 0,
				replacedByContentBundle = new OptionalValue<DataBlockRef<ContentBundleDataBlock>>(),
				biomesToSpawnIn = new WorldGenerationTypeDependentValue<List<Biome>> {
					classic = new List<Biome>(),
					fullRelease = Generation.Random?.BiomesToSpawnIn ?? new List<Biome>()
				},
				minDistanceFromCoreInClassicWorlds = 0,
				canFlipX = supportsFlip && (Generation.CanFlip is FlipDirection.Horizontal or FlipDirection.HorizontalAndVertical),
				canFlipY = supportsFlip && (Generation.CanFlip is FlipDirection.Vertical or FlipDirection.HorizontalAndVertical),
				hasCenter = false,
				center = new int2(boundsSize.x / 2, boundsSize.y / 2),
				boundsSize = boundsSize,
				radius = math.ceil(math.max(boundsSize.x, boundsSize.y) / 2f) + 1f,
				maps = maps,
				prefabs = prefabs,
				prefabPositions = prefabPositions,
				prefabInventoryOverrides = prefabInventoryOverrides,
				prefabColors = prefabColors,
				prefabDirections = prefabDirections
			};
		}
	}
}