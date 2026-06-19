using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;
using Voxon;

/// <summary>
/// The VxHardwareController manages the interaction with 
/// the Voxon VLED Hardware including the starting of motor.
/// Also includes reporting 
/// </summary>



public class VxHardwareController : MonoBehaviour, IDrawable
{

    [Space(10)]
    [Header("Startup")]
    [Space(5)]

    public bool autoStartMotor = false;

    private bool hasAutoStarted = false;
    private int currentMotorSpeed = 0;
    private bool currentMotorSpeedSet = false;
    private bool motorIsOff = true;

    [Header("Flags")]
    [Space(5)]

    public bool setDebugFlag = false;
    [Tooltip("Only Render volumetric output on the 2D Window while the device isn't operating")]
    public bool setExclusiveLEDFlag = false;

    private bool setFlags = false;
    [Space(10)]

    [Header("Menu Options")]
    [Tooltip("If enabled the menu with toggle on and off when the button is pressed; otherwise hold the menu button to use an option")]
    [Space(5)]
    public bool latchMenu = false;
    public VX_KEYS VLEDMenuButton = VX_KEYS.KB_Back_Slash;
    private point2DInt MenuPos = new point2DInt(10, 10);

    [Space(10)]
    [Header("Show Reports On ShowInfo")]
    [Space(5)]
    //    public bool reportVPS = true;
    public bool reportVXLState = true;
    public bool reportNav = true;
    public bool reportJoy = true;
    public bool reportMouse = true;
    public bool reportCaptureVolume = true;
    public bool reportLitShader = true;
    public bool reportCSInterface = false;
    [Space(10)]

    [Header("Export Ply")]

    [Space(5)]
    public string exportfilepath = ""; // leave blank to export to where the build is... //"C:\\Voxon\\";
    public string exportfilename = "UnityCapture";

    int exportNum = 0;
    private string exportStr;
    private string fileExt = ".ply";

    private tiletype nullTT = new tiletype();

    private double drawFrontTime = 0;

    private bool ExclusiveMode = true;
    private bool _VxHCHasTurnedOnEM = false;
    private double ExclusiveModeOffDelay = 0;

    private int forceMotorSpeed = -1;
    private float forceGamma = -1;
    private float forceDensity = -1;
    private int forceTextureFilter = -1;
    private int forceDitherMode = -1;
    private int forceWindowPosX = -1;
    private int forceWindowPosY = -1;
    private int forceWindowResY = -1;
    private int forceWindowResX = -1;
    private int forceBorder = -1;
    private bool forceSettings = false;


    private struct VxLitSettings_t
    {
        public Vector3 asp;
        public Vector3 pos;
        public int res;
        public bool o;

    };

    private VxLitSettings_t VxLitSettings;


    private Voxon.VxLit.VxLitRendererV4 VxLit = null;
    private int exportCol = 0xffaa00;
    private int txtCol = 0xffaa00;
    private bool drawMenu = false;
    private bool menuOn = false;
    private enum LitShaderAdjust
    {
        Adjust_Offset = 0,
        Adjust_Aspect = 1,
    };

    private LitShaderAdjust LitAdjustment = LitShaderAdjust.Adjust_Aspect;


    public void ProcessArgs()
    {
        string[] args = Environment.GetCommandLineArgs();

        foreach (string arg in args)
        {
            if (arg.ToLower().StartsWith("-forcerpm="))
            {
                forceMotorSpeed = int.TryParse(arg.Substring("-forcerpm=".Length), out int result) ? result : -1;
                autoStartMotor = true;
                currentMotorSpeed = forceMotorSpeed;
            }
            else if (arg.ToLower().StartsWith("-exclusiveled"))
            {
                setExclusiveLEDFlag = true;
            }
            else if (arg.ToLower().StartsWith("-forcegamma"))
            {
                forceGamma = float.TryParse(arg.Substring("-forcegamma=".Length), out float result) ? result : -1;
                forceSettings = true;
            }
            else if (arg.ToLower().StartsWith("-forcedensity="))
            {
                forceDensity = float.TryParse(arg.Substring("-forcedensity=".Length), out float result) ? result : -1;
                forceSettings = true;
            }
            else if (arg.ToLower().StartsWith("-forcetexturefilter="))
            {
                forceTextureFilter = int.TryParse(arg.Substring("-forcetexturefilter=".Length), out int result) ? result : -1;
                forceSettings = true;
            }
            else if (arg.ToLower().StartsWith("-forcedithermode="))
            {
                forceDitherMode = int.TryParse(arg.Substring("-forcedithermode=".Length), out int result) ? result : -1;
                forceSettings = true;
            }
            else if (arg.ToLower().StartsWith("-forcewindowresx="))
            {
                forceWindowResX = int.TryParse(arg.Substring("-forcewindowresx=".Length), out int result) ? result : -1;
                forceSettings = true;
            }
            else if (arg.ToLower().StartsWith("-forcewindowresy="))
            {
                forceWindowResY = int.TryParse(arg.Substring("-forcewindowresy=".Length), out int result) ? result : -1;
                forceSettings = true;
            }
            else if (arg.ToLower().StartsWith("-forcewindowposx="))
            {
                forceWindowPosX = int.TryParse(arg.Substring("-forcewindowposx=".Length), out int result) ? result : -1;
                forceSettings = true;
            }
            else if (arg.ToLower().StartsWith("-forcewindowposy="))
            {
                forceWindowPosY = int.TryParse(arg.Substring("-forcewindowposy=".Length), out int result) ? result : -1;
                forceSettings = true;
            }
            else if (arg.ToLower().StartsWith("-forceborder="))
            {
                forceBorder = int.TryParse(arg.Substring("-forceborder=".Length), out int result) ? result : -1;
                forceSettings = true;
            }

        }

    }

    public void Start()
    {

#if UNITY_6000_0_OR_NEWER
        VxHardwareController[] controllers = UnityEngine.Object.FindObjectsByType<VxHardwareController>(FindObjectsSortMode.None);
#else
        // Find all instances of VxHardwareController in the scene
        VxHardwareController[] controllers = FindObjectsOfType<VxHardwareController>();
#endif

        ProcessArgs();


        // If more than one instance is found, destroy the additional instances
        if (controllers.Length > 1)
        {
            Debug.LogWarning("Multiple instances of VxHardwareController found. Please check your Unity Scene and remove additional VXHardwareController instances. For now destroying duplicates... ");
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != this)
                {
                    Destroy(controllers[i].gameObject);
                }
            }
        }

        // Add this 

        // Add this script to the VXProcess drawables...
        VXProcess.Drawables.Add(this);



        if (VXProcess.Instance.active == false || VXProcess.Runtime == null || VXProcess.Instance.VXInterface != VOXON_RUNTIME_INTERFACE.VXLED || VXProcess.Instance.IsClosingVXProcess() == true)
        {
            return;
        }






    }

    IEnumerator AutoStartMotor()
    {

        yield return new WaitForSeconds(2);

        if (forceMotorSpeed > 0)
        {
            currentMotorSpeed = forceMotorSpeed;
            forceMotorSpeed = -1;
        }
        else
        {
            currentMotorSpeed = Math.Abs(VXProcess.Runtime.GetRPMValue());
        }

        currentMotorSpeedSet = true;
        if ((int)VXProcess.Instance.VXUReportingLevel >= (int)VXProcessReportLevel.Processes) Debug.Log($"Auto Starting Motor {currentMotorSpeed}");
        VXProcess.Runtime.SetRPMValue(currentMotorSpeed);
        VXProcess.Runtime.SetDisplay3D();
        motorIsOff = false;

    }

    IEnumerator ResetExportCol()
    {

        // Wait for 1 second
        yield return new WaitForSeconds(2);

        exportCol = txtCol;
    }

    private void UpdateFlags()
    {
        if (setDebugFlag)
        {
            VXProcess.Runtime.SetFlag(1, (int)LW_FLAGS.LW_FLAG_DEBUG);
        }

        if (setExclusiveLEDFlag)
        {
            VXProcess.Runtime.SetFlag(1, (int)LW_FLAGS.LW_FLAG_EXCLUSIVE_LED);
        }

    }

    public void DrawMenu()
    {
        int bgCol = 0x003333;

        int x = MenuPos.x;
        int y = MenuPos.y;
        int h = 90;
        int w = 600;

        VXProcess.Runtime.ScreenDrawRectangleFill(x, y, x + w, y + h, bgCol);

        x += 2;
        /*
        y += 30;
        VXProcess.Runtime.LogToScreenExt(x, y, 0x0000ff, -1, $"/ /\\//\\");
        VXProcess.Runtime.LogToScreenExt(x, y + 9, 0x00ff00, -1, $"\\_\\/ \\/");
        VXProcess.Runtime.LogToScreenExt(x, y + 18, 0xff0000, -1, $" / /\\    ");
        VXProcess.Runtime.LogToScreenExt(x, y + 27, 0xffff00, -1, $" \\_\\/    ");
        VXProcess.Runtime.LogToScreenExt(x, y + 37, 0xff00ff, -1, $"  VLED");
        VXProcess.Runtime.LogToScreenExt(x, y + 47, 0x00ffff, -1, $"  menu");
        y -= 30;
        */
        VXProcess.Runtime.LogToScreenExt(x, y, 0x00ff00, -1, $" V");
        VXProcess.Runtime.LogToScreenExt(x, y + 10, 0x00ff00, -1, $" L");
        VXProcess.Runtime.LogToScreenExt(x, y + 20, 0x00ff00, -1, $" E");
        VXProcess.Runtime.LogToScreenExt(x, y + 30, 0x00ff00, -1, $" D");

        VXProcess.Runtime.LogToScreenExt(x, y + 50, 0x00ff00, -1, $" M");
        VXProcess.Runtime.LogToScreenExt(x, y + 60, 0x00ff00, -1, $" E");
        VXProcess.Runtime.LogToScreenExt(x, y + 70, 0x00ff00, -1, $" N");
        VXProcess.Runtime.LogToScreenExt(x, y + 80, 0x00ff00, -1, $" U");
        x += 48;
        y += 2;


        // Hardware
        VXProcess.Runtime.LogToScreenExt(x - 5, y, 0xffffff, -1, $"Hardware"); y += 10;

        VXProcess.Runtime.LogToScreenExt(x, y, motorIsOff ? 0xFFFF00 : 0x00FF00, -1, $"Motor On/Off = M | STOP MOTOR = BackSpace | Adjust Speed: J / K :: {currentMotorSpeed} :: isMotorOn?: {!motorIsOff}"); y += 10;
        VXProcess.Runtime.LogToScreenExt(x, y, txtCol, -1, $"Adjust Rotational Position = <,> | ({VXProcess.Runtime.GetCentralRotationOffset()}) | Pause Frame = P | Show Info = I"); y += 10;

        // Display
        VXProcess.Runtime.LogToScreenExt(x - 5, y, 0xffffff, -1, $"Display"); y += 10;

        VXProcess.Runtime.LogToScreenExt(x, y, txtCol, -1, $"Adjust : Gamma = G,H |     | Dither Thres. = T,Y |      | ");
        VXProcess.Runtime.LogToScreenExt(x, y, 0xffffff, -1, $"                       {VXProcess.Runtime.GetGamma():F1}                         {VXProcess.Runtime.GetDensity()}");
        VXProcess.Runtime.LogToScreenExt(x, y, exportCol, -1, $"                                                          Export = O to {exportStr}"); y += 10;
        VXProcess.Runtime.LogToScreenExt(x, y, txtCol, -1, $"Toggle : DitherMode{VXProcess.Runtime.GetDitherMode()} = U, Texture Filter{VXProcess.Runtime.GetTextureFilterMode()} = F | Border = B || Reset Display = R"); y += 10;



        // Shader
        VXProcess.Runtime.LogToScreenExt(x - 10, y, 0xffffff, -1, $"Shader"); y += 10;

        VXProcess.Runtime.LogToScreenExt(x, y, txtCol, -1, $"Resolution = Z,X |      | Asp/Off = W,A,S,D,Q,E | A:                   P: ");
        if (VxLit != null)
            VXProcess.Runtime.LogToScreenExt(x, y, 0xffffff, -1, $"                  {VxLit.resolution}                               {VxLit.AspectRatio.ToString()}   {VxLit.PostionOffset.ToString()}      ");
        y += 10;
        VXProcess.Runtime.LogToScreenExt(x, y, txtCol, -1,   $"Occlusion = C |      | Aspect/Offset = V |             | Shadow Shift + ZX:     | Reset = N");
        if (VxLit != null)
            VXProcess.Runtime.LogToScreenExt(x, y, 0xffffff, -1, $"                                           {LitAdjustment.ToString()}                    {VxLit.shadowOpacityValue:F1}");
        if (VxLit != null)
            VXProcess.Runtime.LogToScreenExt(x, y, 0xffffff, -1, $"                {VxLit.CameraOccluding} ");
        y += 10;









        /*
        VXProcess.Runtime.LogToScreenExt(x, y, txtCol, -1, $"Adjust Gamma : G, H | {VXProcess.Runtime.GetGamma()} | Default = F"); y += 10;
        VXProcess.Runtime.LogToScreenExt(x, y, txtCol, -1, $"Adjust Dither : T, Y | {VXProcess.Runtime.GetDensity()}  Default = R, Toggle Dither = U, Toggle Texture Filtering = I"); y += 10;
        VXProcess.Runtime.LogToScreenExt(x, y, txtCol, -1, $"Toggle Motor : M = ON | Space = STOP | Motor Speed: J / K :: {currentMotorSpeed} :: Mot is Off: {motorIsOff}"); y += 10;
        VXProcess.Runtime.LogToScreenExt(x, y, exportCol, -1, $"P = Pause / Resume | Export PLY E to {exportStr}"); y += 10;
        VXProcess.Runtime.LogToScreenExt(x, y, txtCol, -1, $"< > = Adjust Rotational Offset ({VXProcess.Runtime.GetCentralRotationOffset()})"); y += 10;
      
        VXProcess.Runtime.LogToScreenExt(x, y, txtCol, -1, $"Z X = Adjust VxLitShader Resolution:    C = Occlusion");
        if (VxLit != null)
            VXProcess.Runtime.LogToScreenExt(x, y, txtCol, -1, $"                                    {VxLit.resolution}  {VxLit.CameraOccluding} ");
        */
        y += 10;
    }

    public void Update()
    {


        if (latchMenu == false)
        {
            drawMenu = false;
            menuOn = false;
        }
        if (VXProcess.Instance.active == false || VXProcess.Runtime == null || VXProcess.Instance.VXInterface != VOXON_RUNTIME_INTERFACE.VXLED || VXProcess.Instance.IsClosingVXProcess() == true)
        {
            return;
        }

        if (forceSettings)
        {
     
            if (forceGamma > 0)     { VXProcess.Runtime.SetGamma(forceGamma); forceGamma = -1; }
            if (forceDensity > 0)   { VXProcess.Runtime.SetDensity(forceDensity); forceDensity = -1; }
            if (forceTextureFilter > 0) { VXProcess.Runtime.SetTextureFilterMode(forceTextureFilter); forceTextureFilter = -1; }
            if (forceDitherMode > 0) { VXProcess.Runtime.SetDitherMode(forceDitherMode); forceDitherMode = -1; }

            if (forceBorder > 0)
            {
                VXProcess.Instance.showBorder = (forceBorder == 1);
                forceBorder = -1;
            }

            int[] windInt = VXProcess.Runtime.GetEmuWindowInfo();
            bool updateWindowPos = false;
            bool updateWindowRes = false;


            if (forceWindowPosX > 0) { windInt[0] = forceWindowPosX; forceWindowPosX = -1; updateWindowPos = true; }
            if (forceWindowPosY > 0) { windInt[1] = forceWindowPosY; forceWindowPosY = -1; updateWindowPos = true; }
            if (forceWindowResX > 0) { windInt[2] = forceWindowResX; forceWindowResX = -1; updateWindowRes = true; }
            if (forceWindowResY > 0) { windInt[3] = forceWindowResY; forceWindowResY = -1; updateWindowRes = true; }

            if (updateWindowPos)
                  VXProcess.Runtime.SetEmuWindowPos(windInt[0], windInt[1]);
            if (updateWindowRes)
                VXProcess.Runtime.SetEmuWindowRes(windInt[2], windInt[3]);

            forceSettings = false;

        }

        if (currentMotorSpeedSet == false)
        {
            currentMotorSpeed = Math.Abs(VXProcess.Runtime.GetRPMValue());
            currentMotorSpeedSet = true;
        }
        if (autoStartMotor && hasAutoStarted == false)
        {
            StartCoroutine(AutoStartMotor());
            hasAutoStarted = true;
        }



        if (!setFlags && VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
        {
            _findVXLIT();
            UpdateFlags();
        }






        if (VXProcess.Runtime.GetKey((int)VLEDMenuButton) || VXProcess.Runtime.GetKey(VX_KEYS.KB_F10) || VXProcess.Runtime.GetKey(VX_KEYS.KB_Tilde))
        {
            if (!latchMenu)
            {
                menuOn = true;
                drawMenu = true;
            }

            if (ExclusiveMode == true)
            {
                VXProcess.Instance.SetExclusiveInputMode(true);
                ExclusiveModeOffDelay = Time.timeAsDouble + 0.1;
                _VxHCHasTurnedOnEM = true;
            }
        }

        if (VXProcess.Runtime.GetKeyDown((int)VLEDMenuButton) && latchMenu)
        {

            if (menuOn)
            {
                menuOn = false;
                drawMenu = false;

            }
            else
            {
                menuOn = true;
                drawMenu = true;

            }


        }

        if (menuOn)
        {
            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_G))
            {
                VXProcess.Runtime.SetGamma(VXProcess.Runtime.GetGamma() + 0.1f);
                if (VXProcess.Runtime.GetGamma() > 4) VXProcess.Runtime.SetGamma(4);

            }

            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_H))
            {
                VXProcess.Runtime.SetGamma(VXProcess.Runtime.GetGamma() - 0.1f);
                if (VXProcess.Runtime.GetGamma() < 0) VXProcess.Runtime.SetGamma(0);

            }


            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_T))
            {
                VXProcess.Runtime.SetDensity((VXProcess.Runtime.GetDensity() * 2));
                if (VXProcess.Runtime.GetDensity() > 4096) VXProcess.Runtime.SetDensity(4096);
            }

            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_Y))
            {
                VXProcess.Runtime.SetDensity((VXProcess.Runtime.GetDensity() / 2));
                if (VXProcess.Runtime.GetDensity() < 1) VXProcess.Runtime.SetDensity(1);

            }
            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_R))
            {
                VXProcess.Runtime.SetDensity(64);
                VXProcess.Runtime.SetGamma(2);
                VXProcess.Runtime.SetDitherMode(0);
                VXProcess.Runtime.SetTextureFilterMode(1);
            }
            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_U))
            {
                VXProcess.Runtime.SetDitherMode(VXProcess.Runtime.GetDitherMode() == 0 ? 1 : 0);
            }
            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_F))
            {
                VXProcess.Runtime.SetTextureFilterMode(VXProcess.Runtime.GetTextureFilterMode() == 0 ? 1 : 0);
            }

            #region Turn On/Off Motor
            // Turn on and off motor


            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_M))
            {
                if (motorIsOff == true)
                {
                    VXProcess.add_log_line("VXLED motor on");
                    VXProcess.Runtime.SetRPMValue(currentMotorSpeed);
                    VXProcess.Runtime.SetDisplay3D();
                    motorIsOff = false;
                }
            }

            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_J))
            {
                if (!motorIsOff && VXProcess.Runtime.GetRPMValue() < 0)
                {
                    motorIsOff = true;
                }

                if (motorIsOff)
                {
                    currentMotorSpeed -= 50;
                    VXProcess.Runtime.SetRPMValue(currentMotorSpeed);
                    currentMotorSpeed = Math.Abs(VXProcess.Runtime.GetRPMValue());
                    VXProcess.add_log_line($"VXLED motor speed adjusted to {currentMotorSpeed}");


                }
            }

            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_K))
            {
                if (!motorIsOff && VXProcess.Runtime.GetRPMValue() < 0)
                {
                    motorIsOff = true;
                }

                if (motorIsOff)
                {
                    currentMotorSpeed += 50;
                    VXProcess.Runtime.SetRPMValue(currentMotorSpeed);
                    currentMotorSpeed = Math.Abs(VXProcess.Runtime.GetRPMValue());
                    VXProcess.add_log_line($"VXLED motor speed adjusted to {currentMotorSpeed}");
                }

            }




            #endregion

            #region Toggle Pause and Export
            exportStr = exportfilepath + exportfilename + exportNum.ToString() + fileExt;

            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_O))
            {

                if (VXProcess.Runtime.ExportPLY(exportStr) == 1)
                {
                    exportCol = 0x00ff00;
                    exportNum++;
                    StartCoroutine(ResetExportCol());
                }

            }





            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_Backspace) || VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_Space_Bar))
            {
                VXProcess.add_log_line("VXLED motor off");
                VXProcess.Runtime.SetDisplay2D(0);
                motorIsOff = true;
            }


            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_P))
            {
                Time.timeScale = Time.timeScale == 1 ? 0 : 1;

            }

            #endregion




            if (VXProcess.Runtime.GetKey((int)VX_KEYS.KB_Full_Stop))
            {
                drawFrontTime = Time.timeAsDouble + 1;
                // it's a gotcha needs to be + 2 as the number gets rounded down by 1 
                VXProcess.Runtime.SetCentralRotationOffset(Math.Min(VXProcess.Runtime.GetCentralRotationOffset() + 2, 360));

                if (VXProcess.Runtime.GetCentralRotationOffset() >= 360) VXProcess.Runtime.SetCentralRotationOffset(0);
            }

            if (VXProcess.Runtime.GetKey((int)VX_KEYS.KB_Comma))
            {
                drawFrontTime = Time.timeAsDouble + 1;

                VXProcess.Runtime.SetCentralRotationOffset(Math.Max(VXProcess.Runtime.GetCentralRotationOffset() - 1, -1));
                if (VXProcess.Runtime.GetCentralRotationOffset() <= -1) VXProcess.Runtime.SetCentralRotationOffset(360);
            }



            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_I))
            {
                if (VXProcess.Runtime.GetKey((int)VX_KEYS.KB_Shift_Left))
                {
                    this.reportCaptureVolume = true;
                    this.reportLitShader = true;
                    this.reportCSInterface = true;
                    this.reportMouse = true;
                    this.reportNav = true;
                    this.reportJoy = true;
                    this.reportVXLState = true;

                }

                VXProcess.Instance.showInfo = !VXProcess.Instance.showInfo;
            }

            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_B))
            {
                VXProcess.Instance.showBorder = !VXProcess.Instance.showBorder;
            }


            // Lit Shader Adjustments



            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_N))
            {
                if (_findVXLIT())
                {
                    _resetVXLIT();
                }
            }



            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_W))
            {
                if (_findVXLIT())
                {
                    if (LitAdjustment == LitShaderAdjust.Adjust_Aspect) VxLit.AspectRatio.y -= 0.1f;
                    else if (LitAdjustment == LitShaderAdjust.Adjust_Offset) VxLit.PostionOffset.y -= 0.2f;
                }
            }

            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_S))
            {
                if (_findVXLIT())
                {
                    if (LitAdjustment == LitShaderAdjust.Adjust_Aspect) VxLit.AspectRatio.y += 0.1f;
                    else if (LitAdjustment == LitShaderAdjust.Adjust_Offset) VxLit.PostionOffset.y += 0.2f;
                }
            }

            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_A))
            {
                if (_findVXLIT())
                {
                    if (LitAdjustment == LitShaderAdjust.Adjust_Aspect) VxLit.AspectRatio.x -= 0.1f;
                    else if (LitAdjustment == LitShaderAdjust.Adjust_Offset) VxLit.PostionOffset.x -= 0.2f;
                }
            }

            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_D))
            {
                if (_findVXLIT())
                {
                    if (LitAdjustment == LitShaderAdjust.Adjust_Aspect) VxLit.AspectRatio.x += 0.1f;
                    else if (LitAdjustment == LitShaderAdjust.Adjust_Offset) VxLit.PostionOffset.x += 0.2f;
                }
            }

            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_E))
            {
                if (_findVXLIT())
                {
                    if (LitAdjustment == LitShaderAdjust.Adjust_Aspect) VxLit.AspectRatio.z += 0.1f;
                    else if (LitAdjustment == LitShaderAdjust.Adjust_Offset) VxLit.PostionOffset.z -= 0.1f;
                }
            }

            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_Q))
            {
                if (_findVXLIT())
                {
                    if (LitAdjustment == LitShaderAdjust.Adjust_Aspect) VxLit.AspectRatio.z -= 0.1f;
                    else if (LitAdjustment == LitShaderAdjust.Adjust_Offset) VxLit.PostionOffset.z += 0.1f;
                }
            }



            if (VXProcess.Runtime.GetKey((int)VX_KEYS.KB_Shift_Left) || VXProcess.Runtime.GetKey((int)VX_KEYS.KB_Shift_Right))
            {
                if (VXProcess.Runtime.GetKey((int)VX_KEYS.KB_Z))
                {
                    if (_findVXLIT())
                
                        VxLit.shadowOpacityValue += 0.05f;
                    if (VxLit.shadowOpacityValue > 1) VxLit.shadowOpacityValue = 1;
                }

                if (VXProcess.Runtime.GetKey((int)VX_KEYS.KB_X))
                {
                    if (_findVXLIT())
                        VxLit.shadowOpacityValue -= 0.05f;
                    if (VxLit.shadowOpacityValue < 0) VxLit.shadowOpacityValue = 0;
                }

            }
            else
            {

                if (VXProcess.Runtime.GetKey((int)VX_KEYS.KB_Z))
                {
                    if (_findVXLIT())
                        VxLit.resolution += 10;
                }
                if (VXProcess.Runtime.GetKey((int)VX_KEYS.KB_X))
                {
                    if (_findVXLIT())
                        VxLit.resolution -= 10;
                }
            }


            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_C))
            {
                if (_findVXLIT())
                    VxLit.CameraOccluding = !VxLit.CameraOccluding;
            }
            if (VXProcess.Runtime.GetKeyDown((int)VX_KEYS.KB_V))
            {
                if (LitAdjustment == LitShaderAdjust.Adjust_Offset) LitAdjustment = LitShaderAdjust.Adjust_Aspect;
                else
                {
                    LitAdjustment = LitShaderAdjust.Adjust_Offset;
                }
            }


        } // End of holding down engine key






        if (_VxHCHasTurnedOnEM == true && ExclusiveModeOffDelay < Time.timeAsDouble)
        {
            VXProcess.Instance.SetExclusiveInputMode(false);
            _VxHCHasTurnedOnEM = false;
        }


    }

    private bool _findVXLIT()
    {
        if (VxLit == null)
        {
#if UNITY_6000_0_OR_NEWER

            VxLit = FindFirstObjectByType<Voxon.VxLit.VxLitRendererV4>();
#else

            VxLit = FindObjectOfType<Voxon.VxLit.VxLitRendererV4>();
#endif      

        }

        if (VxLit == null) return false;

        this.VxLitSettings.res = VxLit.resolution;
        this.VxLitSettings.pos = VxLit.PostionOffset;
        this.VxLitSettings.asp = VxLit.AspectRatio;
        this.VxLitSettings.o = VxLit.CameraOccluding;


        if (VxLit) return true;
        else return false;
    }

    private void _resetVXLIT()
    {
        if (VxLit == null) return;

        VxLit.resolution = this.VxLitSettings.res;
        VxLit.PostionOffset = this.VxLitSettings.pos;
        VxLit.AspectRatio = this.VxLitSettings.asp;
        VxLit.CameraOccluding = this.VxLitSettings.o;
    }

    public void Draw()
    {
        if (VXProcess.Instance.IsClosingVXProcess() == true || VXProcess.Runtime == null || VXProcess.Instance.VXInterface != VOXON_RUNTIME_INTERFACE.VXLED || VXProcess.Instance.GetExclusive2DMode() == true) return;

        if (drawMenu)
        {
            DrawMenu();
        } else
        {
            VXProcess.Runtime.LogToScreenExt(MenuPos.x, MenuPos.y, 0x9d9d9d, -1, $"Press '`', 'F10' or '{Enum.GetName(typeof(VX_KEYS), VLEDMenuButton)}' For Menu");
        }

        if (drawFrontTime > Time.timeAsDouble)
        {
            float[] asp = VXProcess.Runtime.GetAspectRatio();

            VXProcess.Runtime.DrawPText(ref nullTT, 10, 128, 0, 15, 1, "Front");
            VXProcess.Runtime.DrawSphere(0, (1 * asp[0]) - 0.1f, (1 * asp[2]) - 0.1f, 0.1f, 0, 0x00ffff);
            VXProcess.Runtime.DrawSphere(0, (1 * asp[0]) - 0.1f, (1 * -asp[2]) + 0.1f, 0.1f, 0, 0x00ffff);

        }

        if (VXProcess.Instance.showInfo)
        {
            int[] window = VXProcess.Runtime.GetEmuWindowInfo();
            /*
            if (reportVPS)
            {
                if (window[2] > 500 && window[3] > 300)
                {
                    VXProcess.Runtime.Report("VPS", window[2] - 250, window[3] - 110);
                }
            }
            */
            // LEFT SIDE
            if (window[2] > 300 && window[3] > 600)
            {

                if (reportVXLState)
                {

                    VXProcess.Runtime.Report("VxlState", 10, window[3] - 280);
                }

                if (window[2] > 900)
                {
                    if (reportCSInterface)
                    {
                        VXProcess.Runtime.ReportDLLPathInfo(290, window[3] - 170, 0xffAA00);
                    }
                }
            }
            // RIGHT SIDE
            if (window[2] > 600 && window[3] > 500)
            {


                int ypos = 10;
                if (reportLitShader && VxLit != null)
                {
                    ypos = VxLit.Report(window[2] - 365, 10);
                    ypos += 70;
                }
                if (reportMouse)
                {
                    VXProcess.Runtime.Report("Mouse", window[2] - 210, ypos + 30);

                }
                if (window[2] > 900)
                {

                    if (reportCaptureVolume && VXProcess.Instance.Camera != null)
                    {
                        VXProcess.Instance.Camera.ReportCamera(10, 200);
                    }
                }

                if (window[3] > 800)
                {

                    if (reportNav)
                    {
                        VXProcess.Runtime.Report("Nav", window[2] - 230, ypos + 130);
                    }
                    if (window[2] > 900)
                    {
                        if (reportJoy)
                        {
                            VXProcess.Runtime.Report("Joy", 10, 330);
                        }
                    }
                }
            }

        }


    }

}
