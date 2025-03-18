using System.Collections.Generic;
using Newtonsoft.Json;
using PugTilemap;
using Unity.Mathematics;
using UnityEngine;

namespace SceneBuilder.Structures {
	public class StructureFile {
		public List<ObjectData> Objects;
		public List<TileAndPositions> Tiles;

		public class ObjectData {
			public ObjectID Id;
			public int Variation;
			public float2 Position;
			public ObjectProperties Properties;
		}

		public class ObjectProperties {
			public int3? Direction;
			public PaintableColor? Color;
		}
		
		public class TileAndPositions {
			public Tileset Tileset;
			public TileType TileType;
			public List<int2> Positions;
		}
	}
}