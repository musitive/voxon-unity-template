using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* 
 * Controls camera aspect ratio during operation.
 * Can load existing aspect ratio data from local config
 * Alternatively can set own aspect ratio during play
 */

[ExecuteInEditMode]
public class VxViewFinder : MonoBehaviour
{
	Vector3 base_position = Vector3.zero;
	Quaternion base_rotation = Quaternion.identity;
	public Vector3 local_scalar = new Vector3(1, 0.4f, 1);
	private bool isCube = false;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
#if UNITY_EDITOR
		if (gameObject.transform.hasChanged)
		{
			transform.localPosition = base_position;
			transform.localRotation = base_rotation;
			/*
			if (Voxon.VXProcess.Instance.VXInterface == Voxon.VOXON_RUNTIME_INTERFACE.VXLED)
            {
				local_scalar.x = 2;
				local_scalar.y = 2;
				local_scalar.z = 2;

			} else
            {
			

				local_scalar.x = 1;
				local_scalar.y = 0.4f;
				local_scalar.z = 1f;
			}
			
			SetAspectRatio(local_scalar);
			*/
			gameObject.transform.hasChanged = false;
			// weird have to do this other keeps forgetting
			MeshFilter meshFilter = GetComponent<MeshFilter>();
			if (meshFilter != null && meshFilter.sharedMesh != null)
			{
				Mesh mesh = meshFilter.sharedMesh;

				string meshName = mesh.name;
				if (meshName == "Cube")
                {
					isCube = true;
				} else
                {
					isCube = false;
				}
			}
				
		
		
		}
#endif
	}

	public void SetShape(bool isCubeValue)
    {
		isCube = isCubeValue;
    }

	public bool GetShape()
	{
		return isCube;
	}


	public void SetAspectRatio(Vector3 scalar)
	{
		local_scalar = scalar;

		if(isCube) 
			transform.localScale = scalar;
		else
        {
			transform.localScale = new Vector3(scalar.x, scalar.y * 0.5f, scalar.z);

		}
	}
}
