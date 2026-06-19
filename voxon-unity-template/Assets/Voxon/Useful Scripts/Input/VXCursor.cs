using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxon;

/// <summary>
/// Manages a Cursor on the Voxon display. This script isn't finished yet.
/// But will serve as an easy way to spawn a cursor on the VX display and map inputs to it.
/// </summary>
public class VXCursor : MonoBehaviour, IDrawable
{
    public struct VxCursorState
    {
        public point3d VxPosition;
        public Transform UnityPosTransform;

        public override string ToString()
        {
            return $"VxPosition: {VxPosition}, UnityTransform: {UnityPosTransform?.name ?? "null"}";
        }
    }

    public bool clipCursorToBeWithinVolume = true;
    public bool drawNativeCursorPos = true;
    [Range(0.1f,2)]
    public float movementSpeed = 1f;
    VxCursorState cursorState;

    // enable this if you are using the legacy VXU API from 2022 or earlier    
    private bool UsingLegacyVXU_API = false;
  

    void Start()
    {
        VXProcess.Drawables.Add(this);
    }

    void Update()
    {
       
        CheckNavInput();

    }

    void CheckNavInput()
    {
        var position = Voxon.VXProcess.Runtime.GetSpaceNavPosition();
        var v3pos = transform.position;
        if (position != null)
        {
            // Axis Transform for VoxieBox Legacy V1.00 plugin
            
            float moveSpeed = movementSpeed * 0.1f;

            if (UsingLegacyVXU_API)
            {
                v3pos.x += moveSpeed * (position[0] / 35.0f);
                v3pos.y -= moveSpeed * (position[2] / 35.0f);
                v3pos.z -= moveSpeed * (position[1] / 35.0f);
            } else
            {

                if (Voxon.VXProcess.Instance.VXInterface != VOXON_RUNTIME_INTERFACE.VXLED)
                {

                    v3pos.x += moveSpeed * (position[0] );
                    v3pos.z += moveSpeed * (-position[2]);
                    v3pos.y += moveSpeed * (position[1] );
                } else
                {
                    v3pos.x += moveSpeed * (position[0]);
                    v3pos.y += moveSpeed * (position[1]);
                    v3pos.z += moveSpeed * (position[2]);


                }

            }

            // convert movement to Voxon Space ...
            Matrix4x4 mat = Matrix4x4.identity;
            Vector3[] _vPosition = new Vector3[1]; 
            point3d[] _vxPos = new point3d[1]; 
            _vPosition[0] = v3pos;
            VXProcess.ComputeTransform(ref mat, ref _vPosition, ref _vxPos);

            // Get the VX aspect ratio and compare it with the position to see if the cursor will be inside the volume
            if (clipCursorToBeWithinVolume)
            {
                float[] asp = VXProcess.Runtime.GetAspectRatio();
               /*
                // Alternatibe way to get aspect get them from the camera...
                asp[0] = VXProcess.Instance.Camera.ViewFinderDimensions.x;
                asp[1] = VXProcess.Instance.Camera.ViewFinderDimensions.z;
                asp[2] = VXProcess.Instance.Camera.ViewFinderDimensions.y;
               */
                //Debug.Log($"vxPos = {_vxPos[0].x} {_vxPos[0].y} {_vxPos[0].z} asp = {asp[0]} {asp[1]} {asp[2]}");
                Vector3 op = transform.position; // original / previous frame position
                Vector3 nPos = v3pos;            // next position

               
                if (System.Math.Abs(_vxPos[0].x) > System.Math.Abs(asp[0]) ) {
                    nPos.x = op.x;
                  //  Debug.Log("X hit");

                }

                if (System.Math.Abs(_vxPos[0].y) > System.Math.Abs(asp[1]))
                {
                    // a gotcha Y and Z are flipped between Voxon and Unity Space
                    nPos.z = op.z;
                  //  Debug.Log("Y hit");
                }

                if (System.Math.Abs(_vxPos[0].z) > System.Math.Abs(asp[2]))
                {
                    // a gotcha Y and Z are flipped between Voxon and Unity Space
                    nPos.y = op.y;
                 //   Debug.Log("Z hit");

                }

                transform.position = nPos;
            }
            else {

                transform.position = v3pos;
            }
            cursorState.VxPosition = _vxPos[0];
            cursorState.UnityPosTransform = transform;



        }
        /*
        if (Voxon.Input.GetSpaceNavButton("LeftButton"))
        {
            Debug.Log("Left Space Nav Click");
        }

        if (Voxon.Input.GetSpaceNavButton("RightButton"))
        {
            Debug.Log("Right Space Nav Click");
        }
        */
    }



  
    public void Draw()
    {
        if (VXProcess.Runtime != null && drawNativeCursorPos)
        {
            VXProcess.Runtime.DrawSphere(ref cursorState.VxPosition, 0.05f, 1, 0xff0000);
        }
    }
    public point3d GetVxPositon()
    {

        return cursorState.VxPosition;
    }
}
