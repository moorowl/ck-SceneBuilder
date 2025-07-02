using HarmonyLib;
using Pug.UnityExtensions;
using PugMod;
using SceneBuilder.Utilities;
using UnityEngine;
// ReSharper disable InconsistentNaming

namespace SceneBuilder.UserInterface {
	public class StructureUI : MonoBehaviour {
		[SerializeField] private GameObject framePrefab;
		[SerializeField] private GameObject savePrefab;
		[SerializeField] private GameObject lootPrefab;

		public static StructureFrameUI FrameUI { get; private set; }
		public static StructureSaveUI SaveUI { get; private set; }
		public static StructureDataUI DataUI { get; private set; }
		
		private static Transform _frameRenderAnchor;
		private static bool _isSelectingPinB;

		public static bool IsHoldingSaverTool { get; private set; }
		public static bool IsHoldingDataTool { get; private set; }
		public static bool IsHoldingVoid { get; private set; }
		public static bool IsHoldingAnyTool => IsHoldingSaverTool || IsHoldingDataTool || IsHoldingVoid;
		
		private void Start() {
			_frameRenderAnchor = Manager.camera.GetRenderAnchor();
			FrameUI = Instantiate(framePrefab, _frameRenderAnchor).GetComponent<StructureFrameUI>();
			SaveUI = Instantiate(savePrefab, API.Rendering.UICamera.transform).GetComponent<StructureSaveUI>();
			DataUI = Instantiate(lootPrefab, API.Rendering.UICamera.transform).GetComponent<StructureDataUI>();
		}

		private void OnDestroy() {
			if (_frameRenderAnchor != null)
				Manager.camera.ReturnRenderAnchor(_frameRenderAnchor);
		}

		private void Update() {
			var player = Manager.main.player;
			if (player == null)
				return;

			var heldObject = player.GetHeldObject().objectID;
			IsHoldingSaverTool = heldObject == API.Authoring.GetObjectID(Constants.StructureSaverToolId);
			IsHoldingDataTool = heldObject == API.Authoring.GetObjectID(Constants.StructureDataToolId);
			IsHoldingVoid = heldObject == API.Authoring.GetObjectID(Constants.StructureVoidId);
			
			var input = Manager.input.singleplayerInputModule;
			if (!SaveUI.IsShowing && Manager.ui.currentSelectedUIElement == null && !Manager.main.player.isInteractionBlocked && input.PrefersKeyboardAndMouse()) {
				var mouseTilePosition = EntityMonoBehaviour.ToWorldFromRender(Manager.ui.mouse.GetMouseGameViewPosition()).RoundToInt2();
				
				if (IsHoldingSaverTool) {
					if (_isSelectingPinB)
						FrameUI.PinB = mouseTilePosition;
				
					if (input.WasButtonPressedDownThisFrame(PlayerInput.InputType.UI_INTERACT)) {
						if (!_isSelectingPinB) {
							FrameUI.PinA = mouseTilePosition;
							FrameUI.PinB = null;
							_isSelectingPinB = true;
							Utils.SendLocalChatMessage("First point set. Select a second point.");
						} else {
							_isSelectingPinB = false;
							Utils.SendLocalChatMessage("Second point set. Ready to save!");
						}
					}

					if (FrameUI.IsComplete && input.WasButtonPressedDownThisFrame(PlayerInput.InputType.UI_SECOND_INTERACT)) {
						// save structure
						var position = FrameUI.Position.RoundToInt().ToInt2();
						var size = FrameUI.Size.RoundToInt().ToInt2();

						SaveUI.Show(structureName => {
							Main.StructureRequestClientSystem.SaveStructure(structureName, position, size);
							Utils.SendLocalChatMessage($"Saved as {structureName}.json");
						});
					}
				}
				
				if (IsHoldingDataTool && input.WasButtonPressedDownThisFrame(PlayerInput.InputType.UI_INTERACT))
					DataUI.Show(mouseTilePosition);
			}
		}

		[HarmonyPatch]
		public static class Patches {
			[HarmonyPatch(typeof(SendClientInputSystem), "PlayerInteractionBlocked")]
			[HarmonyPostfix]
			private static void PlayerInteractionBlocked(SendClientInputSystem __instance, ref bool __result) {
				if (Manager.ui.currentSelectedUIElement == null && (IsHoldingSaverTool || IsHoldingDataTool))
					__result = true;
			}
		}
	}
}