using HarmonyLib;
using I2.Loc;
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
		public static StructureLootUI LootUI { get; private set; }
		
		private Transform _frameRenderAnchor;
		private bool _isSelectingPinB;
		private bool _isDraggingPinA;
		private bool _isDraggingPinB;

		public static bool IsHoldingSaverTool { get; private set; }
		public static bool IsHoldingLootTool { get; private set; }
		public static bool IsHoldingVoid { get; private set; }
		public static bool IsHoldingAnyTool => IsHoldingSaverTool || IsHoldingLootTool || IsHoldingVoid;
		
		private void Start() {
			_frameRenderAnchor = Manager.camera.GetRenderAnchor();
			FrameUI = Instantiate(framePrefab, _frameRenderAnchor).GetComponent<StructureFrameUI>();
			SaveUI = Instantiate(savePrefab, API.Rendering.UICamera.transform).GetComponent<StructureSaveUI>();
			LootUI = Instantiate(lootPrefab, API.Rendering.UICamera.transform).GetComponent<StructureLootUI>();
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
			IsHoldingLootTool = heldObject == API.Authoring.GetObjectID(Constants.StructureLootToolId);
			IsHoldingVoid = heldObject == API.Authoring.GetObjectID(Constants.StructureVoidId);
			
			FrameUI.PinPreview = null;
			
			var input = Manager.input.singleplayerInputModule;
			if (!SaveUI.IsShowing && !LootUI.IsShowing && Manager.ui.currentSelectedUIElement == null && !Manager.main.player.isInteractionBlocked && input.PrefersKeyboardAndMouse()) {
				var mouseTilePosition = EntityMonoBehaviour.ToWorldFromRender(Manager.ui.mouse.GetMouseGameViewPosition()).RoundToInt2();
				
				if (IsHoldingSaverTool) {
					FrameUI.PinPreview = mouseTilePosition;
					
					if (_isDraggingPinA)
						FrameUI.PinA = mouseTilePosition;
					if (_isSelectingPinB || _isDraggingPinB)
						FrameUI.PinB = mouseTilePosition;

					if (FrameUI.IsComplete && !_isSelectingPinB) {
						if (input.IsButtonCurrentlyDown(PlayerInput.InputType.UI_INTERACT)) {
							if (FrameUI.PinAHovered && !_isDraggingPinB)
								_isDraggingPinA = true;
							if (FrameUI.PinBHovered && !_isDraggingPinA)
								_isDraggingPinB = true;
						} else {
							_isDraggingPinA = false;
							_isDraggingPinB = false;
						}
					}

					if (input.WasButtonPressedDownThisFrame(PlayerInput.InputType.UI_INTERACT) && !_isDraggingPinA && !_isDraggingPinB) {
						if (!_isSelectingPinB) {
							FrameUI.PinA = mouseTilePosition;
							FrameUI.PinB = null;
							_isSelectingPinB = true;
							Utils.SendLocalChatMessage(LocalizationManager.GetTranslation("SceneBuilder:FirstPointSet"));
						} else {
							_isSelectingPinB = false;
							Utils.SendLocalChatMessage(LocalizationManager.GetTranslation("SceneBuilder:SecondPointSet"));
						}
					}

					if (FrameUI.IsComplete && input.WasButtonPressedDownThisFrame(PlayerInput.InputType.UI_SECOND_INTERACT)) {
						// save structure
						var position = FrameUI.Position.RoundToInt().ToInt2();
						var size = FrameUI.Size.RoundToInt().ToInt2();

						SaveUI.Show(structureName => {
							Main.StructureRequestClientSystem.SaveStructure(structureName, position, size);
							Utils.SendLocalChatMessage(string.Format(LocalizationManager.GetTranslation("SceneBuilder:SavedAs"), structureName));
						});
					}
				}

				if (IsHoldingLootTool) {
					FrameUI.PinPreview = mouseTilePosition;
					
					if (input.WasButtonPressedDownThisFrame(PlayerInput.InputType.UI_INTERACT))
						LootUI.Show(mouseTilePosition);
				}
			}
		}

		[HarmonyPatch]
		public static class Patches {
			[HarmonyPatch(typeof(SendClientInputSystem), "PlayerInteractionBlocked")]
			[HarmonyPostfix]
			private static void PlayerInteractionBlocked(SendClientInputSystem __instance, ref bool __result) {
				if (Manager.ui.currentSelectedUIElement == null && (IsHoldingSaverTool || IsHoldingLootTool))
					__result = true;
			}
		}
	}
}