using Pug.UnityExtensions;
using Unity.Mathematics;
using UnityEngine;

namespace SceneBuilder.UserInterface {
	public class StructureFrameUI : MonoBehaviour {
		[SerializeField] private GameObject root;
		[SerializeField] private SpriteRenderer frame;
		[SerializeField] private SpriteRenderer pinA;
		[SerializeField] private SpriteRenderer pinB;
		[SerializeField] private SpriteRenderer pinPreview;
		[SerializeField] private Sprite pinSprite;
		[SerializeField] private Sprite hoveredPinSprite;

		public int2? PinA;
		public int2? PinB;
		public int2? PinPreview;

		public bool PinAHovered => PinA != null && PinA.Value.Equals(EntityMonoBehaviour.ToWorldFromRender(Manager.ui.mouse.GetMouseGameViewPosition()).RoundToInt2());
		public bool PinBHovered => PinB != null && PinB.Value.Equals(EntityMonoBehaviour.ToWorldFromRender(Manager.ui.mouse.GetMouseGameViewPosition()).RoundToInt2());
		
		public Vector3 PinARenderPosition => PinA != null ? pinA.transform.localPosition : Vector3.zero;
		public Vector3 PinBRenderPosition => PinB != null ? pinB.transform.localPosition : Vector3.zero;
		public Vector3 PinPreviewRenderPosition => PinPreview != null ? pinPreview.transform.localPosition : Vector3.zero;
		
		public bool IsComplete => PinA != null && PinB != null;
		
		public Vector2 Position {
			get {
				if (!IsComplete)
					return default;
				
				var minX = math.min(PinA.Value.x, PinB.Value.x);
				var minY = math.min(PinA.Value.y, PinB.Value.y);
				return new Vector2(minX, minY);
			}
		}
		public Vector2 Size {
			get {
				if (!IsComplete)
					return default;
				
				var minX = math.min(PinA.Value.x, PinB.Value.x);
				var minY = math.min(PinA.Value.y, PinB.Value.y);
				var maxX = math.max(PinA.Value.x, PinB.Value.x) + 1;
				var maxY = math.max(PinA.Value.y, PinB.Value.y) + 1;

				return new Vector2(math.abs(maxX - minX), math.abs(maxY - minY));
			}
		}
		public Vector2 Center => Position + (Size / 2f);
		
		private void LateUpdate() {
			if (IsComplete && StructureUI.IsHoldingSaverTool) {
				frame.size = new Vector2(Size.x, Size.y);
				frame.transform.localPosition = new Vector3(Center.x, Center.y, frame.transform.localPosition.z);
			} else {
				frame.size = new Vector2(0f, 0f);
			}
			
			// pin a
			UpdatePinSr(pinA, PinA, StructureUI.IsHoldingSaverTool, PinAHovered);
			UpdatePinSr(pinB, PinB, StructureUI.IsHoldingSaverTool, PinBHovered);
			UpdatePinSr(pinPreview, PinPreview, StructureUI.IsHoldingSaverTool || StructureUI.IsHoldingLootTool, false);
		}

		private void UpdatePinSr(SpriteRenderer sr, int2? tilePosition, bool isVisible, bool isHovered) {
			if (tilePosition.HasValue && isVisible) {
				var position = tilePosition.Value.ToFloat2() + 0.5f;
				sr.transform.localPosition = new Vector3(position.x, position.y, sr.transform.localPosition.z);
				sr.color = sr.color.ColorWithNewAlpha(1f);
			} else {
				sr.color = sr.color.ColorWithNewAlpha(0f);
			}
			sr.sprite = isHovered ? hoveredPinSprite : pinSprite;
		}
	}
}