using System;
using UnityEditor;
using UnityEngine;

namespace CameraSyncTool.Internal {
	public class GameViewContent : ViewContent {

		public EditorWindow gameView { get; private set; }

		public override bool hasChanged => false;
		public override string name => this.gameView.titleContent.text;
		public override bool isValid => this.gameView;

		public GameViewContent(EditorWindow gameView) {
			this.gameView = gameView;
		}

		public override void Focus() {
			this.gameView.Focus();
		}

		public override void Snapshot(Action<Texture2D> callback) {
			if (callback == null) return;
			SnapshotUtils.TakeGameViewSnapshot(this.gameView, callback, false);
		}
	}
}
