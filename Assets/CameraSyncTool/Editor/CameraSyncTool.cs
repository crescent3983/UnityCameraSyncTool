using CameraSyncTool.Internal;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace CameraSyncTool {
	public class CameraSyncTool : EditorWindow {

		public enum SyncMode {
			None,
			Primary,
			Secondary
		}

		private static CameraSyncTool Window { get; set; }
		[MenuItem("Window/CameraSyncTool")]
		public static void OpenWindow() {
			if (Window == null) {
				Window = EditorWindow.GetWindow<CameraSyncTool>("CameraSyncTool", true);
				Window.minSize = new Vector2(600, 400);
				Window.Show();
			}
			Window.Focus();
		}

		private Dictionary<Camera, CameraViewContent> _cameraViews;
		private Dictionary<SceneView, SceneViewContent> _sceneViews;
		private GameViewContent _gameView;
		private ViewContent _selectedView;

		private ViewContent _primaryView;
		private Dictionary<ViewContent, SyncMode> _syncStatus;
		private bool isSyncable => this._primaryView != null && this._primaryView.isValid;

		private GUIStyle _contentStyle;

		private Vector2 _contentScroll;
		private bool _transformEditorFoldout = true;
		private bool _cameraEditorFoldout = true;

		private Color _selectedColor = new Color32(44, 93, 135, 255);
		private Color _sceneColor = new Color32(255, 167, 0, 255);
		private Color _gameColor =  new Color32(139, 195, 74, 255);
		private Color _cameraColor = new Color32(58, 167, 153, 255);

		#region Editor Hooks
		private void OnEnable() {
			if(this._cameraViews == null) {
				this._cameraViews = new Dictionary<Camera, CameraViewContent>();
			}
			if (this._sceneViews == null) {
				this._sceneViews = new Dictionary<SceneView, SceneViewContent>();
			}
			if (this._syncStatus == null) {
				this._syncStatus = new Dictionary<ViewContent, SyncMode>();
			}
		}

		private void OnInspectorUpdate() {
			this.RefreshContents();
		}

		private void Update() {
			if(this.isSyncable && this._primaryView.hasChanged) {
				this.DoSyncAll();
			}
		}

		private void OnGUI() {
			if (this._contentStyle == null) {
				this._contentStyle = new GUIStyle();
				this._contentStyle.fixedHeight = 37;
				this._contentStyle.alignment = TextAnchor.MiddleLeft;
				this._contentStyle.wordWrap = true;
				this._contentStyle.normal.textColor = Color.white;
				this._contentStyle.normal.background = Texture2D.whiteTexture;
				this._contentStyle.padding = new RectOffset(3, 3, 3, 3);
				this._contentStyle.richText = true;
			}

			EditorGUILayout.BeginHorizontal();
			this.DrawContentLayout();
			this.DrawSelected();
			EditorGUILayout.EndHorizontal();
		}

		private void DrawContentLayout() {
			this._contentScroll = EditorGUILayout.BeginScrollView(this._contentScroll, GUILayout.Width(200));

			foreach (var view in this._sceneViews) {
				if (view.Value.isValid) {
					this.DrawContent(view.Value, this._sceneColor);
				}
			}
			if (this._gameView != null && this._gameView.isValid) {
				this.DrawContent(this._gameView, this._gameColor);
			}
			foreach (var view in this._cameraViews) {
				if (view.Value.isValid) {
					this.DrawContent(view.Value, this._cameraColor, 15);
				}
			}

			EditorGUILayout.EndScrollView();
		}

		private void DrawContent(ViewContent view, Color color, float offset = 0) {
			const float HEADER_WIDTH = 10;
			const float STATUS_WIDTH = 30;

			var oldColor = GUI.backgroundColor;
			var rect = EditorGUILayout.GetControlRect(true, this._contentStyle.fixedHeight);
			rect.x += offset;
			rect.width -= offset;

			GUI.backgroundColor = color;
			var headerRect = new Rect(rect.x, rect.y, HEADER_WIDTH, rect.height);
			GUI.Box(headerRect, string.Empty, this._contentStyle);

			if (view == this._selectedView) {
				GUI.backgroundColor = this._selectedColor;
			}
			else {
				GUI.backgroundColor = new Color32(63, 63, 63, 255);
			}

			rect.x += HEADER_WIDTH;
			rect.width -= HEADER_WIDTH;
			if (GUI.Button(rect, view.name, this._contentStyle)) {
				view.Focus();
				this._selectedView = view;
			}

			if (this._syncStatus.TryGetValue(view, out SyncMode mode)) {
				rect.x = rect.x + rect.width - STATUS_WIDTH;
				rect.width = STATUS_WIDTH;
				GUI.Label(rect, this.GetSyncIcon(mode), this._contentStyle);
			}

			GUI.backgroundColor = oldColor;
		}

		private void DrawSelected() {
			EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));
			if (this._selectedView != null && this._selectedView.isValid) {
				GUIContent content;

				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField(this._selectedView.name);
				if (this._syncStatus.TryGetValue(this._selectedView, out SyncMode mode)) {
					EditorGUIUtility.labelWidth = 20;
					var newMode = (SyncMode)EditorGUILayout.EnumPopup(this.GetSyncIcon(mode), mode);
					if (newMode != mode) {
						this.SetSyncMode(this._selectedView, newMode);
					}
					EditorGUIUtility.labelWidth = 0;
				}
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.Space();

				content = EditorGUIUtility.IconContent("Transform Icon");
				content.text = "Transform";
				this._transformEditorFoldout = EditorGUILayout.Foldout(this._transformEditorFoldout, content);
				if (this._transformEditorFoldout) {
					EditorGUI.indentLevel++;
					this._selectedView.OnTransformGUI();
					EditorGUI.indentLevel--;
				}

				EditorGUILayout.Space();

				content = EditorGUIUtility.IconContent("Camera Icon");
				content.text = "Camera";
				this._cameraEditorFoldout = EditorGUILayout.Foldout(this._cameraEditorFoldout, content);
				if (this._cameraEditorFoldout) {
					EditorGUI.indentLevel++;
					this._selectedView.OnCameraGUI();
					EditorGUI.indentLevel--;
				}

				GUILayout.FlexibleSpace();
				if (GUILayout.Button("Snapshot")) {
					this.SaveToPNG(this._selectedView);
				}
			}
			EditorGUILayout.EndVertical();
		}

		private void RefreshContents() {
			this.RefreshCamera();
			this.RefreshSceneView();
			this.RefreshGameView();
		}

		private void RefreshCamera() {
			var removed = new List<Camera>();
			foreach(var view in this._cameraViews) {
				if (!view.Value.isValid) {
					removed.Add(view.Key);
				}
			}
			for(int i = 0; i < removed.Count; i++) {
				this._syncStatus.Remove(this._cameraViews[removed[i]]);
				this._cameraViews.Remove(removed[i]);
			}

			foreach(var camera in Camera.allCameras) {
				if (!this._cameraViews.ContainsKey(camera)) {
					this._cameraViews[camera] = new CameraViewContent(camera);
					this._syncStatus[this._cameraViews[camera]] = SyncMode.None;
				}
			}
		}

		private void RefreshSceneView() {
			var removed = new List<SceneView>();
			foreach (var view in this._sceneViews) {
				if (!view.Value.isValid) {
					removed.Add(view.Key);
				}
			}
			for (int i = 0; i < removed.Count; i++) {
				this._syncStatus.Remove(this._sceneViews[removed[i]]);
				this._sceneViews.Remove(removed[i]);
			}

			foreach (SceneView view in SceneView.sceneViews) {
				if (!this._sceneViews.ContainsKey(view)) {
					this._sceneViews[view] = new SceneViewContent(view);
					this._syncStatus[this._sceneViews[view]] = SyncMode.None;
				}
			}
		}

		private void RefreshGameView() {
			if (this._gameView == null || !this._gameView.isValid) {
				var assembly = typeof(EditorWindow).Assembly;
				var type = assembly.GetType("UnityEditor.GameView");
				this._gameView = new GameViewContent(EditorWindow.GetWindow(type));
			}
		}	

		private void SetSyncMode(ViewContent view, SyncMode mode) {
			if (this._syncStatus[view] == SyncMode.Primary) {
				this._primaryView = null;
			}
			if (mode == SyncMode.Primary) {
				var oldPrimary = this._primaryView;
				this._primaryView = view;
				if (oldPrimary != null) {
					this._syncStatus[oldPrimary] = SyncMode.Secondary;
					this.DoSync(oldPrimary);
				}
			}
			else if (mode == SyncMode.Secondary) {
				this.DoSync(view);
			}
			this._syncStatus[view] = mode;
		}

		private void DoSyncAll() {
			foreach (var view in this._syncStatus) {
				if (view.Value == SyncMode.Secondary) {
					this.DoSync(view.Key);
				}
			}
		}

		private void DoSync(ViewContent view) {
			if (this.isSyncable) {
				view.Sync(this._primaryView.position, this._primaryView.rotation);
			}
		}

		private GUIContent GetSyncIcon(SyncMode mode) {
			if (mode == SyncMode.None) {
				return EditorGUIUtility.IconContent("d_winbtn_mac_inact");
			}
			else if (mode == SyncMode.Primary) {
				return EditorGUIUtility.IconContent("d_winbtn_mac_close");
			}
			else {
				return EditorGUIUtility.IconContent("d_winbtn_mac_max");
			}
		}

		private async void SaveToPNG(ViewContent view) {
			var path = EditorUtility.SaveFilePanel("Save png", "", view.name, "png");
			if (!string.IsNullOrEmpty(path)) {
				await Task.Delay(500); // wait for save panel closed
				view.Snapshot((tex) =>
				{
					if (tex) {
						var bytes = tex.EncodeToPNG();
						File.WriteAllBytes(path, bytes);
						EditorUtility.RevealInFinder(path);
					}
				});
			}
		}
		#endregion
	}
}