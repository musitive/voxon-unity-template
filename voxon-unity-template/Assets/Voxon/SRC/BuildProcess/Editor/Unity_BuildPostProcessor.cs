using System;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEditor.Compilation;
using System.Collections.Generic;

namespace Voxon
{
	class UnityBuildPostProcessor : IPostprocessBuildWithReport
	{
		public int callbackOrder => 0;

		public void OnPostprocessBuild(BuildReport report)
		{
			string fileName = "";
			string outputDirectory = "";

			string sPattern = ".exe";
			string ePattern = "UnityCrashHandler";


#if UNITY_2022_1_OR_NEWER
			foreach (BuildFile file in report.GetFiles())
#else
			foreach (BuildFile file in report.files)
#endif
			{
				
				// find the first EXE we will exclude the ones that are always included
				if (System.Text.RegularExpressions.Regex.IsMatch(file.path, sPattern))
				{
					// if the EXE is UnityCrashHandler - FORGET IT 
					if (System.Text.RegularExpressions.Regex.IsMatch(file.path, ePattern))
					{
						continue;
					}
					fileName = Path.GetFileName(file.path);
					outputDirectory = Path.GetDirectoryName(file.path);
					break;
				}

				/* Old solution - stopped working with new versions of Unity as 
				* For some reason Unity in later versions changes the file.role  to "exe" which broke this... */
				if (file.role == "Executable" ) continue;

				fileName = Path.GetFileName(file.path);
				outputDirectory = Path.GetDirectoryName(file.path);
				Debug.Log($"File: {fileName}, Folder: {outputDirectory}");
				
			}

			// Generate VX.Bat Batch File - VXU Unity Projects have to be run in -batchmode 

			try
			{
				string batchContents = "start \"\" \"" + fileName + "\" -batchmode";

				using (StreamWriter writer = new StreamWriter(outputDirectory + "\\VX.bat"))
				{
					writer.WriteLine("cls");
					writer.WriteLine("@echo off");
					writer.WriteLine("echo.");
					writer.WriteLine("echo ###     ################# ####   ##################  ####      ##");
					writer.WriteLine("echo  ###  ###   ###       ###   #######   ###        ##  #######   ##");
					writer.WriteLine("echo   ######    ###       ###   #######   ###        ##  ###  #######");
					writer.WriteLine("echo    ####     ##################   #### #################      ####");
					writer.WriteLine("echo.");
					writer.WriteLine("echo                -= Voxon X Unity Plugin Launcher =-");
					writer.WriteLine("echo.");
					writer.WriteLine("echo                  Launching your VLED x Unity App");
					writer.WriteLine("echo.");
					writer.WriteLine("for /f \"tokens=4 delims=[] \" %%a in ('ver') do set version=%%a");
					writer.WriteLine("if \"%version%\" geq \"10.0.22000\" (");
					writer.WriteLine("echo.              ");
					writer.WriteLine(") else (");
					writer.WriteLine("echo    Loading - please wait - this may take a few seconds to load.");
					writer.WriteLine("echo.  ");
					writer.WriteLine("echo.");
					writer.WriteLine(")");
					writer.WriteLine("echo.");
					writer.WriteLine("echo                   (c) 2024 - 2025 Voxon Photonics");
					writer.WriteLine("echo                          www.Voxon.co");
					writer.WriteLine("echo.");
					writer.WriteLine("echo.");
					writer.WriteLine("echo                              Note:");
					writer.WriteLine("echo   If you are launching your VxUnity App outside of this batch");
					writer.WriteLine("echo script, remember to launch your .Exe with the '-batchmode' argument.");
					writer.WriteLine("echo.");
					writer.WriteLine("for /f \"tokens=4 delims=[] \" %%a in ('ver') do set version=%%a");
					writer.WriteLine("if \"%version%\" geq \"10.0.22000\" (");
					writer.WriteLine("echo                  ***  PRESS ANY KEY TO LAUNCH !  ***");
					writer.WriteLine("    pause > nul");
					writer.WriteLine("echo.");
					writer.WriteLine("echo    Loading - please wait - this may take a few seconds to load.");
					writer.WriteLine(") else (");
					writer.WriteLine("echo.  ");
					writer.WriteLine(")");
					writer.WriteLine(" ");
					writer.WriteLine(batchContents);
					writer.WriteLine("TimeOut /T:10 > nul");
				}

				Debug.Log("Batch file written with updated contents.");
			}
			catch (Exception e)
			{
				Debug.LogError("Unable to write batch file");
				Debug.LogError(e.Message);
			}


			string pluginsPath = Path.Combine(Application.dataPath, "Voxon", "Plugins");
			string buildDllsPath = Path.Combine(outputDirectory, "BuildDlls");
			Directory.CreateDirectory(buildDllsPath);

			var dlls = new Dictionary<string, string>
			{
				{ "C#-bridge-interface.dll", "C#-bridge-interface.dll" },
				{ "C#-Runtime.dll", "C#-Runtime.dll" },
				{ "LedWin.dll", "LedWin.dll" },
				{ "Ledhost.dll", "Ledhost.dll" },
				{ "Voxiebox.dll", "Voxiebox.dll" }
			};

			foreach (var dll in dlls)
			{
				string sourcePath = Path.Combine(pluginsPath, dll.Key);
				string destinationPath = Path.Combine(buildDllsPath, dll.Value);

				try
				{
					File.Copy(sourcePath, destinationPath, true);
				}
				catch (Exception e)
				{
					Debug.LogWarning($"UnityBuildPostProcessor.cs - Unable to copy {dll.Key}! (ignore if you are overriding a build) - Source: {sourcePath}, Destination: {destinationPath}, Reason: {e.Message}");
				}
			}

		}
	}
}