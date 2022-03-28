using System;
using UnityEditor;
using UnityEngine;

namespace CameraSyncTool.Internal {
	public class CameraViewContent : ViewContent {

		public Camera camera { get; private set; }

		public override bool hasChanged => this.camera.transform.hasChanged;
		public override string name => this.camera.name;
		public override bool isValid => this.camera;
		public override Vector3 position => this.camera.transform.position;
		public override Quaternion rotation => this.camera.transform.rotation;

		public CameraViewContent(Camera camera) {
			this.camera = camera;
		}

		public override void OnTransformGUI() {
			if (!EditorGUIUtility.wideMode) {
				EditorGUIUtility.wideMode = true;
			}
			EditorGUIUtility.labelWidth = 120;

			var transform = this.camera.transform;
			transform.localPosition = EditorGUILayout.Vector3Field("Position", transform.localPosition);
			var angles = EditorGUILayout.Vector3Field("Rotation", transform.localRotation.eulerAngles);
			transform.localRotation = Quaternion.Euler(angles);

			EditorGUIUtility.labelWidth = 0;
		}

		public override void OnCameraGUI() {
			if (!EditorGUIUtility.wideMode) {
				EditorGUIUtility.wideMode = true;
			}

			EditorGUIUtility.labelWidth = 120;
			var camera = this.camera;
			if (!camera.orthographic) {
				camera.fieldOfView = EditorGUILayout.Slider("Field of View", camera.fieldOfView, 1e-05f, 179f);
			}

			const float PREFIX_WIDTH = 110;
			EditorGUIUtility.labelWidth = 50;
			var rect = EditorGUILayout.GetControlRect();
			EditorGUI.LabelField(rect, "Clipping Planes");
			rect.x += PREFIX_WIDTH;
			rect.width -= PREFIX_WIDTH;
			camera.nearClipPlane = EditorGUI.FloatField(rect, "Near", camera.nearClipPlane);

			rect = EditorGUILayout.GetControlRect();
			rect.x += PREFIX_WIDTH;
			rect.width -= PREFIX_WIDTH;
			camera.farClipPlane = EditorGUI.FloatField(rect, "Far", camera.farClipPlane);

			EditorGUIUtility.labelWidth = 120;
			camera.rect = EditorGUILayout.RectField("Viewport Rect", camera.rect);

			EditorGUIUtility.labelWidth = 0;
		}

		public override void Focus() {
			Selection.activeObject = this.camera;
		}

		public override void Sync(Vector3 position, Quaternion rotation) {
			this.camera.transform.position = position;
			this.camera.transform.rotation = rotation;
		}

		public override void Snapshot(Action<Texture2D> callback) {
			if (callback == null) return;
			var tex = SnapshotUtils.TakeCameraSnapshot(this.camera, false);
			callback.Invoke(tex);
		}

	}
}
