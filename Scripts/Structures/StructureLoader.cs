using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using SceneBuilder.Utilities;
using SceneBuilder.Utilities.DataStructures;

namespace SceneBuilder.Structures {
	public class StructureLoader {
		public static StructureLoader Instance { get; private set; } = new();
		
		private readonly Dictionary<Identifier, StructureFile> _loadedStructures = new();

		public void LoadAll() {
			Utils.Log("Loading structures...");
			Utils.LoadFilesFromBundles("Structures", (id, data) => {
				try {
					var file = JsonConvert.DeserializeObject<StructureFile>(Encoding.UTF8.GetString(data));
					StructureFile.Converter.Validate(file);
					Utils.Log(_loadedStructures.TryAdd(id, file) ? $"- {id} (ok)" : $"- {id} (skip, structure with this id has already been added)");
				} catch (Exception e) {
					Utils.Log($"- {id} (skip, parse error)");
					Utils.Log(e);
				}
			});
		}

		public bool TryGetStructure(Identifier identifier, out StructureFile structure) {
			return _loadedStructures.TryGetValue(identifier, out structure);
		}
	}
}