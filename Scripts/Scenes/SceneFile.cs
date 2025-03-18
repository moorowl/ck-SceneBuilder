using System.Collections.Generic;
using Newtonsoft.Json;
using SceneBuilder.Utilities;
using SceneBuilder.Utilities.DataStructures;

namespace SceneBuilder.Scenes {
	public class SceneFile {
		public Identifier Structure;
		public GenerationSettings Generation;

		public class GenerationSettings {
			public FlipDirection CanFlip;
			public RandomGenerationSettings Random;
		}

		public class RandomGenerationSettings {
			public int MaxOccurrences;
			public List<Biome> BiomesToSpawnIn;
		}
	}
}