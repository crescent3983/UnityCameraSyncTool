using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CameraSyncTool.Internal {

	// from: https://github.com/Unity-Technologies/UnityCsReference/blob/2020.3/Modules/SceneTemplateEditor/SnapshotUtils.cs

	public static class SnapshotUtils {

		public static Texture2D TakeCameraSnapshot(Camera camera, bool compress = true) {
			var rect = compress ? GetCompressedRect(camera.pixelWidth, camera.pixelHeight) : new Rect(0, 0, camera.pixelWidth, camera.pixelHeight);
			var renderTexture = new RenderTexture((int)rect.width, (int)rect.height, 24);
			var snapshotTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);

			var oldCameraRenderTexture = camera.targetTexture;
			camera.targetTexture = renderTexture;
			camera.Render();

			var old = RenderTexture.active;
			RenderTexture.active = renderTexture;
			snapshotTexture.ReadPixels(rect, 0, 0);
			RenderTexture.active = old;
			camera.targetTexture = oldCameraRenderTexture;

			// Don't forget to apply so that all operations are done.
			snapshotTexture.Apply();

			if (compress)
				snapshotTexture.Compress(false);

			return snapshotTexture;
		}

		static PropertyInfo _sceneViewCameraRect;
		static PropertyInfo sceneViewCameraRect {
			get {
				if(_sceneViewCameraRect == null) {
					_sceneViewCameraRect =
#if UNITY_2021_2_OR_NEWER
					typeof(SceneView).GetProperty("cameraViewport", BindingFlags.Instance | BindingFlags.NonPublic);
#else
					typeof(SceneView).GetProperty("cameraRect", BindingFlags.Instance | BindingFlags.NonPublic);
#endif
				}
				return _sceneViewCameraRect;
			}
		}

		public static void TakeSceneViewSnapshot(SceneView sceneView, Action<Texture2D> onTextureReadyCallback, bool compress = true) {
			// Focus the sceneView and wait until it has fully focused
			sceneView.Focus();

			void WaitForFocus() {
#if UNITY_2020_1_OR_NEWER
				if (!sceneView.hasFocus) {
#else
				var prop = typeof(SceneView).GetProperty("hasFocus", BindingFlags.NonPublic | BindingFlags.Instance);
				if (!(bool)prop.GetValue(sceneView)) {
#endif
					EditorApplication.delayCall += WaitForFocus;
					return;
				}

				// Prepare the sceneView region the
				const int tabHeight = 19; // Taken from DockArea, which is internal, and the value is also internal.
				var cameraRect = (Rect)sceneViewCameraRect.GetValue(sceneView);
				var offsetPosition = sceneView.position.position + cameraRect.position + new Vector2(0, tabHeight);
				var region = new Rect(offsetPosition, cameraRect.size);

				// Take the snapshot
				var texture = TakeScreenSnapshot(region, compress);

				// Execute callback
				onTextureReadyCallback(texture);
			}

			EditorApplication.delayCall += WaitForFocus;
		}

		public static Texture2D TakeScreenSnapshot(Rect region, bool compress = true) {
			var actualRegion = compress ? GetCompressedRect(region) : region;
			var colors = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(actualRegion.position, (int)actualRegion.width, (int)actualRegion.height);
			var snapshotTexture = new Texture2D((int)actualRegion.width, (int)actualRegion.height, TextureFormat.RGB24, false);
			snapshotTexture.SetPixels(colors);
			snapshotTexture.Apply();

			if (compress)
				snapshotTexture.Compress(false);

			return snapshotTexture;
		}

		public static void TakeGameViewSnapshot(EditorWindow gameView, Action<Texture2D> onTextureReadyCallback, bool compress = true) {
			// Focus the game view (there is no need to wait for focus here
			// as the snapshot won't happen until there is a render
			gameView.Focus();

			var textureAssetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/game-view-texture.png");
			ScreenCapture.CaptureScreenshot(textureAssetPath);

			void WaitForSnapshotReady() {
				// Wait if the file is not ready
				if (!File.Exists(textureAssetPath)) {
					EditorApplication.delayCall += WaitForSnapshotReady;
					return;
				}

				// Import the texture a first time
				AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceSynchronousImport);

				// Then get the importer for the texture
				var textureImporter = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
				if (textureImporter == null)
					return;

				// Set it readable
				var oldIsReadable = textureImporter.isReadable;
				textureImporter.isReadable = true;
				textureImporter.npotScale = TextureImporterNPOTScale.None;
				textureImporter.textureCompression = TextureImporterCompression.Uncompressed;

				// Re-import it again, then load it
				AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceSynchronousImport);
				var textureAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
				textureImporter.isReadable = oldIsReadable;
				if (!textureAsset) {
					Debug.LogWarning("Texture asset unavailable.");
					return;
				}

				// Copy the texture since we are going to delete the asset
				var textureCopy = new Texture2D(textureAsset.width, textureAsset.height);
				EditorUtility.CopySerialized(textureAsset, textureCopy);

				// Delete the original texture asset
				AssetDatabase.DeleteAsset(textureAssetPath);

				if (compress)
					textureCopy.Compress(false);

				onTextureReadyCallback(textureCopy);
			}

			EditorApplication.delayCall += WaitForSnapshotReady;
		}

		static Rect GetCompressedRect(int width, int height) {
			var compressedWidth = (width >> 2) << 2;
			var compressedHeight = (height >> 2) << 2;
			return new Rect(0, 0, compressedWidth, compressedHeight);
		}

		static Rect GetCompressedRect(Rect rect) {
			var compressedRect = GetCompressedRect((int)rect.width, (int)rect.height);
			compressedRect.position = rect.position;
			return compressedRect;
		}
	}
}
