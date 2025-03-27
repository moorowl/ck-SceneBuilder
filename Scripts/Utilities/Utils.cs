using System;
using System.Linq;
using System.Text.RegularExpressions;
using PugMod;
using SceneBuilder.Utilities.DataStructures;
using UnityEngine;

namespace SceneBuilder.Utilities {
	public static class Utils {
		private static readonly MemberInfo MiRenderText = typeof(ChatWindow).GetMembersChecked().FirstOrDefault(x => x.GetNameChecked() == "RenderText");
		
		public static void Log(string message) {
			Debug.Log($"[{Main.DisplayName}]: {message}");
		}

		public static bool IsContainedObjectEmpty(ContainedObjectsBuffer containedObject) {
			return containedObject.objectID == ObjectID.None || (containedObject.amount <= 0 && !PugDatabase.AmountIsDurabilityOrFullnessOrXp(containedObject.objectID, containedObject.variation));
		}
		
		public static void SendLocalChatMessage(string message) {
			//var text = PugText.ProcessText("CameraMode:SavedCapture", new[] { name }, true, false);
			API.Reflection.Invoke(MiRenderText, Manager.ui.chatWindow, message);
		}

		private static readonly Regex LocalFilesRegex = new(@$"^{Main.InternalName}\/Content\/([^\/]+)\/([^\/]+)\/(.*)\.json$");
		private static readonly Regex ModFilesRegex = new(@$"^(?:.*)\/Resources\/{Main.InternalName}\/Content\/([^\/]+)\/([^\/]+)\/(.*)\.json$");
		
		public static void LoadFilesFromBundles(string dataType, Action<Identifier, byte[]> callback) {
			// Load from local files
			foreach (var path in API.ConfigFilesystem.GetFiles(Main.InternalName)) {
				var match = LocalFilesRegex.Match(path);
				if (!match.Success || match.Groups.Count < 3)
					continue;
				
				if (match.Groups[2].Value != dataType)
					continue;
						
				var identifier = new Identifier(match.Groups[1].Value, match.Groups[3].Value);
				callback(identifier, API.ConfigFilesystem.Read(path));
			}
			
			// Load from mods
			foreach (var mod in API.ModLoader.LoadedMods) {
				foreach (var bundle in mod.AssetBundles) {
					foreach (var assetName in bundle.GetAllAssetNames()) {
						var match = ModFilesRegex.Match(assetName);
						if (!match.Success || match.Groups.Count < 3)
							continue;
						
						if (match.Groups[2].Value != dataType)
							continue;
						
						var identifier = new Identifier(match.Groups[1].Value, match.Groups[3].Value);
						callback(identifier, bundle.LoadAsset<TextAsset>(assetName).bytes);
					}
				}
			}
		}
		
		public static bool TryFindMatchingPrefab(ObjectID id, int variation, out GameObject prefab) {
			prefab = null;
			
			foreach (var entry in Manager.ecs.pugDatabase.prefabList) {
				if (!entry.gameObject.TryGetComponent<EntityMonoBehaviourData>(out var entityMonoBehaviourData))
					continue;

				if (entityMonoBehaviourData.objectInfo.objectID != id || entityMonoBehaviourData.objectInfo.variation != variation)
					continue;
				
				prefab = entry.gameObject;
				return true;
			}

			return false;
		}
	}
}