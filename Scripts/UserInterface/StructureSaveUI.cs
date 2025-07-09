using Pug.UnityExtensions;
using Unity.Mathematics;
using UnityEngine;

namespace SceneBuilder.UserInterface {
	public class StructureSaveUI : MonoBehaviour {
		public delegate void SaveCallbackDelegate(string name);

		[SerializeField] private GameObject root;
		[SerializeField] private PugText statusText;
		[SerializeField] private PugText titleText;
		[SerializeField] private TextInputField textInput;
		[SerializeField] private ErrorButtonUI saveButton;

		public bool IsShowing { get; private set; }

		private SaveCallbackDelegate _saveCallback;

		private void Awake() {
			root.SetActive(false);
			titleText.Render();
		}

		public void Show(SaveCallbackDelegate saveCallback) {
			IsShowing = true;
			_saveCallback = saveCallback;

			UpdateErrors();
		}

		public void Hide() {
			IsShowing = false;
			textInput.SetInputText("");
			_saveCallback = null;
		}

		public void Save() {
			_saveCallback?.Invoke(textInput.GetInputText());
			Hide();
		}

		private void Update() {
			root.transform.localScale = Manager.ui.CalcGameplayUITargetScaleMultiplier();
			root.SetActive(IsShowing && !Manager.ui.isAnyInventoryShowing && !Manager.menu.IsAnyMenuActive() && !Manager.ui.mapUI.IsShowingBigMap);

			UpdateErrors();
		}

		private void UpdateErrors() {
			saveButton.ClearErrors();

			if (string.IsNullOrWhiteSpace(textInput.GetInputText()))
				saveButton.AddError("SceneBuilder:SaveToolUI/ErrorNoName");

			var frameSize = StructureUI.FrameUI.Size.RoundToInt().ToInt2();
			if (math.cmax(frameSize) > Constants.MaxSceneSize)
				saveButton.AddError("SceneBuilder:SaveToolUI/ErrorAreaTooBig");

			if (Manager.main.player != null) {
				var frameCenter = StructureUI.FrameUI.Center.RoundToInt().ToInt2();
				if (math.distance(frameCenter, Manager.main.player.GetEntityPosition().RoundToInt2()) > Constants.MaxSceneSize * 2)
					saveButton.AddError("SceneBuilder:SaveToolUI/ErrorTooFarAway");
			}

			statusText.Render(PugText.ProcessText("SceneBuilder:SaveToolUI/Dimensions", new[] {
				frameSize.x.ToString(),
				frameSize.y.ToString()
			}, true, false));
		}
	}
}