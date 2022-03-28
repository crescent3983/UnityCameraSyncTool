using System;
using UnityEngine;

namespace CameraSyncTool.Internal {
	public abstract class ViewContent {

		public abstract bool isValid { get; }
		public abstract string name { get; }
		public abstract bool hasChanged { get; }

		public virtual Vector3 position => Vector3.zero;
		public virtual Quaternion rotation => Quaternion.identity;

		public virtual void OnTransformGUI() {}

		public virtual void OnCameraGUI() {}

		public virtual void Focus() {}

		public virtual void Sync(Vector3 position, Quaternion rotation) {}

		public virtual void Snapshot(Action<Texture2D> callback) {}
	}
}
