using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEditor;
using Voxon;

/// <summary>
/// This class writes creates Windows Message Boxes for alerts and error handling. 
/// </summary>
//

public static class Windows
{
    // Define constants for MessageBox types
    private static long MB_OK = 0x00000000L;

	 //private static long MB_YESNO = 0x00000004L;
    private static long MB_OKCANCEL = 0x00000001L;
    // private static long MB_DEFBUTTON2 = 0x00000100L;
    // private static int IDYES = 6;
    // private static int IDNO = 7;


    // Define constants for MessageBox return values
    private const int IDOK = 1;
    private const int IDCANCEL = 2;
    private const int IDABORT = 3;
    private const int IDRETRY = 4;
    private const int IDIGNORE = 5;
    private const int IDYES = 6;
    private const int IDNO = 7;

    private const uint MB_ICONEXCLAMATION = 0x00000030; // Warning icon
    private const uint MB_ICONHAND = 0x00000010; // Error icon


    [DllImport("user32.dll")]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, int options);

    public static bool Alert(string Message)
    {

        if (VXProcess.Instance.active == true && VXProcess.Instance.suppressAllWinErrors == false)
        {

            int result = MessageBox(IntPtr.Zero, Message + "\n\nPress OK to continue\nPress CANCEL to close this instance", "VxU Alert", (int)(MB_OKCANCEL | MB_ICONEXCLAMATION));
            if (result == 1) // Press OK
            {
                // do nothing - aka continue
            }
            else
            {

                Debug.Log("Alert Cancelled - Quiting App");
                if (VXProcess.Instance != null)
                {
                    VXProcess.Instance.suppressAllWinErrors = true;

                    VXProcess.Instance.QuitPressed();
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


        return true;
    }

    public static bool Error(string Message)
    {
        if (VXProcess.Instance.active == true && VXProcess.Instance.suppressAllWinErrors == false)
        {
            int result = MessageBox(IntPtr.Zero, Message + "\n\nPress OK to close this instance", "VxU Error", (int)(MB_OK| MB_ICONHAND));
       

            // its an error so always kill 

                if (VXProcess.Instance != null)
                {
                    VXProcess.Instance.suppressAllWinErrors = true;

                    VXProcess.Instance.QuitPressed();
                }
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBPLAYER
				    Application.OpenURL(webplayerQuitURL);
#else
                        Application.Quit();
#endif
                return false;
            
        }


        return true;
    }
}
