using UnityEngine;

namespace SceneBuilder.Objects {
	public class PooledGraphicalObject : MonoBehaviour {
		public int initialSize = 16;
		public int maxFreeSize = 16;
		public int maxSize = 1024;

		public PoolablePrefabBank.PoolablePrefab GetPoolablePrefab() {
			return new PoolablePrefabBank.PoolablePrefab {
				prefab = gameObject,
				initialSize = initialSize,
				maxFreeSize = maxFreeSize,
				maxSize = maxSize
			};
		}
	}
}