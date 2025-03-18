using PugMod;
using SceneBuilder.Commands;
using SceneBuilder.Scenes;
using SceneBuilder.Structures;
using SceneBuilder.Utilities;
using SceneBuilder.Utilities.DataStructures;
using UnityEngine;

namespace SceneBuilder {
	public class Main : IMod {
		public const string Version = "1.0";
		public const string InternalName = "SceneBuilder";
		public const string DisplayName = "Scene Builder";
		
		internal static ClientCommandSystem ClientCommandSystem;
		internal static ServerCommandSystem ServerCommandSystem;

		public void EarlyInit() {
			API.Client.OnWorldCreated += () => {
				ClientCommandSystem = API.Client.World.GetOrCreateSystemManaged<ClientCommandSystem>();
			};
			API.Server.OnWorldCreated += () => {
				ServerCommandSystem = API.Server.World.GetOrCreateSystemManaged<ServerCommandSystem>();
			};
		}

		public void Init() {
			var structureLoader = new StructureLoader();
			structureLoader.LoadAll();

			var sceneLoader = new SceneLoader();
			sceneLoader.LoadAll(structureLoader);
		}

		public void Shutdown() { }

		public void ModObjectLoaded(Object obj) { }

		public void Update() {
			if (Input.GetKeyDown(KeyCode.F7)) {
				ClientCommandSystem.SpawnScene(new Identifier("TestSceneBundle", "RuinedGlurchHouse").AsSceneName, Manager.main.player.GetEntityPosition().RoundToInt2());
			}
		}
	}
}