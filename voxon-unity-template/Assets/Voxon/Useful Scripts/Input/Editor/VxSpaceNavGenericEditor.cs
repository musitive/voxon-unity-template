using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Voxon;
[CustomEditor(typeof(VxSpaceNavGeneric))]
[CanEditMultipleObjects]
public class VxSpaceNavGenericEditor : Editor
{
    SerializedProperty behavourStyle;

    SerializedProperty enableMovement;



    SerializedProperty movementSpeed;

    SerializedProperty allowMovementX;
    SerializedProperty allowMovementY;
    SerializedProperty allowMovementZ;

    SerializedProperty invertMovementX;
    SerializedProperty invertMovementY;
    SerializedProperty invertMovementZ;
    SerializedProperty movementMaxDistance;


    SerializedProperty enableRotation;

    SerializedProperty rotationSpeed;

    SerializedProperty rotateRoll;
    SerializedProperty rotatePitch;
    SerializedProperty rotateYaw;
    SerializedProperty rotateByWorld;


    SerializedProperty invertRoll;
    SerializedProperty invertPitch;
    SerializedProperty invertYaw;

    SerializedProperty enableScale;
    SerializedProperty scaleSpeed;
    SerializedProperty maxScale;
    SerializedProperty minScale;
    SerializedProperty useButtonsToScale;


    SerializedProperty buttonResetsPosition;
    SerializedProperty buttonResetsRotation;
    SerializedProperty buttonResetsScale;

    bool showMovementDetails = true;
    bool showRotationDetails = true;
    bool showScaleDetails = true;
    bool showResetsDetails = false;


    void OnEnable()
    {
        behavourStyle = serializedObject.FindProperty("spaceNavBehaviour");


        enableMovement = serializedObject.FindProperty("moveTransform");

        movementSpeed = serializedObject.FindProperty("movementSpeed");
        movementMaxDistance = serializedObject.FindProperty("movementMaxDistance");

        allowMovementX = serializedObject.FindProperty("allowMovementX");
        allowMovementY = serializedObject.FindProperty("allowMovementY");
        allowMovementZ = serializedObject.FindProperty("allowMovementZ");

        invertMovementX = serializedObject.FindProperty("movementInvertX");
        invertMovementY = serializedObject.FindProperty("movementInvertY");
        invertMovementZ = serializedObject.FindProperty("movementInvertZ");


        enableRotation = serializedObject.FindProperty("rotateTransform");

        rotationSpeed = serializedObject.FindProperty("rotationSpeed");
        rotateRoll = serializedObject.FindProperty("rotateRoll");
        rotatePitch = serializedObject.FindProperty("rotatePitch");
        rotateByWorld = serializedObject.FindProperty("rotateByWorld");
        rotateYaw = serializedObject.FindProperty("rotateYaw");
        invertRoll = serializedObject.FindProperty("invertRoll");
        invertPitch = serializedObject.FindProperty("invertPitch");
        invertYaw = serializedObject.FindProperty("invertYaw");
       

        enableScale = serializedObject.FindProperty("scaleTransform");

        scaleSpeed = serializedObject.FindProperty("scaleSpeed");
        maxScale = serializedObject.FindProperty("maxScale");
        minScale = serializedObject.FindProperty("minScale");
        useButtonsToScale = serializedObject.FindProperty("useButtonsToScale");

        buttonResetsPosition = serializedObject.FindProperty("buttonResetsPosition");
        buttonResetsRotation = serializedObject.FindProperty("buttonResetsRotation");
        buttonResetsScale = serializedObject.FindProperty("buttonResetsScale");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw the default inspector
        //DrawDefaultInspector();
        EditorGUILayout.HelpBox("A generic script for adding Space Nav controls to edit gameobject's transform ", MessageType.Info, true);

        EditorGUILayout.PropertyField(behavourStyle);

   
       
        EditorGUILayout.PropertyField(enableMovement);
        EditorGUILayout.PropertyField(enableRotation);
        EditorGUILayout.PropertyField(enableScale);

        if (enableMovement.boolValue || enableRotation.boolValue || enableScale.boolValue)
        {
            EditorGUILayout.Space();
            DrawSeparator();
        }


        if (enableMovement.boolValue)
        {
            showMovementDetails = EditorGUILayout.BeginFoldoutHeaderGroup(showMovementDetails, "Move Transform Properties");
            if (showMovementDetails)
            {

                EditorGUILayout.PropertyField(movementSpeed);
                EditorGUILayout.PropertyField(movementMaxDistance);
                GUILayout.Label("Allow Movement (Voxon Axis)");
                GUILayout.BeginHorizontal();
                GUILayout.Label("X:");
                allowMovementX.boolValue = GUILayout.Toggle(allowMovementX.boolValue, "  Invert");
                invertMovementX.boolValue = GUILayout.Toggle(invertMovementX.boolValue, "");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Y:");

                allowMovementY.boolValue = GUILayout.Toggle(allowMovementY.boolValue, "  Invert");
                invertMovementY.boolValue = GUILayout.Toggle(invertMovementY.boolValue, "");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Z:");
                allowMovementZ.boolValue = GUILayout.Toggle(allowMovementZ.boolValue, "  Invert");
                invertMovementZ.boolValue = GUILayout.Toggle(invertMovementZ.boolValue, "");
                GUILayout.EndHorizontal();

          

            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }


   
        if (enableRotation.boolValue)
        {
            showRotationDetails = EditorGUILayout.BeginFoldoutHeaderGroup(showRotationDetails, "Rotate Transform Properties");
            if (showRotationDetails)
            {

                EditorGUILayout.PropertyField(rotationSpeed);
                rotateByWorld.boolValue = GUILayout.Toggle(rotateByWorld.boolValue, "RotateByWorld");
             
                GUILayout.Label("Allow Rotation (Voxon Axis)");
                GUILayout.BeginHorizontal();
                GUILayout.Label("Roll:  ");
                rotateRoll.boolValue = GUILayout.Toggle(rotateRoll.boolValue, "Invert");
                invertRoll.boolValue = GUILayout.Toggle(invertRoll.boolValue, "");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Pitch: ");

                rotatePitch.boolValue = GUILayout.Toggle(rotatePitch.boolValue, "Invert");
                invertPitch.boolValue = GUILayout.Toggle(invertPitch.boolValue, "");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Yaw:   ");
                rotateYaw.boolValue = GUILayout.Toggle(rotateYaw.boolValue, "Invert");
                invertYaw.boolValue = GUILayout.Toggle(invertYaw.boolValue, "");
                GUILayout.EndHorizontal();

           


            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }



        if (enableScale.boolValue)
        {
            showScaleDetails = EditorGUILayout.BeginFoldoutHeaderGroup(showScaleDetails, "Scale Transform Properties");
            if (showScaleDetails)
            {
                EditorGUILayout.PropertyField(scaleSpeed);
                GUILayout.BeginHorizontal();

                EditorGUILayout.PropertyField(maxScale);
                EditorGUILayout.PropertyField(minScale);

                GUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(useButtonsToScale);

            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }



        if ((behavourStyle.enumValueIndex != (int)VxSpaceNavGeneric.SpaceNavInputBehaviour.actionBoundToNoButton && behavourStyle.enumValueIndex != (int)VxSpaceNavGeneric.SpaceNavInputBehaviour.actionFree))
        {
            showResetsDetails = EditorGUILayout.BeginFoldoutHeaderGroup(showResetsDetails, "Reset Transform Options");
            if (showResetsDetails)
            {           
                EditorGUILayout.PropertyField(buttonResetsPosition);
                EditorGUILayout.PropertyField(buttonResetsRotation);
                EditorGUILayout.PropertyField(buttonResetsScale);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        serializedObject.ApplyModifiedProperties();
    }

    // Method to draw a separator line
    void DrawSeparator()
    {


        // Create a horizontal line
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Create another space in the editor
        EditorGUILayout.Space();
    }
}
