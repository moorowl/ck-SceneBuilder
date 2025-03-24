using UnityEngine;

namespace SceneBuilder.UserInterface {
	public class StructureSaveUI : MonoBehaviour {
		public delegate void SaveCallbackDelegate(string name);
		
		[SerializeField] private GameObject root;
		[SerializeField] private TextInputField textInput;
		[SerializeField] private ButtonUIElement saveButton;

		public bool IsShowing { get; private set; }

		private SaveCallbackDelegate _saveCallback;

		private void Awake() {
			root.SetActive(false);
		}

		public void Show(SaveCallbackDelegate saveCallback) {
			IsShowing = true;
			_saveCallback = saveCallback;
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
			root.SetActive(IsShowing && !Manager.ui.isAnyInventoryShowing && !Manager.menu.IsAnyMenuActive() && !Manager.ui.mapUI.isShowingBigMap);
			saveButton.canBeClicked = !string.IsNullOrWhiteSpace(textInput.GetInputText());
		}
	}
}