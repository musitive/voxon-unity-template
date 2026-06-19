
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

#if UNITY_2017_1_OR_NEWER
using UnityEngine;
#endif


// Version 0.4.7
namespace Voxon
{


    class RegistryReader
    {
        private const int HKEY_CURRENT_USER = unchecked((int)0x80000001);
        private const int KEY_QUERY_VALUE = 0x0001;
        private const int ERROR_SUCCESS = 0;

        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        private static extern int RegOpenKeyEx(int hKey, string subKey, int ulOptions, int samDesired, out IntPtr phkResult);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        private static extern int RegQueryValueEx(IntPtr hKey, string lpValueName, int lpReserved, out uint lpType, StringBuilder lpData, ref uint lpcbData);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        private static extern int RegCloseKey(IntPtr hKey);

        public static string GetRegistryValue(string keyPath, string valueName)
        {
            IntPtr hKey;
            if (RegOpenKeyEx(HKEY_CURRENT_USER, keyPath, 0, KEY_QUERY_VALUE, out hKey) != ERROR_SUCCESS)
            {
                return null; // Registry key not found
            }

            uint type;
            uint dataSize = 1024;
            StringBuilder data = new StringBuilder((int)dataSize);

            int result = RegQueryValueEx(hKey, valueName, 0, out type, data, ref dataSize);
            RegCloseKey(hKey);

            return result == ERROR_SUCCESS ? data.ToString() : null;
        }
    }

    public class VXURuntime : IRuntimePromise, IRuntimePromiseExtended
    {
        private string _pluginFilePath = "";
        private const string PluginFileName = "C#-Runtime.dll";

        private string DLLsFoundString = string.Empty;
        private int currentFlags = 0;

        private string PluginTypeName = "Voxon.Runtime";
        private string FeatureDictionaryName = "GetFeatures";
        private VOXON_RUNTIME_INTERFACE TypeOfRuntime = VOXON_RUNTIME_INTERFACE.LEGACY;

        public string ActiveRuntime;
        private bool logDLLsearch = false;
        private static Type _tClassType;
        private static object _runtime;

        private string NotSupportedMsg = "This function is not supported using this Voxon x Unity Interface";

        private bool NotSupportedMsgShown = false;

        public bool RuntimeFound = false;

        private static Dictionary<string, MethodInfo> _features;

        public VXURuntime(VOXON_RUNTIME_INTERFACE _TypeOfRuntime, int flags = 0, bool _logDLLsearch = false)
        {
            logDLLsearch = _logDLLsearch;
            TypeOfRuntime = _TypeOfRuntime;

            switch (TypeOfRuntime)
            {
                default:
                case VOXON_RUNTIME_INTERFACE.LEGACY:
                    PluginTypeName = "Voxon.Runtime";
                    FeatureDictionaryName = "GetFeatures";
                    break;
                case VOXON_RUNTIME_INTERFACE.EXTENDED:
                    PluginTypeName = "Voxon.RuntimeExtended";
                    FeatureDictionaryName = "GetFeatures";
                    break;
                case VOXON_RUNTIME_INTERFACE.VXLED:
                    PluginTypeName = "Voxon.RuntimeVXL";
                    FeatureDictionaryName = "GetFeatures";
                    break;

            }




            _features = new Dictionary<string, MethodInfo>();
            _pluginFilePath = GetFilePath(PluginFileName);

            if (!System.IO.File.Exists(_pluginFilePath))
            {
#if UNITY_EDITOR
                LogWarning("C#-Runtime.dll not found in Runtime directory.\nPlease ensure Voxon Runtime is correctly installed");

#elif UNITY_2017_1_OR_NEWER
                LogWarning("C#-Runtime.dll not found in Runtime directory.\nPlease ensure Voxon Runtime is correctly installed");
                Windows.Error("C#-Runtime.dll not found in Runtime directory.\nPlease ensure Voxon Runtime is correctly installed");
                Application.Quit();
#else
                LogWarning("C#-Runtime.dll not found in Runtime directory.\nPlease ensure Voxon Runtime is correctly installed");

#endif

                _runtime = null;
                return;
            }



            try
            {
                Assembly asm = Assembly.LoadFrom(_pluginFilePath);
                _tClassType = asm.GetType(PluginTypeName);
                ActiveRuntime = PluginTypeName;


                if (_tClassType == null)
                {
#if UNITY_2017_1_OR_NEWER
                    LogWarning($"Voxon Runtime failed to load from _pluginFilePath");
                    _runtime = null;
                    Application.Quit();
#endif
                    throw new TypeLoadException($"Voxon Runtime failed to load from {_pluginFilePath}");

                }

                _runtime = Activator.CreateInstance(_tClassType);

                MethodInfo makeRequestMethod = _tClassType.GetMethod(FeatureDictionaryName);
                if (makeRequestMethod == null) return;

                var featureNames = (HashSet<string>)makeRequestMethod.Invoke(_runtime, null);

                foreach (string feature in featureNames)
                {
                    _features.Add(feature, _tClassType.GetMethod(feature));
                }
            }
            catch (Exception e)
            {
#if UNITY_2017_1_OR_NEWER
                Windows.Error(
                    $"Voxon Runtime Bridge failed to load.\n\n" +
                    $"Check your version of C#-bridge-interface.dll / C#-bridge.dll.\n" +
                    $"The issue may be due to incompatible C#-bridge DLLs in the VLED Runtime.\n\n" +
                    $"If this occurs while running a Unity application:\n" +
                    $"Try copying <This Program's Folder>\\BuildDlls\\C#-bridge.dll to the executable's directory.\n\n" +
                    $"If this occurs while running the Unity Editor:\n" +
                    $"Look under the Project's Assets \\ Voxon \\ Plugins \\ C#-bridge.dll and include the C#-bridge.dll for the Editor platform.\n\n" +
                    $"Exception:\n{e}"
                );
                Application.Quit();
#else
                LogWarning(
                    $"Voxon Runtime Bridge failed to load.\n\n" +
                    $"Check your version of C#-bridge-interface.dll / C#-bridge.dll.\n" +
                    $"The issue may be due to incompatible C#-bridge DLLs in the VLED Runtime.\n\n" +
                    $"If this occurs while running a Unity application:\n" +
                    $"Try copying <This Program's Folder>\\BuildDlls\\C#-bridge.dll to the executable's directory.\n\n" +
                    $"If this occurs while running the Unity Editor:\n" +
                    $"Look under the Project's Assets \\ Voxon \\ Plugins \\ C#-bridge.dll and include the C#-bridge.dll for the Editor platform.\n\n" +
                    $"Exception:\n{e}"
                );

#endif
            }

            SetVXUBridgeFlags(flags);
        }


        internal string GetFilePath(string fileName)
        {
            string filePath = "";


            // First look for local dll (in case of override)
            if (File.Exists(fileName))
            {
                filePath = $"{fileName}";
                DLLsFoundString += $"{fileName}=LOCAL;";

                if (logDLLsearch) LogDebug($"{fileName} found in local directory, {filePath}");
                return filePath;
            }


            // Second check the path variables
            if (filePath == "" && (currentFlags & (1 << (int)VXU_FLAG_IDS.VXUB_FLAG_BYPASS_PATH_ENV_DLL_CHECK)) == 0)
            {
                // Check User Path Environment 
                string[] userPaths = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User)?.Split(';');
                foreach (var path in userPaths)
                {
                    if (File.Exists(path + $"\\{fileName}"))
                    {
                        filePath = path + $"\\{fileName}";
                        DLLsFoundString += $"{fileName}=USR_PATH_ENV;";
                        if (logDLLsearch) LogDebug($"{fileName} found usr in path env var, {filePath}");
                        break;
                    }
                }

                if (filePath != "") return filePath;

                // Check System Path Enviroment
                string[] systemPaths = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine)?.Split(';');

                foreach (var path in systemPaths)
                {
                    if (File.Exists(path + $"\\{fileName}"))
                    {
                        filePath = path + $"\\{fileName}";
                        DLLsFoundString += $"{fileName}=SYS_PATH_ENV;";
                        if (logDLLsearch) LogDebug($"{fileName} found in sys path env var, {filePath}");
                        break;
                    }
                }
                if (filePath != "") return filePath;
            }

            // Finally check the registry... Unity 6 doesn't contain RegistryKey so we need to use a different method...
            if (filePath == "" && (currentFlags & (1 << (int)VXU_FLAG_IDS.VXUB_FLAG_BYPASS_REGISTRY_DLL_CHECK)) == 0)
            {
#if UNITY_2017_1_OR_NEWER
                string keyPath = @"SOFTWARE\Voxon\VLED";
                string valueName = "VLED_RUNTIME_PATH";

                object registryPath = RegistryReader.GetRegistryValue(keyPath, valueName);
           
                if (registryPath != null)
                {
                   
                        // Combine the registry path with the fileName
                        var potentialFilePath = Path.Combine(registryPath.ToString(), fileName);

                        // Check if the file exists at the combined path
                        if (File.Exists(potentialFilePath))
                        {
                            filePath = potentialFilePath;
                            DLLsFoundString += $"{fileName}=REG_USR_RT;";
                            if (logDLLsearch) LogDebug($"{fileName} found in user \\ software \\ vled \\ runtime registry, {filePath}");
                        }
                    


                }


#else
                RegistryKey registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Voxon\VLED");

                // Check if the registry key exists and retrieve its value
                if (registryKey != null)
                {
                    var registryPath = registryKey.GetValue("VLED_RUNTIME_PATH") as string;

                    if (registryPath != null)
                    {
                        // Combine the registry path with the fileName
                        var potentialFilePath = Path.Combine(registryPath, fileName);

                        // Check if the file exists at the combined path
                        if (File.Exists(potentialFilePath))
                        {
                            filePath = potentialFilePath;
                            DLLsFoundString += $"{fileName}=REG_USR_RT;";
                            if (logDLLsearch) LogDebug($"{fileName} found in user \\ software \\ vled \\ runtime registry, {filePath}");
                        }
                    }
                }




#endif
            }
                return filePath;

        }

        private void LogDebug(string message)
        {
#if UNITY_2017_1_OR_NEWER
            UnityEngine.Debug.Log(message);
#else
            Console.WriteLine(message);
#endif
        }
        private void LogWarning(string message)
        {
#if UNITY_2017_1_OR_NEWER
            UnityEngine.Debug.LogWarning(message);
#else
            Console.WriteLine(message);
#endif
        }

        #region Core / Legacy Functions

        public double GetTime()
        {

            return (double)_features["GetTime"].Invoke(_runtime, null);
        }

        // TODO is this right?

        public HashSet<string> GetFeatures()
        {
            return (HashSet<string>)_features["DrawPolygon"].Invoke(_runtime, null);
        }

        public void DrawBox(float minX, float minY, float minZ, float maxX, float maxY, float maxZ, int fill, int colour)
        {
            point3d min = new point3d(minX, minY, minZ);
            point3d max = new point3d(maxX, maxY, maxZ);

            DrawBox(ref min, ref max, fill, colour);

        }

        public void DrawBox(ref point3d min, ref point3d max, int fill, int colour)
        {
            _features["DrawBox"].Invoke(_runtime, parameters: new object[] { min, max, fill, colour });
        }

        public void DrawCube(ref point3d pp, ref point3d pr, ref point3d pd, ref point3d pf, int fillMode, int col)
        {
            var paras = new object[] { pp, pr, pd, pf, fillMode, col };
            _features["DrawCube"].Invoke(_runtime, paras);
        }

        public void DrawGuidelines()
        {
            _features["DrawGuidelines"].Invoke(_runtime, null);
        }

        public void DrawHeightmap(ref tiletype texture, ref point3d pp, ref point3d pr, ref point3d pd, ref point3d pf, int colorkey, int minHeight, int flags)
        {
            var paras = new object[] { texture, pp, pr, pd, pf, colorkey, minHeight, flags };
            _features["DrawHeightmap"].Invoke(_runtime, paras);
        }

        public void DrawLetters(ref point3d pp, ref point3d pr, ref point3d pd, int col, byte[] text)
        {
            var paras = new object[] { pp, pr, pd, col, text };
            _features["DrawLetters"].Invoke(_runtime, paras);
        }

        public void DrawLine(float x0, float y0, float z0, float x1, float y1, float z1, int col)
        {
            point3d lineStart = new point3d(x0, y0, z0);
            point3d lineEnd = new point3d(x1, y1, z1);
            DrawLine(ref lineStart, ref lineEnd, col);
        }

        public void DrawLine(ref point3d min, ref point3d max, int col)
        {
            var paras = new object[] { min, max, col };
            _features["DrawLine"].Invoke(_runtime, paras);
        }

        public void DrawPolygon(pol_t[] pt, int ptCount, int col)
        {
            var paras = new object[] { pt, ptCount, col };
            _features["DrawPolygon"].Invoke(_runtime, paras);
        }
        public void DrawSphere(float x, float y, float z, float radius, int issol, int colour)
        {
            point3d position = new point3d(x, y, z);

            var paras = new object[] { position, radius, issol, colour };
            _features["DrawSphere"].Invoke(_runtime, paras);
        }

        public void DrawSphere(ref point3d position, float radius, int issol, int colour)
        {
            var paras = new object[] { position, radius, issol, colour };
            _features["DrawSphere"].Invoke(_runtime, paras);
        }

        public void DrawSphereBulk(poltex[] vertices, float radius)
        {
            var paras = new object[] { vertices, radius };
            _features["DrawSphereBulk"].Invoke(_runtime, paras);
        }

        public void DrawSphereBulkCnt(poltex[] vertices, float radius, int count)
        {
            var paras = new object[] { vertices, radius, count };
            _features["DrawSphereBulkCnt"].Invoke(_runtime, paras);
        }

        public void DrawTexturedMesh(ref tiletype texture, poltex[] vertices, int verticeCount, int[] indices, int indiceCount, int flags)
        {
            var paras = new object[] { texture, vertices, verticeCount, indices, indiceCount, flags };
            _features["DrawTexturedMesh"].Invoke(_runtime, paras);
        }

        public void DrawLitTexturedMesh(ref tiletype texture, poltex[] vertices, int verticeCount, int[] indices, int indiceCount, int flags, int ambient_color = 0x040404)
        {
            var paras = new object[] { texture, vertices, verticeCount, indices, indiceCount, flags, ambient_color };
            _features["DrawLitTexturedMesh"].Invoke(_runtime, paras);
        }

        public void DrawUntexturedMesh(poltex[] vertices, int verticeCount, int[] indices, int indiceCount, int flags, int colour)
        {
            try
            {
                var paras = new object[] { vertices, verticeCount, indices, indiceCount, flags, colour };
                _features["DrawUntexturedMesh"].Invoke(_runtime, paras);
            }
            catch (Exception e)
            {
                Console.WriteLine($"DrawUntexturedMesh Exception: {e.Message} Params : {vertices} {verticeCount} {indices} {flags} {colour}");
            }
        }

        public void DrawVoxel(float x, float y, float z, int col)
        {
            point3d pos = new point3d(x, y, z);
            DrawVoxel(ref pos, col);

        }

        public void DrawVoxel(ref point3d position, int col)
        {
            var paras = new object[] { position, col };
            _features["DrawVoxel"].Invoke(_runtime, paras);
        }

        public void DrawVoxelBatch(ref point3d[] positions, int voxel_count, int colour)
        {

            var paras = new object[] { positions, voxel_count, colour };
            _features["DrawVoxelBatch"].Invoke(_runtime, paras);
        }

        public void DrawVoxels(ref point3d[] positions, int voxel_count, ref int[] colours)
        {
            var paras = new object[] { positions, voxel_count, colours };
            _features["DrawVoxels"].Invoke(_runtime, paras);
        }

        public void SetFlag(int value, int flag)
        {
            var paras = new object[] { value, flag };
            _features["SetFlag"].Invoke(_runtime, paras);
        }

        public int GetFlags()
        {
            return (int)_features["GetFlags"].Invoke(_runtime, null);
        }

        public bool IsFlagSet(int flagID)
        {
            var paras = new object[] { flagID };
            return (bool)_features["IsFlagSet"].Invoke(_runtime, paras);
        }



        public void FrameEnd()
        {
            _features["FrameEnd"].Invoke(_runtime, null);
        }

        /// <summary>
        /// Starts a frame (also known as a breath) in the Voxon Volumetric Engine.
        /// </summary>
        /// <remarks>
        /// Error Codes:
        /// - 0: No issue.
        /// - 1: Runtime is not active.
        /// - 2: Failed during ledWin.Breath() call.
        /// - 3: `hasBreath` from ledWin returned -1.
        /// </remarks>
        /// <returns>
        /// An integer representing the error code:
        /// - 0: Success.
        /// - 1, 2, 3: Specific error cases as described above.
        /// </returns>
        public int FrameStart()
        {
            return (int)_features["FrameStart"].Invoke(_runtime, null);
        }

        public float[] GetAspectRatio()
        {
            return (float[])_features["GetAspectRatio"].Invoke(_runtime, null);

        }

        public float GetAxis(int axis, int player)
        {
            var paras = new object[] { axis, player };
            return (float)_features["GetAxis"].Invoke(_runtime, paras);
        }

        public bool GetButton(int button, int player)
        {
            var paras = new object[] { button, player };
            return (bool)_features["GetButton"].Invoke(_runtime, paras);
        }

        public bool GetButtonDown(int button, int player)
        {
            var paras = new object[] { button, player };
            return (bool)_features["GetButtonDown"].Invoke(_runtime, paras);
        }

        public bool GetButtonUp(int button, int player)
        {
            var paras = new object[] { button, player };
            return (bool)_features["GetButtonUp"].Invoke(_runtime, paras);
        }

        // return true if Key is pressed. Use VX Key codes enum VX_KEY
        public bool GetKey(int keycode)
        {
            var paras = new object[] { keycode };
            return (bool)_features["GetKey"].Invoke(_runtime, paras);
        }
        // returns true if Key is just pressed. Use Voxon Key codes enum VX_KEY
        public bool GetKeyDown(int keycode)
        {
            var paras = new object[] { keycode };
            return (bool)_features["GetKeyDown"].Invoke(_runtime, paras);
        }
        // returns the amount of time a has been pressed, odes enum VX_KEY
        public double GetKeyDownTime(int keycode)
        {
            var paras = new object[] { keycode };
            return (double)_features["GetKeyDownTime"].Invoke(_runtime, paras);
        }

        public int GetKeyState(int keycode)
        {
            var paras = new object[] { keycode };
            return (int)_features["GetKeyState"].Invoke(_runtime, paras);
        }

        public bool GetKeyUp(int keycode)
        {
            var paras = new object[] { keycode };
            return (bool)_features["GetKeyUp"].Invoke(_runtime, paras);
        }

        // Some chaining to make it more friendly to use
        public bool GetKey(VX_KEYS keycode)
        {
            return (bool)GetKey((int)keycode);
        }

        public bool GetKeyDown(VX_KEYS keycode)
        {
            return (bool)GetKeyDown((int)keycode);
        }

        public double GetKeyDownTime(VX_KEYS keycode)
        {
            return (double)GetKeyDownTime((int)keycode);

        }
        public int GetKeyState(VX_KEYS keycode)
        {
            return (int)GetKeyState((int)keycode);
        }
        public bool GetKeyUp(VX_KEYS keycode)
        {
            return (bool)GetKeyUp((int)keycode);
        }

        public bool GetMouseButton(int button)
        {
            var paras = new object[] { button };
            return (bool)_features["GetMouseButton"].Invoke(_runtime, paras);
        }

        public bool GetMouseButtonDown(int button)
        {
            var paras = new object[] { button };
            return (bool)_features["GetMouseButtonDown"].Invoke(_runtime, paras);
        }

        public float[] GetMousePosition()
        {
            return (float[])_features["GetMousePosition"].Invoke(_runtime, null);
        }

        public float[] GetSpaceNavPosition()
        {
#if !UNITY_2017_1_OR_NEWER
            return (float[])_features["GetSpaceNavPosition"].Invoke(_runtime, null);
#endif


#if UNITY_2017_1_OR_NEWER
            if (VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
            {
                return (float[])_features["GetSpaceNavPosition"].Invoke(_runtime, null);
            }
            else
            {

                float[] fpos = (float[])_features["GetSpaceNavPosition"].Invoke(_runtime, null);
                float[] returnval = { fpos[0], -fpos[2], fpos[1] };
                returnval[0] /= 350;
                returnval[1] /= 350;
                returnval[2] /= 350;

                return returnval;

            }
#endif
        }

        public float[] GetSpaceNavRotation()
        {
#if !UNITY_2017_1_OR_NEWER
            return (float[])_features["GetSpaceNavRotation"].Invoke(_runtime, null);
#endif


#if UNITY_2017_1_OR_NEWER
            if (VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
            {
                return (float[])_features["GetSpaceNavRotation"].Invoke(_runtime, null);
            }
            else
            {
                float[] fpos = (float[])_features["GetSpaceNavRotation"].Invoke(_runtime, null);
                float[] returnval = { -fpos[1], fpos[2], -fpos[0] };
                returnval[0] /= 350;
                returnval[1] /= 350;
                returnval[2] /= 350;

                return returnval;

            }
#endif
        }

        public bool GetSpaceNavButton(int button)
        {
            var paras = new object[] { button };
            return (bool)_features["GetSpaceNavButton"].Invoke(_runtime, paras);
        }

        public float GetVolume()
        {
            return (float)_features["GetVolume"].Invoke(_runtime, null);
        }

        public void Initialise(int type)
        {
            var paras = new object[] { type };
            _features["Initialise"].Invoke(_runtime, paras);
        }

        public bool isInitialised()
        {
            return (bool)_features["isInitialised"].Invoke(_runtime, null);
        }

        public bool isLoaded()
        {
            return (bool)_features["isLoaded"].Invoke(_runtime, null);
        }

        /// <summary>
        /// Loads the Voxon DLLs into the application. It searches for the DLL(s) in this order
        /// 1 ** Local Path **
        /// 2 ** User then, System environment PATH variables
        /// 3 ** Checks the registry
        /// </summary>
        /// <returns></returns>
        public string Load()
        {
            return (string)_features["Load"].Invoke(_runtime, null);
        }

        public void LogToFile(string msg)
        {
            var paras = new object[] { msg };
            _features["LogToFile"].Invoke(_runtime, paras);
        }

        public void LogToScreen(int x, int y, string text)
        {
            var paras = new object[] { x, y, text };
            _features["LogToScreen"].Invoke(_runtime, paras);
        }

        public void LogToScreenExt(int x, int y, int colFG, int colBG, string text)
        {
            var paras = new object[] { x, y, colFG, colBG, text };
            _features["LogToScreenExt"].Invoke(_runtime, paras);
        }

        public void SetAspectRatio(float aspx, float aspy, float aspz)
        {
            var paras = new object[] { aspx, aspy, aspz };
            _features["SetAspectRatio"].Invoke(_runtime, paras);
        }

        public void SetColorMode(int colour)
        {
            var paras = new object[] { colour };
            _features["SetColorMode"].Invoke(_runtime, paras);
        }

        public int GetColorMode()
        {
            return (int)_features["isInitialised"].Invoke(_runtime, null);
        }

        public void SetDisplayColor(Voxon.ColorMode color)
        {
            //
            SetColorMode((int)color);
        }

        public Voxon.ColorMode GetDisplayColor()
        {
            return (Voxon.ColorMode)GetColorMode();
        }

        public void Shutdown()
        {
#if UNITY_2017_1_OR_NEWER
            if (PlayerPrefs.GetInt("Voxon_VXProcessReportingLevel") >= (int)VXProcessReportLevel.Processes)
                LogDebug("Runtime.cs - Shutdown() function called... preparing Runtime.cs");
#endif
            _features["Shutdown"].Invoke(_runtime, null);
        }

        public void Unload()
        {
#if UNITY_2017_1_OR_NEWER
            if (PlayerPrefs.GetInt("Voxon_VXProcessReportingLevel") >= (int)VXProcessReportLevel.Processes)
                LogDebug("Runtime.cs - Unload() function called... Unloading DLL");
#endif
            _features["Unload"].Invoke(_runtime, null);
        }

        public long GetDLLVersion()
        {
            return (long)_features["GetDLLVersion"].Invoke(_runtime, null);
        }

        public string GetSDKVersion()
        {
            return (string)_features["GetSDKVersion"].Invoke(_runtime, null);
        }

        public void SetDotSize(int dotSize)
        {
            var paras = new object[] { dotSize };
            _features["SetDotSize"].Invoke(_runtime, paras);
        }

        public int GetDotSize()
        {
            return (int)_features["GetDotSize"].Invoke(_runtime, null);
        }

        public void SetGamma(float gamma)
        {
            var paras = new object[] { gamma };
            _features["SetGamma"].Invoke(_runtime, paras);
        }

        public float GetGamma()
        {
            return (float)_features["GetGamma"].Invoke(_runtime, null);
        }

        public void SetDensity(float density)
        {
            var paras = new object[] { density };
            _features["SetDensity"].Invoke(_runtime, paras);
        }

        public float GetDensity()
        {
            return (float)_features["GetDensity"].Invoke(_runtime, null);
        }

        public void DisableNormalLighting()
        {
            _features["DisableNormalLighting"].Invoke(_runtime, null);
        }

        public void SetNormalLighting(float x, float y, float z)
        {
            var paras = new object[] { x, y, z };
            _features["SetNormalLighting"].Invoke(_runtime, paras);
        }

        public bool SetDisplay2D(int hibernateLeds = -1)
        {
            var paras = new object[] { hibernateLeds };
            return (bool)_features["SetDisplay2D"].Invoke(_runtime, paras);
        }

        public bool SetDisplay3D()
        {

            return (bool)_features["SetDisplay3D"].Invoke(_runtime, null);
        }

        public int GetClipShape()
        {
            //	return _features.ContainsKey("GetClipShape") && (int) _features["GetClipShape"].Invoke(_runtime, null);
            return (int)_features["GetClipShape"].Invoke(_runtime, null);
        }

        public void SetClipShape(int newValue)
        {
            if (_features.ContainsKey("SetClipShape"))
            {
                var paras = new object[] { newValue };
                _features["SetClipShape"].Invoke(_runtime, paras);
            }
        }

        public float GetExternalRadius()
        {
            if (_features.ContainsKey("GetExternalRadius"))
            {
                return (float)_features["GetExternalRadius"].Invoke(_runtime, null);
            }

            return 0.0f;
        }

        public void SetExternalRadius(float radius)
        {
            if (_features.ContainsKey("SetExternalRadius"))
            {
                var paras = new object[] { radius };
                _features["SetExternalRadius"].Invoke(_runtime, paras);
            }
        }

        public float GetInternalRadius()
        {
            if (_features.ContainsKey("GetInternalRadius"))
            {
                return (float)_features["GetInternalRadius"].Invoke(_runtime, null);
            }

            return 0.0f;
        }

        public void SetInternalRadius(float radius)
        {
            if (_features.ContainsKey("SetInternalRadius"))
            {
                var paras = new object[] { radius };
                _features["SetInternalRadius"].Invoke(_runtime, paras);
            }
        }

        public void MenuReset(MenuUpdateHandler menuUpdate, IntPtr userdata)
        {
            var paras = new object[] { menuUpdate, userdata };
            _features["MenuReset"].Invoke(_runtime, paras);
        }

        public void MenuAddTab(string text, int x, int y, int width, int height)
        {
            var paras = new object[] { text, x, y, width, height };
            _features["MenuAddTab"].Invoke(_runtime, paras);
        }

        public void MenuAddText(int id, string text, int x, int y, int width, int height, int colour)
        {
            var paras = new object[] { id, text, x, y, width, height, colour };
            _features["MenuAddText"].Invoke(_runtime, paras);
        }

        public void MenuAddButton(int id, string text, int x, int y, int width, int height, int colour, int position)
        {
            var paras = new object[] { id, text, x, y, width, height, colour, position };
            _features["MenuAddButton"].Invoke(_runtime, paras);
        }

        public void MenuAddVerticleSlider(int id, string text, int x, int y, int width, int height, int colour, double initial_value,
            double min, double max, double minor_step, double major_step)
        {
            var paras = new object[] { id, text, x, y, width, height, colour, initial_value, min, max, minor_step, major_step };
            _features["MenuAddVerticleSlider"].Invoke(_runtime, paras);
        }

        public void MenuAddHorizontalSlider(int id, string text, int x, int y, int width, int height, int colour, double initial_value,
            double min, double max, double minor_step, double major_step)
        {
            var paras = new object[] { id, text, x, y, width, height, colour, initial_value, min, max, minor_step, major_step };
            _features["MenuAddHorizontalSlider"].Invoke(_runtime, paras);
        }

        public void MenuAddEdit(int id, string text, int x, int y, int width, int height, int colour, bool hasFollowupButton = false)
        {
            var paras = new object[] { id, text, x, y, width, height, colour, hasFollowupButton };
            _features["MenuAddEdit"].Invoke(_runtime, paras);
        }

        public void MenuUpdateItem(int id, string st, int down, double v)
        {
            var paras = new object[] { id, st, down, v };
            _features["MenuUpdateItem"].Invoke(_runtime, paras);
        }





        public float SetEmulatorHorizontalAngle(float radians)
        {
            var paras = new object[] { radians };
            return (float)_features["SetEmulatorHorizontalAngle"].Invoke(_runtime, paras);
        }

        public float SetEmulatorVerticalAngle(float radians)
        {
            var paras = new object[] { radians };
            return (float)_features["SetEmulatorVerticalAngle"].Invoke(_runtime, paras);
        }

        public float SetEmulatorDistance(float distance)
        {
            var paras = new object[] { distance };
            return (float)_features["SetEmulatorDistance"].Invoke(_runtime, paras);
        }

        public float GetEmulatorHorizontalAngle()
        {
            return (float)_features["GetEmulatorHorizontalAngle"].Invoke(_runtime, null);
        }

        public float GetEmulatorVerticalAngle()
        {
            return (float)_features["GetEmulatorVerticalAngle"].Invoke(_runtime, null);
        }

        public float GetEmulatorDistance()
        {
            return (float)_features["GetEmulatorDistance"].Invoke(_runtime, null);
        }

        public void StartRecording(string filename, int vps)
        {
#if UNITY_2017_1_OR_NEWER
            if (PlayerPrefs.GetInt("Voxon_VXProcessReportingLevel") >= (int)VXProcessReportLevel.General)
            {
                LogDebug($"Runtime.cs - StartRecording() function called writing: {filename}");
            }
#endif
            var paras = new object[] { filename, vps };
            _features["StartRecording"].Invoke(_runtime, paras);
        }

        public void EndRecording()
        {
#if UNITY_2017_1_OR_NEWER
            if (PlayerPrefs.GetInt("Voxon_VXProcessReportingLevel") >= (int)VXProcessReportLevel.General)
            {
                LogDebug($"Runtime.cs - EndRecording() function called");
            }
#endif
            _features["EndRecording"].Invoke(_runtime, null);
        }

        public void GetVCB(string filename, int vps)
        {
            var paras = new object[] { filename, vps };
            _features["GetVCB"].Invoke(_runtime, paras);
        }

        #endregion

        /*************************
		 *
		 * BRIDGE TYPE FUNCTIONS
		 *
		 *************************/


        #region BridgeTypeFunctions
        public VOXON_RUNTIME_INTERFACE getVXURuntimeType()
        {
            return this.TypeOfRuntime;
        }

        public bool isInterfaceEXTENDED()
        {
            if (this.TypeOfRuntime == VOXON_RUNTIME_INTERFACE.EXTENDED) return true;
            else return false;
        }

        public bool isInterfaceLEGACY()
        {
            if (this.TypeOfRuntime == VOXON_RUNTIME_INTERFACE.LEGACY) return true;
            else
            {
                return false;
            }
        }

        public bool IsInterfaceVXLED()
        {
            if (this.TypeOfRuntime == VOXON_RUNTIME_INTERFACE.VXLED) return true;
            else return false;
        }

        private void NotSupportedBridge(string func)
        {
            if (!NotSupportedMsgShown)
            {
                LogWarning(func + " - " + NotSupportedMsg);
                NotSupportedMsgShown = true;
            }
        }

        public void ScreenDrawPix(int x, int y, int col)
        {
            var paras = new object[] { x, y, col };
            _features["ScreenDrawPix"].Invoke(_runtime, paras);
        }

        public void ScreenDrawHLine(int x0, int x1, int y, int col)
        {
            var paras = new object[] { x0, x1, y, col };
            _features["ScreenDrawHLine"].Invoke(_runtime, paras);
        }

        public void ScreenDrawLine(int x0, int y0, int x1, int y1, int col)
        {
            var paras = new object[] { x0, y0, x1, y1, col };
            _features["ScreenDrawLine"].Invoke(_runtime, paras);
        }

        public void ScreenDrawCircle(int xc, int yc, int r, int col)
        {
            var paras = new object[] { xc, yc, r, col };
            _features["ScreenDrawCircle"].Invoke(_runtime, paras);
        }

        public void ScreenDrawRectangleFill(int x0, int y0, int x1, int y1, int col)
        {
            var paras = new object[] { x0, y0, x1, y1, col };
            _features["ScreenDrawRectangleFill"].Invoke(_runtime, paras);
        }

        public void ScreenDrawCircleFill(int x, int y, int r, int col)
        {
            var paras = new object[] { x, y, r, col };
            _features["ScreenDrawCircleFill"].Invoke(_runtime, paras);
        }

        public void ScreenDrawTile(ref tiletype source, int xpos, int ypos)
        {
            var paras = new object[] { source, xpos, ypos };
            _features["ScreenDrawTile"].Invoke(_runtime, paras);
        }

        #endregion

        /*************************
		 *
		 * SHARED VXLED AND EXTENDED FUNCTIONS
		 *
		 *************************/
        #region EXTENDEDandVXLEDFunctions


        

        public int DrawSprite(string fileName, ref point3d pos, ref point3d rVec, ref point3d dVec, ref point3d fVec, int colour)
        {
            if (isInterfaceLEGACY() == true)
            {
                NotSupportedBridge("DrawSprite");
                return 0;
            }

            var paras = new object[] { fileName, pos, rVec, dVec, fVec, colour };
            return (int)_features["DrawSprite"].Invoke(_runtime, paras);
        }

        public int KeyRead()
        {
            if (isInterfaceLEGACY() == true)
            {
                NotSupportedBridge("KeyRead");
                return 0;
            }
            return (int)_features["KeyRead"].Invoke(_runtime, null);
        }

        public void Report(String reportType, int posX, int posY)
        {
            if (isInterfaceLEGACY() == true)
            {
                NotSupportedBridge("Report");
                return;
            }

            try
            {
                var paras = new object[] { posX, posY };
                _features["Report" + reportType].Invoke(_runtime, paras);
            }
            catch
            {
                if (NotSupportedMsgShown == false)
                {
                    LogWarning($"Report for {reportType} does not exist for this interface");
                    NotSupportedMsgShown = true;
                }
            }
        }

        #endregion
        /*************************
		 *
		 * EXCLUSIVE VXLED FUNCTIONS
		 *
		 *************************/
        #region VXLEDFunctions

        // New LedWin Functions - Hardware / Engine 
        // -------------------------------------------------------------------


        /// <summary>
        /// Flags for the VXU CS_Bridge Interface. These are used to control the behavior
        /// of the Bridge. Currently supported only in the VLED platform.
        /// </summary>
        /// <remarks>
        /// Flag Definitions:
        /// - <c>VXUB_FLAG_DISABLE_LOGGING</c> (0): If enabled, the VXU_Bridge.log does not get written.
        /// - <c>VXUB_FLAG_DONTFREE_HANDLE_ON_EXIT</c> (1): If enabled, the LEDWINCS handle will not be freed on <c>Runtime.Unload()</c>. This helps the DLL integrate with Unity.
        /// - <c>VXUB_FLAG_BYPASS_PATH_ENV_DLL_CHECK</c> (2): Disables looking for the DLLs in the Path environment.
        /// - <c>VXUB_FLAG_BYPASS_REGISTRY_DLL_CHECK</c> (3): Disables looking for the DLLs in the registry environment.
        /// </remarks>
        /// <example>
        /// Example usage:
        /// <code>
        /// int flagsToSet = (1 << (int)VXU_FLAG_IDS.VXUB_FLAG_DISABLE_LOGGING) |
        ///                  (1 << (int)VXU_FLAG_IDS.VXUB_FLAG_BYPASS_PATH_ENV_DLL_CHECK);
        ///
        /// SetVXUBridgeFlags(flagsToSet);
        /// </code>
        /// </example>
        public void SetVXUBridgeFlags(int flags)
        {

            if (IsInterfaceVXLED() == false)
            {
                //NotSupportedBridge("SetVXUBridgeFlags");
                return;
            }

            if (!_features.ContainsKey("SetVXUBridgeFlags"))
                throw new InvalidOperationException("SetVXUBridgeFlags feature is not available.");

            _features["SetVXUBridgeFlags"].Invoke(_runtime, new object[] { flags });
        }

        /// <summary>
        /// Retrieves the current state of the VXU CS_Bridge flags.
        /// </summary>
        /// <returns>The current flags as an integer, where each bit represents a specific flag state.</returns>
        /// <remarks>
        /// The returned integer can be checked using bitwise operations to determine the state of individual flags.
        /// Flag Definitions:
        /// - <c>VXUB_FLAG_DISABLE_LOGGING</c> (0): If enabled, the VXU_Bridge.log does not get written.
        /// - <c>VXUB_FLAG_DONTFREE_HANDLE_ON_EXIT</c> (1): If enabled, the LEDWINCS handle will not be freed on <c>Runtime.Unload()</c>. This helps the DLL integrate with Unity.
        /// - <c>VXUB_FLAG_BYPASS_PATH_ENV_DLL_CHECK</c> (2): Disables looking for the DLLs in the Path environment.
        /// - <c>VXUB_FLAG_BYPASS_REGISTRY_DLL_CHECK</c> (3): Disables looking for the DLLs in the registry environment.
        /// </remarks>
        /// <example>
        /// Example usage:
        /// <code>
        /// int currentFlags = GetVXUBridgeFlags();
        /// if ((currentFlags & (1 << (int)VXU_FLAG_IDS.VXUB_FLAG_DISABLE_LOGGING)) != 0)
        /// {
        ///     Console.WriteLine("Logging is disabled.");
        /// }
        /// </code>
        /// </example>
        public int GetVXUBridgeFlags()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetVXUBridgeFlags");
                return 0;
            }

            if (!_features.ContainsKey("GetVXUBridgeFlags"))
                throw new InvalidOperationException("GetVXUBridgeFlags feature is not available.");

            return (int)_features["GetVXUBridgeFlags"].Invoke(_runtime, null);
        }

        public string GetVersion()
        {

            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetVersion");
                return "";
            }
            return (string)_features["GetVersion"].Invoke(_runtime, null);
        }

        public string GetIVersion()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetIVersion");
                return "";
            }
            return (string)_features["GetIVersion"].Invoke(_runtime, null);
        }

        public bool IsLedWinFocused()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("IsLedWinFocused");
                return false;
            }

            return (bool)_features["IsLedWinFocused"].Invoke(_runtime, null);
        }

        /// <summary>
        /// * VLED INTERFACE ONLY *  
        /// Sets the X and Y position of the 2D window
        /// </summary>
        public void SetEmuWindowPos(int xPos, int yPos)
        {
            var paras = new object[] { xPos, yPos };
            _features["SetEmuWindowPos"].Invoke(_runtime, paras);
        }





        /// <summary>
        /// * VLED INTERFACE ONLY *  
        /// Sets the X and Y resolution of the 2D window
        /// </summary>
        public void SetEmuWindowRes(int xRes, int yRes)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("SetEmuWindowRes");
                return;
            }

            var paras = new object[] { xRes, yRes };
            _features["SetEmuWindowRes"].Invoke(_runtime, paras);
        }


        /// <summary>
        /// * VLED INTERFACE ONLY *  
        /// Retrieves the 2D window resolution and position.  
        /// The returned array contains the following values:  
        /// windInfo[0] = Current window position X.  
        /// windInfo[1] = Current window position Y.  
        /// windInfo[2] = Current window resolution width.  
        /// windInfo[3] = Current window resolution height.  
        /// </summary>
        public int[] GetEmuWindowInfo()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetEmuWindowInfo");
                return new int[] { 0, 0, 0, 0 };
            }
            return (int[])_features["GetEmuWindowInfo"].Invoke(_runtime, null);
        }

        /// <summary>
        /// * VLED INTERFACE ONLY *
        /// Sets the name of the Window in the titlebar - must be called before VXU intialise() is called
        /// </summary>
        public void SetProgramName(string name)
        {

            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("SetProgramName");
                return;
            }
            var paras = new object[] { name };

            _features["SetProgramName"].Invoke(_runtime, paras);

        }

        /// <summary>
        /// * VLED INTERFACE ONLY *
        /// Returns C# bridge DLL path info. So you know which instance of the Ledhost.dll is being used.
        /// </summary>
        public string GetDLLPathInfo()
        {

            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetDLLPathInfo");
                return "";
            }
            return (string)_features["GetDLLPathInfo"].Invoke(_runtime, null);
        }


        /// <summary>
        /// * VLED INTERFACE ONLY *
        /// Returns C# bridge DLL path info. So you know which instance of the Ledhost.dll is being used.
        /// </summary>
        public void ReportDLLPathInfo(int xpos, int ypos, int col)
        {

            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetDLLPathInfo");
                return;
            }

            string mainStr = (string)_features["GetDLLPathInfo"].Invoke(_runtime, null);
            string[] parts = mainStr.Split(';');


            LogToScreenExt(xpos, ypos, 0xffffff, -1, "VX CS BRIDGE REPORT");

            for (int i = 0; i < parts.Length; i++)
            {
                LogToScreenExt(xpos, ypos + 10 + (i * 10), col, -1, parts[i]);
            }

        }

        /// <summary>
        /// * VLED INTERFACE ONLY *
        /// Returns the X,Y and Z LED dimensions in that order. 
        /// </summary>
        public int[] GetLEDSizeDimensions()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetLEDSizeDimensions");
                return new int[3];
            }

            return (int[])_features["GetLEDSizeDimensions"].Invoke(_runtime, null);
        }

        /// <summary>
        /// * VLED INTERFACE ONLY *
        /// Sets the central rotational offset of the display, defining the "front" orientation.
        /// This method is specific to the VLED interface and hardware-dependent.
        /// </summary>
        /// <param name="newAngleDeg">The new offset angle in degrees to apply.</param>
        public void SetCentralRotationOffset(int newAngleDeg)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("SetCentralRotationOffset");
                return;
            }
            var paras = new object[] { newAngleDeg };
            _features["SetCentralRotationOffset"].Invoke(_runtime, paras);
        }

        /// <summary>
        /// * VLED INTERFACE ONLY *
        /// Retrieves the currently applied central rotational offset of the display in degrees.
        /// This method is specific to the VLED interface and hardware-dependent.
        /// </summary>
        /// <returns>
        /// The central rotational offset in degrees. Returns -1 if the current interface is not VXLED.
        /// </returns>
        public int GetCentralRotationOffset()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetCentralRotationOffset");
                return -1;
            }

            return (int)_features["GetCentralRotationOffset"].Invoke(_runtime, null);
        }


        // New LedWin Draw Calls
        // -------------------------------------------------------------------

        /// <summary>
        /// * VLED INTERFACE ONLY *
        /// Draws a voxel directly on the LED display, bypassing dither. Use for precision.
        /// </summary>
        /// <param name="depthX">
        /// Depth (x) (voxie Y) forward/backward axis. 0 equals closest to front, vs.xsiz equals furthest. (use VXURuntime.GetLEDSizeDimensions() to get values)
        /// </param>
        /// <param name="heightY">
        /// Height (y) (voxie Z) top/bottom. 0 is top and vs.ysiz (use VXURuntime.GetLEDSizeDimensions() to get values)
        /// </param>
        /// <param name="angleZ">
        /// Angle (z) (rotation). Angle slice: 0 is front, vs.zsiz equals slice.  (use VXURuntime.GetLEDSizeDimensions() to get values)
        /// </param>
        /// <param name="col">
        /// Color value: 1 = red, 2 = green, 3 = yellow, 4 = blue, 5 = magenta, 6 = cyan, 7 = white. Uses binary values to determine its color state in RGB.
        /// </param>
        public void DrawPVox(int depthX, int heightY, int angleZ, int col)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("DrawPVox");
                return;
            }
            var paras = new object[] { depthX, heightY, angleZ, col };
            _features["DrawPVox"].Invoke(_runtime, paras);
        }


        /// <summary>
        /// * VLED INTERFACE ONLY *
        /// Draws a box of Pvoxels directly on the LED display, use for precision. Define the first Upper Top Left points first and then Bottom Bottom Right co-orienates
        /// </summary>
        /// <param name="depthX0">
        /// Depth (x) (voxie Y) 1st X point forward/backward axis. 0 equals closest to front, vs.xsiz equals furthest. (use VXURuntime.GetLEDSizeDimensions() to get values)
        /// </param>
        /// <param name="heightY0">
        /// Height (y) (voxie Z) 1st Y point top/bottom. 0 is top and vs.ysiz (use VXURuntime.GetLEDSizeDimensions() to get values)
        /// </param>
        /// <param name="angleZ0">
        /// Angle (z) (rotation). 1st Z point Angle slice: 0 is front, vs.zsiz equals slice.  (use VXURuntime.GetLEDSizeDimensions() to get values)
        /// </param>
        ///
        /// <param name="depthX1">
        /// Depth (x) (voxie Y) 2nd X point forward/backward axis. 0 equals closest to front, vs.xsiz equals furthest. (use VXURuntime.GetLEDSizeDimensions() to get values)
        /// </param>
        /// <param name="heightY1">
        /// Height (y) (voxie Z) 2nd Y point top/bottom. 0 is top and vs.ysiz (use VXURuntime.GetLEDSizeDimensions() to get values)
        /// </param>
        /// <param name="angleZ1">
        /// Angle (z) (rotation). 2nd Z point Angle slice: 0 is front, vs.zsiz equals slice.  (use VXURuntime.GetLEDSizeDimensions() to get values)
        /// </param>
        /// <param name="col">
        /// Color value: 1 = red, 2 = green, 3 = yellow, 4 = blue, 5 = magenta, 6 = cyan, 7 = white. Uses binary values to determine its color state in RGB.
        /// </param>
        /// <param name="col">
        /// Color value: 1 = red, 2 = green, 3 = yellow, 4 = blue, 5 = magenta, 6 = cyan, 7 = white. Uses binary values to determine its color state in RGB.
        /// </param>
        public void DrawPBox(int depthX0, int heightY0, int angleZ0, int depthX1, int heightY1, int angleZ1, int col)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("DrawPBox");
                return;
            }
            var paras = new object[] { depthX0, heightY0, angleZ0, depthX1, heightY1, angleZ1, col };
            _features["DrawPBox"].Invoke(_runtime, paras);
        }


        /// <summary>
        /// Draws text directly on the LED display sharply. Can load a custom bitmap font presented as a tiletype. Positioning is the same as PVox.
        /// </summary>
        /// <param name="custFont">
        /// Custom font tile type to render a custom bitmap font. 0 equals closest to front, xsiz equals furthest.
        /// </param>
        /// <param name="depthX">
        /// Depth (x) (voxie Y) forward/backward axis. 0 equals closest to front, xsiz equals furthest.
        /// </param>
        /// <param name="heightY">
        /// Height (y) (voxie Z) top/bottom. 0 is top and ysiz is 128.
        /// </param>
        /// <param name="angleZ">
        /// Angle (z) (rotation). Angle slice: 0 is front, zsiz equals slice.
        /// </param>
        /// <param name="size">
        /// Size of the font. The higher the value, the bigger the font.
        /// </param>
        /// <param name="col">
        /// Color value: 1 = red, 2 = green, 3 = yellow, 4 = blue, 5 = magenta, 6 = cyan, 7 = white. Uses binary values to determine its color state in RGB.
        /// </param>
        /// <param name="msg">
        /// Message to display.
        /// </param>
        /// <example>
        /// Example usage: DrawPText(ref custFont, (int)(Math.Cos(LedWin.GetTime()) * 10 + 10), (int)(Math.Sin(LedWin.GetTime()) * 10 + 48), -1, scale, 7, "Hello World");
        /// </example>
        public void DrawPText(ref tiletype custFont, int depthX, int heightY, int angleZ, int size, int col, string msg)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("DrawPText");
                return;
            }
            var paras = new object[] { custFont, depthX, heightY, angleZ, size, col, msg };
            _features["DrawPText"].Invoke(_runtime, paras);
        }

        /// <summary>
        /// Replaces the whole internal flags with a new value. 
        /// </summary>
        /// <param name="newFlagValue"></param>
        public void SetFlags(int newFlagValue)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("SetFlags");
                return;
            }
            var paras = new object[] { newFlagValue };
            _features["SetFlags"].Invoke(_runtime, paras);
        }


        public void DrawPolygonTextured(poltex[] pt, ref tiletype texture, int ptCount, int col)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("DrawPolygonTextured");
                return;
            }
            var paras = new object[] { pt, texture, ptCount, col };
            _features["DrawPolygonTextured"].Invoke(_runtime, paras);
        }

        /// <summary>
        /// Renders a 2D textured quad (plane) onto the volumetric display. Useful for rendering 2D textures.
        /// Must be called between startFrame() & endFrame() functions.
        /// </summary>
        /// <param name="texture">
        /// Filename or path for the texture to load. Must be a tile type; null for no texture.</param>
        /// <param name="pos">
        /// Center position of the quad to render.</param>
        /// <param name="width">
        /// X dimension of the quad (width).</param>
        /// <param name="height">
        /// Y dimension of the quad (height).</param>
        /// <param name="yawDegrees">
        /// Horizontal angle (yaw). 0 is front-facing, 180 is back-facing. Presented in degrees.</param>
        /// <param name="pitchDegrees">
        /// Vertical angle (pitch). 0 is horizontal, 90 is vertical. Presented in degrees.</param>
        /// <param name="rollDegrees">
        /// Twist angle (roll). 0 is flat. Presented in degrees.</param>
        /// <param name="hexColour">
        /// Color value of the texture. 0x404040 is the natural color; other values add a tint.</param>
        /// <param name="u">
        /// U value of the texture. Adjusting this stretches the horizontal texture size. Default is 1.</param>
        /// <param name="v">
        /// V value of the texture. Adjusting this stretches the vertical texture size. Default is 1.</param>
        /// <returns>
        /// 1 if texture not found or issue with tile type; 0 if successful.</returns>
		public int DrawQuad(ref tiletype texture, ref point3d pos, float width, float height, float yawDegrees, float pitchDegrees, float rollDegrees, int hexColour, float uValue, float vValue)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("DrawQuad");
                return -1;
            }
            var paras = new object[] { texture, pos, width, height, yawDegrees, pitchDegrees, rollDegrees, hexColour, uValue, vValue };
            return (int)_features["DrawQuad"].Invoke(_runtime, paras);
        }

        /// <summary>
        /// *VLED only interface* Loads in a valid image type (.PNG, .JPG, .GIF, .BMP ... most image types supported) and converts it to a tiletype. which can be used to render an image. 
        /// 
        /// </summary>
        /// <param name="filePath"></param> the filepath to the image to convert
        /// <param name="TileToPopulate"></param> the Tiletype to populate the data.
        /// <returns> 0 if successful, -1 if wrong interface</returns>
        public int LoadImg2Tile(string filePath, ref tiletype TileToPopulate)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("LoadImg2Tile");
                return -1;
            }
            var paras = new object[] { filePath, TileToPopulate };
            int result = (int)_features["LoadImg2Tile"].Invoke(_runtime, paras);
            // !! IMPORTANT !! when using reflection and sending references
            // after the function call you need to update the property with
            // the reflected back data
            TileToPopulate = (tiletype)paras[1];
            return result;

        }

        // New Space Nav Functions
        // -------------------------------------------------------------------

        public int SetSpaceNavOrientation(float orientationDegrees)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("SetSpaceNavOrientation");
                return -1;
            }
            var paras = new object[] { orientationDegrees };
            return (int)_features["SetSpaceNavOrientation"].Invoke(_runtime, paras);
        }

        public float GetSpaceNavOrientation()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetSpaceNavOrientation");
                return -1.0f;
            }
            return (float)_features["GetSpaceNavOrientation"].Invoke(_runtime, null);
        }

        public void SetSpaceNavDeadZone(float newDeadZoneValue)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("SetSpaceNavDeadZone");
                return;
            }
            var paras = new object[] { newDeadZoneValue };
            _features["SetSpaceNavDeadZone"].Invoke(_runtime, paras);
        }

        public float GetSpaceNavDeadZone()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetSpaceNavDeadZone");
                return -1.0f;
            }
            return (float)_features["GetSpaceNavDeadZone"].Invoke(_runtime, null);
        }

        public int SetSpaceNavInternalID(int newSpaceNavIndex)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("SetSpaceNavInternalID");
                return -1;
            }
            var paras = new object[] { newSpaceNavIndex };
            return (int)_features["SetSpaceNavInternalID"].Invoke(_runtime, paras);
        }

        public int GetSpaceNavInternalID()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetSpaceNavInternalID");
                return -1;
            }

            return (int)_features["GetSpaceNavInternalID"].Invoke(_runtime, null);
        }
        /// <summary>
        /// /* VXLED only */ Check to see if a Space Nav Button has just been pressed returns 'true' if so
        /// </summary>
        /// <param name="button"> 0 = LEFT BUTTON, 1 = RIGHT BUTTON </param>
        /// <returns> true = button just pressed otherwise, false</returns>
        public bool GetSpaceNavButtonDown(int button)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetSpaceNavButtonDown");
                return false;
            }
            var paras = new object[] { button };
            return (bool)_features["GetSpaceNavButtonDown"].Invoke(_runtime, paras);
        }

        /// <summary>
        /// /* VXLED only */ Check to see if a Space Nav Button has just been released returns 'true' if so
        /// </summary>
        /// <param name="button"> 0 = LEFT BUTTON, 1 = RIGHT BUTTON </param>
        /// <returns> true = button just released otherwise, false</returns>
		public bool GetSpaceNavButtonUp(int button)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetSpaceNavButtonUp");
                return false;
            }
            var paras = new object[] { button };
            return (bool)_features["GetSpaceNavButtonUp"].Invoke(_runtime, paras);
        }



        /// <summary>
        ///  /* VXLED only */ Sets the Space Nav coordinate system for VXLED. This changes SpaceNav to return values based on the specified 3D system.
        /// See <c>LedWinCoordinateSystemsTransforms</c> for valid values.
        /// </summary>
        /// <param name="newCoordinateSystem">The new coordinate system value. 
        /// 0 = Voxon, 1 = Unity, 2 = (Refer to the enum for additional systems).</param>
        public void SetSpaceNavCoordinateSystem(int newCoordinateSystem)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("SetSpaceNavCoordinateSystem");
                return;
            }
            var paras = new object[] { newCoordinateSystem };
            _features["SetSpaceNavCoordinateSystem"].Invoke(_runtime, paras);
        }

        /// <summary>
        ///  /* VXLED only */  Retrieves the current 3D coordinate system being used by SpaceNav.
        /// </summary>
        /// <returns>The current coordinate system value used by SpaceNav, 
        /// as defined in <c>LedWinCoordinateSystemsTransforms</c>.</returns>
        public int GetSpaceNavCoordinateSystem()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetSpaceNavCoordinateSystem");
                return -1;
            }

            return (int)_features["GetSpaceNavCoordinateSystem"].Invoke(_runtime, null);

        }

        // End of New Space Nav Functions
        // -------------------------------------------------------------------

        // New Mouse Functions
        // -------------------------------------------------------------------

        /// <summary>
        /// /* VXLED ONLY */ Sets the mouse input to raw mode. This allows the mouse's inputs to be updated outside of the window.
        /// </summary>
        /// <param name="enable"></param>
        public void SetMouseInputToRaw(bool enable)
        {

            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("SetMouseInputToRaw");
               
            }
            var paras = new object[] { enable };
            _features["SetMouseInputToRaw"].Invoke(_runtime, paras);
        }

        /// <summary>
        /// /* VXLED ONLY */ Checks if mouse input mode is set to raw mode.
        /// </summary>
        /// <returns> Returns `true` if the mouse input is in raw mode.</returns>
        public bool IsMouseInputRaw()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("IsMouseInputRaw");
                return false;
            }
            return (bool)_features["IsMouseInputRaw"].Invoke(_runtime, null);
        }


        /// <summary>
        /// /* VXLED ONLY */ Returns `true` if the specified mouse button was released (mouse button up).
        /// </summary>
        /// <param name="button">The button identifier for the mouse.</param>
        /// <returns>`true` if the button was released; otherwise, `false`.</returns>
        public bool GetMouseButtonUp(int button)
        {

            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetMouseButtonUp");
                return false;
            }
            var paras = new object[] { button };
            return (bool)_features["GetMouseButtonUp"].Invoke(_runtime, paras);
        }

        /// <summary>
        /// /* VXLED ONLY */ Returns the XY pixel coordinates of the pointer on the screen.
        /// Values are `[0, 0]` if the mouse pointer is not inside the VXLED window.
        /// </summary>
        /// <returns>An array containing the X and Y coordinates of the pointer.</returns>
        public int[] GetMouseScreenPos()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetMouseScreenPos");
                return new int[2];
            }

            return (int[])_features["GetMouseScreenPos"].Invoke(_runtime, null);
        }
        /// <summary>
        /// /* VXLED ONLY */ Returns the current state of the mouse, including button and positional information.
        /// </summary>
        /// <returns>An instance of <c>vxl_mou_t</c> representing the mouse state.</returns>
        public vxl_mou_t GetMouseState()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetMouseState");
                return new vxl_mou_t();

            }
            return (vxl_mou_t)_features["GetMouseState"].Invoke(_runtime, null);

        }
        /// <summary>
        /// /* VXLED ONLY */ Returns information about the mouse's location relative to the VXLED window.
        /// The values are:
        /// <list type="bullet">
        /// <item><description><c>0</c> - The mouse is outside the window.</description></item>
        /// <item><description><c>1</c> - The mouse is inside the window.</description></item>
        /// <item><description><c>2</c> - The mouse is on the window's title bar.</description></item>
        /// </list>
        /// </summary>
        /// <returns>An integer indicating the mouse's location relative to the VXLED window.</returns>
        public int GetMouseLocation()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetMouseLocation");
                return -1;

            }
            return (int)_features["GetMouseLocation"].Invoke(_runtime, null);
        }


        /// <summary>
        /// /* VXLED ONLY */ Allows mouse controls to act as a Space Navigator, useful for systems without a Space Navigator attached.
        /// </summary>
        /// <param name="enable">Set to true to enable, false to disable.</param>
        /// <param name="navID">The Nav ID to use.</param>
        /// <remarks>
        /// Enabling this does not disable the mouse from being a mouse or a Space Navigator from being used. 
        /// It simply forwards the inputs to the Space Navigator input.
        /// </remarks>

        public void SetMouseToNav(bool enable, int navID)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("SetMouseToNav");
                return;
            }

            var paras = new object[] { enable, navID };
            _features["SetMouseToNav"].Invoke(_runtime, paras);
        }


        // End of New Mouse Functions
        // -------------------------------------------------------------------

        // New Joystick Functions
        // -------------------------------------------------------------------
        public int SetJoyOrientation(int player, float orientationDegrees)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("SetJoyOrientation");
                return -1;
            }
            var paras = new object[] { player, orientationDegrees };
            return (int)_features["SetJoyOrientation"].Invoke(_runtime, paras);
        }

        public float GetJoyOrientation(int player)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetJoyOrientation");
                return -1.0f;
            }
            var paras = new object[] { player };
            return (float)_features["GetJoyOrientation"].Invoke(_runtime, paras);
        }

        public int SetJoyAPI(int joyAPIType)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("SetJoyAPI");
                return -1;
            }
            var paras = new object[] { joyAPIType };
            return (int)_features["SetJoyAPI"].Invoke(_runtime, paras);
        }

        public int GetJoyAPI()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetJoyAPI");
                return -1;
            }

            return (int)_features["GetJoyAPI"].Invoke(_runtime, null);
        }

        public int SetJoyDeadZone(float newJoyDeadZoneValue)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("SetJoyDeadZone");
                return -1;
            }

            var paras = new object[] { newJoyDeadZoneValue };
            return (int)_features["SetJoyDeadZone"].Invoke(_runtime, paras);
        }

        public float GetJoyDeadZone()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetJoyDeadZone");
                return -1.0f;
            }
            return (float)_features["GetJoyDeadZone"].Invoke(_runtime, null);
        }

        public int SetJoyVibration(int player, float leftMotorSpeed, float rightMotorSpeed)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("SetJoyVibration");
                return -1;
            }
            var paras = new object[] { player, leftMotorSpeed, rightMotorSpeed };
            return (int)_features["SetJoyVibration"].Invoke(_runtime, paras);
        }

        public int GetJoyAxisInversion(int playerID, int axisCode)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetJoyAxisInversion");
                return -1;
            }
            var paras = new object[] { playerID, axisCode };
            return (int)_features["GetJoyAxisInversion"].Invoke(_runtime, paras);
        }

        public void SetJoyAxisInversion(int playerID, int axisCode, int value)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("SetJoyAxisInversion");
                return;
            }
            var paras = new object[] { playerID, axisCode, value };
            _features["SetJoyAxisInversion"].Invoke(_runtime, paras);
        }

        // End of new joystick functions 
        // --------------------------------------------------------------------

        public void SetRPMValue(int newRPM)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("SetRPMValue");
                return;
            }

            var paras = new object[] { newRPM };
            _features["SetRPMValue"].Invoke(_runtime, paras);

        }

        public int GetRPMMaxValue()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetRPMMaxValue");
                return 0;
            }

            return (int)_features["GetRPMMaxValue"].Invoke(_runtime, null);

        }
        public int GetRPMValue()
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("GetRPMValue");
                return 0;
            }

            return (int)_features["GetRPMValue"].Invoke(_runtime, null);

        }

        /// <summary>
        /// VXLED INTERFACE ONLY
        /// Sets the dither mode for the VLED display.
        /// Stored in the vxl state struct as dither: 
        /// 0 = error diffusion (default), 1 = ordered dither.
        /// </summary>
        /// <param name="value">0 = error diffusion (default), 1 = ordered dither</param>
        public void SetDitherMode(int value)
        {
            if (!IsInterfaceVXLED())
            {
                NotSupportedBridge("SetDitherMode");
                return;
            }
            var paras = new object[] { value };
            _features["SetDitherMode"].Invoke(_runtime, paras);
        }

        /// <summary>
        /// VXLED INTERFACE ONLY
        /// Gets the current dither mode setting for the VLED display.
        /// Stored in the vxl state struct as dither: 
        /// 0 = error diffusion (default), 1 = ordered dither.
        /// </summary>
        /// <returns>
        /// 0 = error diffusion (default), 1 = ordered dither, 
        /// -1 if runtime is not initialized or supported in bridge.
        /// </returns>
        public int GetDitherMode()
        {
            if (!IsInterfaceVXLED())
            {
                NotSupportedBridge("GetDitherMode");
                return -1;
            }

            return (int)_features["GetDitherMode"].Invoke(_runtime, null);
        }


        /// <summary>
        /// Sets the texture filter mode for the VLED display.
        /// Stored in the vxl state struct as draw3dmode: 
        /// 0 = nearest neighbour, 1 = bilinear.
        /// </summary>
        /// <param name="value">0 = nearest neighbour, 1 = bilinear filtering</param>
        public void SetTextureFilterMode(int value)
        {
            if (!IsInterfaceVXLED())
            {
                NotSupportedBridge("SetTextureFilterMode");
                return;
            }
            var paras = new object[] { value };
            _features["SetTextureFilterMode"].Invoke(_runtime, paras);
        }

        /// <summary>
        /// Gets the current texture filter mode for the VLED display.
        /// Stored in the vxl state struct as draw3dmode: 
        /// 0 = nearest neighbour, 1 = bilinear.
        /// </summary>
        /// <returns>
        /// 1 = bilinear filtering (default), 
        /// 0 = nearest neighbour, 
        /// -1 if the wrong interface or system not initialized.
        /// </returns>
        public int GetTextureFilterMode()
        {
            if (!IsInterfaceVXLED())
            {
                NotSupportedBridge("GetTextureFilterMode");
                return -1;
            }
            return (int)_features["GetTextureFilterMode"].Invoke(_runtime, null);
        }

        public int ExportPLY(string filepath)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("ExportPLY");
                return 0;
            }
            var paras = new object[] { filepath };
            return (int)_features["ExportPLY"].Invoke(_runtime, paras);
        }


        /// <summary>
        /// /* VXLED only */  Converts 3D transforms between different 3D coordinate systems using the <c>Vxl_InputTypes.h::LedWinCoordinateSystemsTransforms</c> enum.
        /// This function supports systems used by game engines like Unity, Godot, Unreal, and Blender.
        /// </summary>
        /// <param name="from3DSystem">The 3D coordinate system from which the current data originates (e.g., <c>LW_COORDINATE_LEFT_HANDED_Y_UP</c>).</param>
        /// <param name="to3DSystem">The 3D coordinate system to which the data will be converted (e.g., <c>LW_COORDINATE_RIGHT_HANDED_Z_UP</c>).</param>
        /// <param name="data">A reference to the <c>point3d</c> data that will be updated during the conversion.</param>
        /// <returns>Returns <c>true</c> if the transformation is successful, <c>false</c> if the input is invalid.</returns>
        public bool Transform3DCoordinates(int from3DSystem, int to3DSystem, ref point3d data)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("Transform3DCoordinates");
                return false;
            }
            var paras = new object[] { from3DSystem, to3DSystem, data };

            bool returnVal = (bool)_features["Transform3DCoordinates"].Invoke(_runtime, paras);
            // !! IMPORTANT !! when using reflection and sending references
            // after the function call you need to update the property with
            // the reflected back data
            data = (point3d)paras[2];

            return returnVal;
        }

        /// <summary>
        /// /* VXLED only */ Retrieves the component of a 3D vector that aligns with a specified world direction 
        /// based on the given coordinate system. This function facilitates converting 
        /// between different 3D coordinate systems.
        /// </summary>
        /// <param name="WorldDirection">The world direction to query, defined by 
        /// <c>Vxl_InputTypes.h::LedWinCoordinateWorldDirectionCodes</c>.</param>
        /// <param name="CoordinateSystem">The coordinate system used for interpreting the vector, 
        /// defined by <c>Vxl_InputTypes.h::LedWinCoordinateSystemsTransforms</c>.</param>
        /// <param name="value">The <c>point3d</c> vector to query.</param>
        /// <returns>The corresponding value from the vector that matches the specified world direction, 
        /// based on the coordinate system.</returns>
        public float TransformGet3DAxis(int WorldDirection, int CoordinateSystem, ref point3d value)
        {
            if (IsInterfaceVXLED() == false)
            {
                NotSupportedBridge("TransformGet3DAxis");
                return 0;
            }
            var paras = new object[] { WorldDirection, CoordinateSystem, value };
            return (float)_features["TransformGet3DAxis"].Invoke(_runtime, paras);
        }

        #endregion
        /*************************
		 *
		 * EXCLUSIVE VOXIEBOX EXTENDED FUNCTIONS
		 *
		 *************************/
        #region VXEXTENDEDFunctions

        public IntPtr GetVoxieHandle()
        {
            if (isInterfaceEXTENDED() == false)
            {
                NotSupportedBridge("GetVoxieHandle");
                return (IntPtr)null;
            }

            return (IntPtr)_features["GetVoxieHandle"].Invoke(_runtime, null);
        }

        public voxie_wind_t GetVoxieWindow()
        {
            voxie_wind_t wind = new voxie_wind_t();
            if (isInterfaceEXTENDED() == false)
            {
                NotSupportedBridge("GetVoxieWindow");
                return wind;
            }

            object obj = _features["GetVoxieWindow"].Invoke(_runtime, null);
            wind = (voxie_wind_t)obj;

            return wind;
        }

        public void UpdateVoxieWindow()
        {
            if (isInterfaceEXTENDED() == false)
            {
                NotSupportedBridge("UpdateVoxieWindow");
                return;
            }

            _features["UpdateVoxieWindow"].Invoke(_runtime, null);
        }

        public void ReplaceVoxieWindow(ref voxie_wind_t voxieWind)
        {
            if (isInterfaceEXTENDED() == false)
            {
                NotSupportedBridge("ReplaceVoxieWindow");
                return;
            }
            var paras = new object[] { voxieWind };
            _features["ReplaceVoxieWindow"].Invoke(_runtime, paras);
        }

        public void SetView(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            if (isInterfaceEXTENDED() == false)
            {
                NotSupportedBridge("SetView");
                return;
            }
            var paras = new object[] { x0, y0, z0, x1, y1, z1 };
            _features["SetView"].Invoke(_runtime, paras);
        }

        public void SetMaskPlane(float x0, float y0, float z0, float nx, float ny, float nz)
        {
            if (isInterfaceEXTENDED() == false)
            {
                NotSupportedBridge("SetMaskPlane");
                return;
            }

            var paras = new object[] { x0, y0, z0, nx, ny, nz };
            _features["SetMaskPlane"].Invoke(_runtime, paras);
        }

        public void MountZip(string fileName)
        {
            if (isInterfaceEXTENDED() == false)
            {
                NotSupportedBridge("MountZip");
                return;
            }

            var paras = new object[] { fileName };
            _features["MountZip"].Invoke(_runtime, paras);
        }

        public void FreeFromCache(string fileName)
        {
            if (isInterfaceEXTENDED() == false)
            {
                NotSupportedBridge("FreeFromCache");
                return;
            }

            var paras = new object[] { fileName };
            _features["FreeFromCache"].Invoke(_runtime, paras);
        }

        public int PlaySound(string fileName, int sourceChannel, int volumeLeft, int volumeRight, float playBackRate)
        {
            if (isInterfaceEXTENDED() == false)
            {
                NotSupportedBridge("PlaySound");
                return -1;
            }

            var paras = new object[] { fileName, sourceChannel, volumeLeft, volumeRight, playBackRate };
            return (int)_features["PlaySound"].Invoke(_runtime, paras);
        }

        public void PlaySoundUpdate(int handle, int sourceChannel, int volumeLeft, int volumeRight, float playBackRate)
        {
            if (isInterfaceEXTENDED() == false)
            {
                NotSupportedBridge("PlaySoundUpdate");
                return;
            }

            var paras = new object[] { handle, sourceChannel, volumeLeft, volumeRight, playBackRate };
            _features["PlaySoundUpdate"].Invoke(_runtime, paras);
        }

        public int DrawSpriteExtended(string fileName, ref point3d pos, ref point3d rVec, ref point3d dVec, ref point3d fVec, int colour, float forcescale, float fdrawratio = 1, int flags = 0)
        {
            if (isInterfaceEXTENDED() == false)
            {
                NotSupportedBridge("DrawSpriteExtended");
                return 0;
            }

            var paras = new object[] { fileName, pos, rVec, dVec, fVec, colour, forcescale, fdrawratio, flags };
            return (int)_features["DrawSpriteExtended"].Invoke(_runtime, paras);
        }

        public void DrawCone(ref point3d startPoint, float startPointradius, ref point3d endPoint, float endPointRadius, int fillmode, int col)
        {

            DrawCone(startPoint.x, startPoint.y, startPoint.z, startPointradius, endPoint.x, endPoint.y, endPoint.z, endPointRadius, fillmode, col);
        }

        public void DrawCone(float x0, float y0, float z0, float r0, float x1, float y1, float z1, float r1, int fillmode, int col)
        {
            if (isInterfaceEXTENDED() == false)
            {
                NotSupportedBridge("DrawCone");
                return;
            }


            var paras = new object[] { x0, y0, z0, r0, x1, y1, z1, r1, fillmode, col };
            _features["DrawCone"].Invoke(_runtime, paras);
        }

        public void ShutdownAdv(int uninitType = 0)
        {
            if (isInterfaceEXTENDED() == false)
            {
                NotSupportedBridge("ShutdownAdv");
                return;
            }

            var paras = new object[] { uninitType };
            _features["ShutdownAdv"].Invoke(_runtime, paras);
        }




        #endregion
    }
}
