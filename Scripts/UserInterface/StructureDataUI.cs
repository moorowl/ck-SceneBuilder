using System.Text;
using PugMod;
using SceneBuilder.Utilities;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SceneBuilder.UserInterface {
	public class StructureDataUI : MonoBehaviour {
		[SerializeField] private GameObject root;
		[SerializeField] private TextInputField keyTextInput;
		[SerializeField] private TextInputField valueTextInput;
		[SerializeField] private ErrorButtonUI applyButton;

		public bool IsShowing { get; private set; }
		
		private int2 _targetTilePosition;

		private void Awake() {
			root.SetActive(false);
		}

		public void Show(int2 targetTilePosition) {
			IsShowing = true;
			_targetTilePosition = targetTilePosition;
			
			UpdateErrors();
		}

		public void Hide() {
			IsShowing = false;
			keyTextInput.SetInputText("");
			valueTextInput.SetInputText("");
		}

		public void Apply() {
			SetData(_targetTilePosition, new DataEntry {
				Key = keyTextInput.GetInputText(),
				Value = valueTextInput.GetInputText()
			});
			Hide();
		}
		
		private void Update() {
			root.transform.localScale = Manager.ui.CalcGameplayUITargetScaleMultiplier();
			root.SetActive(IsShowing && !Manager.ui.isAnyInventoryShowing && !Manager.menu.IsAnyMenuActive() && !Manager.ui.mapUI.IsShowingBigMap);
			
			UpdateErrors();
		}

		private void UpdateErrors() {
			applyButton.ClearErrors();
			
			if (string.IsNullOrEmpty(keyTextInput.GetInputText()))
				applyButton.AddError("SceneBuilder:DataToolUI/ErrorNoKey");
			else if (keyTextInput.GetInputText() != "LootTable")
				applyButton.AddError("SceneBuilder:DataToolUI/ErrorInvalidKey");
			
			if (string.IsNullOrEmpty(valueTextInput.GetInputText()))
				applyButton.AddError("SceneBuilder:DataToolUI/ErrorNoValue");
		}

		private static void SetData(int2 tilePosition, DataEntry entry) {
			var encodedData = entry.Encode();
		}

		private static DataEntry? GetData(int2 tilePosition) {
			var entity = GetExistingDataEntity(tilePosition);
			if (entity != Entity.Null && EntityUtility.TryGetBuffer<DescriptionBuffer>(entity, API.Client.World, out var descriptionBuffer) && descriptionBuffer.Length > 0) {
				var textBuffer = new byte[descriptionBuffer.Length];
				for (var i = 0; i < descriptionBuffer.Length; i++)
					textBuffer[i] = descriptionBuffer[i].Value;

				foreach (var encodedDataEntry in Encoding.UTF8.GetString(textBuffer).Split("@")) {
					if (DataEntry.TryDecode(encodedDataEntry, out var entry)) {
						return entry;
					}
				}
			}

			return null;
		}

		private static Entity GetExistingDataEntity(int2 tilePosition) {
			var dataId = API.Authoring.GetObjectID("SceneBuilder:StructureDataTool");
			
			return Entity.Null;
		}

		private struct DataEntry {
			public string Key;
			public string Value;

			public bool ShouldSerialize => !string.IsNullOrWhiteSpace(Key) || !string.IsNullOrWhiteSpace(Value);
			
			public string Encode() {
				return $"{Key}={Value}";
			}

			public static bool TryDecode(string data, out DataEntry entry) {
				var parts = data.Split('=');

				if (parts.Length == 2) {
					entry = new DataEntry {
						Key = parts[0],
						Value = parts[1]
					};
					return true;
				}

				entry = default;
				return false;
			}
		}

		private enum Error {
			NoKey,
			InvalidKey,
			NoValue
		}
	}
}