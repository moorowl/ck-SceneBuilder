using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PugMod;
using PugTilemap;
using SceneBuilder.Structures;
using SceneBuilder.Utilities;
using SceneBuilder.Utilities.DataStructures;
using Unity.Mathematics;
using UnityEngine;

namespace SceneBuilder.Scenes {
	public class SceneLoader {
		private readonly Dictionary<Identifier, SceneFile> _loadedScenes = new();
		
		public void LoadAll(StructureLoader structures) {
			Utils.Log("Loading all scenes...");
			
			LoadAllFromDirectory("TestSceneBundle", "SceneBuilder/Bundles/TestSceneBundle/Scenes");
			
			foreach (var (id, sceneFile) in _loadedScenes) {
				if (!structures.TryGetStructure(sceneFile.Structure, out var structureFile))
					continue;

				if (sceneFile.Generation == null)
					continue;

				Converter.ConvertAndInjectScene(id, sceneFile, structureFile);
			}
		}
		
		private void LoadAllFromDirectory(string ns, string path) {
			foreach (var file in API.ConfigFilesystem.GetFiles(path)) {
				var stringData = Encoding.UTF8.GetString(API.ConfigFilesystem.Read(file));
				var data = JsonConvert.DeserializeObject<SceneFile>(stringData);

				var identifier = new Identifier(ns, file.Replace(path + "/", "").Replace(".json", ""));
				
				Utils.Log($"- {identifier} (from {path})");
				
				_loadedScenes.Add(identifier, data);
			}
		}
	}
}