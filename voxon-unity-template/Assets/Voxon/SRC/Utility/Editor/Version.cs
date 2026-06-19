using UnityEditor;
using UnityEngine;

namespace Voxon
{
    public class VersionDisplay : EditorWindow
    {
        // Deprecated this feature... we don't need to see the version number in the editor for now...        
        //[MenuItem("Voxon/Version")]
        public static void Init()
        {
            VersionDisplay window = ScriptableObject.CreateInstance<VersionDisplay>();
            window.position = new Rect(Screen.width / 2, Screen.height / 2, 400, 105);
            window.ShowPopup();
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Voxon X Unity Plugin " + VXProcess.Version);
            EditorGUILayout.LabelField("Build Date: " + VXProcess.BuildDate);
            EditorGUILayout.LabelField("For the latest version visit: ");
            EditorGUILayout.LabelField("www.Voxon.co");
            if (GUILayout.Button("Ok")) this.Close();
        }
    }
}