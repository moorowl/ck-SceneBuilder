using System.Linq;
using HarmonyLib;
using I2.Loc;
using PugMod;

// ReSharper disable InconsistentNaming

namespace SceneBuilder.Commands {
	[HarmonyPatch]
	public static class CreateScenesModCommand {
		private static readonly MemberInfo MiRenderText = typeof(ChatWindow).GetMembersChecked().FirstOrDefault(x => x.GetNameChecked() == "RenderText");
		
		[HarmonyPatch(typeof(ChatWindow), nameof(ChatWindow.Deactivate))]
		[HarmonyPrefix]
		[HarmonyPriority(Priority.First)]
		private static void Deactivate(ChatWindow __instance, ref bool commit) {
			var text = __instance.inputField.GetText();
			if (!commit || !text.ToLowerInvariant().StartsWith("/createscenesmod"))
				return;
			
			commit = false;
			
			var args = text.Split(" ");
			if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1])) {
				API.Reflection.Invoke(MiRenderText, __instance, LocalizationManager.GetTranslation("SceneBuilder:ModNameError"));
				return;
			}

			var modName = args[1].Trim();
			
			API.Reflection.Invoke(MiRenderText, __instance, string.Format(LocalizationManager.GetTranslation("SceneBuilder:ModCreated"), $"{Constants.InternalName}/Content/{modName}"));

			Main.StructureRequestClientSystem.CreateScenesMod(modName);
		}
	}
}