using Unity.Mathematics;
using UnityEngine;

namespace SceneBuilder.UserInterface {
	public class StructureFrameUI : MonoBehaviour {
		[SerializeField] private GameObject root;
		[SerializeField] private SpriteRenderer frame;
		[SerializeField] private SpriteRenderer pinA;
		[SerializeField] private SpriteRenderer pinB;

		public int2? PinA;
		public int2? PinB;
		public Vector3 PinARenderPosition => PinA != null ? pinA.transform.localPosition : Vector3.zero;
		public Vector3 PinBRenderPosition => PinB != null ? pinB.transform.localPosition : Vector3.zero;
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
			root.SetActive(StructureUI.IsHoldingSaverTool);
			
			if (IsComplete) {
				frame.size = new Vector2(Size.x, Size.y);
				frame.transform.localPosition = new Vector3(Center.x, Center.y, frame.transform.localPosition.z);
			} else {
				frame.size = new Vector2(0f, 0f);
			}

			UpdatePin(PinA, PinB, pinA);
			UpdatePin(PinB, PinA, pinB);
		}

		private void UpdatePin(int2? tilePosition, int2? otherTilePosition, SpriteRenderer sr) {
			if (tilePosition.HasValue) {
				var position = tilePosition.Value.ToFloat2() + 0.5f;

				if (otherTilePosition.HasValue) {
					var otherPosition = otherTilePosition.Value.ToFloat2() + 0.5f;
					var direction = new int2(1, 1);

					if (otherPosition.x > position.x)
						direction.x = -1;
					if (otherPosition.y > position.y)
						direction.y = -1;

					position += direction.ToFloat2() * 0.5f;
				}
				
				sr.transform.localPosition = new Vector3(position.x, position.y, sr.transform.localPosition.z);
				sr.color = sr.color.ColorWithNewAlpha(1f);
			} else {
				sr.color = sr.color.ColorWithNewAlpha(0f);
			}
		}
	}
}