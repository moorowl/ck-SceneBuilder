using SceneBuilder.Utilities;
using UnityEngine;

namespace SceneBuilder.UserInterface {
	public class StructureSaveUI : MonoBehaviour {
		public delegate void SaveCallbackDelegate(string name);
		
		[SerializeField] private GameObject root;
		[SerializeField] private PugText statusText;
		[SerializeField] private PugText titleText;
		[SerializeField] private TextInputField textInput;
		[SerializeField] private ButtonUIElement saveButton;

		public bool IsShowing { get; private set; }

		private SaveCallbackDelegate _saveCallback;
		private Status _status;

		private void Awake() {
			root.SetActive(false);
			titleText.Render();
			_status = Status.None;
		}

		public void Show(SaveCallbackDelegate saveCallback) {
			IsShowing = true;
			_saveCallback = saveCallback;

			UpdateStatus();
		}

		public void Hide() {
			IsShowing = false;
			textInput.SetInputText("");
			_saveCallback = null;
			_status = Status.None;
		}

		public void Save() {
			_saveCallback?.Invoke(textInput.GetInputText());
			Hide();
		}
		
		private void Update() {
			root.transform.localScale = Manager.ui.CalcGameplayUITargetScaleMultiplier();
			root.SetActive(IsShowing && !Manager.ui.isAnyInventoryShowing && !Manager.menu.IsAnyMenuActive() && !Manager.ui.mapUI.isShowingBigMap);
			
			UpdateStatus();
			
			saveButton.canBeClicked = !string.IsNullOrWhiteSpace(textInput.GetInputText()) && _status == Status.Ok;
		}

		private void UpdateStatus() {
			_status = Status.Ok;

			var frameSize = StructureUI.FrameUI.Size.RoundToInt().ToInt2();
			if (frameSize.x > Utils.MaxSceneSize || frameSize.y > Utils.MaxSceneSize)
				_status = Status.TooBig;
			
			var text = PugText.ProcessText("SceneBuilder:DimensionsFormat", new[] {
				frameSize.x.ToString(),
				frameSize.y.ToString(),
				PugText.ProcessText($"SceneBuilder:Status/{_status}", null, true, false)
			}, true, false);
			statusText.Render(text);
		}

		enum Status {
			None,
			Ok,
			TooBig,
			AreaUnloaded
		}
	}
}