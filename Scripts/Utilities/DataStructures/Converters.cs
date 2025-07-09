using System;
using Newtonsoft.Json;
using Unity.Mathematics;

namespace SceneBuilder.Utilities.DataStructures {
	public static class Converters {
		public class Int2 : JsonConverter<int2> {
			public override void WriteJson(JsonWriter writer, int2 value, JsonSerializer serializer) {
				writer.WriteRawValue($"[ {value.x}, {value.y} ]");
			}

			public override int2 ReadJson(JsonReader reader, Type objectType, int2 existingValue, bool hasExistingValue, JsonSerializer serializer) {
				var array = serializer.Deserialize<int[]>(reader);
				if (array.Length != 2)
					throw new JsonReaderException();

				return new int2(array[0], array[1]);
			}
		}

		public class Int3 : JsonConverter<int3> {
			public override void WriteJson(JsonWriter writer, int3 value, JsonSerializer serializer) {
				writer.WriteRawValue($"[ {value.x}, {value.y}, {value.z} ]");
			}

			public override int3 ReadJson(JsonReader reader, Type objectType, int3 existingValue, bool hasExistingValue, JsonSerializer serializer) {
				var array = serializer.Deserialize<int[]>(reader);
				if (array.Length != 3)
					throw new JsonReaderException();

				return new int3(array[0], array[1], array[2]);
			}
		}

		public class Float2 : JsonConverter<float2> {
			public override void WriteJson(JsonWriter writer, float2 value, JsonSerializer serializer) {
				writer.WriteRawValue($"[ {value.x:0.##}, {value.y:0.##} ]");
			}

			public override float2 ReadJson(JsonReader reader, Type objectType, float2 existingValue, bool hasExistingValue, JsonSerializer serializer) {
				var array = serializer.Deserialize<float[]>(reader);
				if (array.Length != 2)
					throw new JsonReaderException();

				return new float2(array[0], array[1]);
			}
		}

		public class Float3 : JsonConverter<float3> {
			public override void WriteJson(JsonWriter writer, float3 value, JsonSerializer serializer) {
				writer.WriteRawValue($"[ {value.x:0.##}, {value.y:0.##}, {value.z:0.##} ]");
			}

			public override float3 ReadJson(JsonReader reader, Type objectType, float3 existingValue, bool hasExistingValue, JsonSerializer serializer) {
				var array = serializer.Deserialize<float[]>(reader);
				if (array.Length != 3)
					throw new JsonReaderException();

				return new float3(array[0], array[1], array[2]);
			}
		}
	}
}