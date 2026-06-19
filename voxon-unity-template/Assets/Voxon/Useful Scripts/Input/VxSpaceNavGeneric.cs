using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxon;

/// Generic SpaceNav input
/// A script that can be used to allow for generic SpaceNav 
/// transform manipulation
//

public class VxSpaceNavGeneric : MonoBehaviour
{
    public enum SpaceNavInputBehaviour
    {
        actionFree = 0,
        actionBoundToNoButton = 1,
        actionBoundToButtonLeft = 2,
        actionBoundToButtonRight = 3,
        actionBoundToBothButtons = 4,
    }
    [SerializeField]
    public SpaceNavInputBehaviour spaceNavBehaviour = SpaceNavInputBehaviour.actionFree;

    [SerializeField]
    public bool moveTransform = false;

    [Range(0.01f, 5)]
    [SerializeField]
    public float movementSpeed = 1f;

    [SerializeField]
    public bool allowMovementX = false;
    [SerializeField]
    public bool allowMovementY = false;
    [SerializeField]
    public bool allowMovementZ = false;

    [SerializeField]
    public bool movementInvertX = false;
    [SerializeField]
    public bool movementInvertY = false;
    [SerializeField]
    public bool movementInvertZ = false;

    [SerializeField]
    public float movementMaxDistance = 20;




    [SerializeField]
    public bool rotateTransform = false;

    [Range(0.1f, 5)]
    [SerializeField]
    public float rotationSpeed = 1f;

    [SerializeField]
    public bool rotateRoll = false;
    [SerializeField]
    public bool rotatePitch = false;
    [SerializeField]
    public bool rotateYaw = false;
    [SerializeField]
    public bool invertRoll = false;
    [SerializeField]
    public bool invertPitch = false;
    [SerializeField]
    public bool invertYaw = false;
    [SerializeField]
    public bool rotateByWorld = true;


    [SerializeField]
    public bool scaleTransform = false;
    [Range(0.1f, 5)]
    [SerializeField]
    public float scaleSpeed = 1f;
    [SerializeField]
    public float maxScale = 4;
    [SerializeField]
    public float minScale = 0.0001f;
    [SerializeField]
    public bool useButtonsToScale = true;

    public bool buttonResetsPosition = false;
    public bool buttonResetsRotation = false;
    public bool buttonResetsScale = false;

    private Vector3 orgTransformPos;
    private Quaternion orgTransformRot;
    private Vector3 orgTransformScale;

    private int navButtonFix = 0;
    private float positionInputScalarFix = 0;

    void Start()
    {
        orgTransformPos = transform.position;
        orgTransformRot = transform.rotation;
        orgTransformScale = transform.localScale;
    }


    void Update()
    {
        if (VXProcess.Runtime == null || VXProcess.Instance.IsClosingVXProcess() == true) return;

        // A the older API had a range between 350 -- 0... the newer one is scaled is 1 to 0
        if (VXProcess.Instance.VXInterface == VOXON_RUNTIME_INTERFACE.VXLED)
        {
            navButtonFix = 0;
            positionInputScalarFix = 1;
        }
        else
        {
            navButtonFix = 1;
            positionInputScalarFix = 350;
        }

        bool allowMovement = (

            spaceNavBehaviour == SpaceNavInputBehaviour.actionFree ||
            VXProcess.Runtime.GetSpaceNavButton((int)VX_NAV_BUTTON_CODES.NAV_LEFT_BUTTON + navButtonFix) && spaceNavBehaviour == SpaceNavInputBehaviour.actionBoundToButtonLeft ||
            VXProcess.Runtime.GetSpaceNavButton((int)VX_NAV_BUTTON_CODES.NAV_RIGHT_BUTTON + navButtonFix) && spaceNavBehaviour == SpaceNavInputBehaviour.actionBoundToButtonRight ||
            VXProcess.Runtime.GetSpaceNavButton((int)VX_NAV_BUTTON_CODES.NAV_RIGHT_BUTTON + navButtonFix) && VXProcess.Runtime.GetSpaceNavButton((int)VX_NAV_BUTTON_CODES.NAV_LEFT_BUTTON + navButtonFix) && spaceNavBehaviour == SpaceNavInputBehaviour.actionBoundToBothButtons ||
            !VXProcess.Runtime.GetSpaceNavButton((int)VX_NAV_BUTTON_CODES.NAV_LEFT_BUTTON + navButtonFix) && !VXProcess.Runtime.GetSpaceNavButton((int)VX_NAV_BUTTON_CODES.NAV_RIGHT_BUTTON + navButtonFix) && spaceNavBehaviour == SpaceNavInputBehaviour.actionBoundToNoButton

            );

        if (allowMovement)
        {
            if (rotateTransform)
            {
                adjustRotationByNav();
            }

            if (scaleTransform)
            {
                adjustScaleByNav();
            }

            if (moveTransform)
            {
                adjustPostionByNav();
            }

            if (spaceNavBehaviour != SpaceNavInputBehaviour.actionFree && spaceNavBehaviour != SpaceNavInputBehaviour.actionBoundToNoButton)
            {
                if (buttonResetsPosition || buttonResetsRotation || buttonResetsScale)
                {
                    resetValuesToOriginal();
                }
            }
        }

    }


    private void resetValuesToOriginal()
    {
        if (buttonResetsPosition) transform.position = orgTransformPos;
        if (buttonResetsRotation) transform.rotation = orgTransformRot;
        if (buttonResetsScale) transform.localScale = orgTransformScale;

    }

    public void adjustPostionByNav()
    {
        var position = Voxon.VXProcess.Runtime.GetSpaceNavPosition();
        var v3pos = transform.position;

        float movementadjust = (movementSpeed * 30) * Time.deltaTime;

        if (position != null)
        {
            if (allowMovementX)
            {
                float adjustedValue = position[0] / positionInputScalarFix;
                v3pos.x -= (!movementInvertX ? -adjustedValue : adjustedValue) * movementadjust;
            }

            if (allowMovementY)
            {
                float adjustedValue = position[1] / positionInputScalarFix;
                v3pos.y -= (!movementInvertY ? -adjustedValue : adjustedValue) * movementadjust;
            }

            if (allowMovementZ)
            {
                float adjustedValue = position[2] / positionInputScalarFix;
                v3pos.z -= (!movementInvertZ ? -adjustedValue : adjustedValue) * movementadjust;
            }

            // Ensure the position stays within the movement max distance
            float distance = Vector3.Distance(orgTransformPos, v3pos);
            if (distance > movementMaxDistance)
            {
                v3pos = Vector3.MoveTowards(v3pos, orgTransformPos, distance - movementMaxDistance);
            }

            transform.position = v3pos;
        }
    }

    private void adjustRotationByNav()
    {
        if (Voxon.VXProcess.Runtime == null) return;

        float[] navRotationValues = Voxon.VXProcess.Runtime.GetSpaceNavRotation();

        Vector3 rotation = new Vector3(0, 0, 0);

        float rotspeed = (rotationSpeed * 100) * Time.deltaTime;

        // Apply deadzone and rotation adjustments
        if (rotateRoll)
        {
            float adjustedValue = (navRotationValues[0] / positionInputScalarFix);
            rotation.x = (invertRoll ? -adjustedValue : adjustedValue) * rotspeed;
        }

        if (rotatePitch)
        {
            float adjustedValue = (navRotationValues[1] / positionInputScalarFix);
            rotation.y = (invertPitch ? -adjustedValue : adjustedValue) * rotspeed;
        }

        if (rotateYaw)
        {
            float adjustedValue = (navRotationValues[2] / positionInputScalarFix);
            rotation.z = (invertYaw ? -adjustedValue : adjustedValue) * rotspeed;
        }

        if (rotateByWorld) transform.Rotate(rotation, Space.World);
        else transform.Rotate(rotation);
    }

    public void adjustScaleByNav()
    {
        bool processScaleIncrement = false;
        bool processScaleDecrement = false;

        float scale_amount = (scaleSpeed * 100f) * Time.deltaTime;

        if (useButtonsToScale)
        {
            if (VXProcess.Runtime.GetSpaceNavButton(0) /* && transform.localScale.x < max_scale */)
            //  if (Voxon.Input.GetSpaceNavButton("LeftButton") && transform.localScale.x < max_scale)
            {
                processScaleIncrement = true;
            }
            if (VXProcess.Runtime.GetSpaceNavButton(1) /* && transform.localScale.x < max_scale */)
            //  if (Voxon.Input.GetSpaceNavButton("RightButton") && transform.localScale.x > min_scale)
            {
                processScaleDecrement = true;
            }
        }
        else
        {
            var position = Voxon.VXProcess.Runtime.GetSpaceNavPosition();

            if ((position[2] / positionInputScalarFix) < 0)
            {
                processScaleIncrement = true;
            }

            if ((position[2] / positionInputScalarFix) > 0)
            {
                processScaleDecrement = true;
            }

        }


        // Both of these input styles work. Personally perfer doing it directly.
        if (processScaleIncrement && transform.localScale.x <= maxScale)
        {
            Vector3 scaleVec = new Vector3(scale_amount, scale_amount, scale_amount);
            transform.localScale += scaleVec * Time.deltaTime;
        }

        if (processScaleDecrement && transform.localScale.x >= minScale)
        {

            Vector3 scaleVec = new Vector3(scale_amount, scale_amount, scale_amount);
            transform.localScale -= scaleVec * Time.deltaTime;
        }

    }

}
