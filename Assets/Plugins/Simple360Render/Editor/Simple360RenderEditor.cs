#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using System.IO;

public class Simple360RenderEditor : EditorWindow {
	private int imageWidth = 7680;
	private bool saveAsJPEG = true;
	private string outputFolder = "";

	[MenuItem("Tools/Render 360 Image")]
	public static void ShowWindow() {
		GetWindow<Simple360RenderEditor>("Render 360 Image");
	}

	private void OnEnable() {
		if (string.IsNullOrEmpty(outputFolder)) {
			outputFolder = Application.persistentDataPath;
		}
	}

	private void OnGUI() {
		GUILayout.Label("Render Settings", EditorStyles.boldLabel);
		EditorGUILayout.HelpBox("Reminder: Render will be from any camera in the scene that is tagged MainCamera!", MessageType.Info);
		EditorGUILayout.HelpBox("Reminder: Oculus requirement is 7680x3840 and less than 2.3MB", MessageType.Info);
		imageWidth = EditorGUILayout.IntField("Image Width", imageWidth);
		saveAsJPEG = EditorGUILayout.Toggle("Save as JPEG", saveAsJPEG);

		EditorGUILayout.BeginHorizontal();
		outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
		if (GUILayout.Button("Browse", GUILayout.Width(70))) {
			string selected = EditorUtility.OpenFolderPanel("Select Output Folder", outputFolder, "");
			if (!string.IsNullOrEmpty(selected)) {
				outputFolder = selected;
			}
		}
		EditorGUILayout.EndHorizontal();

		if (GUILayout.Button("Render and Save")) {
			RenderAndSave();
		}
	}

	private void RenderAndSave() {
		if (string.IsNullOrEmpty(outputFolder)) {
			Debug.LogError("Output folder is empty.");
			return;
		}

		if (!Directory.Exists(outputFolder)) {
			Debug.LogError("Output folder does not exist: " + outputFolder);
			return;
		}

		byte[] bytes = I360Render.Capture(imageWidth, saveAsJPEG);
		if (bytes != null) {
			string path = Path.Combine(outputFolder, "360render" + (saveAsJPEG ? ".jpeg" : ".png"));
			File.WriteAllBytes(path, bytes);
			Debug.Log("360 render saved to " + path);
		}
	}
}
#endif
