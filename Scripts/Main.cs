using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Pug.UnityExtensions;
using PugMod;
using PugWorldGen;
using SceneBuilder.Networking;
using SceneBuilder.Scenes;
using SceneBuilder.Structures;
using SceneBuilder.Utilities.DataStructures;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SceneBuilder {
	public class Main : IMod {
		internal static StructureRequestClientSystem StructureRequestClientSystem;
		internal static StructureRequestServerSystem StructureRequestServerSystem;

		private static readonly List<PoolablePrefabBank> PoolablePrefabBanks = new();
		
		public void EarlyInit() {
			API.Client.OnWorldCreated += () => {
				StructureRequestClientSystem = API.Client.World.GetOrCreateSystemManaged<StructureRequestClientSystem>();
			};
			API.Server.OnWorldCreated += () => {
				StructureRequestServerSystem = API.Server.World.GetOrCreateSystemManaged<StructureRequestServerSystem>();
			};
		}

		public void Init() {
			var modInfo = API.ModLoader.LoadedMods.FirstOrDefault(modInfo => modInfo.Handlers.Contains(this));
			var assetBundle = modInfo!.AssetBundles[0];
			
			var gameObject = new GameObject("SceneBuilder");
			Object.DontDestroyOnLoad(gameObject);
			Object.Instantiate(assetBundle.LoadAsset<GameObject>("Assets/SceneBuilder/Prefabs/UserInterface/StructureUI.prefab"), gameObject.transform);
		}

		public void Shutdown() { }

		public void ModObjectLoaded(Object obj) {
			if (obj is PoolablePrefabBank bank)
				PoolablePrefabBanks.Add(bank);
		}

		public void Update() {
			if (Input.GetKeyDown(KeyCode.F8))
				StructureRequestClientSystem?.PlaceScene(new Identifier("TestSceneBundle", "Dirt/MeadowRuin"), Manager.main.player.GetEntityPosition().RoundToInt2(), (uint) Random.Range(0, 10000));
		}
		
		[HarmonyPatch]
		public static class Patches {
			[HarmonyPatch(typeof(MemoryManager), nameof(MemoryManager.Init))]
			[HarmonyPrefix]
			public static void InitMemory(MemoryManager __instance) {
				__instance.poolablePrefabBanks?.AddRange(PoolablePrefabBanks);
			}
			
			[HarmonyPatch(typeof(ECSManager), nameof(MemoryManager.Init))]
			[HarmonyPostfix]
			public static void InitEcs(MemoryManager __instance) {
				StructureLoader.Instance.LoadAll();
				SceneLoader.Instance.LoadAll();
			}
		}
	}
}