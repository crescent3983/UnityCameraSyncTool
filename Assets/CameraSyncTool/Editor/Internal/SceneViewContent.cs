using System;
using UnityEditor;
using UnityEngine;

namespace CameraSyncTool.Internal {
	public class SceneViewContent : ViewContent {

		public SceneView sceneView { get; private set; }

		public override bool hasChanged => this.sceneView.camera.transform.hasChanged;
		public override string name => this.sceneView.titleContent.text;
		public override bool isValid => this.sceneView;
		public override Vector3 position => this.sceneView.camera.transform.position;
		public override Quaternion rotation => this.sceneView.camera.transform.rotation;

		public SceneViewContent(SceneView sceneView) {
			this.sceneView = sceneView;
		}

		public override void OnTransformGUI() {
			if (!EditorGUIUtility.wideMode) {
				EditorGUIUtility.wideMode = true;
			}
			EditorGUIUtility.labelWidth = 120;

			var transform = this.sceneView.camera.transform;
			var position = EditorGUILayout.Vector3Field("Position", transform.localPosition);
			var euler = EditorGUILayout.Vector3Field("Rotation", transform.localRotation.eulerAngles);
			this.sceneView.rotation = Quaternion.Euler(euler);
			this.sceneView.pivot = position + this.sceneView.rotation * new Vector3(0, 0, this.sceneView.cameraDistance);

			EditorGUIUtility.labelWidth = 0;
		}

		public override void OnCameraGUI() {
#if UNITY_2019_1_OR_NEWER
			if (!EditorGUIUtility.wideMode) {
				EditorGUIUtility.wideMode = true;
			}

			EditorGUIUtility.labelWidth = 120;
			var camera = this.sceneView.camera;
			var setting = this.sceneView.cameraSettings;
			if (!camera.orthographic) {
				setting.fieldOfView = EditorGUILayout.Slider("Field of View", setting.fieldOfView, 1e-05f, 179f);
			}

			setting.dynamicClip = EditorGUILayout.Toggle("Dynamic Clip", setting.dynamicClip);
			if (!setting.dynamicClip) {
				const float PREFIX_WIDTH = 110;
				EditorGUIUtility.labelWidth = 50;
				var rect = EditorGUILayout.GetControlRect();
				EditorGUI.LabelField(rect, "Clipping Planes");
				rect.x += PREFIX_WIDTH;
				rect.width -= PREFIX_WIDTH;
				setting.nearClip = EditorGUI.FloatField(rect, "Near", setting.nearClip);

				rect = EditorGUILayout.GetControlRect();
				rect.x += PREFIX_WIDTH;
				rect.width -= PREFIX_WIDTH;
				setting.farClip = EditorGUI.FloatField(rect, "Far", setting.farClip);
			}

			EditorGUIUtility.labelWidth = 0;
#else
			EditorGUILayout.HelpBox($"Camera properties are not supported before Unity 2019.1", MessageType.Warning);
#endif
		}

		public override void Focus() {
			this.sceneView.Focus();
		}

		public override void Sync(Vector3 position, Quaternion rotation) {
			this.sceneView.rotation = rotation;
			this.sceneView.pivot = position + this.sceneView.rotation * new Vector3(0, 0, this.sceneView.cameraDistance);
		}

		public override void Snapshot(Action<Texture2D> callback) {
			if (callback == null) return;
			SnapshotUtils.TakeSceneViewSnapshot(this.sceneView, callback, false);
		}

	}
}
