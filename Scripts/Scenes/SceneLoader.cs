using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json;
using Pug.UnityExtensions;
using PugWorldGen;
using SceneBuilder.Structures;
using SceneBuilder.Utilities;
using SceneBuilder.Utilities.DataStructures;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
// ReSharper disable InconsistentNaming

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
					if (!StructureLoader.Instance.TryGetStructure(file.Structure, out _)) {
						Utils.Log($"- {id} (skip, missing or unknown structure)");
						return;
					}
					
					if (file.Generation == null) {
						Utils.Log($"- {id} (skip, missing generation settings)");
						return;
					}
					
					if (_loadedScenes.TryAdd(id, file)) {
						_runtimeSceneNameMap[GetRuntimeName(id)] = id;
						Utils.Log($"- {id} (ok)");
					} else {
						Utils.Log($"- {id} (skip, scene with this id has already been added)");
					}
				} catch (Exception e) {
					Utils.Log($"- {id} (skip, parse error)");
					Utils.Log(e);
				}
			});
			
			Utils.Log("Injecting scenes...");
			var serverAuthoring = Manager.ecs.serverAuthoringPrefab;
			var customScenesTable = Resources.Load<CustomScenesDataTable>("Scenes/CustomScenesDataTable");
			
			foreach (var (id, sceneFile) in _loadedScenes) {
				if (!StructureLoader.Instance.TryGetStructure(sceneFile.Structure, out var structureFile))
					continue;
				
				var scene = sceneFile.ConvertToDataTableScene(id, structureFile, customScenesTable.scenes.Count);
				if (scene == null) {
					Utils.Log($"- {id} (skip, could not convert)");
					continue;
				}
				if (math.cmax(scene.boundsSize) > Constants.MaxSceneSize) {
					Utils.Log($"- {id} (skip, max scene size is {Constants.MaxSceneSize}x{Constants.MaxSceneSize})");
					continue;
				}
				
				Utils.Log($"- {id} (ok)");
				customScenesTable.scenes.Add(scene);

				if (sceneFile.Generation.Dungeon != null) {
					foreach (var dungeonSettings in sceneFile.Generation.Dungeon) {
						var dungeonCustomSceneAuthorings = FindDungeonCustomSceneAuthorings(serverAuthoring, dungeonSettings.InternalName);

						foreach (var dungeonCustomSceneAuthoring in dungeonCustomSceneAuthorings) {
							if (dungeonCustomSceneAuthoring.groups.IsValidIndex(dungeonSettings.SceneGroupIndex)) {
								dungeonCustomSceneAuthoring.groups[dungeonSettings.SceneGroupIndex].scenes.Add(new DungeonCustomScenesAuthoring.Scene {
									scene = new SceneReference {
										ScenePath = $"#{id}"
									}
								});
							}
						}
					}
				}
			}
		}
		
		public bool TryGet(Identifier identifier, out SceneFile scene) {
			return _loadedScenes.TryGetValue(identifier, out scene);
		}
		
		public bool TryGetFromRuntimeName(FixedString64Bytes name, out SceneFile scene) {
			scene = null;
			return _runtimeSceneNameMap.TryGetValue(name, out var identifier) && TryGet(identifier, out scene);
		}

		public static string GetRuntimeName(Identifier id) {
			return $"SB/{Animator.StringToHash(id.ToString())}";
		}

		public static bool IsRuntimeName(FixedString64Bytes name) {
			return name.Length >= 3 && name[0] == 'S' && name[1] == 'B' && name[2] == '/';
		}

		private static IEnumerable<DungeonCustomScenesAuthoring> FindDungeonCustomSceneAuthorings(GameObject serverAuthoring, string internalName) {
			var dungeonSpawnsTable = serverAuthoring.GetComponentInChildren<DungeonSpawnTable>();
			var pugWorldGenAuthorings = serverAuthoring.GetComponentsInChildren<PugWorldGenAuthoring>();
			
			foreach (var group in dungeonSpawnsTable.biomes) {
				foreach (var entry in group.spawnEntries) {
					if (!entry.prefab.TryGetComponent<DungeonAuthoring>(out var dungeonAuthoring) || dungeonAuthoring.name != internalName)
						continue;

					if (!entry.prefab.TryGetComponent<DungeonCustomScenesAuthoring>(out var dungeonCustomScenesAuthoring))
						continue;

					yield return dungeonCustomScenesAuthoring;
				}
			}

			foreach (var pugWorldGenAuthoring in pugWorldGenAuthorings) {
				if (!pugWorldGenAuthoring.prefab.TryGetComponent<DungeonAuthoring>(out var dungeonAuthoring) || dungeonAuthoring.name != internalName)
					continue;

				if (!pugWorldGenAuthoring.prefab.TryGetComponent<DungeonCustomScenesAuthoring>(out var dungeonCustomScenesAuthoring))
					continue;
				
				yield return dungeonCustomScenesAuthoring;
			}
		}

		[HarmonyPatch]
		public static class Patches {
			[HarmonyPatch(typeof(DungeonCustomScenesAuthoring), nameof(DungeonCustomScenesAuthoring.GetPersistentName))]
			[HarmonyPostfix]
			public static void GetPersistentName(SceneReference scene, ref ReadOnlySpan<char> __result) {
				if (scene.ScenePath.StartsWith("#") && scene.ScenePath.Length >= 4 && Identifier.TryParse(scene.ScenePath.Substring(1), out var id))
					__result = GetRuntimeName(id).AsSpan();
			}
			
			[HarmonyPatch(typeof(CustomScenesDataTable), nameof(CustomScenesDataTable.TryFindSceneByName))]
			[HarmonyPrefix]
			public static void TryFindSceneByName(CustomScenesDataTable __instance, ref ReadOnlySpan<char> sceneName) {
				if (sceneName.StartsWith("#") && sceneName.Length >= 4 && Identifier.TryParse(sceneName[1..].ToString(), out var id))
					sceneName = GetRuntimeName(id).AsSpan();
			}
			
			[HarmonyPatch(typeof(SceneReference), "get_SceneName")]
			[HarmonyPrefix]
			public static bool get_SceneName(SceneReference __instance, ref string __result) {
				if (__instance.ScenePath.StartsWith("#") && __instance.ScenePath.Length >= 4 && Identifier.TryParse(__instance.ScenePath.Substring(1), out var id)) {
					__result = GetRuntimeName(id);
					return false;
				}
				return true;
			}
		}
	}
}