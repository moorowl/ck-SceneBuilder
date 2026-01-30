using System.Collections.Generic;
using UnityEngine;

namespace SceneBuilder.UserInterface {
	public class ErrorButtonUI : ButtonUIElement {
		private readonly List<TextAndFormatFields> _errors = new();

		public void ClearErrors() {
			_errors.Clear();
		}

		public void AddError(string term) {
			_errors.Add(new TextAndFormatFields {
				text = "SceneBuilder-General/ErrorFormat",
				formatFields = new[] {
					term
				},
				color = Color.white
			});

			canBeClicked = _errors.Count == 0;
		}

		protected override void LateUpdate() {
			canBeClicked = _errors.Count == 0;

			base.LateUpdate();
		}

		public override List<TextAndFormatFields> GetHoverDescription() {
			if (!canBeClicked)
				return _errors;

			return base.GetHoverDescription();
		}

		public override HoverWindowAlignment GetHoverWindowAlignment() {
			return HoverWindowAlignment.BOTTOM_RIGHT_OF_CURSOR;
		}
	}
}