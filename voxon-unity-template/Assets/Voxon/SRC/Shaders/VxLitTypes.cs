using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Define common data types to be used VxLit System 

namespace Voxon.VxLit
{
	/// <summary>
	/// Structure tracking Depth values and associated
	/// index value applies to
	/// </summary>
	[System.Serializable]
	public struct depthV2
	{
		public float value;         // Depth value at this point
		public int data_index;      // Data index where depth related value is stored

	};

    [System.Serializable]
    public struct depthV3
    {
        public float value;         // Depth value at this point
        public int data_index;      // Data index where depth related value is stored
        public int depth_frame;     // The DepthFrame that is stored  
	};

	[System.Serializable]
	public enum CameraSightTrackingOptions
	{
		Off = 0,
		AlwaysOn = 1,
		OnAfterAdjust = 2,
		EditorOnly = 3,
		MAX_VALUE = 4,
	}

	[System.Serializable]
	public enum CameraSightDrawOptions
	{
		LineThrough = 0,
		LineToCentre = 1,
		JustAngle = 2,
		LineToCentreAndAngle = 3,
		LineThroughAndAngle = 4,
	}


	[System.Serializable]
	public struct VxRenderSettings
	{
		public int cameraOccluding; // 0 no occluding
		public int captureAllData; 
		public int postProcessDepth;
		public float cameraClipRadius;
		public float globalShadowValue;
		public float globalBrightness;
		public int globalCullValue;
		public int poltexHeadRoom;
		public Vector3 aspectRatio;
		public Vector3 postionOffset;
        public float depthTreshold;
		public float cameraClipFar;
		public float cameraClipNear;

	}


	[System.Serializable]
	public struct DepthCamSettings
	{
		public Vector3 camWorldPos;

		//.. any other data that is needed ..//
	};




	}
