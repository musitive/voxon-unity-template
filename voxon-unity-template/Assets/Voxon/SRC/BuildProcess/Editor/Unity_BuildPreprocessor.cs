using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Voxon
{
    public class UnityBuildPreprocessor : IPreprocessBuildWithReport {

    
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {


#if CICD_Feature_HaventFinished

    private static readonly string buildFilePath = "Assets/Voxon/BuildNumber/build.txt";

            if (File.Exists(buildFilePath))
            {
    //                Debug.LogWarning("build.txt not found! Creating a new one with build number 1.");
      //              File.WriteAllText(buildFilePath, "0.0.1:1");
      

                // Read current build number from the file
                string buildString = File.ReadAllText(buildFilePath);

            

                string[] parts = currentVersion.Split(':');
                if (parts.Length > 1)
                {
                    // Replace last segment with build number
                    parts[parts.Length - 1] = buildNumber.ToString();
                    currentVersion = string.Join(".", parts);

                    PlayerSettings.bundleVersion = $"{currentVersion}";

                    Debug.Log($"Updated Build Number: {buildNumber} - Version: {PlayerSettings.bundleVersion} / {Application.version}");
                }


                    buildNumber++;

                // Write updated build number back to the file
                File.WriteAllText(buildFilePath, buildNumber.ToString());


                // Update Unity build settings
                try
                {
                    string currentVersion = Application.version;


                    string[] parts = currentVersion.Split(':');

                    if (parts.Length > 1)
                    {
                        // Replace last segment with build number
                        parts[parts.Length - 1] = buildNumber.ToString();
                        currentVersion = string.Join(".", parts);

                        PlayerSettings.bundleVersion = $"{currentVersion}";

                        Debug.Log($"Updated Build Number: {buildNumber} - Version: {PlayerSettings.bundleVersion} / {Application.version}");

                    } else
                    {
                        Debug.Log($"Build Numbers aren't being auto generated. To enable this set your Application version to include a ':' (eg Version 1.0.0:) then build number will appear at the end   Build Number: {buildNumber} - Version: {PlayerSettings.bundleVersion} / {Application.version}");

                    }

                } catch(System.Exception e)
                {
                    Debug.LogWarning($"Couldn't Update Build Number! Reason: {e}");
                }
            }
#endif
            // Kill any Voxon Runtime that is running... 2024 - no need to do this anymore...
            //  Voxon.VXProcess.Runtime.Shutdown();
            //  Voxon.VXProcess.Runtime.Unload();

            //            Voxon.VXProcess.Runtime.

            if (!AssetDatabase.IsValidFolder("Assets/StreamingAssets"))
            {
                Debug.LogError("You should never see this; Editor Handler should have fixed this already");

                System.IO.Directory.CreateDirectory("Assets/StreamingAssets");
                // InputController.SaveData(InputController.filename);
                Debug.LogWarning("Assets/StreamingAssets didn't exist. Created and Input File Saved (used loaded filename)");
            }
            else if(InputController.GetKey("Quit") == 0)
            {
                throw new BuildFailedException("Input controller requires 'Quit' to be bound (and saved)");
            }

            //EditorHandler.PrebuildMesh();
           // EditorHandler.PrebuildTextures();
        }
    }
}