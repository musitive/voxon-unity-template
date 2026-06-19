#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Voxon
{
    public class ProcessManager : EditorWindow
    {
        private static ProcessManager _processManager;
        private SerializedObject _serializedVXProcess;
        [MenuItem("Voxon/Process")]
        private static void Init()
        {
            _processManager = (ProcessManager)GetWindow(typeof(ProcessManager));
            // Unnecessary but it prevents Unity's warnings
            _processManager.titleContent = new UnityEngine.GUIContent("Voxon X Unity Plugin " + VXProcess.Version);

            // Limit size of the window
            _processManager.minSize = new Vector2(450, 550);
           // _processManager.maxSize = new Vector2(1920, 470);

            _processManager.Show();
        }

        private void OnGUI()
        {
            Editor editor = Editor.CreateEditor(VXProcess.Instance);
            editor.OnInspectorGUI();


            // Change the GUI based on the selected interface type
            VOXON_RUNTIME_INTERFACE currentInterface = VXProcess.Instance.VXInterface;

            List<string> filter = null;
            GUILayout.Space(10);

            switch (currentInterface)
            {
                case VOXON_RUNTIME_INTERFACE.LEGACY:
                case VOXON_RUNTIME_INTERFACE.EXTENDED:
                    GUILayout.Label("VoxieBox Interface Settings", EditorStyles.boldLabel);
                    GUILayout.Space(10);

                    filter = new List<string> { "addVXComponentsOnStart", "bypassMeshRegister", "CaptureExportPath", "recordOnLoad", "recordingStyle" };

                    break;

                case VOXON_RUNTIME_INTERFACE.VXLED:
                    GUILayout.Label("VLED Interface Settings", EditorStyles.boldLabel);
                    GUILayout.Space(10);
                    filter = new List<string> { "VXUB_Flags", "keepVXU_Handle", "useMouseAsSpaceNav", "PreserveDisplaySettings", "defaultWindowRes" };

                    break;

                default:
//                    GUILayout.Label("Default GUI", EditorStyles.boldLabel);
                    break;
            }



            // Display all public variables in VXProcess that aren't tagged as HideInInspector
            DisplayPublicVariables(VXProcess.Instance, filter);
        }

        private void DisplayPublicVariables(VXProcess vxProcessInstance, List<string> filter)
        {
            FieldInfo[] fields = typeof(VXProcess).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                if (field.GetCustomAttribute<HideInInspector>() != null)
                {
                    // filter for VXLED
                    bool skip = true;
                    if (filter != null)
                    {
                        foreach (string e in filter)
                        {
                            if (field.Name == e)
                            {
                      //          Debug.Log("found " + field.Name);
                                skip = false;
                                continue;
                            }
                        
                        }

                        if (skip)
                            continue;
                    }
                    object value = field.GetValue(vxProcessInstance);
                    System.Type fieldType = field.FieldType;

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(CamelCaseToSpaces(field.Name), GUILayout.Width(200));


                    if (fieldType == typeof(int))
                    {
                        int intValue = (int)value;
                        intValue = EditorGUILayout.IntField(intValue);
                        field.SetValue(vxProcessInstance, intValue);
                    }
                    else if (fieldType == typeof(float))
                    {
                        float floatValue = (float)value;
                        floatValue = EditorGUILayout.FloatField(floatValue);
                        field.SetValue(vxProcessInstance, floatValue);
                    }
                    else if (fieldType == typeof(bool))
                    {
                        bool boolValue = (bool)value;
                        boolValue = EditorGUILayout.Toggle(boolValue);
                        field.SetValue(vxProcessInstance, boolValue);
                    }
                    else if (fieldType == typeof(string))
                    {
                        string stringValue = (string)value;
                        stringValue = EditorGUILayout.TextField(stringValue);
                        field.SetValue(vxProcessInstance, stringValue);
                    }
                    else if (fieldType.IsEnum)
                    {
                        System.Enum enumValue = (System.Enum)value;
                        enumValue = EditorGUILayout.EnumPopup(enumValue);
                        field.SetValue(vxProcessInstance, enumValue);
                    }
                    else if (field.FieldType == typeof(point2DInt))
                    {
                        point2DInt pointValue = (point2DInt)value;

                        GUILayout.BeginHorizontal(); // Layout the fields horizontally
                        GUILayout.Label("X:", GUILayout.Width(20));
                        pointValue.x = EditorGUILayout.IntField(pointValue.x, GUILayout.Width(50));
                        GUILayout.Label("Y:", GUILayout.Width(20));
                        pointValue.y = EditorGUILayout.IntField(pointValue.y, GUILayout.Width(50));
                        GUILayout.EndHorizontal();

                        field.SetValue(vxProcessInstance, pointValue);

                    }
                    else
                    {
                        GUILayout.Label("Unsupported type");
                    }

                    GUILayout.EndHorizontal();
                }
            }
        }

        public static string CamelCaseToSpaces(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            System.Text.StringBuilder result = new System.Text.StringBuilder();
            result.Append(char.ToUpper(input[0]));

            for (int i = 1; i < input.Length; i++)
            {
                if (char.IsUpper(input[i]) && !char.IsUpper(input[i - 1]))
                {
                    result.Append(' ');
                }
               result.Append(input[i]);
            }

            return result.ToString();
        }


    }
}
#endif