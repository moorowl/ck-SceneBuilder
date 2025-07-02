using SceneBuilder.UserInterface;

namespace SceneBuilder.Objects {
	public class StructureVoid : EntityMonoBehaviour {
		public override void ManagedLateUpdate() {
			XScaler.gameObject.SetActive(!isHidden && StructureUI.IsHoldingAnyTool);
		}
	}
} 