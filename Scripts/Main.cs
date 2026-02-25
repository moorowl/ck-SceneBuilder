using System.Linq;
using HarmonyLib;
using Pug.UnityExtensions;
using PugMod;
using PugWorldGen;
using SceneBuilder.Networking;
using SceneBuilder.Objects;
using SceneBuilder.Scenes;
using SceneBuilder.Structures;
using SceneBuilder.Utilities.DataStructures;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace SceneBuilder {
	public class Main : IMod {
		internal static StructureRequestClientSystem StructureRequestClientSystem { get; private set; }

		public void EarlyInit() {
			Debug.Log($"[{Constants.FriendlyName}]: Mod version: {Constants.Version}");

			API.Client.OnWorldCreated += () => {
				StructureRequestClientSystem = API.Client.World.GetOrCreateSystemManaged<StructureRequestClientSystem>();
			};
			API.Server.OnWorldCreated += () => {
				API.Server.World.GetExistingSystemManaged<ApplySpawnedCustomSceneSystem>().Enabled = false;
			};
		}

		public void Init() {
			var modInfo = API.ModLoader.LoadedMods.FirstOrDefault(modInfo => modInfo.Handlers.Contains(this));
			var assetBundle = modInfo!.AssetBundles[0];

			var gameObject = new GameObject(Constants.InternalName);
			Object.DontDestroyOnLoad(gameObject);
			Object.Instantiate(assetBundle.LoadAsset<GameObject>(Constants.StructureUiPrefabPath), gameObject.transform);
			
			BurstDisabler.DisableBurstForSystem<DungeonApplySpawnedObjectsSystem>();
		}

		public void Shutdown() { }

		public void ModObjectLoaded(Object obj) {
			if (obj is GameObject gameObject && gameObject.TryGetComponent<PooledGraphicalObject>(out var pooledGraphicalObject))
				PooledGraphicalObjectConverter.Register(pooledGraphicalObject);
		}

		public void Update() {
			/*if (Input.GetKeyDown(KeyCode.F8)) {
				StructureRequestClientSystem.PlaceScene(new Identifier("MoreScenes", "Desert/DodoIsland"), Manager.main.player.WorldPosition.RoundToInt2());
			}*/
		}

		[HarmonyPatch]
		public static class Patches {
			[HarmonyPatch(typeof(ECSManager), nameof(MemoryManager.Init))]
			[HarmonyPostfix]
			public static void InitEcs(MemoryManager __instance) {
				StructureLoader.Instance.LoadAll();
				SceneLoader.Instance.LoadAll();
			}
		}
	}
}