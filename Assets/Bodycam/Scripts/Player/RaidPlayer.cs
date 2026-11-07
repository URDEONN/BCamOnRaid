using System.Collections;
using UnityEngine;

namespace BodycamRaid
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class RaidPlayer : MonoBehaviour
    {
        [Header("Camera")]
        public Transform cameraRig;
        public Camera viewCamera;
        public Light flashlight;
        [Range(0.2f, 5f)] public float mouseSensitivity = 1.6f;
        [Range(0f, 1f)] public float cameraMotion = 0.7f;
        public bool reducedMotion;
        [Header("Movement")]
        public float walkSpeed = 2.8f;
        public float runSpeed = 5.2f;
        public float crouchSpeed = 1.45f;
        public float acceleration = 14f;
        public float jumpHeight = 0.95f;
        public float gravity = 24f;
        public bool Paused { get; private set; }
        public bool IsCrouched { get; private set; }
        public bool IsVaulting { get; private set; }
        public float Stamina01 { get; private set; } = 1f;
        public float Speed01 { get; private set; }
        public float GaitPhase { get; private set; }
        public float GaitWeight { get; private set; }
        public Vector3 LocalPlanarVelocity { get; private set; }
        public float TurnRate { get; private set; }
        public float VaultProgress { get; private set; } = -1f;
        public bool IsGrounded => motor && motor.enabled && motor.isGrounded;
        public string InteractionPrompt { get; private set; } = "";
        const float StandingHeight = 1.8f;
        const float CrouchingHeight = 1.12f;
        const int WorldMask = ~(1 << 2);
        CharacterController motor;
        RaidAudio sound;
        OfficerBody officerBody;
        RaidDoor focusedDoor;
        Vector3 planarVelocity;
        Vector3 spawn;
        Quaternion spawnRotation;
        float verticalVelocity, pitch, yaw, lean, landingOffset, landingRecovery;
        float coyote, jumpBuffer, currentRoll, fov = 78f;
        bool crouchToggle, exhausted;
        Coroutine vaultRoutine;
        Vector3 lastPosition;

        void Awake()
        {
            gameObject.layer = 2; // Ignore Raycast: world queries exclude our own capsule.
            motor = GetComponent<CharacterController>();
            motor.height = StandingHeight;
            motor.radius = 0.28f;
            motor.center = Vector3.up * StandingHeight * 0.5f;
            motor.stepOffset = 0.3f;
            motor.skinWidth = 0.03f;
            motor.slopeLimit = 48f;
            motor.minMoveDistance = 0f;
            spawn = transform.position;
            spawnRotation = transform.rotation;
            yaw = transform.eulerAngles.y;
            if (!cameraRig)
            {
                cameraRig = new GameObject("Bodycam rig").transform;
                cameraRig.SetParent(transform, false);
            }
            if (!viewCamera)
            {
                var cameraObject = new GameObject("Bodycam", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(cameraRig, false);
                viewCamera = cameraObject.GetComponent<Camera>();
            }
            viewCamera.nearClipPlane = 0.055f;
            viewCamera.farClipPlane = 100f;
            viewCamera.fieldOfView = fov;
            viewCamera.allowHDR = true;
            if (!flashlight)
            {
                var lamp = new GameObject("Linterna", typeof(Light));
                lamp.transform.SetParent(viewCamera.transform, false);
                lamp.transform.localPosition = new Vector3(0.12f, -0.1f, 0.05f);
                flashlight = lamp.GetComponent<Light>();
                flashlight.type = LightType.Spot;
                flashlight.range = 22f;
                flashlight.spotAngle = 56f;
                flashlight.innerSpotAngle = 24f;
                flashlight.intensity = 6f;
                flashlight.color = new Color(0.86f, 0.93f, 1f);
                flashlight.shadows = LightShadows.Soft;
            }
            if (!viewCamera.GetComponent<BodycamLens>()) viewCamera.gameObject.AddComponent<BodycamLens>();
            sound = gameObject.AddComponent<RaidAudio>();
            if (!GetComponent<RaidHUD>()) gameObject.AddComponent<RaidHUD>();
            cameraRig.localPosition = Vector3.up * 1.46f;
            lastPosition = transform.position;
            officerBody = GetComponent<OfficerBody>();
            if (!officerBody) officerBody = gameObject.AddComponent<OfficerBody>();
        }

        void Start() { SetPaused(false); }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) SetPaused(!Paused);
            if (Paused) return;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (Input.GetMouseButtonDown(0)) SetPaused(false);
                return;
            }
            if (Input.GetKeyDown(KeyCode.R)) { ResetToSpawn(); return; }
            if (Input.GetKeyDown(KeyCode.F)) flashlight.enabled = !flashlight.enabled;
            if (Input.GetKeyDown(KeyCode.F3) && officerBody)
            {
                bool visible = !officerBody.visibleBody;
                officerBody.visibleBody = visible;
                officerBody.visibleHands = visible;
            }
            Look();
            FocusDoor();
            if (Input.GetKeyDown(KeyCode.G) && focusedDoor) focusedDoor.Toggle();
            if (IsVaulting) return;
            Move();
            if (transform.position.y < -8f) ResetToSpawn();
        }

        void Look()
        {
            float yawDelta = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            yaw += yawDelta;
            TurnRate = Mathf.Lerp(TurnRate, Mathf.Clamp(yawDelta / Mathf.Max(Time.deltaTime, 0.001f), -540f, 540f),
                1f - Mathf.Exp(-14f * Time.deltaTime));
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * mouseSensitivity, -78f, 78f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void Move()
        {
            float dt = Time.deltaTime;
            bool grounded = motor.isGrounded;
            coyote = grounded ? 0.1f : coyote - dt;
            jumpBuffer = Input.GetKeyDown(KeyCode.Space) ? 0.13f : jumpBuffer - dt;
            if (Input.GetKeyDown(KeyCode.C)) crouchToggle = !crouchToggle;
            bool crouchRequested = crouchToggle || Input.GetKey(KeyCode.LeftControl);
            if (!crouchRequested && !HasCapsuleSpace(transform.position, StandingHeight)) crouchRequested = true;
            IsCrouched = crouchRequested;
            float desiredHeight = IsCrouched ? CrouchingHeight : StandingHeight;
            motor.height = Mathf.MoveTowards(motor.height, desiredHeight, dt * 4.8f);
            motor.center = Vector3.up * motor.height * 0.5f;
            Vector2 input = new Vector2(
                (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
                (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f));
            input = Vector2.ClampMagnitude(input, 1f);
            if (exhausted && Stamina01 > 0.25f) exhausted = false;
            bool sprint = Input.GetKey(KeyCode.LeftShift) && input.y > 0.1f && !IsCrouched && !exhausted;
            if (sprint)
            {
                Stamina01 = Mathf.Max(0f, Stamina01 - dt / 5.5f);
                if (Stamina01 <= 0f) exhausted = true;
            }
            else Stamina01 = Mathf.Min(1f, Stamina01 + dt / 4f);
            float speed = IsCrouched ? crouchSpeed : (sprint ? runSpeed : walkSpeed);
            Vector3 desired = (transform.right * input.x + transform.forward * input.y) * speed;
            float rate = grounded ? (input.sqrMagnitude > 0 ? acceleration : acceleration * 1.5f) : 3f;
            planarVelocity = Vector3.MoveTowards(planarVelocity, desired, dt * rate);
            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (jumpBuffer > 0f && coyote > 0f && !IsCrouched)
            {
                jumpBuffer = 0f;
                coyote = 0f;
                if (TryVault()) return;
                verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
                sound.Jump();
            }
            verticalVelocity -= gravity * dt;
            float fallingVelocity = verticalVelocity;
            CollisionFlags flags = motor.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
            if (!grounded && (flags & CollisionFlags.Below) != 0 && fallingVelocity < -4f)
            {
                float strength = Mathf.InverseLerp(4f, 12f, -fallingVelocity);
                sound.Land(strength);
                LandingImpulse(strength);
            }
            Vector3 travelled = transform.position - lastPosition;
            travelled.y = 0f;
            Speed01 = Mathf.Clamp01(travelled.magnitude / Mathf.Max(dt, 0.001f) / runSpeed);
            LocalPlanarVelocity = transform.InverseTransformDirection(travelled / Mathf.Max(dt, 0.001f));
            if (motor.isGrounded && travelled.sqrMagnitude > 0.000001f)
            {
                float stepLength = IsCrouched ? 0.85f : (sprint ? 1.55f : 1.25f);
                float nextPhase = GaitPhase + travelled.magnitude / stepLength * Mathf.PI;
                if (Mathf.FloorToInt(nextPhase / Mathf.PI) > Mathf.FloorToInt(GaitPhase / Mathf.PI))
                    sound.Step(IsCrouched ? 0.28f : (sprint ? 0.85f : 0.6f));
                GaitPhase = Mathf.Repeat(nextPhase, Mathf.PI * 2f);
            }
            lastPosition = transform.position;
        }

        void LateUpdate()
        {
            if (Paused) return;
            float dt = Time.deltaTime;
            float gaitTarget = IsGrounded && !IsVaulting ? Mathf.Clamp01(Speed01 * 1.8f) : 0f;
            GaitWeight = Mathf.Lerp(GaitWeight, gaitTarget, 1f - Mathf.Exp(-12f * dt));
            float desiredLean = (Input.GetKey(KeyCode.Q) ? -1f : 0f) + (Input.GetKey(KeyCode.E) ? 1f : 0f);
            if (IsVaulting) desiredLean = 0f;
            float height = Mathf.Lerp(0.91f, 1.46f, Mathf.InverseLerp(CrouchingHeight, StandingHeight, motor.height));
            Vector3 leanOrigin = transform.position + Vector3.up * height;
            Vector3 leanDirection = transform.right * Mathf.Sign(desiredLean);
            float leanAmount = 0.32f;
            if (desiredLean != 0f && Physics.SphereCast(leanOrigin, 0.12f, leanDirection, out RaycastHit hit, leanAmount + 0.07f, WorldMask, QueryTriggerInteraction.Ignore))
                leanAmount = Mathf.Max(0f, hit.distance - 0.07f);
            lean = Mathf.Lerp(lean, desiredLean * leanAmount, 1f - Mathf.Exp(-12f * dt));
            // Constrain the actual smoothed lean too, including release/switching direction.
            if (Mathf.Abs(lean) > 0.001f && Physics.SphereCast(leanOrigin, 0.12f,
                transform.right * Mathf.Sign(lean), out RaycastHit leanHit, Mathf.Abs(lean) + 0.07f,
                WorldMask, QueryTriggerInteraction.Ignore))
                lean = Mathf.Sign(lean) * Mathf.Min(Mathf.Abs(lean), Mathf.Max(0f, leanHit.distance - 0.07f));
            float motion = reducedMotion ? 0f : cameraMotion;
            // A contact occurs at phase multiples of PI: image, boots and audio share one gait.
            float bob = -Mathf.Cos(GaitPhase * 2f) * 0.014f * GaitWeight * motion;
            float sway = Mathf.Sin(GaitPhase) * 0.013f * GaitWeight * motion;
            landingOffset = Mathf.SmoothDamp(landingOffset, 0f, ref landingRecovery, 0.14f, 5f, dt);
            Vector3 desiredPosition = new Vector3(lean + sway, height + bob + landingOffset * motion, 0.02f);
            Vector3 cameraPosition = Vector3.Lerp(cameraRig.localPosition, desiredPosition, 1f - Mathf.Exp(-16f * dt));
            cameraPosition.x = lean + sway; // lean already has its own smoothing and immediate collision clamp
            if (Mathf.Abs(cameraPosition.x) > 0.001f && Physics.SphereCast(leanOrigin, 0.12f,
                transform.right * Mathf.Sign(cameraPosition.x), out RaycastHit cameraHit,
                Mathf.Abs(cameraPosition.x) + 0.07f, WorldMask, QueryTriggerInteraction.Ignore))
                cameraPosition.x = Mathf.Sign(cameraPosition.x) * Mathf.Max(0f, cameraHit.distance - 0.07f);
            cameraRig.localPosition = cameraPosition;
            float roll = -lean / 0.32f * (reducedMotion ? 4f : 11f) - Mathf.Clamp(TurnRate * 0.007f, -2.2f, 2.2f) * motion;
            roll += Mathf.Sin(GaitPhase) * 0.7f * GaitWeight * motion;
            currentRoll = Mathf.Lerp(currentRoll, roll, 1f - Mathf.Exp(-12f * dt));
            cameraRig.localRotation = Quaternion.Euler(pitch, 0f, currentRoll);
            float targetFov = 78f + (reducedMotion ? 0f : Speed01 * 4f);
            fov = Mathf.Lerp(fov, targetFov, 1f - Mathf.Exp(-5f * dt));
            viewCamera.fieldOfView = fov;
        }

        void FocusDoor()
        {
            focusedDoor = null;
            InteractionPrompt = "";
            if (Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out RaycastHit hit, 2.5f, WorldMask, QueryTriggerInteraction.Ignore))
            {
                focusedDoor = hit.collider.GetComponentInParent<RaidDoor>();
                if (focusedDoor) InteractionPrompt = "[G] " + focusedDoor.Prompt;
            }
        }

        bool HasCapsuleSpace(Vector3 foot, float height)
        {
            return !Physics.CheckCapsule(foot + Vector3.up * 0.3f,
                foot + Vector3.up * (height - 0.28f), 0.25f, WorldMask, QueryTriggerInteraction.Ignore);
        }

        bool TryVault()
        {
            Vector3 forward = transform.forward;
            Vector3 origin = transform.position + Vector3.up * 0.45f;
            if (!Physics.Raycast(origin, forward, out RaycastHit face, 0.9f, WorldMask, QueryTriggerInteraction.Ignore))
            {
                origin.y += 0.55f;
                if (!Physics.Raycast(origin, forward, out face, 0.9f, WorldMask, QueryTriggerInteraction.Ignore)) return false;
            }
            if (face.collider.GetComponentInParent<RaidDoor>()) return false;
            Vector3 above = face.point + forward * 0.23f;
            above.y = transform.position.y + 1.9f;
            if (!Physics.Raycast(above, Vector3.down, out RaycastHit top, 2f, WorldMask, QueryTriggerInteraction.Ignore)) return false;
            float height = top.point.y - transform.position.y;
            if (height < 0.35f || height > 1.45f || top.normal.y < 0.8f) return false;
            Vector3 destination = top.point + forward * (height < 0.95f ? 1.1f : 0.38f);
            if (!Physics.Raycast(destination + Vector3.up * 0.25f, Vector3.down, out RaycastHit landing, 1.8f, WorldMask, QueryTriggerInteraction.Ignore)) return false;
            destination = landing.point + Vector3.up * 0.035f;
            if (Mathf.Abs(destination.y - transform.position.y) > 1.5f || !HasCapsuleSpace(destination, StandingHeight)) return false;
            vaultRoutine = StartCoroutine(Vault(destination, top.point.y + 0.15f, height > 0.95f ? 0.85f : 0.65f));
            return true;
        }

        IEnumerator Vault(Vector3 destination, float clearanceY, float duration)
        {
            IsVaulting = true;
            VaultProgress = 0f;
            LocalPlanarVelocity = Vector3.zero;
            Speed01 = 0f;
            motor.enabled = false;
            Vector3 start = transform.position;
            float apex = Mathf.Max(clearanceY, destination.y + 0.15f);
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            sound.Jump();
            bool interrupted = false;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = elapsed / duration;
                VaultProgress = t;
                Vector3 next;
                if (t < 0.38f)
                    next = Vector3.Lerp(start, new Vector3(start.x, apex, start.z), Mathf.SmoothStep(0f, 1f, t / 0.38f));
                else if (t < 0.82f)
                    next = Vector3.Lerp(new Vector3(start.x, apex, start.z), new Vector3(destination.x, apex, destination.z), Mathf.SmoothStep(0f, 1f, (t - 0.38f) / 0.44f));
                else
                    next = Vector3.Lerp(new Vector3(destination.x, apex, destination.z), destination, Mathf.SmoothStep(0f, 1f, (t - 0.82f) / 0.18f));
                if (!CanTraverseTo(next)) { interrupted = true; break; }
                transform.position = next;
                yield return null;
            }
            // Finish exactly when the remaining path is clear; otherwise keep the last safe position.
            if (!interrupted && CanTraverseTo(destination)) transform.position = destination;
            motor.enabled = true;
            IsVaulting = false;
            VaultProgress = -1f;
            vaultRoutine = null;
            lastPosition = transform.position;
            sound.Land(0.25f);
            LandingImpulse(0.25f);
        }

        void LandingImpulse(float strength)
        {
            landingOffset = Mathf.Max(-0.065f, landingOffset - Mathf.Lerp(0.018f, 0.055f, strength));
            landingRecovery = 0f;
        }

        bool CanTraverseTo(Vector3 destination)
        {
            if (!HasCapsuleSpace(destination, StandingHeight)) return false;
            Vector3 delta = destination - transform.position;
            float distance = delta.magnitude;
            if (distance < 0.0001f) return true;
            return !Physics.CapsuleCast(transform.position + Vector3.up * 0.3f,
                transform.position + Vector3.up * (StandingHeight - 0.28f), 0.25f,
                delta / distance, distance, WorldMask, QueryTriggerInteraction.Ignore);
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }

        public void ResetToSpawn()
        {
            if (vaultRoutine != null) StopCoroutine(vaultRoutine);
            vaultRoutine = null;
            IsVaulting = false;
            VaultProgress = -1f;
            motor.enabled = false;
            transform.SetPositionAndRotation(spawn, spawnRotation);
            motor.height = StandingHeight;
            motor.center = Vector3.up * StandingHeight * 0.5f;
            motor.enabled = true;
            yaw = spawnRotation.eulerAngles.y;
            pitch = lean = currentRoll = verticalVelocity = Speed01 = 0f;
            GaitPhase = GaitWeight = TurnRate = landingOffset = landingRecovery = 0f;
            LocalPlanarVelocity = Vector3.zero;
            planarVelocity = Vector3.zero;
            crouchToggle = IsCrouched = exhausted = false;
            Stamina01 = 1f;
            lastPosition = spawn;
            SetPaused(false);
        }

        void OnApplicationFocus(bool focus) { if (!focus && !Paused) SetPaused(true); }
        void OnDestroy() { Time.timeScale = 1f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
