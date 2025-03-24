using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using SceneBuilder.Structures;
using SceneBuilder.Utilities;
using SceneBuilder.Utilities.DataStructures;
using Unity.Collections;

namespace SceneBuilder.Scenes {
	public class SceneLoader {
		public static SceneLoader Instance { get; private set; } = new();
		
		private readonly Dictionary<Identifier, SceneFile> _loadedScenes = new();
		private readonly Dictionary<FixedString64Bytes, Identifier> _runtimeSceneNameMap = new();
		
		public void LoadAll() {
			Utils.Log("Loading scenes...");
			Utils.LoadFilesFromBundles("Scenes", (id, data) => {
				try {
					var file = JsonConvert.DeserializeObject<SceneFile>(Encoding.UTF8.GetString(data));
					if (_loadedScenes.TryAdd(id, file)) {
						_runtimeSceneNameMap[id.AsSceneName] = id;
						Utils.Log($"- {id} (ok)");
					} else {
						Utils.Log($"- {id} (skip, scene with this id has already been added)");
					}
				} catch (Exception e) {
					Utils.Log($"- {id} (skip, parse error)");
					Utils.Log(e.Message);
				}
			});
			
			Utils.Log("Injecting scenes...");
			var serverCustomScenesTable = Manager.ecs.serverAuthoringPrefab.GetComponentInChildren<CustomSceneAuthoring>().CustomScenesDataTable;
			foreach (var (id, sceneFile) in _loadedScenes) {
				if (!StructureLoader.Instance.TryGetStructure(sceneFile.Structure, out var structureFile)) {
					Utils.Log($"- {id} (skip, missing or invalid structure name)");
					continue;
				}

				if (sceneFile.Generation == null) {
					Utils.Log($"- {id} (skip, missing generation settings)");
					continue;
				}

				var scene = sceneFile.ConvertToDataTableScene(id, structureFile);
				if (scene != null) {
					Utils.Log($"- {id} (ok)");
					serverCustomScenesTable.scenes.Add(scene);
				}
			}
		}
		
		public bool TryGetScene(Identifier identifier, out SceneFile scene) {
			return _loadedScenes.TryGetValue(identifier, out scene);
		}
		
		public bool TryGetSceneFromRuntimeName(FixedString64Bytes name, out SceneFile scene) {
			scene = null;
			return _runtimeSceneNameMap.TryGetValue(name, out var identifier) && TryGetScene(identifier, out scene);
		}

		public static bool IsModdedScene(FixedString64Bytes name) {
			return name.Length >= 3 && name[0] == 'S' && name[1] == 'B' && name[2] == '/';
		}
	}
}