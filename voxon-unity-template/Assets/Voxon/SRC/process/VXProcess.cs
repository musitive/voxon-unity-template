//#define DEBUG_VXPROCESS // Debug for internal

using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Voxon.VxLit;

/// <summary>
/// The VxProcess class is the main class that manages the Voxon Runtime and the Volumetric Display.
/// </summary>

namespace Voxon
{
    public enum VXLaunchType
    {
        Standard = 0,       // Standard                         - Run VXApp with all features. Warning VoxieBox.dll is unstable when its using its own sound engine.  
        Unity = 1,          // VxUnity App                      - Run VXApp without the VoxieBox.dll sound engine (Unity VxApps use Unity's sound engine 
        UnityEditor = 3,    // Testing VxUnity App within Unity - Disables Voxon Audio, exclusive mouse, the shutdown button and window buttons  
    };

    public enum VXProcessReportLevel
    {
        None = 0,               // Log no errors
        General = 1,            // Show only important warnings / errors for typical users for the plug in
        Processes = 2,          // General Reports + Show when various processes are called or other logs related to the process of using the plugin
        Debug = 3               // Process Reports + Extra debug information
    };

    public enum VXProcessRecordingType { SINGLE_FRAME, ANIMATION };


    public class VXProcess : Singleton<VXProcess>
    {

#if DEBUG_VXPROCESS
        public bool crashBugIt = false;
        public bool StopItTest = false;
#else
        private bool crashBugIt = false;
        private bool StopItTest = false;
#endif
#region public Vars

        public const string BuildDate = "20250521";
        public const string Version = "0.4.9";


        [HideInInspector]
        public static VXURuntime Runtime;
        [Space(10)]
                        

        [Tooltip("Choose which target platform to build for")]
        [SerializeField]
        public VOXON_RUNTIME_INTERFACE VXInterface = VOXON_RUNTIME_INTERFACE.VXLED;

        [Tooltip("Toggle weather to VXRuntime is activated on play / builds")]
        public bool active = true;

        [Tooltip("If enabled the scene can be loaded as a subscene. This ensures the scene is properly memory managed.")]
        public bool loadAsSubScene = false;

        [Header("Reporting")]
        [SerializeField]
        [Tooltip("Select the level of details that the VxU will log to the Unity Console")]
        public VXProcessReportLevel VXUReportingLevel = VXProcessReportLevel.General;

        [Tooltip("Show VxU and various reports on the Voxon application window")]
        public bool showInfo = true;

        [Header("Volume Capture")]
        [FormerlySerializedAs("_editor_camera")]
        [Tooltip("The virtual volume that gets sent to the volumetrid display.\nUtilizes the GameObject's scale, rotation, and position")]
        [SerializeField]
        private VXCaptureVolume captureVolume;


        [FormerlySerializedAs("_guidelines")]
        [Tooltip("Renders an outline of the capture volume on the volumetric display")]
        public bool showBorder;

        [Header("Performance")]
        [Tooltip("Toggles a fixed framerate for the VxU application")]
        public bool enableFixFrameRate = false;

        [Tooltip("When fixed framerate is enabled set the target framerate when rendering and recording (default is 60 FPS)")]
        [Range(0, 120)]
        public int fixedTargetFrameRate = 60;

        [Header("Logging")]
        [Tooltip("Forwarrds Console messages on the Voxon application window")]
        public bool logUnityConsole = false;

        [Header("Bridge Settings")]
        [Tooltip("Disables the VXU plugin .log file from being generated")]
        public bool disableVXUBridgeLog = false;

        [Tooltip("Suppresses Windows dialog errors spawned from the Voxon application window")]
        public bool suppressAllWinErrors = false;

        //[Header("VoxieBox Settings")]
        [Tooltip("On the Start() of a scene, all GameObjects within the volume will get a VX_Component attached to them.")]
        [HideInInspector]
        public bool addVXComponentsOnStart = true;

        [HideInInspector]
        [Tooltip("Disables the Mesh Register so mesh isn't forwarded onto the Voxon Runtime")]
        public bool bypassMeshRegister = false;

        [Tooltip("Path of captured volume data. Use for static playback")]
        [HideInInspector]
        public string captureExportPath = "C:\\Voxon\\Media\\MyCaptures\\framedata";

        [Tooltip("If enabled, captures recording on project load")]
        [HideInInspector]
        public bool recordOnLoad = false;

        [Tooltip("Capture all VCB into a single zip, or as individual frames")]
        [HideInInspector]
        public VXProcessRecordingType recordingStyle = VXProcessRecordingType.ANIMATION;

        //[Header("VLED Settings")]
        [HideInInspector]
        [Tooltip("Set custom launch flag for VXU Bridge. See VXU_FLAG_IDS enum for reference")]
        public int VXUB_Flags = 0;

        [Tooltip("For slower systems, enable this if when the Unity Editor is losing the Voxon Runtime handle")]
        [HideInInspector]
        public bool keepVXU_Handle = false;

        [Tooltip("If enabled the Mouse will act as a Space Nav")]
        [HideInInspector]
        public bool useMouseAsSpaceNav = false;

        [Tooltip("If enabled VXProcess will save and load VLED display settings (gamma, dithermode, bilnear filter & dithermode)")]
        [HideInInspector]
        public bool PreserveDisplaySettings = true;

        [Tooltip("Set the default window size (in pixels) for the VLED application window")]
        [HideInInspector]
        public point2DInt defaultWindowRes = new point2DInt(1024, 600);


        #endregion

        #region drawables

        public static List<IDrawable> Drawables = new List<IDrawable>();
        public static List<VXGameObject> Gameobjects = new List<VXGameObject>();

        #endregion

#region internal_vars


// Internal things to test
        [Tooltip("When enabled skips all the graphical calls being sent to Volumetric display - to check performance without draw calls")]
        private bool bypassDraws = false;




        private Vector3 normalLighting = new Vector3();
        private bool lightingUpdated = false;

        private int LoggerMaxLines = 10;


        private bool oFixedFrameRate = false;
        private int oTargetFrameRate = 60;
        // Emu Saved positions
        private float[] EmuSimView = new float[3];
        private int[] EmuWindowInfo = new int[4];
        private float VLEDGamma = 0;
        private float VLEDDensity = 0;
        private int VLEDDitherMode = 0;
        private int VLEDTextFilterMode = 0;

        double startTime = 0;

        private TimeSpan DurationOfDrawCalls;
        private Int64 current_frame = 0;
        private VolumetricCamera _camera = new VolumetricCamera();
        static List<string> _logger = new List<string>();
        bool is_closing_VXProcess = false;
        bool is_recording = false;
        private double AvgVPS = 0;
        double HoldBreathTime = 0;
        private int BreathState = 0;
        private bool exclusiveInputMode = false;
        private bool exclusive2DDisplayMode = false;

        #endregion


        #region getters_setters


        public VOXON_RUNTIME_INTERFACE GetCurrentInterfaceType()
        {
            return VXInterface;
        }

        public bool IsClosingVXProcess()
        {
            return is_closing_VXProcess;
        }
        public VXCaptureVolume Camera
        {
            get => _camera?.Camera;
            set => _camera.Camera = value;
        }

        public Matrix4x4 Transform => _camera.Transform;

        public Vector3 EulerAngles
        {
            get => _camera.EulerAngles;
            set => _camera.EulerAngles = value;
        }

        public bool HasChanged => _camera.HasChanged;

        public Vector3 NormalLight
        {
            get => normalLighting;
            set
            {
                lightingUpdated = true;
                normalLighting = value;
            }
        }

        public double GetVPS()
        {
            return AvgVPS;
        }

        #endregion

        #region unity_functions

        /// <summary>
        /// Setting Exclusive InputMode disables the Input Manager which can be 
        /// used for Voxon direct menu behaviours 
        /// </summary>
        /// <param name="value"></param>
        public void SetExclusiveInputMode(bool value)
        {
            exclusiveInputMode = value;
        }

        public bool GetExclusiveInputMode()
        {
            return exclusiveInputMode;
        }

        public void SetExclusive2DMode(bool value)
        {
            exclusiveInputMode = value;
        }

        public bool GetExclusive2DMode()
        {
            return exclusive2DDisplayMode;
        }


        // Function to delay a Voxon Breath update... (incase you want Unity to do something first)
        public void HoldBreath(double time)
        {
            HoldBreathTime = Time.timeAsDouble + time;
        }

        private void Awake()
        {

            FrameRateCheck();


            current_frame = -1; // We haven't started our first frame yet

            if (!loadAsSubScene)
            {
                Drawables.Clear();
                Gameobjects.Clear();
            }
        }

        public void FrameRateCheck()
        {


            if (enableFixFrameRate)
            {
                QualitySettings.vSyncCount = 0;  // VSync must be disabled
                Application.targetFrameRate = (int)fixedTargetFrameRate;
                Time.captureFramerate = (int)fixedTargetFrameRate;
            } else
            {
                QualitySettings.vSyncCount = 0; // VSync settings 0 = off, 1 = once per frame, 2 = every  second frame0 i
                Application.targetFrameRate = -1;
                Time.captureFramerate = 0;
         
            }
        
        }

        public bool TryToFindCamera()
        {
            if (_camera.Camera == null)
            {
                // maybe the editor camera has been set but the scene has lost it?
                if (captureVolume != null)
                {
                    _camera.Camera = captureVolume;
                    return true;
                }

                string scriptNameToSearch = "VXCaptureVolume";

                // Find all GameObjects in the scene
#if UNITY_6000_0_OR_NEWER
                GameObject[] gameObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
#else
                GameObject[] gameObjects = GameObject.FindObjectsOfType<GameObject>();
#endif
                // Iterate through each GameObject
                foreach (GameObject go in gameObjects)
                {
                    // Check if the GameObject has the specified script attached
                    if (go.GetComponent(scriptNameToSearch) != null)
                    {
                        // Set the captureVolume and _camera.Camera
                        captureVolume = go.GetComponent<VXCaptureVolume>();
                        _camera.Camera = captureVolume;

                        // Save the changes perdmenantly... will finish this laper
                        //#if Unity_Editor
                        //                     EditorUtility.SetDirty(this);
                        //                     serializedObject.ApplyModifiedProperties();
                        //#endif
                        return true;
                    }
                }
            }

            return false;

        }


        private IEnumerator CrashBugItCoroutine()
        {
        
                for (int i = 0; i < 100; i++)
                {
                    UnityEngine.Debug.Log($"CrashBugIt Call {i}");
                    Runtime.Shutdown();
                    Runtime.Unload();
                    Runtime.Load();
                    Runtime.Initialise(1);

                    // Wait for 0.05 seconds before continuing to the next iteration
                    yield return new WaitForSeconds(0.05f);
                }

                active = false;
                CloseVXProcess();
            
        }

        public void Start()
        {
         


            if (loadAsSubScene)
            {
                Destroy(gameObject);
            }



            BreathState = 0;


#if UNITY_EDITOR
            // Enables VXProcess logging only for editor. 
            PlayerPrefs.SetInt("Voxon_VXProcessReportingLevel", (int)VXUReportingLevel);
            // Load in the player refs
            if (VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
            {
                if (keepVXU_Handle)
                {
                    VXUB_Flags |= (1 << (int)VXU_FLAG_IDS.VXUB_FLAG_DONTFREE_HANDLE_ON_EXIT);
                }
            }

#else
            PlayerPrefs.SetInt("Voxon_VXProcessReportingLevel", 0 );
#endif
            if (VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
            {
                if (disableVXUBridgeLog)
                {
                    VXUB_Flags |= (1 << (int)VXU_FLAG_IDS.VXUB_FLAG_DISABLE_LOGGING);
                }
                bypassMeshRegister = true;
                addVXComponentsOnStart = false;

                // Load in the user's window and emu settings  
                EmuWindowInfo[0] = PlayerPrefs.GetInt("Voxon_EmuWindowPosX", 100);
                EmuWindowInfo[1] = PlayerPrefs.GetInt("Voxon_EmuWindowPosY", 100);
                EmuWindowInfo[2] = PlayerPrefs.GetInt("Voxon_EmuWindowResX", defaultWindowRes.x);
                EmuWindowInfo[3] = PlayerPrefs.GetInt("Voxon_EmuWindowResY", defaultWindowRes.y);

                //emuhang = 0.f, emuvang = -.5f, emudist = 2.5f;
                EmuSimView[0] = PlayerPrefs.GetFloat("Voxon_EmuHAng", 0);
                EmuSimView[1] = PlayerPrefs.GetFloat("Voxon_EmuVAng", -.33f);
                EmuSimView[2] = PlayerPrefs.GetFloat("Voxon_EmuDist", 6.75f);

                if (PreserveDisplaySettings)
                {
                    VLEDGamma = PlayerPrefs.GetFloat("Voxon_EmuGamma", 2f);
                    VLEDDensity = PlayerPrefs.GetFloat("Voxon_EmuDensity", 64);
                    VLEDDitherMode = PlayerPrefs.GetInt("Voxon_EmuDitherMode", 0);
                    VLEDTextFilterMode = PlayerPrefs.GetInt("Voxon_EmuTextFilterMode", 0);
                }

                if (EmuWindowInfo[0] == 0 && EmuWindowInfo[1] == 0 && EmuWindowInfo[2] == 0 && EmuWindowInfo[3] == 0)
                {
                    EmuWindowInfo[0] = 100;
                    EmuWindowInfo[1] = 100;
                    EmuWindowInfo[2] = defaultWindowRes.x;
                    EmuWindowInfo[3] = defaultWindowRes.y;
                }
                if (EmuSimView[0] == 0 && EmuSimView[1] == 0 && EmuSimView[2] == 0)
                {
                    EmuSimView[0] = 0;
                    EmuSimView[1] = -.33f;
                    EmuSimView[2] = 6.75f;

                }
                if (VLEDGamma == 0)
                    VLEDGamma = 2f;
                if (VLEDDensity == 0)
                    VLEDGamma = 64;

                if ((int)VXUReportingLevel >= (int)VXProcessReportLevel.Processes)
                    UnityEngine.Debug.Log("VXProcess.cs - Emu and Window Settings loaded from player prefs");

#if UNITY_EDITOR

#else
                if (PlayerPrefs.GetInt("Voxon_Show_Info", 0) == 0)
                {
                    showInfo = false;
                }  else
                {
                    showInfo = true;
                }
#endif


            }

            if (logUnityConsole)
            {
                Application.logMessageReceived += HandleLog;
            }

            Camera = captureVolume;

            startTime = Time.timeAsDouble;


            // Should VX Load?
            if (!active)
            {
                return;
            }
            else if (_camera.Camera == null)
            {
                if ((int)VXUReportingLevel >= (int)VXProcessReportLevel.General) UnityEngine.Debug.LogWarning("VXProcess - Capture Volume assigned!, attempting to find one in the scene. Edit the VXProcess settings and add a volume");

                if (TryToFindCamera() == false)
                {
                    UnityEngine.Debug.LogError("No Capture Volume Assigned - Please attach a VX Camera in Process Manager (Voxon -> Process)");

                    active = false;
                    return;
                }
            }
      

            LoadRuntime();

        }

        // Function to load in the VXU Runtime 
        private void LoadRuntime()
        {

            if (Runtime == null)
            {
                Runtime = new Voxon.VXURuntime(VXInterface, VXUB_Flags);
            } else
            {
                if (  Runtime.isLoaded() == false || Runtime.isInitialised() == false || active == false )
                {
                    UnityEngine.Debug.LogError($"VXProcess - LoadRuntime() called. VXRuntime was not null but wasn't not fully active... will complete setup. Status was isLoaded(){Runtime.isLoaded()} isInited?{Runtime.isInitialised()} isActive?{active}");

                }

                if ((int)VXUReportingLevel >= (int)VXProcessReportLevel.Processes) UnityEngine.Debug.Log($"VXProcess - LoadRuntime() called. VXRuntime DLL is already loaded (Possible new scene switch). Current Status is isLoaded(){Runtime.isLoaded()} isInited?{Runtime.isInitialised()} isActive?{active}");
                // Runtime has been loaded checking status
            }

            int LaunchVXAppType = (int)VXLaunchType.Unity;

            // Load DLL
            // Runtime Load Check

            if (!Runtime.isLoaded())
            {
              
                try
                {
                    

                    string rumtimeResult = Runtime.Load();
                    if ((int)VXUReportingLevel >= (int)VXProcessReportLevel.Processes) UnityEngine.Debug.Log($"Accessing Voxon Runtime:{rumtimeResult}");


                }
                catch (Exception e)
                {

                 
                    UnityEngine.Debug.LogError($"VXProcess - Couldn't access VXURuntime DLL (Voxiebox.dll or LedHost.dll) DLL(s) missing or are different to what VXURuntime expects. Exception Message: {e}");
                    CloseVXProcess();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else 

                    Windows.Error($"VXProcess - Couldn't access VXURuntime DLL (Voxiebox.dll or LedHost.dll) DLL(s) missing or are different to what VXURuntime expects. Exception Message: {e}");

#endif
                    return;
                }

#if UNITY_EDITOR
                LaunchVXAppType = (int)VXLaunchType.UnityEditor;
#endif
            }
            
            // Runtime  
            if (!Runtime.isLoaded())
            {
                UnityEngine.Debug.LogError($"VXProcess - Couldn't load VXURuntime DLL (Voxiebox.dll or LedHost.dll) DLL(s) missing or are different to what VXURuntime expects.");
                CloseVXProcess();
                return;
            }

            // Initalise
            if (Runtime.isInitialised() == false)
            {
                try
                {
                    if (VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
                    {
                        Runtime.SetProgramName($"{Application.companyName} {Application.productName} v{Application.version}");
                    }
                    Runtime.Initialise(LaunchVXAppType);
                }
                catch (Exception e)
                {


                    UnityEngine.Debug.LogError($"VXProcess - Couldn't Initialise VXURuntime DLL (Voxiebox.dll or LedHost.dll). Exception Message: {e}");
                    CloseVXProcess();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                    return;
                }
            }

            if (Runtime.isInitialised() == false)
            {

                UnityEngine.Debug.LogError("VXProcess - VXURuntime failed to initalise.");
                CloseVXProcess();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
                return;
            }

  

            if (active)
            {
                // VXProcess -> RuntimeRead STARTUP
                // Load Window Settings 
                if (VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
                {

                    Runtime.SetEmuWindowPos(EmuWindowInfo[0], EmuWindowInfo[1]);
                    Runtime.SetEmuWindowRes(EmuWindowInfo[2], EmuWindowInfo[3]);

                    Runtime.SetEmulatorHorizontalAngle(EmuSimView[0]);
                    Runtime.SetEmulatorVerticalAngle(EmuSimView[1]);
                    Runtime.SetEmulatorDistance(EmuSimView[2]);
                    if (PreserveDisplaySettings)
                    {
                        Runtime.SetDitherMode(VLEDDitherMode);
                        Runtime.SetTextureFilterMode(VLEDTextFilterMode);
                        Runtime.SetGamma(VLEDGamma);
                        Runtime.SetDensity(VLEDDensity);

                    }

                    if ((int)VXUReportingLevel >= (int)VXProcessReportLevel.Processes)
                            UnityEngine.Debug.Log("VXProcess - Loaded Window Emu Settings");
                }

          


                if (addVXComponentsOnStart)
                {
                    // Load all existing drawable components
#if UNITY_6000_0_OR_NEWER
                    Renderer[] pack = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
#else
                    Renderer[] pack = FindObjectsOfType<Renderer>();
#endif
                    foreach (Renderer piece in pack)
                    {
                        if (piece.gameObject.GetComponent<ParticleSystem>())
                        {

                        }
                        else if (piece.gameObject.GetComponent<LineRenderer>() && !piece.gameObject.GetComponent<Line>())
                        {
                            piece.gameObject.AddComponent<Line>();
                        }
                        else
                        {
                            GameObject parent = piece.transform.root.gameObject;
                            if (!parent.GetComponent<VXGameObject>())
                            {
                                Gameobjects.Add(parent.AddComponent<VXGameObject>());
                            }
                        }

                    }
                }

                if (recordOnLoad)
                {
                    is_recording = true;
                    if (recordingStyle == VXProcessRecordingType.ANIMATION)
                    {
                        Voxon.VXProcess.Runtime.StartRecording(captureExportPath, (int)fixedTargetFrameRate);
                    }
                }

            }

            active = true;

            if (StopItTest)
            {
                active = false;
                CloseVXProcess();
                return;
            }

            if (crashBugIt)
            {
                StartCoroutine(CrashBugItCoroutine());
            }

        }

        private void Update()
        {
          


        }

        void LateUpdate()
        {

            if (HoldBreathTime > Time.timeAsDouble)
            {
                if ((int)VXUReportingLevel >= (int)VXProcessReportLevel.Processes)  UnityEngine.Debug.Log("VxProcess - Holding Breath");
                return;
            }

            if (!active || Runtime == null || HoldBreathTime > Time.timeAsDouble)
            {
                return;
            }



            if (oFixedFrameRate != enableFixFrameRate || fixedTargetFrameRate != oTargetFrameRate)
                FrameRateCheck();
            oFixedFrameRate = enableFixFrameRate;
            oTargetFrameRate = fixedTargetFrameRate;

            startTime = Time.timeAsDouble;


            current_frame++;
            BreathState = -2;
            BreathState = Runtime.FrameStart();

            // VX quit command; TODO this should be by choice
            if (Runtime.GetKey(0x1) || BreathState != 0)
            {
                QuitPressed();
                return;
            }

            // A camera must always be active while in process
            if (_camera != null && _camera.Camera == null)
            {
                if ((int)VXUReportingLevel >= (int)VXProcessReportLevel.General) UnityEngine.Debug.LogWarning("VxProcess - No Active VXVolumeCapture / Volumetric Camera has been set the VXProcess! - attempting to find one in the scene.");
                if (TryToFindCamera() == false)
                {
                    active = false;
                    this.CloseVXProcess();
                    return;
                }
            }

            if (showBorder)
                Runtime.DrawGuidelines();

            if ((showInfo && Time.timeScale != 0 && exclusive2DDisplayMode == false) || VXProcess.Instance.VXInterface != VOXON_RUNTIME_INTERFACE.VXLED)
            {

                int showInfoXPos = 10;
                int showInfoYPos = 650;
            

                if (VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
                {
                    int[] window = VXProcess.Runtime.GetEmuWindowInfo();

                    showInfoXPos = window[2] - 375;
                    showInfoYPos = window[3] - 60;
                }


                AvgVPS += (Time.unscaledDeltaTime - AvgVPS) * .1;
                Runtime.LogToScreenExt(showInfoXPos, showInfoYPos, 0x00ff80, -1, $"{Application.companyName} {Application.productName} v{Application.version}");


                Runtime.LogToScreenExt(showInfoXPos, showInfoYPos + 10, 0x0040ff, -1, $"                                {VXProcess.Instance.VXInterface.ToString()}");
                Runtime.LogToScreenExt(showInfoXPos, showInfoYPos + 10, 0xff4000, -1, $"Voxon X Unity Plugin {VXProcess.Version}                     VPS: ");
                Runtime.LogToScreenExt(showInfoXPos, showInfoYPos + 10, 0xffffff, -1, $"                                                       {(1 / AvgVPS):F2}");

                Runtime.LogToScreenExt(showInfoXPos, showInfoYPos + 20, 0xffffff, -1, $"VxU Bridge Version:  ");
                Runtime.LogToScreenExt(showInfoXPos, showInfoYPos + 20, 0xffff00, -1, $"                             {VXProcess.Runtime.GetSDKVersion()} ");
    
                Runtime.LogToScreenExt(showInfoXPos, showInfoYPos + 30, 0x00ffff, -1, $"Compatible with Unity versions ");
                Runtime.LogToScreenExt(showInfoXPos, showInfoYPos + 30, 0xffff00, -1, $"                                                     >= 2020");

                Runtime.LogToScreenExt(showInfoXPos, showInfoYPos + 40, 0xffff00, -1, $"             {Drawables.Count}               {Gameobjects.Count}              {DurationOfDrawCalls}");
                Runtime.LogToScreenExt(showInfoXPos, showInfoYPos + 40, 0xff00ff, -1, $"IDrawables:     GameObjects:     DrawTime:");

            }

            // TODO if Loaded Camera Animation - > Set Camera Transform
            _camera?.LoadCameraAnim();

            if (_camera != null && _camera.HasChanged)
            {
                _camera?.ForceUpdate();
            }

            if (bypassDraws == false)
            {
                // TODO If Loaded Capture Playback -> Set Capture Frame Else Draw
                Draw();
            }

            // TODO Save Camera Pos
            _camera?.SaveCameraAnim();
            // TODO Save Frame
            if (is_recording && recordingStyle == VXProcessRecordingType.SINGLE_FRAME)
            {
                Runtime.GetVCB(captureExportPath, (int)fixedTargetFrameRate);
            }

            _camera?.ClearUpdated();

            AudioListener.volume = Runtime.GetVolume();

            if (VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
            {
                Runtime.SetMouseToNav(useMouseAsSpaceNav, 0);
            }
            Runtime.FrameEnd();




        }


        public void QuitPressed()
        {
            if ((int)VXUReportingLevel >= (int)VXProcessReportLevel.Processes) UnityEngine.Debug.Log("VX Process - Escape Button Pressed");

            CloseVXProcess();

            return;
        }


        private IEnumerator CloseVxProcessAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            CloseVXProcess();
        }

        /// Summary ///
        /* Running this function closes the VXProcess. 
         * Having a single function to handle unloading the DLL makes it easier to manage 

         */
        public void CloseVXProcess()
        {
            if ((int)VXUReportingLevel >= (int)VXProcessReportLevel.Processes) UnityEngine.Debug.Log($"VXProcess CloseVXProcess Called isClosingVXProcess?={is_closing_VXProcess}");

            if (active == true && Runtime != null)
            {
                Runtime.FrameEnd();
            }
            if (is_closing_VXProcess == false )
            { 
               
              
                if (BreathState != 0)
                {
                    string errorReason = "";

                    /// Error Codes:
                    /// - -2: LateUpdate Started
                    /// - -1: Esc Key Pressed
                    /// - 0: No issue.
                    /// - 1: Runtime is not active.
                    /// - 2: Failed during ledWin.Breath() call.
                    /// - 3: `hasBreath` from ledWin returned -1.


                    switch (BreathState)
                    {
                        default:
                            errorReason = "Unknown Error";
                            break;
                        case -2:
                            errorReason = "VXUProcess.LateUpdate() Started but didn't get a response from FrameStart()";
                            // likey attempted to quit before the system could finish initalising so just wait a 1/5 of a second and try to exit again 
                            StartCoroutine(CloseVxProcessAfterDelay(0.2f));
                            return;

                        case -1:
                            errorReason = "BreathState initalised on Start() or Quit Key was registered but then never used changed";
                            break;
                        case 0:
                            errorReason = "No issue";
                            break;
                        case 1:
                            errorReason = "VXURuntime.cs is not active - DLLs couldn't be found or VXProcess.Runtime was null?";
                            break;
                        case 2:
                            errorReason = "Runtime.FrameStart() failed during the ledWin.Breath() call";
                            break;
                        case 3:
                            errorReason = "'hasBreath' from ledwin returned -1. Meaning it was busy or inaccessible (LedWinCS Handle might be null)";
                            break;

                    }


                    UnityEngine.Debug.LogWarning($"VXU Runtime.FrameStart() Failed! BreathState={BreathState} Reason={errorReason} IsVxURuntime isInitailised? {Runtime.isInitialised()} {Runtime.isLoaded()} {Runtime.ActiveRuntime}\nYou can run running your project with Dont_Free_VXUB_Handle_On_Exit enabled in the VXProcess settings");
                }

                // Save current view to player prefs -- TODO put these in a .ini 
                if (active == true && Runtime != null)
                {

                    if (VXInterface == VOXON_RUNTIME_INTERFACE.VXLED && Runtime.isInitialised() == true)
                    {



                        EmuWindowInfo = Runtime.GetEmuWindowInfo();
                        if (EmuWindowInfo[2] != 0 && EmuWindowInfo[3] != 0) {
                            PlayerPrefs.SetInt("Voxon_EmuWindowPosX", EmuWindowInfo[0]);
                            PlayerPrefs.SetInt("Voxon_EmuWindowPosY", EmuWindowInfo[1]);
                            PlayerPrefs.SetInt("Voxon_EmuWindowResX", EmuWindowInfo[2]);
                            PlayerPrefs.SetInt("Voxon_EmuWindowResY", EmuWindowInfo[3]);

                            EmuSimView[0] = Runtime.GetEmulatorHorizontalAngle();
                            EmuSimView[1] = Runtime.GetEmulatorVerticalAngle();
                            EmuSimView[2] = Runtime.GetEmulatorDistance();

                            PlayerPrefs.SetFloat("Voxon_EmuHAng", EmuSimView[0]);
                            PlayerPrefs.SetFloat("Voxon_EmuVAng", EmuSimView[1]);
                            PlayerPrefs.SetFloat("Voxon_EmuDist", EmuSimView[2]);
                        }

                        if (PreserveDisplaySettings == true)
                        {

                            VLEDGamma = Runtime.GetGamma();
                            PlayerPrefs.SetFloat("Voxon_EmuGamma", VLEDGamma);
                            VLEDDensity = Runtime.GetDensity();
                            PlayerPrefs.SetFloat("Voxon_EmuDensity", VLEDDensity);
                            VLEDDitherMode = Runtime.GetDitherMode();
                            PlayerPrefs.SetInt("Voxon_EmuDitherMode", VLEDDitherMode);
                            VLEDTextFilterMode = Runtime.GetTextureFilterMode();
                            PlayerPrefs.SetInt("Voxon_EmuTextFilterMode", VLEDDitherMode);


                        }

#if UNITY_EDITOR

#else
                    PlayerPrefs.SetInt("Voxon_Show_Info", showInfo == true ? 1 : 0 );
#endif
                        if ((int)VXUReportingLevel >= (int)VXProcessReportLevel.Processes)
                            UnityEngine.Debug.Log("VXProcess.cs - Emu and Window Seetings saved in player prefs");
                    }


                    if (Runtime.isInitialised() == false) return;
                }
                if ((int)VXUReportingLevel >= (int)VXProcessReportLevel.Processes) UnityEngine.Debug.Log("VXProcess.cs - CloseVXProcess() called, VXProcess and Runtime shutting down.");
                is_closing_VXProcess = true;
                HoldBreathTime = Time.timeAsDouble + 10;

                // Unloading the DLL
                if (is_closing_VXProcess == false)
                {
                    if ((int)VXUReportingLevel >= (int)VXProcessReportLevel.Debug) UnityEngine.Debug.LogWarning("VXProcess.cs - CloseRuntime() was called outside of calling CloseVXProcess() this could make the system unstable call CloseVXProcess() first");
                    return;
                }

                if (Runtime != null)
                {
                    if (is_recording && recordingStyle == VXProcessRecordingType.ANIMATION)
                    {
                        is_recording = false;
                        Runtime.EndRecording();
                    }

                    Runtime.Shutdown();

                    try
                    {
                        Runtime.Unload();
                    }
                    catch
                    {
                        if ((int)VXUReportingLevel >= (int)VXProcessReportLevel.Debug) UnityEngine.Debug.Log("VXRuntime wasn't initialized, no need to unload it.");
                    }
                }
                this.active = false;
                if (_camera.Camera != null)
                {
                    _camera?.Camera.CloseAnimator();
                }
               
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
                

#elif UNITY_WEBPLAYER
				Application.OpenURL(webplayerQuitURL);
#else
                Application.Quit();
#endif
            }
        }

        private new void OnApplicationQuit()
        {
            /*
            UnityEngine.Debug.Log("APP QUIT CALLED");

            
            if(Runtime != null && Runtime.isInitialised() == true) {
                UnityEngine.Debug.Log("CLOSE VX FROM ON APP QUIT");
                CloseVXProcess();
            }
            */

            base.OnApplicationQuit();

        }
#endregion

#region drawing
        private void Draw()
        {
            if (lightingUpdated)
            {
               // if ((int)VXProcessReportingLevel >= (int)VXProcessReportLevel.Processes) UnityEngine.Debug.Log($"VXRuntime normal lighting updated: {normalLighting}");
                Runtime.SetNormalLighting(normalLighting.x, normalLighting.y, normalLighting.z);
                lightingUpdated = false;

            }

            Stopwatch stopwatch = null; 

            if (showInfo)
            {
             stopwatch = new Stopwatch();

             stopwatch.Start();
            }
            foreach (IDrawable go in Drawables)
            {
                go.Draw();
            }

            if (VXProcess.Instance.GetExclusive2DMode() == true) return;
          
            if (showInfo && stopwatch != null)
            {
                stopwatch.Stop();
                DurationOfDrawCalls = stopwatch.Elapsed;
                stopwatch.Reset();
            }

            while (_logger.Count > LoggerMaxLines)
            {
                _logger.RemoveAt(0);
            }

            for (var idx = 0; idx < _logger.Count; idx++)
            {
                Runtime.LogToScreen(10, 110 + (idx * 8), _logger[idx]);
            }


        }

        public static void add_log_line(string str)
        {
            _logger.Add(str);
        }
#endregion

#region computing_transforms

        /// <summary>
        /// Used to convert Unity and Voxon spaces... As Voxon uses a different 3D Coorinate system the difference beween Unity Y = Voxon -Z and Unity Z = Voxon -Y
        /// </summary>
        /// <param name="targetWorld"></param>
        /// <param name="vertices"></param>
        /// <param name="outPoltex"></param>
        public static void ComputeTransform(ref Matrix4x4 targetWorld, ref Vector3[] vertices, ref point3d[] outPoltex)
        {
            if (vertices.Length != outPoltex.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(outPoltex));
            }

            if (Instance == null) return;

            // Build Camera transform
            Matrix4x4 matrix = Instance.Transform * targetWorld;

            for (int idx = vertices.Length - 1; idx >= 0; --idx)
            {

                var inV = new Vector4(vertices[idx].x, vertices[idx].y, vertices[idx].z, 1.0f);

                inV = matrix * inV;

                outPoltex[idx].x = inV.x;
                outPoltex[idx].y = -inV.z;
                outPoltex[idx].z = -inV.y;
            }
        }

        public static void ComputeInverseTransform(ref Matrix4x4 targetWorld, ref point3d[] inPoltex, ref Vector3[] outVertices)
        {
            if (inPoltex.Length != outVertices.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(outVertices));
            }

            if (Instance == null) return;

            // Compute the inverse transformation
            Matrix4x4 inverseMatrix = (Instance.Transform * targetWorld).inverse;

            for (int idx = inPoltex.Length - 1; idx >= 0; --idx)
            {
                var inP = new Vector4(inPoltex[idx].x, -inPoltex[idx].z, -inPoltex[idx].y, 1.0f);

                // Apply inverse transformation
                inP = inverseMatrix * inP;

                outVertices[idx] = new Vector3(inP.x, inP.y, inP.z);
            }
        }


        private static void ComputeTransform(ref Matrix4x4 targetWorld, ref Vector3[] vertices, ref Vector2[] uvs, ref poltex[] outPoltex)
        {
            if (vertices.Length != outPoltex.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(outPoltex));
            }

            // Build Camera transform
            Matrix4x4 matrix = Instance.Transform * targetWorld;

            for (int idx = vertices.Length - 1; idx >= 0; --idx)
            {
                var inV = new Vector4(vertices[idx].x, vertices[idx].y, vertices[idx].z, 1.0f);

                inV = matrix * inV;

                outPoltex[idx].x = inV.x;
                outPoltex[idx].y = -inV.z;
                outPoltex[idx].z = -inV.y;
                outPoltex[idx].u = uvs[idx].x;
                outPoltex[idx].v = uvs[idx].y;
            }
        }

        public static void ComputeInverseTransform(ref Matrix4x4 targetWorld, ref poltex[] inPoltex, ref Vector3[] outVertices)
        {
            if (inPoltex.Length != outVertices.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(outVertices));
            }

            if (Instance == null) return;

            // Compute the inverse transformation
            Matrix4x4 inverseMatrix = (Instance.Transform * targetWorld).inverse;

            for (int idx = inPoltex.Length - 1; idx >= 0; --idx)
            {
                var inP = new Vector4(inPoltex[idx].x, -inPoltex[idx].z, -inPoltex[idx].y, 1.0f);

                // Apply inverse transformation
                inP = inverseMatrix * inP;

                outVertices[idx] = new Vector3(inP.x, inP.y, inP.z);
            }
        }





        public static void ComputeTransform(ref Matrix4x4 target, ref Vector3[] vertices, ref poltex[] outPoltex)
        {
            var uvs = new Vector2[vertices.Length];

            ComputeTransform(ref target, ref vertices, ref uvs, ref outPoltex);
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            add_log_line(logString);
        }
        #endregion
  

    }
}
