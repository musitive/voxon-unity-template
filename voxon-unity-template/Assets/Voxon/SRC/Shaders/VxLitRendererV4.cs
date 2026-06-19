using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
/*//////////////////////////////////////////////////////////////////////////////  
 TODO 

*/
// VX Light Renderer Version 4
// --------------------------- 
// Turns a Unity camera in a depth camera to convert its view into voxels
//
// uses VxLightShaderV3.shader to build the poltex  [ voxel array ] 


namespace Voxon.VxLit
{

	public enum Vx_RenderType : int
	{
		Render_Shader_Direct = 0,
		Render_From_Depth_Data = 1,

		// These are debug rendering options
		Render_As_Single_Call = 2,
		Render_Debug_Poltex_On_2D_Display = 3,
		Render_Debug_Depth_Data_On_2D = 4,
		Render_Debug_Depth_Data_On_Both = 5,
		Render_Raw_Poltex_FromGPU = 6,
		Render_Raw_Poltex_FromGPU_Cull = 7,
		Render_Debug_Draw_Using_DrawVox = 8,


	};

	public enum CullValues : int
    {
        NoCull = 0,
        FrontFaceCull = 1,
        BackFaceCull = 2,
    };



    /// <summary>
    /// How does the Lit Render scale its view? 
    /// Aspect_From_Hardware = Get the size of the display from the hardware and map it.
    /// Aspect_From_VXCaptureVolume = Get the size of the display from the VXVolumeCapture and map it.
    ///  
    /// Fit vs Extend = Fit - resizes the full image to try to fit within the volume. Exact would apply no extra scaling so if the VX Camera is set for a smaller display the view in the hardware will extend. 


    /// </summary>
    public enum VxLit_AspectRatioFromTypes
	{
		//		Aspect_From_Hardware_Fit = 0,  Depreated as use the VX_CAMERA to manage this 
		//		Aspect_From_Hardware_Extend = 1,
		Aspect_From_VXCaptureVolume_Fit = 2,
		Aspect_From_VXCaptureVolume_Extend = 3,
		Aspect_Independant = 4,
	}


	/// <summary>
	///	Each new fragment tests itself against the depth buffer using it's x, y and if depth is less than active depth, it will use the existing x,y,z (if they exist), 
	///	and update the data buffer with the fragments issue, before updating the depth buffer to the new values. Deeper values will be discarded.
	/// </summary>
	public class VxLitRendererV4 : MonoBehaviour, IDrawable
	{




		[Space(10)]
        [Tooltip("The Unity camera used to create the depth volumetric image. (Uses the camera's Target Texture to generate a point cloud of voxels)")]
        public Camera depthCamera;
		[HideInInspector]
		public Vx_RenderType renderType = Vx_RenderType.Render_Shader_Direct;
		private Vx_RenderType oRenderType = Vx_RenderType.Render_Shader_Direct;

		/// <summary>
		/// X / Y resolution of capture
		/// </summary>
		int oResolution = 0;
        [Tooltip("The resolution of the voxel depth map. Higher values provide more detail but result in slower processing.")]
        public int resolution = 400;
		private int poltexHeadRoom = 4; // poltextHEad room automatically managed
		private int oPoltexHeadRoom = 4;
		const int POLTEX_MAX_HEADROOM = 15;

        ///

        [Tooltip("How the aspect ratio relates to the volume. 'Aspect_From_VXCaptureVolume' uses the size of the volume from the VXCaptureVolume object. 'Fit' resizes the capture to fit within the volume, while 'Exact' applies no resizing, so if the VX Capture Volume is smaller, the view in the hardware will extend. 'Independent' ignores the capture volume and lets you input your own values.")]
        public VxLit_AspectRatioFromTypes AspectRatioContext = VxLit_AspectRatioFromTypes.Aspect_From_VXCaptureVolume_Fit;
        private VxLit_AspectRatioFromTypes oAspectRatioContext = VxLit_AspectRatioFromTypes.Aspect_Independant;

        [Tooltip("The aspect ratio of the view. This is used to scale the view to fit the volumetric display.")]
        public Vector3 AspectRatio = new Vector3(1f, 1f, 1f);
		private float[] previousVXHardwareAspectRatio = { 0, 0, 0 }; 
		private Vector3 oAspectRatio = new Vector3(0, 0, 0);
		private Vector3 camScale = new Vector3(0, 0, 0);
		private Vector3 oCamScale = new Vector3(0, 0, 0);


        [Tooltip("Offsets the rendered view by this amount.")]
        public Vector3 PostionOffset = new Vector3(0f, 0f, 0f);
		private bool SimpleSetupMode = true;
		[Space(10)]
        [Tooltip("Toggles camera occlusion, allowing the depth camera to see past closer objects. No occlusion = 'X-Ray view'")]
        public bool CameraOccluding = true;



        [Tooltip("Actively clips the radius of the view; 0 is off, -1 is a cube, positive numbers are a cylinder shape and use a radial value.")]
        [Range(-1f, 5f)]
		public float CameraClipRadius = 0;
     
		[Range(0, 5)]
        [Tooltip("Global brightness adjustment to the voxel depth map; the default is 1.")]
        public float GlobalBrightnessAdjust = 1;

        [Range(0.0f, 1.0f)]
        //[HideInInspector]
        [Tooltip("Make shadows opaque. Warning can lead to some strange artifacts. Play with 'Sort Queue By Distance' and 'Reverse Z Sorting Order'")]
        public float shadowOpacityValue = 0; // shadowOpacityValue is experitmental doesn't work for all viewpoints


        [Space(10)]
        [Tooltip("Overrides culling information for all meshes; 0 = no culling, 1 = front face culling, 2 = back face culling.")]
        public bool UseGlobalCullValue = false;
		private bool oUseGlobalCullValue = false;
		private bool forceBackfaceCull = false; // Hack to ensure that Depth Camera is always backface culling
        [Tooltip("Sets all materials to the same culling value. 0 = no culling, 1 = front face culling, 2 = back face culling.")]
        public CullValues GlobalCullValue = CullValues.BackFaceCull;


		[Range(-0.1f, 0.1f)]
        private float DepthThreshold = 0;
		[Range(0, 3)]
		private int PostProcessDepthMode = 0;
		[Range(1, 10)]
		private int depthCalls = 3;
		/// <summary>
		/// Stores pixel color data
		/// </summary>
		ComputeBuffer pdata_raw_buffer;
		/// <summary>
		/// Stores pixel depth data (convert plane to volume)
		/// </summary>
		ComputeBuffer depth_buffer;
		/// <summary>
		/// Stores pixel index
		/// </summary>
		ComputeBuffer index_buffer;

		/// <summary>
		/// A ComputeShader to clear pixel data
		/// </summary>
		ComputeShader computeShader;
		/// <summary>
		/// Stores pixel index
		/// </summary>
		ComputeBuffer pdata_refined_buffer;

		/// <summary>
		/// Active Voxon Camera
		/// </summary>
		VXCaptureVolume VxCaptureVolume;
		/// <summary>
		/// Unity Camera used to capture the depth and colour in scene
		/// </summary>

		DepthCamSettings depthCamSettings;
		VxRenderSettings renderSettings;
		/// <summary>
		/// Render Target of Unity Camera
		/// </summary>
		RenderTexture rt;
		/// <summary>
		/// The maximum number of voxels that can be drawn
		/// </summary>
		int maxVoxels = 0;
		/// <summary>
		/// Total number of depth pixels
		/// </summary>
		int maxDepthPixels = 0;


        private int DepthFrameNo = 0;


		/// <summary>
		/// Read Voxel position & color data from
		/// compute buffer stored here
		/// </summary>
		poltex[] poltex_data;

		/// <summary>
		/// Depth data.
		/// </summary>
		depthV3[] depth_data;

		/// <summary>
		/// Current index values. Updated based on which
		/// indices will be drawn
		/// </summary>
		int[] current_index = new int[] { 0, 0, 0};

		/// <summary>
		/// Buffer used by draw calls
		/// </summary>
		poltex[] voxel_render_buffer;
		/// <summary>
		/// Total number of compute groups to ensure
		/// 1 megabyte is processed per group
		/// </summary>
		int group_size = (1024 * 1024) / 24; // How many poltex per Megabyte
		/// <summary>
		/// Total number of groups (division of pixels by group size)
		/// </summary>
		int group_count = 0;
		int[] voxel_count = new int[] { 0 };

		private int LEDPoltexDrawSize = 100000; // 1000000 is the optimised 
		int oLEDPoltexDrawSize = 100000;

		// How many times to redraw the poltex ... not that useful 
		[Range(1, 8)]
		private int renderDrawCount = 1;
		// privates to manage flow
		bool isShaderInited = false;
		int totalVoxelCount;


		[Space(10)]
        [Tooltip("Selects when to show various information about where the depth camera is facing.")]
        public CameraSightTrackingOptions showCameraDirection = CameraSightTrackingOptions.EditorOnly;
        [Tooltip("Selects the type of information about the depth camera to show.")]
        public CameraSightDrawOptions showCameraStyle = CameraSightDrawOptions.LineThrough;
		private int showCameraFColor = 4;
		private int showCameraBColor = 6;


		private double showCameraDirectionTimeOut = 0; 
		private float oShadowOpacityValue = 0;
		[Space(10)]
        [Tooltip("If enabled, clears the depth buffer after drawing the depth map. (Usually, you want this enabled)")]
        public bool clearShaderAfterDraw = true;

		private bool useInternalRT = true;       // use internal render texture
		[HideInInspector]
		public bool useVoxBatchCalling = true;
        [Tooltip("If enabled, forces the camera to be orthographic.")]
        public bool ForceOthoCamera = false;



		/// <summary>
		/// ID for each shader parameter
		/// </summary>
		private static readonly int
			resolutionId = Shader.PropertyToID("_Resolution"),

			indexId = Shader.PropertyToID("_LastPoltexIndex"),
			dataId = Shader.PropertyToID("_PoltexData"),

			dataRefinedId = Shader.PropertyToID("_PoltexDataRefined"),
			depthId = Shader.PropertyToID("_DepthData"),

			cameraId = Shader.PropertyToID("_VxCamera"), // Voxon Camera

			camPosId = Shader.PropertyToID("_CamPos"),
			camPosThreholdId = Shader.PropertyToID("_CamPosThreshold"),

			viewAspectRatioId = Shader.PropertyToID("_ViewAspectRatio"),
			viewOffsetPosId = Shader.PropertyToID("_ViewOffset"),
			viewOcclusionId = Shader.PropertyToID("_ViewOccluding"),
			viewDepthPostId = Shader.PropertyToID("_ViewDepthPostProcessMode"),
			viewClipShapeId = Shader.PropertyToID("_ViewClipRadius"),
			shadowValueId = Shader.PropertyToID("_GlobalShadowValue"),
			globalBrightnessId = Shader.PropertyToID("_GlobalBrightnessValue"),
			poltexHeadRoomId = Shader.PropertyToID("_PoltexHeadRoom"),
			depthThresholdId = Shader.PropertyToID("_DepthThreshold"),
        
            captureAllDataId = Shader.PropertyToID("_CaptureAllData");

				



		private bool RenderWithSpheres = false;
		[Range(0.0001f, 0.05f)]
		private float SphereRadius = 0.001f;
        [Tooltip("If enabled, the depth camera will always face the capture volume.")]
        public bool ForceLookAtVXCamera = false;
		public bool forceShaderReInit = false;
        [Tooltip("When toggled, forces the render and all settings to reinitialize. Resets to false once initialization is complete.")]
        public bool forceRenderReInit = false;



        private int drawMap2DRes = 1; // dynamic number that will change for rendering 2D maps

		private Transform initalCamTrans;

		// When the shader is processing the camera values needs to have a threshold to find
		private float camPosThreshold = 0.5f;
		private Vector3 oCamWorldPos = new Vector3();
		private int clearDBuffKernelHndl = 0;
		private int clearPoltexKernelHndl = 0;
		private int buildPoltexFromDepthKernelHndl = 0;

		/// <summary>
		///  DEBUG THINGS // ADVANCED SETTINGS
		///  You can make these public or in the inspector and see their behaviour
		/// </summary>

		private bool managePoltexDepthOverflow = false; // try to manage a poltex depth index overflow.. seems to be OK to just skip the case
		private bool forceClearOnFilledPoltexBuffer = true;
		private bool forceSetAllMaterialShadowValuesToZero = false; // if sorting set all material values to zero
		private bool extendDepthBufferSize = false;
		private double clearTime = 0;
        private int poltextMax = 0;
        private double clearInterval = 0;
        private bool useIDs = false;
        private bool oscillateIDs = false;
        [Tooltip("If enabled the renderer will render in reverse Z order. This can be useful when the depth of the volume isn't looking accurate")]
        public bool reverseZSortingOrder = false;
		private bool autoEnableBackfaceCullWhenOccluding = false;
        [Tooltip("If enabled, the renderer will sort the render queue by the distance from the camera. It achieves this by editing each material instance's render queue.")]
		//[HideInInspector]
		public bool SortQueueByDistance = true;
		private bool renderForVoxieBox = false;


		void InitShader()
        {

            DepthFrameNo = 0;
            oUseGlobalCullValue = UseGlobalCullValue;

            if (resolution < 7) return;

			oResolution = resolution;

			computeShader = (ComputeShader)Resources.Load("VxLightComputeV3");
			clearDBuffKernelHndl = computeShader.FindKernel("DepthBufferClear");
			clearPoltexKernelHndl = computeShader.FindKernel("PoltexClear");
			buildPoltexFromDepthKernelHndl = computeShader.FindKernel("BuildPoltexFromDepthMap");

			if (poltexHeadRoom < 1) poltexHeadRoom = 1;
			// Voxel Setup
			// the total amount of voxels that can be drawn/
			maxVoxels = (resolution * resolution) * poltexHeadRoom;

			oPoltexHeadRoom = poltexHeadRoom;

			// Determine Groups based on group size and set up buffer
			if (renderForVoxieBox || VXProcess.Instance.VXInterface != VOXON_RUNTIME_INTERFACE.VXLED)
			{
				renderForVoxieBox = true;
				group_size = (1024 * 1024) / 24; // 24 = poltex size How many poltex per Megabyte
			}
			else
				group_size = LEDPoltexDrawSize; // 80,000 standard draw 10,000 voxels per group

			oLEDPoltexDrawSize = LEDPoltexDrawSize;


			voxel_render_buffer = new poltex[group_size];
		
			group_count = Mathf.CeilToInt(maxVoxels / (float)group_size);

			Array.Resize(ref voxel_count, group_count);

			// the data that gets processed by the shader
			poltex_data = new poltex[maxVoxels];

			// poltex = 3 floats (xyz) + 2 floats (uv) + int col
			int poltex_stride = 4 /*32 bit*/ * (3 /* Vector3 */ + 2 /* Vector2 */ + 1 /* int */);
			pdata_raw_buffer = new ComputeBuffer(maxVoxels, poltex_stride, ComputeBufferType.Default);
			pdata_refined_buffer = new ComputeBuffer(maxVoxels, poltex_stride, ComputeBufferType.Default);

			// how much depth is supported... as we use cantor pairing the array has to be
			// The largest Cantor pairing value for a 512x512 grid is: 523774 ... its basically this:
			maxDepthPixels = ((resolution * resolution) * 2) - (resolution * 2);

			if (extendDepthBufferSize) maxDepthPixels *= 2;

			depth_data = new depthV3[maxDepthPixels];
            int pixel_stride = 4 /* int */ + 4 /* float */ + 4 /* float */ ;

			depth_buffer = new ComputeBuffer(maxDepthPixels, pixel_stride, ComputeBufferType.Default);

			// Index Setup
			int index_count = 3; // 0 == currentSize Index for refined,  1 == frame count, 2 == current Index for Raw
			int index_stride = 4;
			index_buffer = new ComputeBuffer(index_count, index_stride, ComputeBufferType.Default);



			Graphics.ClearRandomWriteTargets();                             // Clear all the buffers
			
			Shader.SetGlobalBuffer(dataId, pdata_raw_buffer);               // pdata buffer holds the all data from the shader
			Shader.SetGlobalBuffer(dataRefinedId, pdata_refined_buffer);    // pdata buffer holds only the occulsion data from the shader colours
			Shader.SetGlobalBuffer(depthId, depth_buffer);                  // depth buffer holds depth
			Shader.SetGlobalBuffer(indexId, index_buffer);                  // pixel index  
		
			
			int[] default_index = new int[] { 0, 0 };

			index_buffer.SetData(default_index);    // set the default index	
			Shader.SetGlobalInt(resolutionId, resolution);
			
			Graphics.SetRandomWriteTarget(1, pdata_refined_buffer, false);
			Graphics.SetRandomWriteTarget(2, depth_buffer, false);
			Graphics.SetRandomWriteTarget(3, index_buffer, false);
			Graphics.SetRandomWriteTarget(4, pdata_raw_buffer, false);


			DepthBufferRefresh();   // function to clear the shader before we render

			// Camera Init
			if (depthCamera == null)
			{
				Debug.LogError("No Unity Camera assigned to act as a depth camera!");
			}

			depthCamerasUpdate(ref depthCamera, ref depthCamSettings);


			isShaderInited = true;
		}


		// function to update a depthCamera with the settings
		void depthCamerasUpdate(ref Camera dCam, ref DepthCamSettings dCamSettings)
		{
			if (useInternalRT)
			{
				rt = new RenderTexture(resolution, resolution, 8);
				dCam.targetTexture = rt;
				rt.antiAliasing = 1; // is turns off anti-aliasing which causes errors in the depth.  
				rt.filterMode = FilterMode.Point;

				dCam.allowMSAA = false; // Disable Multisample Anti-Aliasing
										// for this camera, as it ruins the
										// volumetric conversion in the shader 

			}
			if (ForceOthoCamera)
			{
				dCam.orthographic = true;
			}

			if (PostProcessDepthMode >= 1)
			{
				dCam.depthTextureMode = DepthTextureMode.Depth;

			}
			else
			{
				dCam.depthTextureMode = 0;
			}

			dCamSettings.camWorldPos = dCam.transform.position;
			// in the future we will send these as a float array to support multiple cameras

			// Camera Pos, Camera Aspect, Camera Offset
			int occlusionSetting = this.CameraOccluding ? 1 : 0;



            oShadowOpacityValue = shadowOpacityValue;
            // Aspect Ratio Routines
            ///////////////////////////////////////////////////////////////////
            if (AspectRatio != oAspectRatio || AspectRatioContext != oAspectRatioContext || camScale != oCamScale)
			{


				switch (AspectRatioContext)
				{
					case VxLit_AspectRatioFromTypes.Aspect_Independant:

						if ((int)VXProcess.Instance.VXUReportingLevel >= (int)VXProcessReportLevel.Processes) Debug.Log("Setting Aspect to Independant...");


						renderSettings.aspectRatio = AspectRatio;

						if (VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
						{
							// aspect VLED VS Voxiebox fix 
							renderSettings.aspectRatio.x *= 2;
							renderSettings.aspectRatio.y *= 2;
							renderSettings.aspectRatio.z *= 2;

						}
						oCamScale = camScale;
						break;
					case VxLit_AspectRatioFromTypes.Aspect_From_VXCaptureVolume_Fit:
					case VxLit_AspectRatioFromTypes.Aspect_From_VXCaptureVolume_Extend:

						if (VxCaptureVolume == null) break;

						if ((int)VXProcess.Instance.VXUReportingLevel >= (int)VXProcessReportLevel.Processes) Debug.Log("Setting Aspect to VxCamera...");

						// the vxCamera uses Unity's transform
						// the VxLitRender uses Voxon's transform
						// Thus .z .y axises are different
						if (VxCaptureVolume.uniformScale)
						{
							camScale.x = VxCaptureVolume.BaseScale;
							camScale.z = VxCaptureVolume.BaseScale;
							camScale.y = VxCaptureVolume.BaseScale;

						}
						else
						{
							camScale.x = VxCaptureVolume.vectorScale.x;
							camScale.z = VxCaptureVolume.vectorScale.y;
							camScale.y = VxCaptureVolume.vectorScale.z;
						}

						Vector3 vfdim = VxCaptureVolume.ViewFinderDimensions;

						// this is a the ratio betten a unity 2 voxon unit
						float UxVscalar = 0.1f;

						// work out 

						if (VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
						{
							renderSettings.aspectRatio.x = AspectRatio.x * (((camScale.x + camScale.y) * 0.5f) * UxVscalar);
							renderSettings.aspectRatio.y = AspectRatio.y * (((camScale.x + camScale.y) * 0.5f) * UxVscalar);
							renderSettings.aspectRatio.z = AspectRatio.z * (camScale.z * UxVscalar);

							// aspect Voxiebox fix 
							if (VXProcess.Instance.VXInterface != VOXON_RUNTIME_INTERFACE.VXLED)
								renderSettings.aspectRatio *= 2;


						}
						else
						{
							renderSettings.aspectRatio.x = AspectRatio.x * (camScale.x * UxVscalar);
							renderSettings.aspectRatio.y = AspectRatio.y * (camScale.y * UxVscalar);
							renderSettings.aspectRatio.z = AspectRatio.z * (camScale.z * UxVscalar);

							// aspect Voxiebox fix 
							if (VXProcess.Instance.VXInterface != VOXON_RUNTIME_INTERFACE.VXLED)
								renderSettings.aspectRatio *= 2;
						}

						// correct aspect
						if (AspectRatioContext == VxLit_AspectRatioFromTypes.Aspect_From_VXCaptureVolume_Fit)
						{


							float[] asp = VXProcess.Runtime.GetAspectRatio();
							float[] casp = { camScale.x * UxVscalar, camScale.y * UxVscalar, camScale.z * UxVscalar };

							if (casp[0] != asp[0] || casp[2] != asp[1])
							{

								///// work out if the camera is bigger than the hardware
								bool dontStretch = true;

								float rescaleXY = 1;
								float rescaleZ = 1;
								// casp[2] = height
								// caps[0] = width


								// if camera is taller than hardware
								if (casp[2] > asp[1])
								{
									//Debug.Log("cam is taller");
									rescaleZ = (asp[1] / casp[2]);
									if (dontStretch)
										rescaleXY = rescaleZ;
								}
								else if (asp[1] > casp[2])
								{
									//Debug.Log("HW is taller");
									rescaleZ = 1 / (casp[2] / asp[1]);

									// unfortunately have to stretch if its smaller;
									// alternative is to stretch
									//if (dontStretch)
									//	rescaleXY = rescaleZ;
								}

								// if camera is wider than hardware
								if (casp[0] > asp[0])
								{
									//Debug.Log("cam is wider");
									rescaleXY = (asp[0] / casp[0]);
									if (dontStretch)
										rescaleZ = rescaleXY;

								}
								else if (asp[0] > casp[0])
								{
									//Debug.Log("HW is wider");

									rescaleXY = 1 / (casp[0] / asp[0]);
									// unfortunately have to stretch if its smaller;
									// alternative is to stretch
									//if (dontStretch)
									//	rescaleZ = (rescaleXY);

								}

								renderSettings.aspectRatio.x *= rescaleXY;
								renderSettings.aspectRatio.y *= rescaleXY;
								renderSettings.aspectRatio.z *= rescaleZ;


								// aspect Voxiebox fix 
								if (VXProcess.Instance.VXInterface != VOXON_RUNTIME_INTERFACE.VXLED)
									renderSettings.aspectRatio *= 2;

							}

						}
						oCamScale = camScale;
						break;


				}

				// update previous variables to ensure its the latest change
				oAspectRatio = AspectRatio;
				oAspectRatioContext = AspectRatioContext;
			}



			renderSettings.captureAllData = 1;
			renderSettings.cameraClipRadius = CameraClipRadius;
			renderSettings.cameraOccluding = occlusionSetting;
			renderSettings.postionOffset = PostionOffset;
			renderSettings.postProcessDepth = PostProcessDepthMode;
			renderSettings.globalShadowValue = shadowOpacityValue;
			renderSettings.poltexHeadRoom = poltexHeadRoom;
			renderSettings.globalBrightness = GlobalBrightnessAdjust;
            renderSettings.depthTreshold = DepthThreshold;
			if (UseGlobalCullValue)
			{
				if (SortQueueByDistance == false)
				{
					SortQueueByDistance = true;

                }
                renderSettings.globalCullValue = (int)GlobalCullValue;
			}
			else
			{
				renderSettings.globalCullValue = (int)CullValues.NoCull;
			}

			if (UseGlobalCullValue == false && oUseGlobalCullValue == true)
			{
				RestoreCullMaterialCullValues();
            }
            oUseGlobalCullValue = UseGlobalCullValue;


            renderSettings.cameraClipNear = dCam.nearClipPlane;
			renderSettings.cameraClipFar = dCam.farClipPlane;
			
			if (showCameraDirection == CameraSightTrackingOptions.OnAfterAdjust)
            {
				showCameraDirectionTimeOut = Time.timeAsDouble + 2;

			}


			Shader.SetGlobalVector(camPosId, depthCamera.transform.position);
			Shader.SetGlobalFloat(camPosThreholdId, camPosThreshold);

		
			Shader.SetGlobalVector(viewAspectRatioId, renderSettings.aspectRatio);
			Shader.SetGlobalVector(viewOffsetPosId, renderSettings.postionOffset);
			Shader.SetGlobalInt(viewOcclusionId, renderSettings.cameraOccluding);
			Shader.SetGlobalInt(viewDepthPostId, renderSettings.postProcessDepth);
			Shader.SetGlobalFloat(viewClipShapeId, renderSettings.cameraClipRadius);
			Shader.SetGlobalFloat(shadowValueId, renderSettings.globalShadowValue);
			Shader.SetGlobalFloat(globalBrightnessId, renderSettings.globalBrightness);
			Shader.SetGlobalInt(poltexHeadRoomId, renderSettings.poltexHeadRoom);
            Shader.SetGlobalFloat(depthThresholdId, renderSettings.depthTreshold);
//			Shader.SetGlobalInt(globalCullId, renderSettings.globalCullValue); // not used
			Shader.SetGlobalInt(captureAllDataId, renderSettings.captureAllData);
		

		}



		void Start()
		{
			if (depthCamera == null)
			{
				depthCamera = GetComponent<UnityEngine.Camera>();
			}

			depthCamera.depthTextureMode = depthCamera.depthTextureMode | DepthTextureMode.Depth;
			initalCamTrans = depthCamera.transform;

			InitShader(); // this is to ensure that VX Camera is found

			VXProcess.Drawables.Add(this); // Hook the LitShaders Draw() function to be called by VXProcess

			// Wait a few frames before we update the depthCam as sometimes things are still initalising...
			// also useful to query the hardware
			StartCoroutine(LateStart(0.1f));
		}

		// Coroutine to mimic a LateStart functionality
		private System.Collections.IEnumerator LateStart(float delay)
		{
			// Wait for one frame
			yield return new WaitForSeconds(delay);
			depthCamerasUpdate(ref depthCamera, ref depthCamSettings);

		}


		public void FixedUpdate()
        {
			// Sort Render Cue By Distance of Camera
			if (SortQueueByDistance)
			{
				SortRenderByDistance();

			}
			
        }







		public void DepthBufferRefresh(bool forceClear = false)
		{

			if (clearShaderAfterDraw && isShaderInited || forceClear && isShaderInited)
			{


				DepthFrameNo++;
				int[] default_index = new int[] { 0, DepthFrameNo, 0 };

				index_buffer.SetData(default_index);    // set the default index

				Array.Clear(voxel_count, 0, voxel_count.Length);

				if (oCamWorldPos != depthCamera.transform.position)
				{
					Shader.SetGlobalFloat(camPosThreholdId, 10);
				}
				else
				{
					Shader.SetGlobalFloat(camPosThreholdId, camPosThreshold);
				}
				oCamWorldPos = depthCamera.transform.position;



				
                if (SimpleSetupMode && CameraOccluding && renderType == Vx_RenderType.Render_From_Depth_Data && shadowOpacityValue >= 0.005)
				{
					// ShadowMode
                    renderType = Vx_RenderType.Render_Shader_Direct;
					forceBackfaceCull = false;
					RestoreCullMaterialCullValues();
                }
				else if (SimpleSetupMode && CameraOccluding && renderType == Vx_RenderType.Render_Shader_Direct && shadowOpacityValue < 0.005)
				{
					shadowOpacityValue = 0;
	                renderType = Vx_RenderType.Render_From_Depth_Data;
					forceBackfaceCull = true;
					if (SortQueueByDistance == false)
					{
                        SortQueueByDistance = true;
                    }


                }
				else if (SimpleSetupMode && !CameraOccluding && renderType == Vx_RenderType.Render_From_Depth_Data)
				{
					renderType = Vx_RenderType.Render_Shader_Direct;
					forceBackfaceCull = false;
                    RestoreCullMaterialCullValues();

                }
				
				//forceShaderReInit = true;
			
				oRenderType = renderType;
			}



		

			if (renderType == Vx_RenderType.Render_From_Depth_Data || renderType == Vx_RenderType.Render_Debug_Depth_Data_On_2D)
			{
				for (int i = 0; i < depthCalls; i++)
				{
					depthCamera.Render();


				}
			}
			else
			{
				depthCamera.Render();
			}





		}



        void FitToCylinder(VXCaptureVolume VXCapture)
        {
            //Get height and radius of the capture volume.
            float height = VXCapture.vectorScale.y * 2f;
            float captureRadius = (VXCapture.vectorScale.x + VXCapture.vectorScale.z) / 4f;

            //Normalise the position vector, since we're not interested in magnitudes in an ortho view
            Vector3 cameraToCylinder = transform.position - VXCapture.transform.position;
            float lateralDistance = new Vector3(cameraToCylinder.x, 0f, cameraToCylinder.z).magnitude;

            //use the normalised vector to see how much of the cylinder depth should be added to the Y component of the ortho view
            Vector2 offsetVector = new Vector2(lateralDistance, transform.position.y - VXCapture.transform.position.y).normalized;
            float diff = 1 - Mathf.Abs(Mathf.Abs(offsetVector.x) - Mathf.Abs(offsetVector.y)); // Absolute difference between the two

            //Still trims off a tiny bit of the corners at some angles. 0.55f adds a 0.05f buffer to the capture size.
            float orthoSizeY = height * 0.55f + (captureRadius * diff);

            // Take the max of both to ensure full fit
            depthCamera.orthographicSize = Mathf.Max(orthoSizeY, captureRadius);
        }




        /// <summary>
        /// Called per Frame
        /// Ensures activeCamera is valid
        /// updates light camera position, direction, and updates shaders to current values
        /// </summary>
        void Update()
		{




			if (oResolution != resolution || forceShaderReInit)
			{
                
				DepthBufferRefresh();
				isShaderInited = false;
                DisposeBuffers();
				InitShader();
				forceShaderReInit = false;
			
			}

			
			

			if (isShaderInited)
			{
                // Camera Settings
                if (VXProcess.Instance.active == false) return;

                VxCaptureVolume = VXProcess.Instance.Camera;
				if (VxCaptureVolume == null) return;

                if (ForceLookAtVXCamera)
				{
					depthCamera.transform.LookAt(VxCaptureVolume.transform);
                    FitToCylinder(VxCaptureVolume);
                }

				int camOccludeChk = 0;
				if (CameraOccluding == true) camOccludeChk = 1;


				// hack to force camera scale to update settings if the display is locked to it
				if (AspectRatioContext == VxLit_AspectRatioFromTypes.Aspect_From_VXCaptureVolume_Extend && VxCaptureVolume != null||
					AspectRatioContext == VxLit_AspectRatioFromTypes.Aspect_From_VXCaptureVolume_Fit && VxCaptureVolume != null

					)
				{

					if (VxCaptureVolume.uniformScale)
					{
						camScale.x = VxCaptureVolume.BaseScale;
						camScale.z = VxCaptureVolume.BaseScale;
						camScale.y = VxCaptureVolume.BaseScale;

					}
					else
					{
						camScale.x = VxCaptureVolume.vectorScale.x;
						camScale.z = VxCaptureVolume.vectorScale.y;
						camScale.y = VxCaptureVolume.vectorScale.z;
					}

				}


				if (
					depthCamera.transform.position != depthCamSettings.camWorldPos ||
					AspectRatio != oAspectRatio ||
					AspectRatioContext != oAspectRatioContext ||
					camScale != oCamScale ||
					PostionOffset != renderSettings.postionOffset ||
					camOccludeChk != renderSettings.cameraOccluding ||
					CameraClipRadius != renderSettings.cameraClipRadius ||
					GlobalBrightnessAdjust != renderSettings.globalBrightness 
					|| PostProcessDepthMode != renderSettings.postProcessDepth
					|| shadowOpacityValue != oShadowOpacityValue
					|| DepthThreshold != renderSettings.depthTreshold
					|| UseGlobalCullValue != oUseGlobalCullValue

                    )
                {
	
					depthCamerasUpdate(ref depthCamera, ref depthCamSettings);

                }

				if (  poltexHeadRoom    != oPoltexHeadRoom    ||
					  LEDPoltexDrawSize != oLEDPoltexDrawSize )
				
                {
					forceShaderReInit = true;
                }
				


				Shader.SetGlobalMatrix(cameraId, VxCaptureVolume.transform.worldToLocalMatrix);

			}
			


		}

		void DisposeBuffers()
        {
			try
			{
				Array.Clear(depth_data, 0, depth_data.Length);
			} catch { }
			try
			{
				Array.Clear(poltex_data, 0, poltex_data.Length);
			} catch { }
			try
			{
				Array.Clear(voxel_render_buffer, 0, voxel_render_buffer.Length);
			} catch { }

			// Dispose Vs Release -> Release will just clear it in the GPU memory
			if (pdata_raw_buffer != null)
			{
				pdata_raw_buffer.Dispose();
				pdata_raw_buffer = null;
			}

			if (pdata_refined_buffer != null)
			{
				pdata_refined_buffer.Dispose();
				pdata_refined_buffer = null;
			}

			if (depth_buffer != null)
			{
				depth_buffer.Dispose();
				depth_buffer = null;
			}

			if (index_buffer != null)
			{
				index_buffer.Dispose(); // Dispose or Release?
				index_buffer = null;
			}

			Graphics.ClearRandomWriteTargets();
		}

		/// <summary>
		/// Called on Application Quit.
		/// Releases all compute buffers
		/// </summary>
		void OnApplicationQuit()
		{
            
	    	DisposeBuffers();
            
        }
		private void OnDisable()
		{
			DisposeBuffers();
		}
		
		[ExecuteInEditMode]
		void OnDrawGizmos()
		{
			if (depthCamera == null) return;

			if (showCameraDirection == CameraSightTrackingOptions.AlwaysOn ||
				showCameraDirection == CameraSightTrackingOptions.EditorOnly ||
				showCameraDirection == CameraSightTrackingOptions.OnAfterAdjust && showCameraDirectionTimeOut > Time.time
				)
			{
				// Set the gizmo color
				Gizmos.color = Color.red;

				// Draw a line representing the camera's forward direction
				Vector3 direction = depthCamera.transform.forward * depthCamera.farClipPlane;
				Gizmos.DrawLine(depthCamera.transform.position, depthCamera.transform.position + direction);

				Gizmos.color = Color.magenta;

				// Save the original matrix
				Matrix4x4 originalMatrix = Gizmos.matrix;

				// Set the gizmo matrix to match the camera's position and rotation
				Gizmos.matrix = Matrix4x4.TRS(depthCamera.transform.position, depthCamera.transform.rotation, Vector3.one);

				// Draw the wire cube with its rotation aligned to the camera
				Gizmos.DrawWireCube(Vector3.zero, new Vector3(1, 10, 10));

				// Restore the original matrix
				Gizmos.matrix = originalMatrix;
			}
		}



		/// <summary>
		/// Called by VXProcess per frame
		/// Collects voxel data
		/// tracks depth of each captured voxel 
		/// (only grab the voxel closest to the light per line)
		/// Draws all voxels
		/// </summary>

		public void RenderCameraDirection(int fcolor, int bcolor)
        {
			if (showCameraDirection == CameraSightTrackingOptions.AlwaysOn ||
				showCameraDirection == CameraSightTrackingOptions.OnAfterAdjust && showCameraDirectionTimeOut > Time.time
				)
			{

				// Inline function to map color codes
				int MapColor(int color) => color switch
				{
					1 => 0xff0000,
					2 => 0x00ff00,
					3 => 0x0000ff,
					4 => 0xffff00,
					5 => 0x00ffff,
					6 => 0xff00ff,
					7 => 0xffffff,
					_ => 0xffffff, // Default
				};

				bcolor = MapColor(bcolor % 8);
				fcolor = MapColor(fcolor % 8);


				bool drawLineThrough = false;
				bool drawLineToCentre = false;
				bool drawAngle = false;

				switch(showCameraStyle)
                {
					case CameraSightDrawOptions.LineToCentre:
						drawLineToCentre = true;

						break;
					case CameraSightDrawOptions.LineThrough:
						drawLineThrough = true;
						fcolor = 0xff0000;

						break;
					case CameraSightDrawOptions.JustAngle:
						drawAngle = true;
						break;
					case CameraSightDrawOptions.LineThroughAndAngle:
						drawAngle = true;
						drawLineThrough = true;
						fcolor = 0xff0000;

						break;
					case CameraSightDrawOptions.LineToCentreAndAngle:
						drawAngle = true;
						drawLineToCentre = true;
						break;

				}



				// DRAW EXACT LINE OUT OF TRUE DEPTH CAMERA
				// This is works but doesn't normalize the line it liter
				Vector3 backDirection = depthCamera.transform.forward * depthCamera.nearClipPlane; //
				Vector3 direction = depthCamera.transform.forward * depthCamera.farClipPlane;
				Vector3 VstartLine = depthCamera.transform.position + backDirection;
				Vector3 VendLine = depthCamera.transform.position + direction;

				point3d startLine = new point3d(VstartLine.x, VstartLine.y, VstartLine.z);
				point3d endLine = new point3d(VendLine.x, VendLine.y, VendLine.z);

				point3d U2Vscale = new point3d();

				// Hmmm need to work this out based on the VXVolumeCapture Properties
				if (VxCaptureVolume.uniformScale)
				{
					U2Vscale.x = VxCaptureVolume.BaseScale * 0.1f;
					U2Vscale.y = U2Vscale.z = U2Vscale.x;
				}
				else
				{
					U2Vscale.x = VxCaptureVolume.vectorScale.x * 0.1f;
					U2Vscale.y = VxCaptureVolume.vectorScale.y * 0.1f;
					U2Vscale.z = VxCaptureVolume.vectorScale.z * 0.1f;

				}
				// Translate Unit to Voxon Space
				startLine *= U2Vscale;
				endLine *= U2Vscale;

				startLine.x *= .1f;
				startLine.y *= .1f;
				startLine.z *= .1f;

				endLine.x *= .1f;
				endLine.y *= .1f;
				endLine.z *= .1f;

				int magnitude = 1;
				// work out the magnitude based on the size
				if (VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.VXLED) {
					
					int[] sizes = VXProcess.Runtime.GetLEDSizeDimensions();

					try
					{
						sizes[0] /= 64;
						sizes[1] /= 64;

						magnitude = (sizes[0]);
				
					} catch
                    {

                    }
				}
				magnitude = 1;

				startLine = new point3d(startLine.x, -startLine.z, -startLine.y);
				endLine = new point3d(endLine.x, -endLine.z, -endLine.y );

				if (drawLineToCentre)
				{
					endLine = new point3d(0, 0, 0); // Draw Only to centre of display Dont render straight through
													//Debug.Log($"Post Start{startLine} end{endLine}");
					fcolor = 0xffff00;
				}
#if codeblock
				VXProcess.Runtime.LogToScreenExt(100, 100, 0xff0000, 0, $"start = X{startLine.x}, Y{startLine.y}, Z{startLine.z}");
				VXProcess.Runtime.LogToScreenExt(100, 110, 0xff0000, 0, $"end = X{endLine.x}, Y{endLine.y}, Z{endLine.z}");
#endif

				if (drawLineThrough || drawLineToCentre)
				{

					VXProcess.Runtime.DrawLine(ref startLine, ref endLine, fcolor);
					VXProcess.Runtime.DrawSphere(ref startLine, 0.1f, 0, fcolor);
					VXProcess.Runtime.DrawSphere(ref endLine, 0.1f, 0, fcolor);
				}

				if (drawAngle)
				{



					// SHOW NORMALSED ANGLE ON VX DISPLAY -- this shows the angle in which the light is coming from.. it is always in view. 
					// Calculate direction vector
					direction = depthCamera.transform.forward * depthCamera.farClipPlane;

					// Get the start position, we use the center of the display but you could offset it here. 
					VstartLine = new Vector3();

					// Calculate the end position based on the desired length
					Vector3 directionNormalized = direction.normalized; // Normalize the direction
					float maxLength = 10f; //  
					VendLine = VstartLine + directionNormalized * maxLength;
					VstartLine = -VendLine;

					// Convert to point3d
					startLine = new point3d(VstartLine.x, VstartLine.y, VstartLine.z);
					endLine = new point3d(VendLine.x, VendLine.y, VendLine.z);

					// Scale from Unity to Voxon Space
					startLine.x *= 0.1f;
					startLine.y *= 0.1f;
					startLine.z *= 0.1f;

					endLine.x *= 0.1f;
					endLine.y *= 0.1f;
					endLine.z *= 0.1f;

					// Swap Axes to the Voxon 3D Space
					startLine = new point3d(startLine.x, -startLine.z, -startLine.y);
					endLine = new point3d(endLine.x, -endLine.z, -endLine.y);

					// apply position offset 
					// get size of panels

					// make it smaller
					startLine.x *= 0.33f;
					startLine.y *= 0.33f;
					startLine.z *= 0.33f;
					endLine.x *= 0.33f;
					endLine.y *= 0.33f;
					endLine.z *= 0.33f;

					// offset it 
					point3d posOffset = new point3d(-0.8f, -0.8f, -0.8f);
					startLine += posOffset;
					endLine += posOffset;




#if codeblock
					VXProcess.Runtime.LogToScreenExt(100, 120, 0xff0000, 0, $"Angle start = X{startLine.x}, Y{startLine.y}, Z{startLine.z}");
					VXProcess.Runtime.LogToScreenExt(100, 130, 0xff0000, 0, $"Angle end = X{endLine.x}, Y{endLine.y}, Z{endLine.z}");
#endif
					// Draw the line and spheres
					VXProcess.Runtime.DrawLine(ref startLine, ref endLine, 0x0000ff);
					VXProcess.Runtime.DrawSphere(ref startLine, 0.05f, 0, 0x00ff00);
					VXProcess.Runtime.DrawSphere(ref endLine, 0.05f, 0, 0xff0000);
				}

			}
		}

		public void Draw()
		{
			
			if (VXProcess.Instance.IsClosingVXProcess() == true || VXProcess.Runtime == null || !isShaderInited )
			{
				return;
			}
			else if (!gameObject.activeInHierarchy || !enabled )
            {
				Debug.Log($"VxLit Renderer is not active");
				return;
			}
			else if (!isShaderInited)
			{
				Debug.Log($"VxLit Renderer  not initialized, initializing now");
				InitShader();
				return;
			}
			else if (resolution <= 8)
			{
				Debug.Log($"VxLit Renderer's resolution needs to be at least a value of 8 to render");
			
				return;
			}


			RenderCameraDirection(showCameraFColor, showCameraBColor);

			


			if (renderType == Vx_RenderType.Render_Debug_Poltex_On_2D_Display ||
				renderType == Vx_RenderType.Render_Debug_Depth_Data_On_2D ||
				renderType == Vx_RenderType.Render_Raw_Poltex_FromGPU ||
				renderType == Vx_RenderType.Render_Raw_Poltex_FromGPU_Cull)
			{
				RenderVoxels(renderType);
			} else {

				for (int i = 0; i < renderDrawCount; i++) {
					RenderVoxels(renderType);
				}
			}
			

          
		}


		void RenderVoxels(Vx_RenderType renderType)
        {
			if (resolution < 8 || isShaderInited == false) return;

			int voxBatchFlag = useVoxBatchCalling ? -1 : 0; 

			if (renderForVoxieBox) voxBatchFlag = 0;

			float f;
			int groups;

			DepthBufferRefresh();
	
			

			// draw from depth buffer
			switch (renderType)
            {
                case Vx_RenderType.Render_Shader_Direct:
                    // This method optimized for displays and supports shadows


                    if (clearTime < Time.timeAsDouble)
                    {

                        clearTime = Time.timeAsDouble + clearInterval;
						pdata_refined_buffer.GetData(poltex_data); // gather the poltex_data
                        index_buffer.GetData(current_index); // index is pixel
                        poltextMax = current_index[0];
                        PoltexHeadRoomCheck(current_index[0]);

                    }

                    // if the total poltex size is smaller than the group size don't worry about dividing it up.
                    if (poltextMax < group_size)
                    {
                        voxel_count[0] = current_index[0];
                        if (RenderWithSpheres)
                            VXProcess.Runtime.DrawSphereBulkCnt(poltex_data, SphereRadius, poltextMax);
                        else
                            VXProcess.Runtime.DrawUntexturedMesh(poltex_data, poltextMax, null, 0, voxBatchFlag,
                                0xffffff);

                    }
                    else
                    {
                        // Draw the voxels in groups... so only sending 1 MB of data -- might be better when Voxels are optimized
                        groups = Mathf.CeilToInt((float)poltextMax / (float)group_size);

                        // copy each group into an array and sum it together...
                        for (int idx = 0; idx < groups; idx++)
                        {
                            // Group size unless last group
                            voxel_count[idx] = idx < (groups - 1) ? group_size : (poltextMax % group_size);

                            if (voxel_count[idx] == 0) continue;

                            try
                            {
                                System.Array.Copy(poltex_data, idx * group_size, voxel_render_buffer, 0,
                                    voxel_count[idx]);
                            }
                            catch (Exception e)
                            {
                                Debug.Log(
                                    $"poltex data and render buffer mismatch : {poltextMax} vs {idx * group_size + voxel_count[idx]} exception = {e}");
                                break;
                            }

                            if (RenderWithSpheres)
                            {
                                VXProcess.Runtime.DrawSphereBulkCnt(voxel_render_buffer, SphereRadius,
                                    voxel_count[idx]);
                            }
                            else
                            {
                                VXProcess.Runtime.DrawUntexturedMesh(voxel_render_buffer, voxel_count[idx], null, 0,
                                    voxBatchFlag, 0xffffff);
                            }

                        }
                    }

                    break;


                case Vx_RenderType.Render_As_Single_Call:
					// For Debug Only - just to see if its working.... it might overload the array
					pdata_refined_buffer.GetData(poltex_data); // light buffer is colour -- poltex data u
                    index_buffer.GetData(current_index); // index is pixel
                    PoltexHeadRoomCheck(current_index[0]);
                    voxel_count[0] = current_index[0];

                    // standard draw all of them.... based on the current_index[0] for max
                    if (RenderWithSpheres)
                    {
                        VXProcess.Runtime.DrawSphereBulkCnt(poltex_data, SphereRadius, current_index[0]);

                    }
                    else
                    {
                        // flag -1 to draw using batch with led for optimized 
                        VXProcess.Runtime.DrawUntexturedMesh(poltex_data, current_index[0], null, 0, voxBatchFlag,
                            0xffffff);


                    }

                    break;

                case Vx_RenderType.Render_From_Depth_Data:



					pdata_refined_buffer.GetData(poltex_data); // light buffer is colour -- poltex data u
					depth_buffer.GetData(depth_data); // you can view the depth data if you want to debug it.  

					int gpc = 0;
					int inx = 0;
					int bufferLength = voxel_render_buffer.Length;
					// iterates through the in the depth data, maps it the poltex and then writes all the valid poltex entries to the voxel buffer to the 
					for (int i = 0; i < depth_data.Length; i++)
					{
						depthV3 depthData = depth_data[i];
						int depthIndex = depthData.data_index;

						// Skip invalid or irrelevant entries
						if (depthIndex == -1 || depthData.value == -1 ||
							(depthIndex == 0 && depthData.value == 0 && depthData.depth_frame == 0) ||
							depthData.depth_frame != DepthFrameNo)
						{
							continue;
						}

						if (depthData.data_index > poltex_data.Length)
						{

							if (!managePoltexDepthOverflow) continue;

							if ((int)VXProcess.Instance.VXUReportingLevel >= (int)VXProcessReportLevel.General)
							{
								Debug.LogWarning($"VxLitRendererV4 - Poltex buffer is full! Increasing Poltex Headroom and clearning the buffer to draw again... To change this behaviour edit the 'forceClearOnFilledPoltexBuffer' variable in the .cs script or adjust 'clearShaderAfterDraw' setting.");

							}
							if (clearShaderAfterDraw == true)
							{
								poltexHeadRoom++;
								if (poltexHeadRoom > POLTEX_MAX_HEADROOM) poltexHeadRoom = POLTEX_MAX_HEADROOM;
							}
							if (forceClearOnFilledPoltexBuffer)
							{
								DepthBufferRefresh(true);
							}
							continue;

						}

						poltex pt = poltex_data[depthData.data_index];

						// if the color is black or if the ID is out of range skip; .U is depth value
						if (pt.col == 0 || pt.v == -1) continue;

						if (inx >= bufferLength)
						{
							VXProcess.Runtime.DrawUntexturedMesh(voxel_render_buffer, inx, null, 0, voxBatchFlag,
								0xffffff);
							inx = 0;
							voxel_count[gpc++] = voxel_render_buffer.Length;

						}

						voxel_render_buffer[inx].x = pt.x;
						voxel_render_buffer[inx].y = pt.y;
						voxel_render_buffer[inx].z = pt.z;
						voxel_render_buffer[inx].col = pt.col;

						inx++;
					}

					// send off the last batch
					if (inx > 0)
					{
						VXProcess.Runtime.DrawUntexturedMesh(voxel_render_buffer, inx, null, 0, voxBatchFlag, 0xffffff);
					}

					voxel_count[gpc] = inx;


					break;

                /* Various DEBUG renders follow */
                case Vx_RenderType.Render_Debug_Draw_Using_DrawVox:
					// This method optimized for displays and supports shadows
					pdata_refined_buffer.GetData(poltex_data); // gather the poltex_data
                    index_buffer.GetData(current_index); // index is pixel
                    PoltexHeadRoomCheck(current_index[0]);

                    for (int idx = 0; idx < current_index[0]; idx++)
                    {
                        VXProcess.Runtime.DrawVoxel(poltex_data[idx].x, poltex_data[idx].y, poltex_data[idx].z,
                            poltex_data[idx].col);
                    }



                    break;

                case Vx_RenderType.Render_Raw_Poltex_FromGPU:

					
						clearTime = Time.timeAsDouble + clearInterval;
						pdata_raw_buffer.GetData(poltex_data); // gather the poltex_data
						index_buffer.GetData(current_index); // index is pixel
						poltextMax = current_index[2];
						PoltexHeadRoomCheck(current_index[2]);

			

					// if the total poltex size is smaller than the group size don't worry about dividing it up.
					if (poltextMax < group_size)
					{
						voxel_count[0] = poltextMax;
						if (RenderWithSpheres)
							VXProcess.Runtime.DrawSphereBulkCnt(poltex_data, SphereRadius, poltextMax);
						else
							VXProcess.Runtime.DrawUntexturedMesh(poltex_data, poltextMax, null, 0, voxBatchFlag,
								0xffffff);

					}
					else
					{
						// Draw the voxels in groups... so only sending 1 MB of data -- might be better when Voxels are optimized
						groups = Mathf.CeilToInt((float)poltextMax / (float)group_size);

						// copy each group into an array and sum it together...
						for (int idx = 0; idx < groups; idx++)
						{
							// Group size unless last group
							voxel_count[idx] = idx < (groups - 1) ? group_size : (poltextMax % group_size);

							if (voxel_count[idx] == 0) continue;

							try
							{
								System.Array.Copy(poltex_data, idx * group_size, voxel_render_buffer, 0,
									voxel_count[idx]);
							}
							catch (Exception e)
							{
								Debug.Log(
									$"poltex data and render buffer mismatch : {poltextMax} vs {idx * group_size + voxel_count[idx]} exception = {e}");
								break;
							}

							if (RenderWithSpheres)
							{
								VXProcess.Runtime.DrawSphereBulkCnt(voxel_render_buffer, SphereRadius,
									voxel_count[idx]);
							}
							else
							{
								VXProcess.Runtime.DrawUntexturedMesh(voxel_render_buffer, voxel_count[idx], null, 0,
									voxBatchFlag, 0xffffff);
							}

						}
					}
					break;

				case Vx_RenderType.Render_Raw_Poltex_FromGPU_Cull:


					clearTime = Time.timeAsDouble + clearInterval;
					pdata_raw_buffer.GetData(poltex_data); // gather the poltex_data
					index_buffer.GetData(current_index); // index is pixel
					poltextMax = current_index[2];
					PoltexHeadRoomCheck(current_index[2]);
					depth_buffer.GetData(depth_data);



					//_AllPoltexData[data_index].v = depth_index;
					//_AllPoltexData[data_index].u = depth;
					//
					//
					point3d max = new point3d(20, 20, 20);

					for (int i = 0; i < poltex_data.Length; i++)
					{
						poltex_data[i].col = 0;
					}

						for (int i = 0; i < depth_data.Length; i++)
                    {

						poltex_data[depth_data[i].data_index].col = 0xff0000;







					}
					


					// if the total poltex size is smaller than the group size don't worry about dividing it up.
					if (poltextMax < group_size)
					{
						voxel_count[0] = poltextMax;
						if (RenderWithSpheres)
							VXProcess.Runtime.DrawSphereBulkCnt(poltex_data, SphereRadius, poltextMax);
						else
							VXProcess.Runtime.DrawUntexturedMesh(poltex_data, poltextMax, null, 0, voxBatchFlag,
								0xffffff);

					}
					else
					{
						// Draw the voxels in groups... so only sending 1 MB of data -- might be better when Voxels are optimized
						groups = Mathf.CeilToInt((float)poltextMax / (float)group_size);

						// copy each group into an array and sum it together...
						for (int idx = 0; idx < groups; idx++)
						{
							// Group size unless last group
							voxel_count[idx] = idx < (groups - 1) ? group_size : (poltextMax % group_size);

							if (voxel_count[idx] == 0) continue;

							try
							{
								System.Array.Copy(poltex_data, idx * group_size, voxel_render_buffer, 0,
									voxel_count[idx]);
							}
							catch (Exception e)
							{
								Debug.Log(
									$"poltex data and render buffer mismatch : {poltextMax} vs {idx * group_size + voxel_count[idx]} exception = {e}");
								break;
							}

							if (RenderWithSpheres)
							{
								VXProcess.Runtime.DrawSphereBulkCnt(voxel_render_buffer, SphereRadius,
									voxel_count[idx]);
							}
							else
							{
								VXProcess.Runtime.DrawUntexturedMesh(voxel_render_buffer, voxel_count[idx], null, 0,
									voxBatchFlag, 0xffffff);
							}

						}
					}
					break;

				//Note these 2D maps should be in their own function and could do with a tidy up but they work
				case Vx_RenderType.Render_Debug_Poltex_On_2D_Display:

					pdata_refined_buffer.GetData(poltex_data);
                    index_buffer.GetData(current_index);

                    PoltexHeadRoomCheck(current_index[0]);


                    // draw 2D map 
                    bool mapYtoZ = false;
                    bool invertX = true;

                    float mapSize = 50;
                    float pixMap = (1 + (100 / resolution) / (AspectRatio.x + AspectRatio.y / 2)) * mapSize;

                    int colMapPosX = 500;
                    int colMapPosY = 350;
                    int depMapPosX = colMapPosX + (int)(pixMap * 10);
                    int depMapPosY = colMapPosY;

                    int x = 0;
                    int y = 0;

                    int wastecount = 0;
                    //	int noclearcount = 0;
                    int noColCount = 0;
                    float size = 2;
                    int maxY = 0;

                    /*
                    // VPS management for reports
                    if (VXProcess.Instance.GetVPS() < 5) drawMap2DRes++;
                    if (VXProcess.Instance.GetVPS() > 15) drawMap2DRes--;
                    if (drawMap2DRes < 1) drawMap2DRes = 1;
                    if (drawMap2DRes > 10) drawMap2DRes = 10;
                    */


                    for (int i = 0; i < current_index[0]; i++)
                    {
                        int col = poltex_data[i].col;

                        if (poltex_data[i].col == 0x000000)
                        {
                            noColCount++;
                            continue;
                        }

                        if (poltex_data[i].v == -1)
                        {
                            col = 0x00ffff;
                            wastecount++;
                        }
                        //if (poltex_data[i].v == 0) { col = 0xffff00; noclearcount++; }

                        if (i % drawMap2DRes != 0) continue;

                        f = -(poltex_data[i].x * pixMap);
                        x = (int)f * (int)(size);
                        if (invertX) x = -x;
                        if (mapYtoZ) f = (poltex_data[i].z * pixMap);
                        else f = (poltex_data[i].y * pixMap);
                        y = (int)f * (int)(size);

                        VXProcess.Runtime.ScreenDrawPix(colMapPosX + x, colMapPosY + y, col);

                        // draw depth map

                        f = (poltex_data[i].z + AspectRatio.z) / (AspectRatio.z * 2) * 255;

                        col = 256 - (int)f;
                        col = (col << 16) | (col << 8) | col;

                        VXProcess.Runtime.ScreenDrawPix(depMapPosX + x, depMapPosY + y, col);
                        if (colMapPosY + x > maxY) maxY = colMapPosY + x;
                    }

                    VXProcess.Runtime.LogToScreenExt(colMapPosX, maxY + 10, 0x00ff00, -1,
                        $"marked to be deleted count: {wastecount}");
                    //VXProcess.Runtime.LogToScreenExt(colMapPosX, maxY + 20, 0x00ff00, -1, $"no clear flag count: { noclearcount }");
                    VXProcess.Runtime.LogToScreenExt(colMapPosX, maxY + 30, 0x00ff00, -1,
                        $"no col value count: {noColCount}");

                    break;


                //Note these 2D maps should be in their own function and could do with a tidy up but they work
                case Vx_RenderType.Render_Debug_Depth_Data_On_2D:
                case Vx_RenderType.Render_Debug_Depth_Data_On_Both:


					pdata_refined_buffer.GetData(poltex_data);


                    // Draw Depth Array

                    bool invertXd = false;
                    bool invertYd = false;

                    int spacing = (int)((float)(256 / resolution));
                    int depthColArrayX = 50 + (resolution / 2);
                    int depthColArrayY = 200 + (resolution / (spacing + 1));
                    int depthArrayX = depthColArrayX + 20 + (resolution);
                    int depthArrayY = depthColArrayY;

                    // VPS management for reports
                    /*
                    if (VXProcess.Instance.GetVPS() < 5) drawMap2DRes++;
                    if (VXProcess.Instance.GetVPS() > 15) drawMap2DRes--;
                    if (drawMap2DRes < 1) drawMap2DRes = 1;
                    if (drawMap2DRes > 10) drawMap2DRes = 10;
                    */
                    depth_buffer.GetData(depth_data); // you can view the depth data if you want to debug it.

                    for (int i = 0; i < depth_data.Length; i++)
                    {

                        if (depth_data[i].depth_frame != DepthFrameNo) continue;
                        if (depth_data[i].data_index == 0 && depth_data[i].value == 0 &&
                            depth_data[i].depth_frame == 0) continue;

                        if (renderType == Vx_RenderType.Render_Debug_Depth_Data_On_Both)
                            VXProcess.Runtime.DrawVoxel(poltex_data[depth_data[i].data_index].x,
                                poltex_data[depth_data[i].data_index].y, poltex_data[depth_data[i].data_index].z,
                                poltex_data[depth_data[i].data_index].col);

                        if (i % drawMap2DRes != 0) continue;

                        // Get X and Y from Cantor Pairing
                        int z = i;
                        int w = (int)Math.Floor((Math.Sqrt(8 * z + 1) - 1) / 2);
                        int t = (w * w + w) / 2;
                        int k2 = z - t;
                        int k1 = w - k2;



                        x = k1 + spacing;
                        y = k2 + spacing;

                        if (invertXd) x = -x;
                        if (invertYd) y = -y;

                        f = (depth_data[i].value);

                        f *= 255; // now map it to be between 0 and 255
                        // normalise to 0 too 1
                        //	col = (col / 2) * 255; // if aspect 2 is
                        int col = (int)f;

                        col = (col << 16) | (col << 8) | col;
                        if (col < 1) continue; // col = 0xff0000;


                        VXProcess.Runtime.ScreenDrawPix(depthColArrayX + x, depthColArrayY + y,
                            poltex_data[depth_data[i].data_index].col);

                        VXProcess.Runtime.ScreenDrawPix(depthArrayX + x, depthArrayY + y, col);
                        VXProcess.Runtime.LogToScreenExt(0, 20, 0x00ff00, -1, $"spacing: {spacing}");

                    }

                    break;
            }



        
		}

        private void PoltexHeadRoomCheck(int currentIndex)
        {

			/* reducing poltex isn't working right ... 
			 
			// if only a 1 / 3 of the voxels are being used scale back down...
			int groups = Mathf.CeilToInt((float)currentIndex / (float)group_size);


			if ((groups + 3) < group_size && poltexHeadRoom > 2)
			{
				poltexHeadRoom--;

			}
			*/

			// dynamic head room for poltex to avoid the poltex from being filled.
			if (currentIndex >= poltex_data.Length - 1)
			{
				poltexHeadRoom++;
				if (poltexHeadRoom > POLTEX_MAX_HEADROOM) poltexHeadRoom = POLTEX_MAX_HEADROOM;
				DepthBufferRefresh();
				isShaderInited = false;
				DisposeBuffers();
				InitShader();
				forceShaderReInit = false;
				DepthBufferRefresh();


			}

			
			
		}

        public int Report(int xPos, int yPos)
        {
            if (VXProcess.Runtime == null || VXProcess.Instance.GetExclusive2DMode() == true) return 0;


            int w = 350;
            int h = 100;
            int bgCol = 0x250000;
            int groups = Mathf.CeilToInt((float)current_index[0] / (float)group_size);
            VXProcess.Runtime.ScreenDrawRectangleFill(xPos, yPos, xPos + w, (yPos + h) + ((10 * (groups / 2 + groups % 2) - 10) ), bgCol);
            Vx_RenderType rType = (Vx_RenderType)renderType;
            VXProcess.Runtime.LogToScreenExt(xPos, yPos, 0xffffff, -1,
                $"                    VxLitRendererV4 Report");
            yPos += 10;

			int rCol = 0x00ff00;
			if (!isShaderInited) rCol = 0xff0000;

			VXProcess.Runtime.LogToScreenExt(xPos, yPos, rCol, -1,
				$"is Render Active?       Mode: ");
			VXProcess.Runtime.LogToScreenExt(xPos, yPos, 0xffffff, -1,
                $"                   {isShaderInited}       {rType.ToString()}");
            yPos += 10;

            if (groups == group_count) rCol = 0xff0000;
            else if (groups + 3 >= group_count) rCol = 0xffff00;

            VXProcess.Runtime.LogToScreenExt(xPos, yPos, rCol, -1,
                $"Resolution:        Voxels In Group:         Count:  ");
			VXProcess.Runtime.LogToScreenExt(xPos, yPos, 0xffffff, -1,
				$"            {resolution}                     {group_size}         {groups} / {group_count}");


			yPos += 10;

            totalVoxelCount = voxel_count[0];
            try
            {
				int o = 0;
                for (int i = 0; i < groups; i++)
                {
                    if (voxel_count[i] == 0) break;

                    int[] gCol = { 0xff0000, 0x00ff00, 0x0000ff, 0xffff00, 0xff00ff, 0x00ffff, 0xffffff };

					if (o == 0)
					{
						VXProcess.Runtime.LogToScreenExt(xPos, yPos, gCol[i % 7], -1,
							$"Group    Voxel Count  ");
						VXProcess.Runtime.LogToScreenExt(xPos, yPos, 0xcccccc, -1,
							$"      {i}              {voxel_count[i]}");
						o = 1;
						yPos += 10;

					}
					else
					{
						VXProcess.Runtime.LogToScreenExt(xPos + 175, yPos - 10, gCol[i % 7], -1,
								$"Group    Voxel Count  ");
						VXProcess.Runtime.LogToScreenExt(xPos + 175, yPos - 10, 0xcccccc, -1,
							$"      {i}              {voxel_count[i]}");

						o = 0;
					}
                    totalVoxelCount += voxel_count[i];
				}
			}
            catch (Exception e)
            {
                Debug.Log($"Error building Report {e}");
            }
			if (totalVoxelCount == current_index[0]) rCol = 0x00ff00;
            else rCol = 0xff0000;

            VXProcess.Runtime.LogToScreenExt(xPos, yPos, rCol, -1,
                $"Total Voxel Count:   ");

			VXProcess.Runtime.LogToScreenExt(xPos, yPos, 0xffffff, -1,
			    $"                   {totalVoxelCount}");

			yPos += 10;
			VXProcess.Runtime.LogToScreenExt(xPos, yPos, rCol, -1,
				$"GPU PoltexCount: ");

			VXProcess.Runtime.LogToScreenExt(xPos, yPos, 0xffffff, -1,
				$"                 {current_index[0]}");

			yPos += 10;
			VXProcess.Runtime.LogToScreenExt(xPos, yPos, 0xff00ff, -1,
		   $"GPU returned DepthIndex:        CPU Depth Index: ");
			VXProcess.Runtime.LogToScreenExt(xPos, yPos, 0xffffff, -1,
		   $"                         {current_index[1]}                        {DepthFrameNo}");


			yPos += 10;
			VXProcess.Runtime.LogToScreenExt(xPos, yPos, 0x00ffff, -1,
                $"poltex_data[]:                             ");
			VXProcess.Runtime.LogToScreenExt(xPos, yPos, 0xffffff, -1,
				$"                       {poltex_data.Length}");
			yPos += 10;
			VXProcess.Runtime.LogToScreenExt(xPos, yPos, 0x00ffff, -1,
				$"voxel_render_buffer[]: ");
			VXProcess.Runtime.LogToScreenExt(xPos, yPos, 0xffffff, -1,
				$"                       {voxel_render_buffer.Length}");

			yPos += 10;
			VXProcess.Runtime.LogToScreenExt(xPos, yPos, 0x00ffff, -1,
				$"depth_data[]:                             ");
			VXProcess.Runtime.LogToScreenExt(xPos, yPos, 0xffffff, -1,
				$"                       {depth_data.Length}");

			return yPos; 
		}

		// Function to calculate the inverse of the Cantor pairing function
		static (int k1, int k2) CantorInverse(int z)
		{
			// Calculate t using the inverse of the Cantor pairing
			int t = (int)(Math.Floor((-1 + Math.Sqrt(1 + 8 * z)) / 2));

			// Calculate k2 and k1 using the formula
			int k2 = z - (t * (t + 1)) / 2;
			int k1 = t - k2;

			return (k1, k2);
		}



		// Helper class to hold GameObject and its Y position
		private class GameObjectDistance
		{
			public GameObject gameObject;
			public float distance;


			public GameObjectDistance(GameObject obj, float distance)
			{
				gameObject = obj;
				this.distance = distance;
			}
		}




		public void RestoreCullMaterialCullValues()
		{

#if UNITY_6000_0_OR_NEWER
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
#else
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
#endif


            // Create a list to store the GameObjects along with their Y positions
            System.Collections.Generic.List<GameObjectDistance> objectsWithDistance = new List<GameObjectDistance>();

            foreach (GameObject obj in allObjects)
            {


                Renderer renderer = obj.GetComponent<Renderer>();
                if (renderer == null) continue;

                if (renderer.material.shader.name.Contains("Voxon/VxLit"))
                {

                    // idea to have to material not to add to render cue...
                    objectsWithDistance.Add(new GameObjectDistance(obj, Vector3.Distance(depthCamera.transform.position, obj.transform.position)));


                }
            }

            // Sort the list based on distance position (ascending order)
            objectsWithDistance = objectsWithDistance.OrderBy(obj => obj.distance).ToList();
            //	renderQueueValue = obj.transform.y * 100;


            int idValInr = Time.frameCount % 2 == 0 ? 0 : 1;
            if (!oscillateIDs)
            {
                idValInr = 0;

            }

            foreach (var obj in objectsWithDistance)
            {
                Renderer renderer = obj.gameObject.GetComponent<Renderer>();
 

                if (renderer.material.GetFloat("_OriginalCullMode") != -1)
				{

                    renderer.material.SetFloat("_CullMode", renderer.material.GetFloat("_OriginalCullMode"));
					renderer.material.SetFloat("_OriginalCullMode", -1);

                }

            }
			//Debug.Log("RestoreCullMaterialCullValues() - Restored all materials to their original cull values");

        }

		

		public void SortRenderByDistance()
		{
#if UNITY_6000_0_OR_NEWER
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
#else
			GameObject[] allObjects = FindObjectsOfType<GameObject>();
#endif


            // Create a list to store the GameObjects along with their Y positions
            System.Collections.Generic.List<GameObjectDistance> objectsWithDistance = new List<GameObjectDistance>();

			foreach (GameObject obj in allObjects)
			{


				Renderer renderer = obj.GetComponent<Renderer>();
				if (renderer == null) continue;

				if (renderer.material.shader.name.Contains("Voxon/VxLit"))
				{

					// idea to have to material not to add to render cue...
					objectsWithDistance.Add(new GameObjectDistance(obj, Vector3.Distance(depthCamera.transform.position, obj.transform.position)));


				}
			}

			// Sort the list based on distance position (ascending order)
			objectsWithDistance = objectsWithDistance.OrderBy(obj => obj.distance).ToList();
			//	renderQueueValue = obj.transform.y * 100;
			int queVal = 0;
			float shadValue = 0;
			float idVal = 1;

            int idValInr = Time.frameCount % 2 == 0 ? 0 : 1;
            if (!oscillateIDs)
            {
                idValInr = 0;

            }

			foreach (var obj in objectsWithDistance)
			{
				Renderer renderer = obj.gameObject.GetComponent<Renderer>();
				shadValue = 0;


				
				
				if (this.autoEnableBackfaceCullWhenOccluding == true && CameraOccluding && renderer.material.GetFloat("_CullMode") == 0 && shadowOpacityValue < 0.005)
                {
					renderer.material.SetFloat("_CullMode", 2);
				}

				if (this.UseGlobalCullValue || forceBackfaceCull == true)
				{
				
					if (renderer.material.GetFloat("_OriginalCullMode") == -1 )
					{
                        renderer.material.SetFloat("_OriginalCullMode", renderer.material.GetFloat("_CullMode"));
                    }

					if (forceBackfaceCull == true)
					{
                        renderer.material.SetFloat("_CullMode", 2);
                    } else
					{
                        renderer.material.SetFloat("_CullMode", (int)this.GlobalCullValue);
                    }

					

				}
				

				if (renderer.material.HasProperty("_MatShadowValue")) {
					shadValue = renderer.material.GetFloat("_MatShadowValue");
				}
				if (shadValue == 0 )
                {

                    if (!reverseZSortingOrder)
						renderer.material.renderQueue = 2000 + queVal;
				    else
                        renderer.material.renderQueue = 3000 + queVal;

				} else
                {
                    if (!reverseZSortingOrder)
						renderer.material.renderQueue = 3000 + queVal;
                    else
                        renderer.material.renderQueue = 2000 + queVal;
				}

                if (useIDs)
                {
                    renderer.material.SetFloat("_Id", idVal + idValInr);
                    idVal = idVal + 1 + idValInr;
                }
                else
                {
                  //  renderer.material.SetFloat("_Id", 0);
				}

                if (!reverseZSortingOrder)
                    queVal--;
                else
                    queVal++;


				if (forceSetAllMaterialShadowValuesToZero)
				{
					renderer.material.SetFloat("_MatShadowValue", 0);
				}


			}
		}

	}
}