using System;
using SceneBuilder.Utilities;
using Unity.Mathematics;
using UnityEngine;

namespace SceneBuilder.UserInterface {
	public class StructureLootUI : MonoBehaviour {
		[SerializeField] private GameObject root;
		[SerializeField] private TextInputField inventoryLootTableTextInput;
		[SerializeField] private TextInputField dropLootTableTextInput;
		[SerializeField] private ErrorButtonUI applyButton;

		public bool IsShowing { get; private set; }

		private int2 _targetTilePosition;

		private void Awake() {
			root.SetActive(false);
		}

		public void Show(int2 targetTilePosition) {
			IsShowing = true;
			_targetTilePosition = targetTilePosition;

			var existingDataEntry = DataToolUtils.GetDataAt(_targetTilePosition, false);
			inventoryLootTableTextInput.SetInputText(existingDataEntry.InventoryLootTable == LootTableID.Empty ? "" : existingDataEntry.InventoryLootTable.ToString());
			dropLootTableTextInput.SetInputText(existingDataEntry.DropLootTable == LootTableID.Empty ? "" : existingDataEntry.DropLootTable.ToString());

			UpdateErrors();
		}

		public void Hide() {
			IsShowing = false;
			inventoryLootTableTextInput.SetInputText("");
			dropLootTableTextInput.SetInputText("");
		}

		public void Apply() {
			if (TryGetLootTable(inventoryLootTableTextInput, out var inventoryLootTable) && TryGetLootTable(dropLootTableTextInput, out var dropLootTable)) {
				Main.StructureRequestClientSystem.SetData(_targetTilePosition, new DataToolUtils.DataEntry {
					InventoryLootTable = inventoryLootTable,
					DropLootTable = dropLootTable
				});
			}

			Hide();
		}

		private void Update() {
			root.transform.localScale = Manager.ui.CalcGameplayUITargetScaleMultiplier();
			root.SetActive(IsShowing && !Manager.ui.isAnyInventoryShowing && !Manager.menu.IsAnyMenuActive() && !Manager.ui.mapUI.IsShowingBigMap);

			UpdateErrors();
		}

		private void UpdateErrors() {
			applyButton.ClearErrors();

			if (!TryGetLootTable(inventoryLootTableTextInput, out _) || !TryGetLootTable(dropLootTableTextInput, out _))
				applyButton.AddError("SceneBuilder:LootToolUI/ErrorUnknownLootTable");
		}

		private bool TryGetLootTable(TextInputField textInput, out LootTableID lootTable) {
			lootTable = LootTableID.Empty;

			var text = textInput.GetInputText();
			return string.IsNullOrWhiteSpace(text) || Enum.TryParse(textInput.GetInputText(), out lootTable);
		}
	}
}