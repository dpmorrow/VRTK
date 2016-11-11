﻿// Knob|Controls3D|0060
namespace VRTK
{
    using UnityEngine;

    /// <summary>
    /// Attaching the script to a game object will allow the user to interact with it as if it were a radial knob. The direction can be freely set.
    /// </summary>
    /// <remarks>
    /// The script will instantiate the required Rigidbody and Interactable components automatically in case they do not exist yet.
    /// </remarks>
    /// <example>
    /// `VRTK/Examples/025_Controls_Overview` has a couple of rotator knobs that can be rotated by grabbing with the controller and then rotating the controller in the desired direction.
    /// </example>
    public class VRTK_Knob : VRTK_Control
    {
        public enum KnobDirection
        {
            x, y, z // TODO: autodetect not yet done, it's a bit more difficult to get it right
        }

        [Tooltip("An optional game object to which the knob will be connected. If the game object moves the knob will follow along.")]
        public GameObject connectedTo;
        [Tooltip("The axis on which the knob should rotate. All other axis will be frozen.")]
        public KnobDirection direction = KnobDirection.x;
        [Tooltip("The minimum value of the knob.")]
        public float min = 0f;
        [Tooltip("The maximum value of the knob.")]
        public float max = 100f;
        [Tooltip("The increments in which knob values can change.")]
        public float stepSize = 1f;

        private static float MAX_AUTODETECT_KNOB_WIDTH = 3; // multiple of the knob width
        private KnobDirection finalDirection;
        private KnobDirection subDirection;
        private bool subDirectionFound = false;
        private Quaternion initialRotation;
        private Vector3 initialLocalRotation;
        private Rigidbody rb;
        private ConfigurableJoint cj;
        private VRTK_InteractableObject io;
        private bool cjCreated = false;

        protected override void InitRequiredComponents()
        {
            initialRotation = transform.rotation;
            initialLocalRotation = transform.localRotation.eulerAngles;
            InitKnob();
        }

        protected override bool DetectSetup()
        {
            finalDirection = direction;

            if (cjCreated)
            {
                cj.angularXMotion = ConfigurableJointMotion.Locked;
                cj.angularYMotion = ConfigurableJointMotion.Locked;
                cj.angularZMotion = ConfigurableJointMotion.Locked;

                switch (finalDirection)
                {
                    case KnobDirection.x:
                        cj.angularXMotion = ConfigurableJointMotion.Free;
                        break;
                    case KnobDirection.y:
                        cj.angularYMotion = ConfigurableJointMotion.Free;
                        break;
                    case KnobDirection.z:
                        cj.angularZMotion = ConfigurableJointMotion.Free;
                        break;
                }
            }

            if (cj)
            {
                cj.xMotion = ConfigurableJointMotion.Locked;
                cj.yMotion = ConfigurableJointMotion.Locked;
                cj.zMotion = ConfigurableJointMotion.Locked;

                if (connectedTo)
                {
                    cj.connectedBody = connectedTo.GetComponent<Rigidbody>();
                }
            }

            return true;
        }

        protected override ControlValueRange RegisterValueRange()
        {
            return new ControlValueRange() { controlMin = min, controlMax = max };
        }

        protected override void HandleUpdate()
        {
            value = CalculateValue();
        }

        private void InitKnob()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.angularDrag = 10; // otherwise knob will continue to move too far on its own
            }
            rb.isKinematic = false;
            rb.useGravity = false;

            io = GetComponent<VRTK_InteractableObject>();
            if (io == null)
            {
                io = gameObject.AddComponent<VRTK_InteractableObject>();
            }
            io.isGrabbable = true;
            io.grabAttachMechanicScript = gameObject.AddComponent<GrabAttachMechanics.VRTK_TrackObjectGrabAttach>();
            io.grabAttachMechanicScript.precisionGrab = true;
            io.stayGrabbedOnTeleport = false;

            cj = GetComponent<ConfigurableJoint>();
            if (cj == null)
            {
                cj = gameObject.AddComponent<ConfigurableJoint>();
                cj.configuredInWorldSpace = false;
                cjCreated = true;
            }

            if (connectedTo)
            {
                Rigidbody rb2 = connectedTo.GetComponent<Rigidbody>();
                if (rb2 == null)
                {
                    rb2 = connectedTo.AddComponent<Rigidbody>();
                    rb2.useGravity = false;
                    rb2.isKinematic = true;
                }
            }
        }

        private KnobDirection DetectDirection()
        {
            KnobDirection direction = KnobDirection.x;
            Bounds bounds = Utilities.GetBounds(transform);

            // shoot rays in all directions to learn about surroundings
            RaycastHit hitForward;
            RaycastHit hitBack;
            RaycastHit hitLeft;
            RaycastHit hitRight;
            RaycastHit hitUp;
            RaycastHit hitDown;
            Physics.Raycast(bounds.center, Vector3.forward, out hitForward, bounds.extents.z * MAX_AUTODETECT_KNOB_WIDTH, Physics.DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
            Physics.Raycast(bounds.center, Vector3.back, out hitBack, bounds.extents.z * MAX_AUTODETECT_KNOB_WIDTH, Physics.DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
            Physics.Raycast(bounds.center, Vector3.left, out hitLeft, bounds.extents.x * MAX_AUTODETECT_KNOB_WIDTH, Physics.DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
            Physics.Raycast(bounds.center, Vector3.right, out hitRight, bounds.extents.x * MAX_AUTODETECT_KNOB_WIDTH, Physics.DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
            Physics.Raycast(bounds.center, Vector3.up, out hitUp, bounds.extents.y * MAX_AUTODETECT_KNOB_WIDTH, Physics.DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);
            Physics.Raycast(bounds.center, Vector3.down, out hitDown, bounds.extents.y * MAX_AUTODETECT_KNOB_WIDTH, Physics.DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal);

            // shortest valid ray wins
            float lengthX = (hitRight.collider != null) ? hitRight.distance : float.MaxValue;
            float lengthY = (hitDown.collider != null) ? hitDown.distance : float.MaxValue;
            float lengthZ = (hitBack.collider != null) ? hitBack.distance : float.MaxValue;
            float lengthNegX = (hitLeft.collider != null) ? hitLeft.distance : float.MaxValue;
            float lengthNegY = (hitUp.collider != null) ? hitUp.distance : float.MaxValue;
            float lengthNegZ = (hitForward.collider != null) ? hitForward.distance : float.MaxValue;

            // TODO: not yet the right decision strategy, works only partially
            if (Utilities.IsLowest(lengthX, new float[] { lengthY, lengthZ, lengthNegX, lengthNegY, lengthNegZ }))
            {
                direction = KnobDirection.z;
            }
            else if (Utilities.IsLowest(lengthY, new float[] { lengthX, lengthZ, lengthNegX, lengthNegY, lengthNegZ }))
            {
                direction = KnobDirection.y;
            }
            else if (Utilities.IsLowest(lengthZ, new float[] { lengthX, lengthY, lengthNegX, lengthNegY, lengthNegZ }))
            {
                direction = KnobDirection.x;
            }
            else if (Utilities.IsLowest(lengthNegX, new float[] { lengthX, lengthY, lengthZ, lengthNegY, lengthNegZ }))
            {
                direction = KnobDirection.z;
            }
            else if (Utilities.IsLowest(lengthNegY, new float[] { lengthX, lengthY, lengthZ, lengthNegX, lengthNegZ }))
            {
                direction = KnobDirection.y;
            }
            else if (Utilities.IsLowest(lengthNegZ, new float[] { lengthX, lengthY, lengthZ, lengthNegX, lengthNegY }))
            {
                direction = KnobDirection.x;
            }

            return direction;
        }

        private float CalculateValue()
        {
            if (!subDirectionFound)
            {
                float angleX = Mathf.Abs(transform.localRotation.eulerAngles.x - initialLocalRotation.x) % 90;
                float angleY = Mathf.Abs(transform.localRotation.eulerAngles.y - initialLocalRotation.y) % 90;
                float angleZ = Mathf.Abs(transform.localRotation.eulerAngles.z - initialLocalRotation.z) % 90;
                angleX = (Mathf.RoundToInt(angleX) >= 89) ? 0 : angleX;
                angleY = (Mathf.RoundToInt(angleY) >= 89) ? 0 : angleY;
                angleZ = (Mathf.RoundToInt(angleZ) >= 89) ? 0 : angleZ;

                if (Mathf.RoundToInt(angleX) != 0 || Mathf.RoundToInt(angleY) != 0 || Mathf.RoundToInt(angleZ) != 0)
                {
                    subDirection = angleX < angleY ? (angleY < angleZ ? KnobDirection.z : KnobDirection.y) : (angleX < angleZ ? KnobDirection.z : KnobDirection.x);
                    subDirectionFound = true;
                }
            }

            float angle = 0;
            switch (subDirection)
            {
                case KnobDirection.x:
                    angle = transform.localRotation.eulerAngles.x - initialLocalRotation.x;
                    break;
                case KnobDirection.y:
                    angle = transform.localRotation.eulerAngles.y - initialLocalRotation.y;
                    break;
                case KnobDirection.z:
                    angle = transform.localRotation.eulerAngles.z - initialLocalRotation.z;
                    break;
            }
            angle = Mathf.Round(angle * 1000f) / 1000f; // not rounding will produce slight offsets in 4th digit that mess up initial value

            // Quaternion.angle will calculate shortest route and only go to 180
            float value = 0;
            if (angle > 0 && angle <= 180)
            {
                value = 360 - Quaternion.Angle(initialRotation, transform.rotation);
            }
            else
            {
                value = Quaternion.Angle(initialRotation, transform.rotation);
            }

            // adjust to value scale
            value = Mathf.Round((min + Mathf.Clamp01(value / 360f) * (max - min)) / stepSize) * stepSize;
            if (min > max && angle != 0)
            {
                value = (max + min) - value;
            }

            return value;
        }
    }
}