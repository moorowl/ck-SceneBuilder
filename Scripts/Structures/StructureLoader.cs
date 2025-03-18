using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using PugMod;
using SceneBuilder.Utilities;
using SceneBuilder.Utilities.DataStructures;
using UnityEngine;

namespace SceneBuilder.Structures {
	public class StructureLoader {
		private readonly Dictionary<Identifier, StructureFile> _loadedStructures = new();

		public void LoadAll() {
			Utils.Log("Loading all structures...");
			
			LoadAllFromDirectory("TestSceneBundle", "SceneBuilder/Bundles/TestSceneBundle/Structures");
		}

		public bool TryGetStructure(Identifier identifier, out StructureFile structure) {
			return _loadedStructures.TryGetValue(identifier, out structure);
		}

		private void LoadAllFromDirectory(string ns, string path) {
			foreach (var file in API.ConfigFilesystem.GetFiles(path)) {
				var stringData = Encoding.UTF8.GetString(API.ConfigFilesystem.Read(file));
				var data = JsonConvert.DeserializeObject<StructureFile>(stringData);

				var identifier = new Identifier(ns, file.Replace(path + "/", "").Replace(".json", ""));
				
				Utils.Log($"- {identifier} (from {path})");
				
				_loadedStructures.Add(identifier, data);
			}
		}
	}
}