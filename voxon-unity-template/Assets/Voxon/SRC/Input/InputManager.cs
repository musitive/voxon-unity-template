using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

namespace Voxon
{
    public class InputManager : EditorWindow
    {
      
        private static InputManager _inputManager;

        [MenuItem("Voxon/Input Manager")]
        private static void Init()
        {
            _inputManager = (InputManager)GetWindow(typeof(InputManager));
		
			_inputManager.maxSize = new Vector2(800, 800);
			_inputManager.minSize = new Vector2(800, 800);
			_inputManager.Show();

            InputController.LoadData();


        }

        private void OnGUI()
        {

            Editor editor = Editor.CreateEditor(InputController.Instance);
   
            editor.OnInspectorGUI();
        
        }

        private void OnDisable()
        {
            InputController.SaveData();
      
        }
    }
}
#endif