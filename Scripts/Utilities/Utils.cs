using System;
using System.Linq;
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
		
		public static void LoadFilesFromBundles(string type, Action<Identifier, byte[]> callback) {
			var paths = API.ConfigFilesystem.GetFiles(Main.InternalName);

			foreach (var path in paths) {
				var parts = path.Split("/");
				if (parts.Length < 5 || parts[1] != "Data" || parts[3] != type)
					continue;
				
				var identifier = new Identifier(parts[2], string.Join("/", parts[4..]).Replace(".json", string.Empty));
				callback(identifier, API.ConfigFilesystem.Read(path));
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