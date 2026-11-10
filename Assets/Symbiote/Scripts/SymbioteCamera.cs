using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SymbioteRaid
{
    /// <summary>Third-person orbit with transported heading as the adhered surface changes.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class SymbioteCamera : MonoBehaviour
    {
        public UnityEngine.Camera Camera { get; private set; }
        public Ray AimRay => Camera ? Camera.ViewportPointToRay(new Vector3(.5f, .5f, 0f)) : new Ray(transform.position, transform.forward);
        public Vector3 MoveForward => Planar(transform.forward, CurrentUp);
        public Vector3 MoveRight => Vector3.Cross(CurrentUp, MoveForward).normalized;
        public float sensitivity = 1.8f;
        SymbioteGame game;
        Vector3 orbitUp = Vector3.up, heading = Vector3.forward, focus;
        float pitch = 16f, distance = 4.6f;
        bool initialized;
        Vector3 CurrentUp => game && !game.IsPossessed && game.Blob ? game.Blob.SurfaceNormal : Vector3.up;

        public void Configure(SymbioteGame owner)
        {
            game = owner;
            Camera = GetComponent<UnityEngine.Camera>();
            if (!Camera) Camera = gameObject.AddComponent<UnityEngine.Camera>();
            if (!GetComponent<AudioListener>()) gameObject.AddComponent<AudioListener>();
            gameObject.tag = "MainCamera";
            Camera.nearClipPlane = .06f;
            Camera.farClipPlane = 120f;
            Camera.fieldOfView = 65f;
            Camera.allowHDR = true;
            Camera.backgroundColor = new Color(.025f, .04f, .055f);
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            orbitUp = CurrentUp;
            heading = Planar(Vector3.forward, orbitUp);
            focus = owner.TargetPoint;
            UpdateView(0f);
            initialized = true;
        }

        void LateUpdate()
        {
            if (!initialized || !game || game.Paused || game.GameOver || game.Won) return;
            UpdateView(Time.deltaTime);
        }

        void UpdateView(float dt)
        {
            Vector3 targetUp = CurrentUp;
            float blend = dt <= 0f ? 1f : 1f - Mathf.Exp(-9f * dt);
            Vector3 nextUp = Vector3.Slerp(orbitUp, targetUp, blend).normalized;
            if (nextUp.sqrMagnitude < .5f) nextUp = targetUp;
            Quaternion transport = Quaternion.FromToRotation(orbitUp, nextUp);
            heading = Planar(transport * heading, nextUp);
            orbitUp = nextUp;
            if (dt > 0f && Cursor.lockState == CursorLockMode.Locked)
            {
                heading = Quaternion.AngleAxis(Input.GetAxisRaw("Mouse X") * sensitivity, orbitUp) * heading;
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * sensitivity, -38f, 72f);
                distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * .4f, 1.5f, 7f);
            }
            Vector3 desiredFocus = game.TargetPoint + orbitUp * (game.IsPossessed ? .24f : .13f);
            focus = dt <= 0f ? desiredFocus : Vector3.Lerp(focus, desiredFocus, 1f - Mathf.Exp(-13f * dt));
            // A smoothed focus can cut through an inside corner; start collision queries from clear space.
            if (Physics.CheckSphere(focus, .16f, 1, QueryTriggerInteraction.Ignore)) focus = game.TargetPoint;
            Vector3 right = Vector3.Cross(orbitUp, heading).normalized;
            Vector3 backward = -heading * Mathf.Cos(pitch * Mathf.Deg2Rad) + orbitUp * Mathf.Sin(pitch * Mathf.Deg2Rad);
            Vector3 desired = focus + backward * distance + right * (game.IsPossessed ? .42f : 0f);
            Vector3 ray = desired - focus;
            if (ray.sqrMagnitude > .001f && Physics.SphereCast(focus, .15f, ray.normalized, out RaycastHit hit,
                ray.magnitude, 1, QueryTriggerInteraction.Ignore))
                desired = focus + ray.normalized * Mathf.Max(.12f, hit.distance - .06f);
            transform.position = desired;
            Vector3 look = focus - desired;
            if (look.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(look.normalized, orbitUp);
            Camera.fieldOfView = Mathf.Lerp(Camera.fieldOfView, game.IsPossessed ? 62f : 66f,
                dt <= 0f ? 1f : 1f - Mathf.Exp(-5f * dt));
        }

        static Vector3 Planar(Vector3 direction, Vector3 normal)
        {
            Vector3 flat = Vector3.ProjectOnPlane(direction, normal);
            if (flat.sqrMagnitude < .0001f) flat = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (flat.sqrMagnitude < .0001f) flat = Vector3.ProjectOnPlane(Vector3.right, normal);
            return flat.normalized;
        }
    }
}
