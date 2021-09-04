using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace jwellone.Editor.Inspector
{
	[CustomEditor(typeof(Camera))]
	public class CustomCameraInspector : CameraEditor
	{
		private const int SIZE = 256;

		enum SyncType
		{
			Default = 0,
			SceneVeiwToSlef,
			SelfToSceneView
		}

		private static List<CustomCameraInspector> s_inspectors = new List<CustomCameraInspector>();

		private bool m_isOpen = false;
		private SyncType m_syncSelectType = SyncType.Default;
		private Vector2 m_prevMousePos = Vector2.zero;

		private Vector2 ScreenRes
		{
			get
			{
				var screenRes = UnityStats.screenRes.Split('x');
				return new Vector2(int.Parse(screenRes[0]), int.Parse(screenRes[1]));
			}
		}

		[InitializeOnLoadMethod]
		private static void OnInitializeOnLoadMethod()
		{
			EditorApplication.update -= Update;
			EditorApplication.update += Update;
			s_inspectors.Clear();
		}

		private static void Update()
		{
			var sceneViewCamera = SceneView.lastActiveSceneView?.camera;
			if (sceneViewCamera == null)
			{
				return;
			}

			foreach (var inspector in s_inspectors)
			{
				if (inspector.m_syncSelectType == SyncType.Default)
				{
					continue;
				}

				var camera = inspector.target as Camera;
				switch (inspector.m_syncSelectType)
				{
					case SyncType.SceneVeiwToSlef:
						{
							camera.transform.position = sceneViewCamera.transform.position;
							camera.transform.rotation = sceneViewCamera.transform.rotation;
							break;
						}

					case SyncType.SelfToSceneView:
						{
							var sceneCamera = SceneView.lastActiveSceneView;
							sceneCamera.in2DMode = false;
							sceneCamera.LookAtDirect(camera.transform.position, camera.transform.rotation, 0.01f);
							break;
						}
				}
			}
		}

		public new void OnEnable()
		{
			base.OnEnable();
			s_inspectors.Add(this);
		}

		public new void OnDestroy()
		{
			base.OnDestroy();
			s_inspectors.Remove(this);
		}

		public override void OnInspectorGUI()
		{
			m_syncSelectType = (SyncType)GUILayout.Toolbar((int)m_syncSelectType, new string[] { "Default", "From SceneView To Self", "From Self To Scene View" });

			DrawPreview();
			base.OnInspectorGUI();
		}

		private void DrawPreview()
		{
			m_isOpen = EditorGUILayout.Foldout(m_isOpen, "Preview", true);
			if (!m_isOpen)
			{
				return;
			}

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			var isScreenShot = GUILayout.Button("Screen Shot", GUILayout.Width(128));
			GUILayout.EndHorizontal();

			var camera = target as Camera;
			var size = ScreenRes;
			if (size.x > size.y)
			{
				size.y *= SIZE / size.x;
				size.x = SIZE;
			}
			else
			{
				size.x *= SIZE / size.y;
				size.y = SIZE;
			}

			var rt = RenderTexture.GetTemporary((int)size.x, (int)size.y, 24, RenderTextureFormat.ARGB32);
			var tmpRT = camera.targetTexture;
			camera.targetTexture = rt;
			camera.Render();
			camera.targetTexture = tmpRT;

			GUI.backgroundColor = Color.gray;
			GUILayout.BeginHorizontal(EditorStyles.textArea);
			GUILayout.FlexibleSpace();
			GUILayout.Box(rt);
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUI.backgroundColor = Color.white;

			RenderTexture.ReleaseTemporary(rt);

			var e = Event.current;
			var lastRect = GUILayoutUtility.GetLastRect();

			if (lastRect.Contains(e.mousePosition))
			{
				var transform = camera.transform;
				switch (e.type)
				{
					case EventType.MouseDown:
						{
							Event.current.Use();
							break;
						}

					case EventType.MouseDrag:
						{
							var diff = e.mousePosition - m_prevMousePos;

							if (e.button == 1)
							{
								diff *= Time.deltaTime * 35f;
								transform.RotateAround(transform.position, transform.right, diff.y);
								transform.RotateAround(transform.position, Vector3.up, diff.x);
							}
							else
							{
								diff.y *= -1;
								transform.Translate(-diff * Time.deltaTime);
							}

							Event.current.Use();
							break;
						}

					case EventType.ScrollWheel:
						{
							transform.position += transform.forward * Time.deltaTime * -e.delta.y * 10;
							Event.current.Use();
							break;
						}
				}

				m_prevMousePos = e.mousePosition;
			}

			if (isScreenShot)
			{
				WriteScreenShot();
			}
		}

		private void WriteScreenShot()
		{
			var size = ScreenRes;
			var filePath = string.Format("{0}/../ss{1}.png", Application.dataPath, DateTime.Now.ToString("yyyyMMddHHmmss"));
			var rt = new RenderTexture((int)size.x, (int)size.y, 24, RenderTextureFormat.ARGB32);
			var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
			var camera = target as Camera;
			var tmpRT = camera.targetTexture;

			rt.antiAliasing = 2;
			camera.targetTexture = rt;
			camera.Render();
			camera.targetTexture = tmpRT;

			tmpRT = RenderTexture.active;
			RenderTexture.active = rt;
			tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
			tex.Apply();
			RenderTexture.active = tmpRT;


			rt.Release();

			File.WriteAllBytes(filePath, tex.EncodeToPNG());

			GameObject.DestroyImmediate(tex);
		}
	}
}
