using Codice.Utils;
using System.Linq.Expressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Voxon.Editor
{
[CustomEditor(typeof(InputController))]
    public class InputControllerExt : UnityEditor.Editor
    {
        public static Vector2 ScrollPosition;
        private SerializedObject _serializedInputController;

        /*
        private string helpText =
            "Voxon x Unity inputs are managed by the Voxon.Input class, not Unity.Input.\n\n" +
            "Each input property consists of a unique string and an input code.\n\n" +
            "To use a defined input, reference it by its unique string (e.g., Voxon.Input.GetKey(<InputString>)).\n" +
            "On launch, input data is loaded from the specified filename, located in <PROJECT>\\StreamingAssets.\n\n" +
            "NOTE: To maintain consistent inputs across multiple scenes, use the same filename.";
        */
        private void OnEnable()
        {        
            if (target == null)
            {
                return;
            }
            _serializedInputController = new SerializedObject(target);
   
        }
        public override void OnInspectorGUI()
        {
            GUILayout.MaxHeight(800);
            GUILayout.MinHeight(800);
            GUILayout.MaxWidth(800);
            GUILayout.MinWidth(800);

            ScrollPosition = GUILayout.BeginScrollView(ScrollPosition, GUILayout.Width(800), GUILayout.Height(710));


            GUILayout.Label("VxU Input Manager", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
         
            
            //EditorGUILayout.HelpBox(helpText, MessageType.Info);
            

            SerializedProperty property = _serializedInputController.GetIterator();


            GUILayout.Label("Defined Inputs:", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

            property.NextVisible(true); // Skip the script field, Unity often requires this.
            while (property.NextVisible(false))
            {
                if (property.name == "filename") // filter out filename as we want to draw it last. 
                {
                  continue;
                }

                EditorGUILayout.PropertyField(property, true);
    
            }
            GUILayout.EndScrollView();
     

            GUILayout.Space(20);

            SerializedProperty fileNameProperty = _serializedInputController.FindProperty("filename");
            EditorGUILayout.PropertyField(fileNameProperty, true);
            
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Clear All Inputs"))
            {
                InputController.Instance.keyboard.Clear();
                InputController.Instance.mouse.Clear();
                InputController.Instance.spacenav.Clear();
                InputController.Instance.j1Axis.Clear();
                InputController.Instance.j1Buttons.Clear();
                InputController.Instance.j2Axis.Clear();
                InputController.Instance.j2Buttons.Clear();
                InputController.Instance.j3Axis.Clear();
                InputController.Instance.j3Buttons.Clear();
                InputController.Instance.j4Axis.Clear();
                InputController.Instance.j4Buttons.Clear();
            }

            if (GUILayout.Button("Load File"))
            {
                InputController.LoadData();
            }

            GUILayout.EndHorizontal();
      
            _serializedInputController.ApplyModifiedProperties();
           

        }
    }
}

