using HarmonyLib;
using PugMod;
using SceneBuilder.Utilities;
using UnityEngine;
// ReSharper disable InconsistentNaming

namespace SceneBuilder.UserInterface {
	public class StructureUI : MonoBehaviour {
		[SerializeField] private GameObject framePrefab;
		[SerializeField] private GameObject savePrefab;

		private StructureFrameUI _frameUI;
		private Transform _frameRenderAnchor;
		private StructureSaveUI _saveUI;
		
		private bool _isSelectingPinB;

		public static bool IsHoldingSaverTool { get; private set; }
		public static bool IsHoldingDataTool { get; private set; }
		public static bool IsHoldingVoid { get; private set; }
		
		private void Start() {
			_frameRenderAnchor = Manager.camera.GetRenderAnchor();
			_frameUI = Instantiate(framePrefab, _frameRenderAnchor).GetComponent<StructureFrameUI>();
			_saveUI = Instantiate(savePrefab, API.Rendering.UICamera.transform).GetComponent<StructureSaveUI>();
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
			IsHoldingSaverTool = heldObject == API.Authoring.GetObjectID("SceneBuilder:StructureSaverTool");
			IsHoldingDataTool = heldObject == API.Authoring.GetObjectID("SceneBuilder:StructureDataTool");
			IsHoldingVoid = heldObject == API.Authoring.GetObjectID("SceneBuilder:StructureVoid");
			
			var input = Manager.input.singleplayerInputModule;
			if (IsHoldingSaverTool && !_saveUI.IsShowing && Manager.ui.currentSelectedUIElement == null && input.PrefersKeyboardAndMouse()) {
				var mouseTilePosition = EntityMonoBehaviour.ToWorldFromRender(Manager.ui.mouse.GetMouseGameViewPosition()).RoundToInt2();
				
				if (_isSelectingPinB)
					_frameUI.PinB = mouseTilePosition;
				
				if (input.WasButtonPressedDownThisFrame(PlayerInput.InputType.UI_INTERACT)) {
					if (!_isSelectingPinB) {
						_frameUI.PinA = mouseTilePosition;
						_frameUI.PinB = null;
						_isSelectingPinB = true;
						Utils.SendLocalChatMessage("First point set. Select a second point.");
					} else {
						_isSelectingPinB = false;
						Utils.SendLocalChatMessage("Second point set. Ready to save!");
					}
				}

				if (_frameUI.IsComplete && input.WasButtonPressedDownThisFrame(PlayerInput.InputType.UI_SECOND_INTERACT)) {
					// save structure
					var position = _frameUI.Position.RoundToInt().ToInt2();
					var size = _frameUI.Size.RoundToInt().ToInt2();

					_saveUI.Show(structureName => {
						Main.ClientCommandSystem.SaveStructure(structureName, position, size);
						Utils.SendLocalChatMessage($"Saved as {structureName}.json");
					});

					//Main.ClientCommandSystem.SaveStructure("Test", position, size);
					//Utils.SendLocalChatMessage("Saved");
				}
			}
		}

		[HarmonyPatch]
		public static class Patches {
			[HarmonyPatch(typeof(SendClientInputSystem), "PlayerInteractionBlocked")]
			[HarmonyPostfix]
			private static void PlayerInteractionBlocked(SendClientInputSystem __instance, ref bool __result) {
				if (Manager.ui.currentSelectedUIElement == null && IsHoldingSaverTool)
					__result = true;
			}
		}
	}
}