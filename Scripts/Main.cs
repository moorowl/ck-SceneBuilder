using System.Linq;
using HarmonyLib;
using PugMod;
using SceneBuilder.Networking;
using SceneBuilder.Objects;
using SceneBuilder.Scenes;
using SceneBuilder.Structures;
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
		}

		public void Init() {
			var modInfo = API.ModLoader.LoadedMods.FirstOrDefault(modInfo => modInfo.Handlers.Contains(this));
			var assetBundle = modInfo!.AssetBundles[0];

			var gameObject = new GameObject(Constants.InternalName);
			Object.DontDestroyOnLoad(gameObject);
			Object.Instantiate(assetBundle.LoadAsset<GameObject>(Constants.StructureUiPrefabPath), gameObject.transform);
		}

		public void Shutdown() { }

		public void ModObjectLoaded(Object obj) {
			if (obj is GameObject gameObject && gameObject.TryGetComponent<PooledGraphicalObject>(out var pooledGraphicalObject))
				PooledGraphicalObjectConverter.Register(pooledGraphicalObject);
		}

		public void Update() { }

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