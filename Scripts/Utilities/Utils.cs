using UnityEngine;

namespace SceneBuilder.Utilities {
	public static class Utils {
		public static void Log(string message) {
			Debug.Log($"[{Main.DisplayName}]: {message}");
		}
	}
}