using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Pug.UnityExtensions;
using PugMod;
using Pug.Properties;
using SceneBuilder.Scenes;
using SceneBuilder.Utilities.DataStructures;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace SceneBuilder.Utilities {
	public static class Utils {
		private static readonly MemberInfo MiRenderText = typeof(ChatWindow).GetMembersChecked().FirstOrDefault(x => x.GetNameChecked() == "RenderText");

		public static void Log(string message) {
			Debug.Log($"[{Constants.FriendlyName}]: {message}");
		}

		public static void Log(Exception exception) {
			Debug.LogException(exception);
		}
		
		public static bool IsContainedObjectEmpty(ContainedObjectsBuffer containedObject) {
			return containedObject.objectID == ObjectID.None || (containedObject.amount <= 0 && !PugDatabase.AmountIsDurabilityOrFullnessOrXp(containedObject.objectID, containedObject.variation));
		}

		public static void SendLocalChatMessage(string message) {
			//var text = PugText.ProcessText("CameraMode:SavedCapture", new[] { name }, true, false);
			API.Reflection.Invoke(MiRenderText, Manager.ui.chatWindow, message);
		}

		private static readonly Regex LocalFilesRegex = new(@$"^{Constants.InternalName}\/Content\/([^\/]+)\/([^\/]+)\/(.*)\.json$");
		private static readonly Regex ModEmbeddedFilesRegex = new(@$"^(?:.*)\/Data\/{Constants.InternalName}\/([^\/]+)\/([^\/]+)\/(.*)\.json$");
		private static readonly Regex ModFilesRegex = new(@$"^(?:.*)\/Data\/{Constants.InternalName}\/([^\/]+)\/([^\/]+)\/(.*)\.json$");

		public static void LoadFilesFromBundles(string dataType, Action<Identifier, byte[]> callback) {
			/* Load from local files
			if (!API.ConfigFilesystem.DirectoryExists(Constants.InternalName))
				API.ConfigFilesystem.CreateDirectory(Constants.InternalName);
			foreach (var path in API.ConfigFilesystem.GetFiles(Constants.InternalName)) {
				var match = ModFilesRegex.Match(path);
				if (!match.Success || match.Groups.Count < 3)
					continue;

				if (match.Groups[2].Value != dataType)
					continue;

				var identifier = new Identifier(match.Groups[1].Value, match.Groups[3].Value);
				callback(identifier, API.ConfigFilesystem.Read(path));
			}*/

			// Load from mod bundles
			foreach (var mod in API.ModLoader.LoadedMods) {
				foreach (var bundle in mod.AssetBundles) {
					foreach (var assetName in bundle.GetAllAssetNames()) {
						var match = ModEmbeddedFilesRegex.Match(assetName);
						if (!match.Success || match.Groups.Count < 3)
							continue;

						if (match.Groups[2].Value != dataType)
							continue;

						var identifier = new Identifier(match.Groups[1].Value, match.Groups[3].Value);
						callback(identifier, bundle.LoadAsset<TextAsset>(assetName).bytes);
					}
				}

				/*var directory = API.ModLoader.GetDirectory(mod.ModId);
				if (directory != null) {
					var paths = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).Select(path => path.Replace(@"\", @"/"));
					foreach (var path in paths) {
						var match = ModFilesRegex.Match(path);
						if (!match.Success || match.Groups.Count < 3)
							continue;

						if (match.Groups[2].Value != dataType)
							continue;

						var identifier = new Identifier(match.Groups[1].Value, match.Groups[3].Value);
						callback(identifier, File.ReadAllBytes(path));
					}
				}*/
			}
		}

		public static string GetObjectIdName(ObjectID id) {
			return API.Authoring.ObjectProperties.GetPropertyString(id, PropertyID.name) ?? id.ToString();
		}

		public static bool TryFindMatchingPrefab(string id, int variation, out GameObject prefab) {
			prefab = null;

			var objectId = API.Authoring.GetObjectID(id);

			foreach (var entry in Manager.ecs.pugDatabase.prefabList) {
				if (entry.gameObject.TryGetComponent<EntityMonoBehaviourData>(out var entityMonoBehaviourData) && entityMonoBehaviourData.objectInfo.objectID == objectId && entityMonoBehaviourData.objectInfo.variation == variation) {
					prefab = entry.gameObject;
					return true;
				}
			}

			foreach (var entry in Manager.mod.ExtraAuthoring) {
				if (entry.gameObject.TryGetComponent<EntityMonoBehaviourData>(out var entityMonoBehaviourData) && entityMonoBehaviourData.objectInfo.objectID == objectId && entityMonoBehaviourData.objectInfo.variation == variation) {
					prefab = entry.gameObject;
					return true;
				}

				if (entry.gameObject.TryGetComponent<ObjectAuthoring>(out var objectAuthoring) && objectAuthoring.objectName == id) {
					prefab = entry.gameObject;
					return true;
				}
			}

			return variation > 0 && TryFindMatchingPrefab(id, 0, out prefab);
		}
		
		public static List<(ObjectDataCD ObjectData, float3 Position, Entity Entity)> ObjectQuery(CollisionWorld collisionWorld, World ecsWorld, int2 position, int2 size) {
			var objects = new List<(ObjectDataCD ObjectData, float3 Position, Entity Entity)>();
			var entitiesAdded = new HashSet<Entity>();

			var hits = new NativeList<DistanceHit>(Allocator.Temp);
			var center = position.ToFloat3() + (new float2(size.x - 1f, size.y - 1f).ToFloat3() / 2f);
			collisionWorld.OverlapBox(center, quaternion.identity, new float3(size.x / 2f, 10f, size.y / 2f), ref hits, new CollisionFilter {
				BelongsTo = PhysicsLayerID.Everything,
				CollidesWith = PhysicsLayerID.Everything
			});

			foreach (var hit in hits) {
				var entity = hit.Entity;
				if (entitiesAdded.Contains(entity))
					continue;

				if (!EntityUtility.TryGetComponentData<ObjectDataCD>(entity, ecsWorld, out var objectData))
					continue;

				if (!EntityUtility.TryGetComponentData<LocalTransform>(entity, ecsWorld, out var transform))
					continue;

				var tilePosition = transform.Position.RoundToInt2();
				if (tilePosition.x < position.x || tilePosition.y < position.y || tilePosition.x >= position.x + size.x || tilePosition.y >= position.y + size.y)
					continue;

				objects.Add((objectData, transform.Position, entity));
				entitiesAdded.Add(entity);
			}

			hits.Dispose();

			return objects;
		}

		public static List<(ObjectDataCD ObjectData, float3 Position, Entity Entity)> ObjectQuery(CollisionWorld collisionWorld, World ecsWorld, int2 position) {
			return ObjectQuery(collisionWorld, ecsWorld, position, new int2(1, 1));
		}
	}
}