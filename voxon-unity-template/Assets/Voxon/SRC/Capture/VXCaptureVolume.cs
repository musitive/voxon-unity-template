using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Voxon;
/// <summary>
///  This VXVolumeCapture class is the virtual view that gets sent to the Voxon Engine. 
/// </summary>





[ExecuteInEditMode]
[RequireComponent(typeof(VxHardwareController))]
public class VXCaptureVolume : MonoBehaviour
{

	public enum VXCaptureShape
	{
		VXCAP_LOCK_TO_HARDWARE = 0,
		VXCAP_VLED_VX2 = 5,
		VXCAP_VLED_VX2XL = 6,

		VXCAP_VX1_CLASSIC = 1,
		VXCAP_CUBE = 2,
		VXCAP_CYLINDER = 3,
		VXCAP_HALF_CYLINDER = 4,

	}


	[SerializeField]
	private Color CameraOutLineColor = Color.cyan;

	[HideInInspector]
	public bool uniformScale = false;   // Uniform Scale will become depreciated 
	[SerializeField]
	float baseScale = 20; // Need to adjust controls for this
	public Vector3 vectorScale = Vector3.one;



	[SerializeField]
	public bool loadViewFinder = false;
	private Vector3 lastHWVectorScale;

	[SerializeField]
	public VXCaptureShape cameraShape = VXCaptureShape.VXCAP_VLED_VX2;
	private VXCaptureShape oCameraShape;
	private VXCaptureShape gizmoDrawShape = VXCaptureShape.VXCAP_VLED_VX2;
	private VOXON_RUNTIME_INTERFACE OVXInterface; // = VXProcess.Instance.VXInterface;
	public Vector3 ViewFinderDimensions = new Vector3(1, 1, 1);

	VxViewFinder view_finder;
	Renderer vf_renderer;

	public CameraAnimation CameraAnimator;

	[SerializeField]
	public bool showVolumeSurface = false;



	public float[] lastAspectRatio = new float[] { 0, 0, 0 };

	private bool InitaliseCameraValues = false;

	

	public float BaseScale
	{
		get
		{
			return baseScale;
		}

		set
		{
			baseScale = value;
			this.transform.hasChanged = true;
		}
	}

	void UpdatePerspective()
	{
		if (uniformScale)
		{
			this.transform.localScale = new Vector3(baseScale, baseScale, baseScale);
		}
		else
		{
			this.transform.localScale = vectorScale;
		}
	}


    // because the camera always needs to have a view_finder game object. It will check if these references don't exist
    // and create a new view_finder child
    void ViewFinderCheck()
	{

		bool view_finder_update = false;

		if (oCameraShape != cameraShape || OVXInterface != VXProcess.Instance.VXInterface)
		{
			InitaliseCameraValues = true;
		}
		oCameraShape = cameraShape;
		OVXInterface = VXProcess.Instance.VXInterface;
	
		

		if (view_finder != null)
		{
			if (
				(view_finder.GetShape() == true && gizmoDrawShape != VXCaptureShape.VXCAP_CUBE) ||
				(view_finder.GetShape() == false && gizmoDrawShape == VXCaptureShape.VXCAP_CUBE) ||
				(cameraShape == VXCaptureShape.VXCAP_LOCK_TO_HARDWARE && VXProcess.Instance.VXInterface != OVXInterface)
				)
			{

				/*
				GameObject parentObject = view_finder.gameObject;
				DestroyImmediate(parentObject);
				*/
				this.transform.hasChanged = true;
				view_finder_update = true;
				if (cameraShape == VXCaptureShape.VXCAP_LOCK_TO_HARDWARE) InitaliseCameraValues = true;
				Debug.Log("View finder shape mismatch, rebuilding");
			}


		}
		if (InitaliseCameraValues)
		{


			if (VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.LEGACY || VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.EXTENDED)
			{
				ViewFinderDimensions = new Vector3(1, 0.4f, 1);
				view_finder_update = true;

				if (vectorScale.x == 0 && vectorScale.y == 0 && vectorScale.z == 0)
				{
					vectorScale.x = 10;
					vectorScale.y = 10;
					vectorScale.z = 10;
					baseScale = 10;
				}

			}
			else if (VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
			{

				if (vectorScale.x == 0 && vectorScale.y == 0 && vectorScale.z == 0)
				{
					vectorScale.x = 20;
					vectorScale.y = 20;
					vectorScale.z = 20;
					baseScale = 20;
				}


				ViewFinderDimensions = new Vector3(2, 2, 2);
				view_finder_update = true;

			}

			if (cameraShape == VXCaptureShape.VXCAP_VLED_VX2)
			{
				vectorScale.x = 20;
				vectorScale.y = 20;
				vectorScale.z = 20;
				baseScale = 20;
				ViewFinderDimensions = new Vector3(2, 2, 2);
				view_finder_update = true;

			}

			else if (cameraShape == VXCaptureShape.VXCAP_VLED_VX2XL)
			{
				vectorScale.x = 40;
				vectorScale.y = 20;
				vectorScale.z = 40;
				baseScale = 20;
				ViewFinderDimensions = new Vector3(2, 2, 2);
				view_finder_update = true;

			}


			else if (cameraShape == VXCaptureShape.VXCAP_VX1_CLASSIC)
			{
				vectorScale.x = 10;
				vectorScale.y = 10;
				vectorScale.z = 10;
				baseScale = 10;
				ViewFinderDimensions = new Vector3(1, 0.4f, 1);
				view_finder_update = true;
			}

			InitaliseCameraValues = false;
		}

		switch (cameraShape)
		{
			case VXCaptureShape.VXCAP_LOCK_TO_HARDWARE:

				updateInfoFromHardware();

				break;
			case VXCaptureShape.VXCAP_VLED_VX2:



				gizmoDrawShape = VXCaptureShape.VXCAP_CYLINDER;
				break;
			case VXCaptureShape.VXCAP_VLED_VX2XL:




				gizmoDrawShape = VXCaptureShape.VXCAP_CYLINDER;

				break;
			case VXCaptureShape.VXCAP_CUBE:

				if (VXProcess.Instance.VXInterface != VOXON_RUNTIME_INTERFACE.VXLED)
				{
					ViewFinderDimensions.y = ViewFinderDimensions.x;
					ViewFinderDimensions.z = ViewFinderDimensions.x;
				}
				gizmoDrawShape = cameraShape;
				break;

			case VXCaptureShape.VXCAP_CYLINDER:
			case VXCaptureShape.VXCAP_HALF_CYLINDER:
		

				gizmoDrawShape = cameraShape;

				break;
			case VXCaptureShape.VXCAP_VX1_CLASSIC:
				gizmoDrawShape = VXCaptureShape.VXCAP_CUBE;
				break;
		}

		if (view_finder == null || view_finder_update)
		{
			view_finder = GetComponentInChildren<VxViewFinder>();

			// Handle Corrupted Prefab or missing viewfinder
			if (view_finder == null)
			{
				GameObject go;
				Vector3 scaleAdjust;
				bool isCube = false;
				if (gizmoDrawShape == VXCaptureShape.VXCAP_CUBE)
				{
					go = GameObject.CreatePrimitive(PrimitiveType.Cube);
				
					isCube = true;
					if (cameraShape == VXCaptureShape.VXCAP_CUBE && VXProcess.Instance.VXInterface != VOXON_RUNTIME_INTERFACE.VXLED)
                    {
						ViewFinderDimensions.y = ViewFinderDimensions.x;
						ViewFinderDimensions.z = ViewFinderDimensions.x;


					}
					scaleAdjust = ViewFinderDimensions;

					BoxCollider boxCollider = go.GetComponent<BoxCollider>();
					DestroyImmediate(boxCollider);
				}
				else
				{
					go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
					scaleAdjust = new Vector3(ViewFinderDimensions.x, ViewFinderDimensions.y * 0.5f, ViewFinderDimensions.z);
					isCube = false;

					CapsuleCollider capCollider = go.GetComponent<CapsuleCollider>();
					DestroyImmediate(capCollider);
				}

				go.transform.localPosition = Vector3.zero;
				go.transform.localRotation = Quaternion.identity;

				// This value should be loaded from config data. Defaulting to current standard
				go.transform.localScale = scaleAdjust;

				go.transform.parent = gameObject.transform;

				// Add a view finder component 
				go.name = "view_finder";
				go.AddComponent<VxViewFinder>();
				go.GetComponent<VxViewFinder>().SetShape(isCube);

				// Add the correct material to the game object
				go.GetComponent<MeshRenderer>().material = Resources.Load<Material>("ViewFinder_mat");

				// Add voxie_hide tag
				go.tag = "VoxieHide"; // Assign the "VoxieHide" tag to the GameObject so it doesn't get added to the Drawables array

			
			} 
			else if (view_finder_update)
			{

					bool isCube = false;
					GameObject vf = GameObject.Find("view_finder");
					Vector3 scaleAdjust;
					if (gizmoDrawShape == VXCaptureShape.VXCAP_CUBE)
					{

						if (cameraShape == VXCaptureShape.VXCAP_CUBE && VXProcess.Instance.VXInterface != VOXON_RUNTIME_INTERFACE.VXLED)
						{
							ViewFinderDimensions.y = ViewFinderDimensions.x;
							ViewFinderDimensions.z = ViewFinderDimensions.x;
						}
						else if (cameraShape == VXCaptureShape.VXCAP_VX1_CLASSIC && VXProcess.Instance.VXInterface != VOXON_RUNTIME_INTERFACE.VXLED)
	                    {
						ViewFinderDimensions.x = 1f;
						ViewFinderDimensions.y = 0.4f;
						ViewFinderDimensions.z = 1f;
						}

						scaleAdjust = ViewFinderDimensions;
					}
					else
					{
						if (VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
						{
							ViewFinderDimensions.x = 2f;
							ViewFinderDimensions.y = 2f;
							ViewFinderDimensions.z = 2f;

						} else
						{
							ViewFinderDimensions.x = 1f;
							ViewFinderDimensions.y = 1f;
							ViewFinderDimensions.z = 1f;
						}
				 
					scaleAdjust = new Vector3(ViewFinderDimensions.x, ViewFinderDimensions.y * 0.5f, ViewFinderDimensions.z);
					}



					if (gizmoDrawShape == VXCaptureShape.VXCAP_CUBE && view_finder.GetShape() == false)
					{
						GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
						Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
						DestroyImmediate(temp); // Destroy the temporary object immediately

						MeshFilter mf = vf.GetComponent<MeshFilter>();
						mf.mesh = mesh;

						isCube = true;
						// disable the collider so physics can be used in the box
					}
					else if (gizmoDrawShape != VXCaptureShape.VXCAP_CUBE && view_finder.GetShape() == true)
					{

						GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
						Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
						DestroyImmediate(temp); // Destroy the temporary object immediately

						MeshFilter mf = vf.GetComponent<MeshFilter>();
						mf.mesh = mesh;
						isCube = false;

					}

					vf.transform.localPosition = Vector3.zero;
					vf.transform.localRotation = Quaternion.identity;
			

					// This value should be loaded from config data. Defaulting to current standard
					//vf.transform.localScale = scaleAdjust;

					vf.GetComponent<VxViewFinder>().SetShape(isCube);

			}

			return;
		}
		



		vf_renderer = view_finder.GetComponent<Renderer>();
	}




	public void updateInfoFromHardware()
	{
	

		if (VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
		{
			gizmoDrawShape = VXCaptureShape.VXCAP_CYLINDER;
		}
		else
		{
			gizmoDrawShape = VXCaptureShape.VXCAP_CUBE;
		}

		if (VXProcess.Runtime == null /* VXProcess.Runtime.isLoaded() == false*/) return;


		




		if (VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.LEGACY || VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.EXTENDED)
		{
			// if VX1 
			if (VXProcess.Runtime != null)
			{

				vectorScale.x = 10;
				vectorScale.y = 10;
				vectorScale.z = 10;
				baseScale = 10;



				if (cameraShape == VXCaptureShape.VXCAP_VX1_CLASSIC)
				{
					vectorScale.x = 10;
					vectorScale.y = 10;
					vectorScale.z = 10;
					baseScale = 10;
				}

					lastAspectRatio = VXProcess.Runtime.GetAspectRatio();
			
				if (lastAspectRatio[0] == 0 && lastAspectRatio[1] == 0 && lastAspectRatio[2] == 0) { }
				else
				{
					ViewFinderDimensions.x = lastAspectRatio[0];
					ViewFinderDimensions.y = lastAspectRatio[2]; // Z and Y are backwards ..
					ViewFinderDimensions.z = lastAspectRatio[1];
				}

				if (VXProcess.Runtime.GetClipShape() == 1)
				{
					gizmoDrawShape = VXCaptureShape.VXCAP_CYLINDER;

				}
				else
				{
					gizmoDrawShape = VXCaptureShape.VXCAP_CUBE;
				}
	
			}

			if (vectorScale.x != 0 && vectorScale.y != 0 && vectorScale.z != 0)
			{
				
				lastHWVectorScale = vectorScale;
			}



		}
		if (VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
		{
	
			//ViewFinderDimensions = new Vector3(1,1,1);
			if (VXProcess.Runtime != null)
			{
			
				lastAspectRatio = VXProcess.Runtime.GetAspectRatio();

				if (lastAspectRatio[0] != lastAspectRatio[1] || lastAspectRatio[1] != lastAspectRatio[2])
                {
					this.uniformScale = false;
					baseScale = lastAspectRatio[0];

				} else
                {
					baseScale = ((lastAspectRatio[0] + lastAspectRatio[1] + lastAspectRatio[2]) * .3333f) * 10;

				}

				vectorScale.x = lastAspectRatio[0] * 10;
				vectorScale.y = lastAspectRatio[1] * 10;
				vectorScale.z = lastAspectRatio[2] * 10;

				// IMPORTANT VectorScale.z should be same as X for a cylnder
				vectorScale.z = vectorScale.x;

				ViewFinderDimensions.x = ViewFinderDimensions.y = ViewFinderDimensions.z = 2;

				/*
				if (lastAspectRatio[0] == 0 && lastAspectRatio[1] == 0 && lastAspectRatio[2] == 0) { }
				else
				{
					// View Finder is relatitive to the the vector scales so don't need to transform it
					// 2 for each value should be fine for VX2,..

					ViewFinderDimensions.x = ViewFinderDimensions.y = ViewFinderDimensions.z = 2;

					}
				*/
				if (vectorScale.x != 0 && vectorScale.y != 0 && vectorScale.z != 0)
				{
					lastHWVectorScale = vectorScale;
				}
				



			}

		}

		if (vectorScale.x == 0 && vectorScale.y == 0 && vectorScale.z == 0)
        {
			vectorScale.x = lastHWVectorScale[0];
			vectorScale.y = lastHWVectorScale[1];
			vectorScale.z = lastHWVectorScale[2];

		}

	}


	void UpdateViewFinder()
	{
		ViewFinderCheck();
		if (view_finder != null)
		{
			view_finder.SetAspectRatio(ViewFinderDimensions);
			view_finder.GetComponent<MeshRenderer>().enabled = showVolumeSurface;
		}
	}

	private void OnEnable()
	{
		if (CameraAnimator == null)
		{
			CameraAnimator = gameObject.GetComponent<CameraAnimation>();
			if (CameraAnimator == null)
			{
				CameraAnimator = gameObject.AddComponent<CameraAnimation>();
			}
		}
	}

	private void Awake()
	{
		oCameraShape = cameraShape;
		OVXInterface = VXProcess.Instance.VXInterface;
		UpdateCamera();
	}

	void Update()
	{
		UpdateCamera();
	}


    public void UpdateCamera()
    {
		if (CameraAnimator == null)
		{
			CameraAnimator = gameObject.GetComponent<CameraAnimation>();
			if (CameraAnimator == null)
			{
				CameraAnimator = gameObject.AddComponent<CameraAnimation>();
			}
		}
//#if UNITY_EDITOR -- was initially to only be for Editor but good for all the time.
// has a bit of overhead 
		UpdateViewFinder();
		UpdatePerspective();
//#endif -- 
	}



	public void LoadTransform()
	{
		CameraAnimator?.LoadTransform(this);
	}

	public void SaveTransform(bool hasChanged)
	{
		// TODO : Should handle non-uniform scales
		if (hasChanged)
		{
			CameraAnimator?.SaveTransform(transform, baseScale, ViewFinderDimensions);
		}
		else
		{
			CameraAnimator?.IncrementFrame();
		}

	}


	public void CloseAnimator()
	{
		CameraAnimator.StopPlayback();
		CameraAnimator.StopRecording();
		CameraAnimator.SaveRecording();
	}

	public Matrix4x4 GetMatrix()
	{
		ViewFinderCheck();

		return view_finder.transform.worldToLocalMatrix;
	}

	public Bounds GetBounds()
	{
		return vf_renderer.bounds;
	}

	public void Start()
	{

#if UNITY_EDITOR
		if (cameraShape == VXCaptureShape.VXCAP_LOCK_TO_HARDWARE)
		{
			if (VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
			{
				lastHWVectorScale.x = EditorPrefs.GetFloat("lastHWVectorScaleX", 20);
				lastHWVectorScale.y = EditorPrefs.GetFloat("lastHWVectorScaleY", 20);
				lastHWVectorScale.z = EditorPrefs.GetFloat("lastHWVectorScaleZ", 20);
			} else
            {
				lastHWVectorScale.x = EditorPrefs.GetFloat("lastHWVectorScaleX", 10);
				lastHWVectorScale.y = EditorPrefs.GetFloat("lastHWVectorScaleY", 10);
				lastHWVectorScale.z = EditorPrefs.GetFloat("lastHWVectorScaleZ", 10);
			}
		}


		// this fixes a strange bug that sometimes the previous camera values overwrite on launch
		if (cameraShape == VXCaptureShape.VXCAP_VLED_VX2)
        {

			vectorScale.x = 20;
			vectorScale.y = 20;
			vectorScale.z = 20;
		}

		if (cameraShape == VXCaptureShape.VXCAP_VLED_VX2XL)
		{
			vectorScale.x = 40;
			vectorScale.y = 20;
			vectorScale.z = 40;
		}
#endif

	}

	public void OnDestroy()
    {
#if UNITY_EDITOR
		if (cameraShape == VXCaptureShape.VXCAP_LOCK_TO_HARDWARE)
		{
			EditorPrefs.SetFloat("lastHWVectorScaleX", lastHWVectorScale.x);
			EditorPrefs.SetFloat("lastHWVectorScaleY", lastHWVectorScale.y);
			EditorPrefs.SetFloat("lastHWVectorScaleZ", lastHWVectorScale.z);
		}
#endif
	}

	private void OnDrawGizmos()
	{
		// Draw Shape on the Display
		Gizmos.color = CameraOutLineColor;
		//Gizmos.color = Color.black;

		float radius = 0;
		float height = 0;
		int segments = 0;

		Vector3 scaler;
		if (cameraShape == VXCaptureShape.VXCAP_LOCK_TO_HARDWARE)
        {
			scaler.x = lastHWVectorScale.x;
			scaler.y = lastHWVectorScale.y;
			scaler.z = lastHWVectorScale.z;

		}
		else if (uniformScale)
        {
			scaler.x = baseScale;
			scaler.y = baseScale;
			scaler.z = baseScale;
		} else
        {
			scaler.x = vectorScale.x;
			scaler.y = vectorScale.y;
			scaler.z = vectorScale.z;
		}

		Vector3 position = transform.position;
		float angleStep = 0;
		Vector3[] bottomVertices;
		Vector3[] topVertices;
		switch (gizmoDrawShape)
		{
			case VXCaptureShape.VXCAP_CUBE:
				// Calculate half extents for easy calculations
				Vector3 size = new Vector3(ViewFinderDimensions.x * scaler.x, ViewFinderDimensions.y * scaler.y, ViewFinderDimensions.z * scaler.z);
				size *= 0.5f;

				// Calculate the 8 vertices of the box
				Vector3[] vertices = new Vector3[8]
				{
					transform.position +  new Vector3(-size.x, -size.y, -size.z), // Bottom face vertices
					transform.position +  new Vector3(size.x, -size.y, -size.z),
					transform.position + new Vector3(size.x, -size.y, size.z),
					transform.position +  new Vector3(-size.x, -size.y, size.z),

					transform.position +  new Vector3(-size.x, size.y, -size.z), // Top face vertices
					transform.position +  new Vector3(size.x, size.y, -size.z),
					transform.position + new Vector3(size.x, size.y, size.z),
					transform.position +  new Vector3(-size.x, size.y, size.z)
				};

				// Draw bottom face
				Gizmos.DrawLine(vertices[0], vertices[1]);
				Gizmos.DrawLine(vertices[1], vertices[2]);
				Gizmos.DrawLine(vertices[2], vertices[3]);
				Gizmos.DrawLine(vertices[3], vertices[0]);

				// Draw top face
				Gizmos.DrawLine(vertices[4], vertices[5]);
				Gizmos.DrawLine(vertices[5], vertices[6]);
				Gizmos.DrawLine(vertices[6], vertices[7]);
				Gizmos.DrawLine(vertices[7], vertices[4]);

				// Draw vertical lines
				Gizmos.DrawLine(vertices[0], vertices[4]);
				Gizmos.DrawLine(vertices[1], vertices[5]);
				Gizmos.DrawLine(vertices[2], vertices[6]);
				Gizmos.DrawLine(vertices[3], vertices[7]);

				break;
			case VXCaptureShape.VXCAP_CYLINDER:

				radius = (scaler.x + scaler.z) * 0.5f;

//				radius = scaler.x * (0.5f * ViewFinderDimensions.x); // Set the radius of the cylinder
				height = scaler.y * (0.5f * ViewFinderDimensions.y); // Set the height of the cylinder
				segments = 32; // Number of segments

				if (VXProcess.Instance.VXInterface != VOXON_RUNTIME_INTERFACE.VXLED)
                {
					radius *= 0.5f;
				}

				angleStep = 360.0f / segments;

				bottomVertices = new Vector3[segments];
				topVertices = new Vector3[segments];

				// Calculate bottom and top vertices
				for (int i = 0; i < segments; i++)
				{
					float angle = i * angleStep * Mathf.Deg2Rad;
					float x = Mathf.Cos(angle) * radius;
					float z = Mathf.Sin(angle) * radius;

					bottomVertices[i] = position + new Vector3(x, height * -1f, z);
					topVertices[i] = position + new Vector3(x, height, z);
				}

				// Draw bottom circle
				for (int i = 0; i < segments; i++)
				{
					Gizmos.DrawLine(bottomVertices[i], bottomVertices[(i + 1) % segments]);
				}

				// Draw top circle
				for (int i = 0; i < segments; i++)
				{
					Gizmos.DrawLine(topVertices[i], topVertices[(i + 1) % segments]);
				}

				// Draw vertical lines
				for (int i = 0; i < segments; i++)
				{
					// Draw Front
					if (i == segments * .75)
					{
						Gizmos.color = Color.green;
						Gizmos.DrawSphere(topVertices[i], radius * 0.03f);
						Gizmos.DrawSphere(bottomVertices[i], radius * 0.03f);
						Gizmos.DrawLine(bottomVertices[i], topVertices[i]);
					}
					else
					{
						Gizmos.color = CameraOutLineColor;
						Gizmos.DrawLine(bottomVertices[i], topVertices[i]);
					}
				}

				break;
			case VXCaptureShape.VXCAP_HALF_CYLINDER:
				radius = scaler.x * (0.5f * ViewFinderDimensions.x); // Set the radius of the cylinder
				height = scaler.y * (1.0f * ViewFinderDimensions.y); // Set the height of the cylinder

				segments = 32; // Number of segments

				angleStep = 180.0f / segments; // Change the angle step to 180 degrees for half-cylinder

				bottomVertices = new Vector3[segments + 1];
				topVertices = new Vector3[segments + 1];

				// Calculate bottom and top vertices for half-cylinder
				for (int i = 0; i <= segments; i++)
				{
					float angle = i * angleStep * Mathf.Deg2Rad;
					float x = Mathf.Cos(angle) * radius;
					float z = Mathf.Sin(angle) * radius;

					bottomVertices[i] = position + new Vector3(x, -height / 2f, z); // Adjusted for bottom center
					topVertices[i] = position + new Vector3(x, height / 2f, z); // Adjusted for top center
				}

				// Draw bottom semicircle
				for (int i = 0; i < segments; i++)
				{
					Gizmos.DrawLine(bottomVertices[i], bottomVertices[i + 1]);
				}

				// Draw top semicircle
				for (int i = 0; i < segments; i++)
				{
					Gizmos.DrawLine(topVertices[i], topVertices[i + 1]);
				}

				// Draw vertical lines between top and bottom semicircles
				for (int i = 0; i <= segments; i++)
				{
					Gizmos.DrawLine(bottomVertices[i], topVertices[i]);
				}

				// Draw the straight sides
				Gizmos.DrawLine(bottomVertices[0], topVertices[0]);
				Gizmos.DrawLine(bottomVertices[segments], topVertices[segments]);


				// Draw the back part
				Gizmos.DrawLine(bottomVertices[0], bottomVertices[segments]);
				Gizmos.DrawLine(topVertices[0], topVertices[segments]);
				// Diagonal line 
				Gizmos.DrawLine(topVertices[0], bottomVertices[segments]);
				Gizmos.DrawLine(topVertices[segments], bottomVertices[0]);

				break;



		}



	}

	public void ReportCamera(int xPos, int yPos)
	{

		if (VXProcess.Runtime != null)
		{

			int y = yPos;
			int col = 0xffffff;
			int w = 250;
			int h = 120;

			VXProcess.Runtime.ScreenDrawRectangleFill(xPos, yPos, xPos + w, yPos + h, 0x003000);
			VXProcess.Runtime.LogToScreenExt(xPos, y, 0xffffff, -1, "             Camera Report  "); y += 10;
			VXProcess.Runtime.LogToScreenExt(xPos, y, 0xffff00, -1, $"Using Uniform Scale: {uniformScale}"); y += 10;
			if (uniformScale == true) col = 0x00ff00;
			else col = 0xff0000;
			VXProcess.Runtime.LogToScreenExt(xPos, y, col, -1, $"Base Scale {baseScale}"); y += 10;
			if (uniformScale != true) col = 0x00ff00;
			else col = 0xff0000;
			VXProcess.Runtime.LogToScreenExt(xPos, y, col, -1, $"Vector Scale {vectorScale.x:F3}, {vectorScale.y:F3}, {vectorScale.z:F3} "); y += 10;
			VXProcess.Runtime.LogToScreenExt(xPos, y, 0xff00ff, -1, $"ViewFinder Dimensions {ViewFinderDimensions.x:F3}, {ViewFinderDimensions.y:F3}, {ViewFinderDimensions.z:F3} "); y += 10;
			float[] asp = VXProcess.Runtime.GetAspectRatio();
			VXProcess.Runtime.LogToScreenExt(xPos, y, 0x00ff00, -1, $"Aspect Ratio (DLL) X:{asp[0]}. Y:{asp[2]} Z:{asp[1]}"); y += 10;
			VXProcess.Runtime.LogToScreenExt(xPos, y, 0xffffff, -1, $"Shape {cameraShape.ToString()}"); y += 10;
			y += 10;
			VXProcess.Runtime.LogToScreenExt(xPos, y, 0xffffff, -1, $"Camera Transform"); y += 10;

			VXProcess.Runtime.LogToScreenExt(xPos, y, 0xffff00, -1, $"Cam Pos (XYZ) {transform.position.x:F3}, {transform.position.y:F3}, {transform.position.z:F3} "); y += 10;
			VXProcess.Runtime.LogToScreenExt(xPos, y, 0x00ff00, -1, $"Cam Rot (XYZ) {transform.rotation.x:F3}, {transform.rotation.y:F3}, {transform.rotation.z:F3} "); y += 10;
			VXProcess.Runtime.LogToScreenExt(xPos, y, 0x00ffff, -1, $"Cam Sca (XYZ) {transform.localScale.x:F3}, {transform.localScale.y:F3}, {transform.localScale.z:F3} "); y += 10;
		}


	}
}
