using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Voxon;

[CustomEditor(typeof(VXCaptureVolume))]
[CanEditMultipleObjects]
public class VXCaptureVolumeEditor : Editor
{
	SerializedProperty uniformScale;
	SerializedProperty baseScale;
	SerializedProperty vectorScale;

	SerializedProperty camOutlineCol;
	SerializedProperty showVolumeSurface;
	SerializedProperty camShape;

	SerializedProperty loadViewFinder;
	SerializedProperty ViewFinderDimensions;

	SerializedProperty reportBool;
	SerializedProperty reportXPos;
	SerializedProperty reportYPos;

	//private bool showViewFinder = false;
	//bool showReportInfo = true;


	void OnEnable()
	{
		uniformScale = serializedObject.FindProperty("uniformScale");
		baseScale = serializedObject.FindProperty("baseScale");
		vectorScale = serializedObject.FindProperty("vectorScale");
		camOutlineCol = serializedObject.FindProperty("CameraOutLineColor");
		showVolumeSurface = serializedObject.FindProperty("showVolumeSurface");
		camShape = serializedObject.FindProperty("cameraShape");
		loadViewFinder = serializedObject.FindProperty("loadViewFinder");
		ViewFinderDimensions = serializedObject.FindProperty("ViewFinderDimensions");

		reportBool = serializedObject.FindProperty("reportCamera");
		reportXPos = serializedObject.FindProperty("reportXPos");
		reportYPos = serializedObject.FindProperty("reportYPos");
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		EditorGUILayout.HelpBox("Voxon X Unity Scale :: 1 Unity Unit = 0.1 Voxon Unit", MessageType.None, true);


		// Draw the default inspector
		//DrawDefaultInspector();

		if (VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
		{
			// show all values
			EditorGUILayout.PropertyField(camShape);

		}
		else
		{
			// Create an array of allowed enum values when not using VXLED
			VXCaptureVolume.VXCaptureShape[] allowedCaptureShapes = new VXCaptureVolume.VXCaptureShape[] {
			VXCaptureVolume.VXCaptureShape.VXCAP_LOCK_TO_HARDWARE,
			VXCaptureVolume.VXCaptureShape.VXCAP_VX1_CLASSIC,
			VXCaptureVolume.VXCaptureShape.VXCAP_CUBE,
			VXCaptureVolume.VXCaptureShape.VXCAP_CYLINDER,
			VXCaptureVolume.VXCaptureShape.VXCAP_HALF_CYLINDER
			};

			// Get the current value of camShape
			VXCaptureVolume.VXCaptureShape currentShape = (VXCaptureVolume.VXCaptureShape)camShape.intValue;

			// Get the enum names
			string[] enumNames = System.Enum.GetNames(typeof(VXCaptureVolume.VXCaptureShape));

			// Create an array of names that match the allowed shapes
			List<string> allowedEnumNames = new List<string>();
			foreach (var shape in allowedCaptureShapes)
			{
				allowedEnumNames.Add(shape.ToString());
			}

			// Find the index of the current value in the allowed options
			int currentIndex = allowedEnumNames.IndexOf(currentShape.ToString());

			// Create a custom dropdown for the filtered enum values
			int newIndex = EditorGUILayout.Popup("Capture Shape", currentIndex, allowedEnumNames.ToArray());

			// Update the serialized property if the selection has changed
			if (newIndex != currentIndex)
			{
				camShape.intValue = (int)allowedCaptureShapes[newIndex];
			}
		}

		EditorGUILayout.Space();

		EditorGUILayout.PropertyField(showVolumeSurface);
		EditorGUILayout.PropertyField(camOutlineCol);

		EditorGUILayout.Space();



		// Uniform Scale is to be deprecated 
		/*
		EditorGUILayout.PropertyField(uniformScale);
		if (uniformScale.boolValue)
		{
			EditorGUILayout.PropertyField(baseScale);
		}
		else
		{
		*/
		EditorGUILayout.PropertyField(vectorScale);
		//}


		if (camShape.intValue != 0) {


		}

		/*
		
		showViewFinder = EditorGUILayout.BeginFoldoutHeaderGroup(showViewFinder, "Edit ViewFinder");

		if (showViewFinder)
		{
			EditorGUILayout.PropertyField(loadViewFinder);
			if (!loadViewFinder.boolValue)
			{
				EditorGUILayout.PropertyField(ViewFinderDimensions);
			}
		}
		*/
		
		EditorGUILayout.EndFoldoutHeaderGroup();

		EditorGUILayout.Space();

		/*
		EditorGUILayout.PropertyField(reportBool);

		if (reportBool.boolValue)
		{
			EditorGUILayout.PropertyField(reportXPos);
			EditorGUILayout.PropertyField(reportYPos);
		}
		*/
		
		EditorGUILayout.EndFoldoutHeaderGroup();

		serializedObject.ApplyModifiedProperties();
	}
}