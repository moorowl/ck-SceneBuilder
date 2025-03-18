using System;

namespace SceneBuilder.Utilities.DataStructures {
	public readonly struct Identifier : IEquatable<Identifier> {
		public readonly string Namespace;
		public readonly string Path;

		public readonly string AsSceneName;
		
		public Identifier(string ns, string path) {
			Namespace = ns;
			Path = path;

			AsSceneName = $"{Namespace}:{Path}";
			if (AsSceneName.Length > 28)
				AsSceneName = AsSceneName.Substring(0, 28);
		}

		public override string ToString() {
			return $"{Namespace}:{Path}";
		}
		
		public bool Equals(Identifier other) {
			return Namespace == other.Namespace && Path == other.Path;
		}

		public override int GetHashCode() {
			return HashCode.Combine(Namespace, Path);
		}

		public static implicit operator Identifier(string input) {
			return TryParse(input, out var identifier) ? identifier : default;
		}
		
		public static bool TryParse(string input, out Identifier identifier) {
			identifier = default;
			
			if (string.IsNullOrEmpty(input))
				throw new ArgumentException();
    
			var parts = input.Split(':', 2);
			if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
				return false;

			identifier = new Identifier(parts[0], parts[1]);
			return true;
		}
	}
}