using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.Compilation;
using System.Collections.Generic;

/// <summary>
/// The EditorHandler class is a static class that is used to handle the editor 
/// side of the Voxon Unity Plugin.Responsible for drawing the menu bar options 
/// and handling some of the clean up when the editor changes play state or 
/// scripts are being recompileds closed. 
/// </summary>
namespace Voxon
{

	[InitializeOnLoad]
	public class EditorHandler : MonoBehaviour
	{
	
		
		static PlayModeStateChange currentState = PlayModeStateChange.EnteredEditMode;

		public static void OnBeforeAssemblyReload()
		{
			
			 try
			{
				// perform actions before script recompilation
				if (VXProcess.Instance.active == true && currentState != PlayModeStateChange.ExitingEditMode && VXProcess.Runtime != null)
				{
                    if (PlayerPrefs.GetInt("Voxon_VXProcessReportingLevel") >=(int)VXProcessReportLevel.General) Debug.LogWarning($"Script recompilation detected. Shutting down VX Simulator to avoid system crash");
				
					VXProcess.Instance.CloseVXProcess();
					MeshRegister.Instance.OnDestroy();
					TextureRegister.Instance.OnDestroy();
					VXProcess.Runtime.Unload();

					// ReImportVXUDLLs();
				}
			}
			catch(System.Exception e)
            {
				if (PlayerPrefs.GetInt("Voxon_VXProcessReportingLevel") >= (int)VXProcessReportLevel.General) Debug.LogError($"Error couldn't shutdown the Voxon X Unity Plugin, Reason: {e}");
			}
			
		}

		// Force Reimport of VXU Dllse
		static void ReImportVXUDLLs()
		{
			string[] dllPaths =
			{
			"Assets/Voxon/Plugins/LedWin.dll",
			"Assets/Voxon/Plugins/C#-Runtime.dll",
			"Assets/Voxon/Plugins/C#-bridge-interface.dll"
		};

			foreach (string dllPath in dllPaths)
			{
				// Force reimport of each DLL
				AssetDatabase.ImportAsset(dllPath, ImportAssetOptions.ForceUpdate);
				Debug.Log($"VX DLLs Reimported: {dllPath}");
			}
		}

		
		private static bool wasCompiling = false;
		private static void CheckCompilationStatus()
		{
			if (EditorApplication.isCompiling && !wasCompiling)
			{
				Debug.Log("Compilation started.");
				wasCompiling = true;
			}
			else if (!EditorApplication.isCompiling && wasCompiling)
			{
		
				Debug.Log("Compilation finished. Reimporting VXU DLLs.");
				ReImportVXUDLLs();
				wasCompiling = false;
			}
		}


		static EditorHandler()
		{
			
			// Subscribe to various points in Unity stages
			// beforeAssemblyReload & after Assembly reload event
			
			AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
			//AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
			//EditorApplication.update += CheckCompilationStatus;

			if (PlayerPrefs.GetInt("Voxon_VXProcessReportingLevel")  >= (int)VXProcessReportLevel.Processes) Debug.Log("Editor_Handler.cs - Initializes");

			EditorApplication.playModeStateChanged += PlayStateChange;
			try
			{
#if UNITY_6000_0_OR_NEWER
				if (Object.FindFirstObjectByType<VXProcess>() == null)

#else
				if (FindObjectOfType<VXProcess>() == null)
#endif
				{ 
					// Force creation of VXProcess
					VXProcess a = VXProcess.Instance;
					if (PlayerPrefs.GetInt("Voxon_VXProcessReportingLevel")  >= (int)VXProcessReportLevel.General) Debug.Log("VXProcess is created");
				}

				if (AssetDatabase.IsValidFolder("Assets/StreamingAssets") == false)
				{
					Directory.CreateDirectory("Assets\\StreamingAssets");
					if (PlayerPrefs.GetInt("Voxon_VXProcessReportingLevel")  >= (int)VXProcessReportLevel.General) Debug.LogWarning("No streaming asset folder - making it now");
				}

				if (InputController.GetKey("Quit") == 0)
				{

				

                    InputController.LoadData();
					if (PlayerPrefs.GetInt("Voxon_VXProcessReportingLevel")  >= (int)VXProcessReportLevel.General) Debug.Assert(InputController.GetKey("Quit") != 0, "No 'Quit' keybinding found. Adding to Input Manager");
                    InputController.Instance.keyboard.Add("Quit", Voxon.Keys.Escape);

                }


            }
			catch (System.InvalidOperationException e)
			{
				if (PlayerPrefs.GetInt("Voxon_VXProcessReportingLevel")  >= (int)VXProcessReportLevel.General) Debug.Log(e.Message);
			}

			DefaultPlayerSettings();

			// Check for VoxieTag
			CheckForVoxieHideTag();
		}
		/*
		[MenuItem("Layout Hack/Load Layout")]
		static void LoadLayoutHack()
		{
			// Loading layout from an asset
			        LayoutUtility.LoadLayoutFromAsset("Assets/Voxon/Layout/VxLayout.wlt");
			//string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets/Voxon/Layout/VxLayout.wlt");
			//EditorUtility.LoadWindowLayout(path);
		}


		[MenuItem("Layout Hack/Save Layout")]
		static void SaveLayoutHack()
		{
			// Loading layout from an asset
			LayoutUtility.SaveLayoutToAsset("Assets/Voxon/Layout/VxLayout.wlt");
			//string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets/Voxon/Layout/VxLayout.wlt");
			//EditorUtility.LoadWindowLayout(path);
		}
		*/

		[MenuItem("Voxon/VoxieBox/Reimport Textures")]
		static void ReimportMaterials()
		{
			string[] guids = AssetDatabase.FindAssets("t:Texture2d", null);
			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				var texImporter = AssetImporter.GetAtPath(path) as TextureImporter;
				if (texImporter != null) texImporter.isReadable = true;
				AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

				Texture2D tex = AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D)) as Texture2D;
			}
			EditorUtility.DisplayDialog("Reimported Textures", "Textures Reimported.", "Ok");
		}


		[MenuItem("Voxon/VoxieBox/Reimport Textures", true)]
		static bool ValidateReimportMaterials()
		{
			// Disable the menu item if the specific API interface is not enabled
			return VXProcess.Instance.VXInterface != VOXON_RUNTIME_INTERFACE.VXLED;
		}

		[MenuItem("Voxon/VoxieBox/Reimport Meshes")]
		static void ReimportMeshes()
		{
			string[] guids = AssetDatabase.FindAssets("t:Mesh", null);
			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				var meshImporter = AssetImporter.GetAtPath(path) as ModelImporter;
				if (meshImporter != null)
				{
					meshImporter.isReadable = true;
				}

				AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			}
			EditorUtility.DisplayDialog("Reimported Meshes", "Meshes Reimported.", "Ok");
		}

		[MenuItem("Voxon/VoxieBox/Prebuild Mesh")]
		public static void PrebuildMesh()
		{
			// Prebuild 
			string scene_path = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
			string scene_directory = Path.GetDirectoryName(scene_path).Replace("Assets/", "");
			string scene_filename = Path.GetFileNameWithoutExtension(scene_path);

			Debug.Log("Prebuilding Meshes for Scene");
			// string[] guids = AssetDatabase.FindAssets("t:Mesh", null);

			MeshRegister meshRegister = MeshRegister.Instance;
			meshRegister.Clear();

            /* All Meshes in Scene */
#if UNITY_6000_0_OR_NEWER
            MeshFilter[] meshes = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
#else
			MeshFilter[] meshes = FindObjectsOfType<MeshFilter>();

#endif

            Debug.Log($"{meshes.Length} mesh filters in scene");
			VXComponent vXBuffer;

			for (uint idx = 0; idx < meshes.Length; idx++)
			{
				Mesh sharedmesh = meshes[idx].sharedMesh;

				string path = UnityEditor.AssetDatabase.GetAssetPath(sharedmesh);

				// We don't rename default resources
				if (!path.StartsWith("Library"))
				{
					meshes[idx].name = path;

					vXBuffer = meshes[idx].gameObject.GetComponent<VXComponent>();
					if (vXBuffer)
					{
						vXBuffer.MeshPath = path;
					}
				}

				meshRegister.get_registed_mesh(ref sharedmesh);
			}

#if UNITY_6000_0_OR_NEWER
            SkinnedMeshRenderer[] skinned_meshes = FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
#else
			SkinnedMeshRenderer[] skinned_meshes = FindObjectsOfType<SkinnedMeshRenderer>();
#endif
            Debug.Log($"{skinned_meshes.Length} skinned meshes in scene");

			for (uint idx = 0; idx < skinned_meshes.Length; idx++)
			{
				Mesh mesh = skinned_meshes[idx].sharedMesh;

				string path = UnityEditor.AssetDatabase.GetAssetPath(skinned_meshes[idx].sharedMesh);

				path += $":{mesh.name}";
				Debug.Log($"Path: { path }");

				// We don't rename default resources
				if (!path.StartsWith("Library"))
				{
					// skinned_meshes[idx].name = path;

					vXBuffer = skinned_meshes[idx].gameObject.GetComponent<VXComponent>();
					if (vXBuffer)
					{
						vXBuffer.MeshPath = path;
					}
				}

				meshRegister.get_registed_mesh(ref mesh);
			}

			// Create an instance of the type and serialize it.
			IFormatter formatter = new BinaryFormatter();

			if (!AssetDatabase.IsValidFolder($"{Application.dataPath}/StreamingAssets/{scene_directory}"))
			{
				Directory.CreateDirectory($"{Application.dataPath}/StreamingAssets/{scene_directory}");
			}

			string SerializedRegisterPath = $"{Application.dataPath}/StreamingAssets/{scene_directory}/{scene_filename}-Meshes.bin";
			// Debug.Log(SerializedRegisterPath);

			using (var s = new FileStream(SerializedRegisterPath, FileMode.Create))
			{
				try
				{
					// THIS APPROACH WONT WORK (We don't have points for de-serialisation).
					MeshData[] allData = meshRegister.PackMeshes();
					int mdCount = allData.Length;

					s.Write(System.BitConverter.GetBytes(mdCount), 0, sizeof(int));

					foreach (MeshData md in allData)
					{
						byte[] bytes = md.toByteArray();
						s.Write(bytes, 0, bytes.Length);
					}

				}
				catch (SerializationException e)
				{
					Debug.Log("Failed to serialize. Reason: " + e.Message);
					throw;
				}

			}

			UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
		}

		[MenuItem("Voxon/VoxieBox/Prebuild Textures")]
		public static void PrebuildTextures()
		{
			// Prebuild 
			string scene_path = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
			string scene_directory = Path.GetDirectoryName(scene_path).Replace("Assets/", "");
			string scene_filename = Path.GetFileNameWithoutExtension(scene_path);

			Debug.Log("Prebuilding Meshes for Scene");
			string[] guids = AssetDatabase.FindAssets("t:Texture", null);

			TextureRegister textureRegister = TextureRegister.Instance;
			textureRegister.ClearRegister();
            /* All Meshes in Scene */
#if UNITY_6000_0_OR_NEWER
            MeshRenderer[] meshes = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
#else
            MeshRenderer[] meshes = FindObjectsOfType<MeshRenderer>();
#endif
			Debug.Log($"{meshes.Length} mesh renderers in scene");

			for (uint idx = 0; idx < meshes.Length; idx++)
			{
				Material[] materials = meshes[idx].sharedMaterials;
				Material mat = meshes[idx].sharedMaterial;

				for (uint m_idx = 0; m_idx < materials.Length; m_idx++)
				{
					Texture2D tex = (Texture2D)materials[m_idx].mainTexture;

					if (tex == null) continue;


					string path = UnityEditor.AssetDatabase.GetAssetPath(tex);
					Debug.Log($"{tex.name}, {path}");

					// We don't rename default resources
					if (!path.StartsWith("Library"))
					{
						Debug.Log(tex.name);
						tex.name = path;
					}

					textureRegister.get_tile(ref tex);
				}
			}
#if UNITY_6000_0_OR_NEWER
            SkinnedMeshRenderer[] skinned_meshes = FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
#else
			SkinnedMeshRenderer[] skinned_meshes = FindObjectsOfType<SkinnedMeshRenderer>();
#endif
			Debug.Log($"{skinned_meshes.Length} skinned meshes in scene");

			for (uint idx = 0; idx < skinned_meshes.Length; idx++)
			{
				Material[] materials = skinned_meshes[idx].sharedMaterials;

				for (uint m_idx = 0; m_idx < materials.Length; m_idx++)
				{
					Texture2D tex = (Texture2D)materials[m_idx].mainTexture;

					if (tex == null) continue;

					// Debug.Log(tex.name);

					string path = UnityEditor.AssetDatabase.GetAssetPath(tex);

					// We don't rename default resources
					if (!path.StartsWith("Library"))
					{
						tex.name = path;
					}

					textureRegister.get_tile(ref tex);
				}
			}

			/* All Meshes in project 
			foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == "") continue;

                var t = (Mesh)AssetDatabase.LoadAssetAtPath(path, typeof(Mesh));

				// We don't rename default resources
				if (!path.StartsWith("Library"))
				{
					t.name = path;
				}

                meshRegister.get_registed_mesh(ref t);
            }
			*/

			// Create an instance of the type and serialize it.
			IFormatter formatter = new BinaryFormatter();


			if (!AssetDatabase.IsValidFolder($"{Application.dataPath}/StreamingAssets/{scene_directory}"))
			{
				Directory.CreateDirectory($"{Application.dataPath}/StreamingAssets/{scene_directory}");
			}

			string SerializedRegisterPath = $"{Application.dataPath}/StreamingAssets/{scene_directory}/{scene_filename}-Textures.bin";
			// Debug.Log(SerializedRegisterPath);

			using (var s = new FileStream(SerializedRegisterPath, FileMode.Create))
			{
				try
				{
					TextureData[] allData = textureRegister.PackMeshes();
					int mdCount = allData.Length;

					s.Write(System.BitConverter.GetBytes(mdCount), 0, sizeof(int));

					foreach (TextureData md in allData)
					{
						byte[] bytes = md.toByteArray();
						s.Write(bytes, 0, bytes.Length);
					}

				}
				catch (SerializationException e)
				{
					Debug.Log("Failed to serialize. Reason: " + e.Message);
					throw;
				}

			}


		}


		private static void PlayStateChange(PlayModeStateChange state)
		{
			currentState = state;
			if (PlayerPrefs.GetInt("Voxon_VXProcessReportingLevel")  >= (int)VXProcessReportLevel.Processes) Debug.Log($"Editor_Handler.cs - PlayStateChange() called State was {currentState}");
		
			// If the playmode has been changed by clicking on the Play/Stop button we need to ensure runtime is being closed this is how we capture that instance
			if (state == PlayModeStateChange.ExitingPlayMode/* && VXProcess.Instance.IsClosingVXProcess() == false*/)
			{
               
				if (PlayerPrefs.GetInt("Voxon_VXProcessReportingLevel") >= (int)VXProcessReportLevel.Processes) Debug.Log($"Editor_Handler.cs - ExitingPlayMode... Calling VXProcess.QuitPressed");

                try
                {
                    if (VXProcess.Instance != null)
                    {
                        VXProcess.Instance.QuitPressed();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Editor_Handler.cs - Error calling QuitPressed: {e}");


                }
              
				
			}
		}

		private static void CheckForVoxieHideTag()
        {
			//Debug.Log("Checking for VoxieHideTag");
			string tag = "VoxieHide";

			if (System.Array.Exists(UnityEditorInternal.InternalEditorUtility.tags, t => t == tag))
			{
				//Debug.Log("VoxieHideTag Already Present");
				return; // Exit if the tag is already present
			}


			SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
			SerializedProperty tagsProp = tagManager.FindProperty("tags");

			if (tagsProp == null)
			{
				//Debug.LogError("Could not find TagManager.asset!");
				return;
			}

			// Ensure the tag is not already in the list
			for (int i = 0; i < tagsProp.arraySize; i++)
			{
				if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
				{
					return;
				}
			}

			// Add new tag
			tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
			tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;

			tagManager.ApplyModifiedProperties();
			AssetDatabase.SaveAssets();
			//Debug.Log("VoxieHideTag Added");

		}


		private static void DefaultPlayerSettings()
		{
			PlayerSettings.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
#if UNITY_6000_0_OR_NEWER

#else
			PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_4_6);
#endif
			PlayerSettings.allowFullscreenSwitch = false;
			PlayerSettings.defaultScreenHeight = 480;
			PlayerSettings.defaultScreenWidth = 640;
			PlayerSettings.forceSingleInstance = true;
			PlayerSettings.resizableWindow = false;
			PlayerSettings.runInBackground = true;
			PlayerSettings.usePlayerLog = true;
			PlayerSettings.visibleInBackground = true;
		}
	}
}
