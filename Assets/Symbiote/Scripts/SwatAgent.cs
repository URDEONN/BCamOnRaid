using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SymbioteRaid
{
    /// <summary>Deterministic state based guard, with a directly controlled possessed mode.</summary>
    [DisallowMultipleComponent]
    public sealed class SwatAgent : MonoBehaviour
    {
        enum State { Patrol, Investigate, Engage, Cover, Stunned, Possessed, Dead }
        public bool IsAlive => Health > 0f;
        public bool IsPossessed => state == State.Possessed;
        public bool IsStunned => state == State.Stunned;
        public float Health { get; private set; } = 100f;
        public float Shield { get; private set; }
        public float PossessionAmount { get; private set; }
        public Transform Chest { get; private set; }
        public Transform Head { get; private set; }
        public Transform Muzzle { get; private set; }
        public Vector3 Center => transform.position + Vector3.up * (motor ? motor.height * .6f : 1.08f);
        public string StateLabel
        {
            get
            {
                switch (state)
                {
                    case State.Investigate: return "INVESTIGANDO";
                    case State.Engage: return "COMBATE";
                    case State.Cover: return "COBERTURA";
                    case State.Stunned: return "ATURDIDO";
                    case State.Possessed: return "POSEÍDO";
                    case State.Dead: return "ABATIDO";
                    default: return "PATRULLANDO";
                }
            }
        }

        const int WorldMask = 1;
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Renderer> bodyRenderers = new List<Renderer>();
        readonly HashSet<int> animatorParameters = new HashSet<int>();
        readonly int speedParameter = Animator.StringToHash("Speed");
        readonly int crouchParameter = Animator.StringToHash("Crouch");
        readonly int aimParameter = Animator.StringToHash("Aim");
        readonly int fireParameter = Animator.StringToHash("Fire");
        readonly Vector3[] tendrilPoints = new Vector3[10];
        readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        CharacterController motor;
        SymbioteGame game;
        State state;
        Vector3[] route = Array.Empty<Vector3>();
        Vector3[] cover = Array.Empty<Vector3>();
        Transform model, leftLeg, rightLeg, leftArm, rightArm, weaponRoot, rightHandBone;
        Vector3 weaponAimDirection = Vector3.forward;
        Animator animator;
        Vector3 modelScale = Vector3.one;
        Vector3 planarVelocity, impulse, investigationPoint, selectedCover;
        Vector3 previousPosition;
        float verticalVelocity, awareness, decisionTimer, fireTimer, investigateUntil;
        float stunUntil, lastDamageTime = -100f, lostSightTime, walkPhase, actualSpeed, recoil;
        float deathProgress, nextAlarmTime, signedAnimationSpeed;
        int routeIndex;
        bool configured, crouched, importedModel, coverSelected, dying;
        LineRenderer[] dorsalTendrils;

        void Awake()
        {
            gameObject.layer = 8;
            motor = GetComponent<CharacterController>();
            if (!motor) motor = gameObject.AddComponent<CharacterController>();
            motor.height = 1.8f;
            motor.radius = .28f;
            motor.center = Vector3.up * .9f;
            motor.skinWidth = .03f;
            motor.stepOffset = .28f;
            motor.slopeLimit = 48f;
            motor.minMoveDistance = 0f;
            previousPosition = transform.position;
        }

        public void Configure(SymbioteGame owner, Vector3[] patrol, Vector3[] coverLocations)
        {
            if (configured) return;
            game = owner;
            route = patrol ?? Array.Empty<Vector3>();
            cover = coverLocations ?? Array.Empty<Vector3>();
            BuildVisual();
            previousPosition = transform.position;
            configured = true;
            state = State.Patrol;
            decisionTimer = UnityEngine.Random.Range(0f, .12f);
            fireTimer = UnityEngine.Random.Range(.4f, 1.2f);
        }

        void Update()
        {
            if (!configured || !game || game.Paused) return;
            if (!IsAlive)
            {
                AnimateDeath();
                return;
            }
            if (game.GameOver || game.Won) return;
            PossessionAmount = Mathf.MoveTowards(PossessionAmount, IsPossessed ? 1f : 0f, Time.deltaTime * 1.7f);
            UpdateInfection();
            if (IsPossessed) { AnimateVisual(); return; }
            if (state == State.Stunned)
            {
                if (Time.time >= stunUntil) { state = State.Investigate; investigateUntil = Time.time + 5f; }
                Move(Vector3.zero, 0f, false);
                AnimateVisual();
                return;
            }
            decisionTimer -= Time.deltaTime;
            fireTimer -= Time.deltaTime;
            if (decisionTimer <= 0f) { decisionTimer = .1f; Think(); }
            Vector3 desired = Vector3.zero;
            float speed = 1.65f;
            if (state == State.Patrol)
            {
                if (route.Length > 0)
                {
                    Vector3 target = route[routeIndex];
                    if (HorizontalDistance(target, transform.position) < .55f) routeIndex = (routeIndex + 1) % route.Length;
                    desired = Horizontal(target - transform.position);
                }
            }
            else if (state == State.Investigate)
            {
                desired = Horizontal(investigationPoint - transform.position);
                if (desired.magnitude < .7f) desired = Vector3.zero;
                speed = 2.2f;
                if (Time.time > investigateUntil) { state = State.Patrol; awareness = 0f; }
            }
            else if (state == State.Engage || state == State.Cover)
            {
                Vector3 toPlayer = Horizontal(game.TargetPoint - transform.position);
                SetAimDirection(game.TargetPoint - Center);
                float distance = toPlayer.magnitude;
                if (coverSelected && HorizontalDistance(selectedCover, transform.position) > .6f)
                {
                    desired = Horizontal(selectedCover - transform.position);
                    speed = 2.4f;
                }
                else if (distance < 5f)
                {
                    desired = -toPlayer.normalized;
                    speed = 1.9f;
                }
                else if (distance > 13f)
                {
                    desired = toPlayer;
                    speed = 2.1f;
                }
                else if (coverSelected && !HasClearShot(game.TargetPoint))
                {
                    // Peek sideways, but only through a direction that is clear for the capsule.
                    float side = Mathf.Sin(Time.time * .8f + GetInstanceID()) >= 0f ? 1f : -1f;
                    desired = transform.right * side;
                    speed = 1.3f;
                }
                if (fireTimer <= 0f && CanSeePoint(game.TargetPoint, 19f) && HasClearShot(game.TargetPoint))
                {
                    fireTimer = UnityEngine.Random.Range(.7f, 1.1f);
                    FireVisual(game.TargetPoint, false);
                    // Accuracy drops against the small, fast surface form and while retreating.
                    float accuracy = game.IsPossessed ? .78f : .43f;
                    if (desired.sqrMagnitude > .1f) accuracy *= .8f;
                    if (UnityEngine.Random.value < accuracy) game.DamagePlayer(game.IsPossessed ? 9f : 6f);
                    game.EmitNoise(Center, .8f);
                }
            }
            Move(desired, speed, state == State.Patrol || state == State.Investigate);
            AnimateVisual();
        }

        void Think()
        {
            float range = game.IsShadowed(game.TargetPoint) ? 9f : 17f;
            if (!game.IsPossessed) range *= .85f;
            bool visible = CanSeePoint(game.TargetPoint, range);
            if (visible)
            {
                awareness = Mathf.Min(1f, awareness + .22f);
                investigationPoint = game.TargetPoint;
                lostSightTime = Time.time;
                if (awareness >= .8f || state == State.Engage || state == State.Cover)
                {
                    state = State.Engage;
                    if (Time.time >= nextAlarmTime)
                    {
                        nextAlarmTime = Time.time + 3f;
                        game.RaiseAlarm(game.TargetPoint, this);
                    }
                    if (!coverSelected) SelectCover();
                }
            }
            else
            {
                awareness = Mathf.Max(0f, awareness - .045f);
                if ((state == State.Engage || state == State.Cover) && Time.time - lostSightTime > 3.5f)
                {
                    state = State.Investigate;
                    investigateUntil = Time.time + 7f;
                    coverSelected = false;
                }
            }
        }

        void SelectCover()
        {
            float nearest = float.PositiveInfinity;
            Vector3 enemy = game.TargetPoint;
            for (int i = 0; i < cover.Length; i++)
            {
                Vector3 candidate = cover[i];
                if (Physics.CheckCapsule(candidate + Vector3.up * .3f, candidate + Vector3.up * 1.5f,
                    .26f, WorldMask, QueryTriggerInteraction.Ignore)) continue;
                Vector3 target = candidate + Vector3.up;
                bool sheltered = Physics.Linecast(enemy, target, WorldMask, QueryTriggerInteraction.Ignore);
                if (!sheltered) continue;
                float cost = HorizontalDistance(candidate, transform.position);
                if (cost < nearest && cost < 10f) { nearest = cost; selectedCover = candidate; coverSelected = true; }
            }
            if (coverSelected) state = State.Cover;
        }

        public bool CanSeePoint(Vector3 point, float range = 16f)
        {
            if (!IsAlive || IsPossessed || !configured || state == State.Stunned) return false;
            Vector3 origin = Head ? Head.position : Center + Vector3.up * .45f;
            Vector3 delta = point - origin;
            if (delta.sqrMagnitude > range * range) return false;
            // Full 3D cone: a guard can notice a creature on a wall or ceiling ahead.
            if (delta.sqrMagnitude > .01f && Vector3.Dot(transform.forward, delta.normalized) < .17f) return false;
            return !Physics.Linecast(origin, point, WorldMask, QueryTriggerInteraction.Ignore);
        }

        bool HasClearShot(Vector3 target)
        {
            Vector3 start = Muzzle ? Muzzle.position : Center;
            if (Physics.CheckSphere(start, .045f, WorldMask, QueryTriggerInteraction.Ignore)) return false;
            Vector3 chest = Chest ? Chest.position : Center;
            Vector3 barrel = start - chest;
            if (barrel.sqrMagnitude > .0001f && Physics.SphereCast(chest, .06f, barrel.normalized,
                out RaycastHit barrelBlock, barrel.magnitude, WorldMask, QueryTriggerInteraction.Ignore)) return false;
            Vector3 delta = target - start;
            return delta.sqrMagnitude < .0001f || !Physics.Raycast(start, delta.normalized,
                Mathf.Max(0f, delta.magnitude - .05f), WorldMask, QueryTriggerInteraction.Ignore);
        }

        public void HearNoise(Vector3 position, float loudness)
        {
            if (!configured || !IsAlive || IsPossessed || state == State.Stunned) return;
            float distance = Vector3.Distance(Center, position);
            float range = Mathf.Max(0f, loudness) * 22f;
            if (Physics.Linecast(Center, position, WorldMask, QueryTriggerInteraction.Ignore)) range *= .5f;
            if (distance > range) return;
            investigationPoint = position;
            investigateUntil = Time.time + 6f;
            awareness = Mathf.Max(awareness, .35f);
            if (state != State.Engage && state != State.Cover) state = State.Investigate;
        }

        public void MoveAsHost(Vector3 worldDirection, bool sprint, bool jump, bool crouch)
        {
            if (!configured || !IsAlive || !IsPossessed || !game || game.Paused || game.GameOver || game.Won) return;
            float dt = Time.deltaTime;
            bool wantsCrouch = crouch;
            if (!wantsCrouch && Physics.CheckCapsule(transform.position + Vector3.up * .3f,
                transform.position + Vector3.up * 1.52f, .25f, WorldMask, QueryTriggerInteraction.Ignore)) wantsCrouch = true;
            crouched = wantsCrouch;
            motor.height = Mathf.MoveTowards(motor.height, crouched ? 1.12f : 1.8f, dt * 4f);
            motor.center = Vector3.up * (motor.height * .5f);
            if (jump && motor.isGrounded && !crouched) verticalVelocity = Mathf.Sqrt(2f * 24f * 1.65f);
            float speed = crouched ? 1.6f : sprint ? 6.2f : 3.3f;
            Move(worldDirection, speed, false);
        }

        void Move(Vector3 desired, float speed, bool rotateTowardMovement)
        {
            if (!motor || !motor.enabled) return;
            float dt = Time.deltaTime;
            desired.y = 0f;
            desired = Vector3.ClampMagnitude(desired, 1f);
            if (!IsPossessed && desired.sqrMagnitude > .01f) desired = AvoidObstacle(desired);
            planarVelocity = Vector3.MoveTowards(planarVelocity, desired * speed, 15f * dt);
            if (motor.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity -= 24f * dt;
            Vector3 movement = planarVelocity + impulse;
            movement.y += verticalVelocity;
            CollisionFlags flags = motor.Move(movement * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
            impulse = Vector3.MoveTowards(impulse, Vector3.zero, 12f * dt);
            Vector3 travelled = Horizontal(transform.position - previousPosition);
            actualSpeed = travelled.magnitude / Mathf.Max(dt, .001f);
            signedAnimationSpeed = Mathf.Clamp(Vector3.Dot(travelled / Mathf.Max(dt, .001f), transform.forward) / 6.2f, -1f, 1f);
            walkPhase += travelled.magnitude * 5.2f;
            previousPosition = transform.position;
            if (rotateTowardMovement && planarVelocity.sqrMagnitude > .02f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(planarVelocity), 1f - Mathf.Exp(-8f * dt));
        }

        Vector3 AvoidObstacle(Vector3 direction)
        {
            Vector3 origin = Center;
            if (!Physics.SphereCast(origin, .29f, direction, out RaycastHit hit, .7f, WorldMask, QueryTriggerInteraction.Ignore)) return direction;
            Vector3 tangent = Vector3.ProjectOnPlane(direction, hit.normal);
            tangent.y = 0f;
            if (tangent.sqrMagnitude > .03f) return tangent.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
            bool leftFree = !Physics.SphereCast(origin, .29f, side, out _, .75f, WorldMask, QueryTriggerInteraction.Ignore);
            bool rightFree = !Physics.SphereCast(origin, .29f, -side, out _, .75f, WorldMask, QueryTriggerInteraction.Ignore);
            if (leftFree) return side;
            if (rightFree) return -side;
            return Vector3.zero;
        }

        public void SetAimDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude > .0001f) weaponAimDirection = direction.normalized;
            direction.y = 0f;
            if (direction.sqrMagnitude < .0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 1f - Mathf.Exp(-16f * Time.deltaTime));
        }

        public void AddImpulse(Vector3 value)
        {
            if (!IsAlive) return;
            impulse += Vector3.ClampMagnitude(value, 16f);
        }

        public void ApplyDamage(float amount, Vector3 source, float stunSeconds = 0f)
        {
            if (!IsAlive || amount <= 0f) return;
            lastDamageTime = Time.time;
            if (IsPossessed)
            {
                float absorbed = Mathf.Min(Shield, amount);
                Shield -= absorbed;
                amount -= absorbed;
            }
            Health = Mathf.Max(0f, Health - amount);
            if (Health <= 0f) { Die(); return; }
            if (!IsPossessed)
            {
                investigationPoint = source;
                investigateUntil = Time.time + 8f;
                awareness = 1f;
                coverSelected = false;
                if (stunSeconds > 0f) { state = State.Stunned; stunUntil = Mathf.Max(stunUntil, Time.time + stunSeconds); }
                else state = State.Engage;
            }
        }

        public void TakeControl()
        {
            if (!IsAlive || !configured) return;
            state = State.Possessed;
            gameObject.layer = 9;
            Shield = 60f;
            planarVelocity = Vector3.zero;
            impulse = Vector3.zero;
            awareness = 0f;
            crouched = false;
            lastDamageTime = Time.time;
            CreateDorsalTendrils();
        }

        public void Release(bool die)
        {
            if (die) { if (IsAlive) Die(); return; }
            if (!IsAlive) return;
            state = State.Stunned;
            stunUntil = Time.time + 2f;
            gameObject.layer = 8;
            Shield = 0f;
        }

        public void RegenerateShield(float dt)
        {
            if (IsPossessed && IsAlive && Time.time - lastDamageTime >= 3f) Shield = Mathf.Min(60f, Shield + dt * 12f);
        }

        void Die()
        {
            if (dying) return;
            dying = true;
            bool wasPossessed = IsPossessed;
            Health = Shield = 0f;
            state = State.Dead;
            gameObject.layer = 8;
            if (motor) motor.enabled = false;
            if (animator) animator.enabled = false;
            if (game)
            {
                game.OnAgentKilled(this);
                if (wasPossessed) game.HostDied(this);
            }
        }

        public void FireVisual(Vector3 target, bool empowered)
        {
            if (!Muzzle) return;
            if (!HasClearShot(target)) return;
            recoil = 1f;
            if (animator && animatorParameters.Contains(fireParameter)) animator.SetTrigger(fireParameter);
            SymbioteVfx.Beam(Muzzle.position, target, empowered ? new Color(.55f, .25f, 1f) : new Color(1f, .65f, .22f), empowered ? .045f : .022f, .08f);
        }

        static Vector3 Horizontal(Vector3 v) { v.y = 0f; return v; }
        static float HorizontalDistance(Vector3 a, Vector3 b) { return Horizontal(a - b).magnitude; }

        void BuildVisual()
        {
            if (game.Assets && game.Assets.swatModel)
            {
                model = Instantiate(game.Assets.swatModel, transform).transform;
                model.name = "Imported SWAT visual";
                importedModel = true;
                foreach (Collider child in model.GetComponentsInChildren<Collider>(true)) { child.enabled = false; Destroy(child); }
                foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
                foreach (Transform child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 8;
                Renderer[] importedRenderers = model.GetComponentsInChildren<Renderer>(true);
                if (importedRenderers.Length > 0)
                {
                    Bounds bounds = importedRenderers[0].bounds;
                    for (int i = 1; i < importedRenderers.Length; i++) bounds.Encapsulate(importedRenderers[i].bounds);
                    if (bounds.size.y > .1f)
                    {
                        model.localScale *= 1.8f / bounds.size.y;
                        bounds = importedRenderers[0].bounds;
                        for (int i = 1; i < importedRenderers.Length; i++) bounds.Encapsulate(importedRenderers[i].bounds);
                        model.position += Vector3.up * (transform.position.y - bounds.min.y);
                    }
                }
                animator = model.GetComponentInChildren<Animator>();
                if (animator)
                {
                    animator.applyRootMotion = false;
                    if (game.Assets.swatAnimations) animator.runtimeAnimatorController = game.Assets.swatAnimations;
                    if (animator.runtimeAnimatorController)
                        foreach (AnimatorControllerParameter parameter in animator.parameters) animatorParameters.Add(parameter.nameHash);
                }
                Chest = FindBone(model, "Spine2") ?? FindBone(model, "Spine1");
                Head = FindBone(model, "Head");
                rightHandBone = FindBone(model, "RightHand");
                // Mixamo character exports generally contain no weapon mesh. Provide an original
                // provisional rifle, independent of the source hand bone's axis convention.
                weaponRoot = Pivot("Provisional tactical rifle", transform, new Vector3(.2f, 1.12f, .4f));
                MakeRifle(weaponRoot, MakeMaterial(new Color(.055f, .06f, .07f)), MakeMaterial(new Color(.22f, .24f, .26f)));
                Muzzle = Anchor("Rifle muzzle", weaponRoot, new Vector3(0, .035f, .49f));
                ApplyInfectionMaterials(importedRenderers);
            }
            else BuildPlaceholder();
            if (!Chest) Chest = Anchor("Chest anchor", model, new Vector3(0f, 1.28f, 0f));
            if (!Head) Head = Anchor("Head anchor", model, new Vector3(0f, 1.65f, 0f));
            if (!Muzzle) Muzzle = Anchor("Muzzle anchor", model, new Vector3(.2f, 1.18f, .8f));
            modelScale = model.localScale;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true)) bodyRenderers.Add(renderer);
            if (importedModel && weaponRoot)
                foreach (Renderer renderer in weaponRoot.GetComponentsInChildren<Renderer>(true)) bodyRenderers.Add(renderer);
            weaponAimDirection = transform.forward;
            UpdateWeaponPose();
            UpdateInfection();
        }

        static Transform FindBone(Transform root, string suffix)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return child;
            return null;
        }

        static Transform Anchor(string name, Transform parent, Vector3 position)
        {
            var child = new GameObject(name);
            child.layer = 8;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            return child.transform;
        }

        void BuildPlaceholder()
        {
            model = new GameObject("Provisional tactical operator").transform;
            model.SetParent(transform, false);
            model.gameObject.layer = 8;
            Material navy = MakeMaterial(new Color(.065f, .13f, .20f));
            Material armor = MakeMaterial(new Color(.07f, .08f, .085f));
            Material skin = MakeMaterial(new Color(.32f, .26f, .21f));
            Material steel = MakeMaterial(new Color(.21f, .24f, .26f));
            Part("Uniform torso", model, new Vector3(0, 1.15f, 0), new Vector3(.43f, .48f, .25f), navy);
            Part("Ballistic vest", model, new Vector3(0, 1.18f, .035f), new Vector3(.47f, .39f, .28f), armor);
            Part("Belt", model, new Vector3(0, .88f, 0), new Vector3(.42f, .08f, .28f), armor);
            Head = Part("Head", model, new Vector3(0, 1.61f, 0), new Vector3(.24f, .28f, .23f), skin);
            Part("Helmet", model, new Vector3(0, 1.77f, -.015f), new Vector3(.31f, .16f, .30f), armor);
            Part("Visor", model, new Vector3(0, 1.65f, .135f), new Vector3(.26f, .085f, .04f), steel);
            leftLeg = Pivot("Left leg", model, new Vector3(-.125f, .85f, 0));
            rightLeg = Pivot("Right leg", model, new Vector3(.125f, .85f, 0));
            foreach (Transform leg in new[] { leftLeg, rightLeg })
            {
                Part("Trouser leg", leg, new Vector3(0, -.33f, 0), new Vector3(.18f, .66f, .21f), navy);
                Part("Boot", leg, new Vector3(0, -.76f, .055f), new Vector3(.20f, .18f, .33f), armor);
                Part("Knee pad", leg, new Vector3(0, -.37f, .115f), new Vector3(.17f, .16f, .035f), armor);
            }
            leftArm = Pivot("Left rifle arm", model, new Vector3(-.29f, 1.34f, 0));
            rightArm = Pivot("Right rifle arm", model, new Vector3(.29f, 1.34f, 0));
            foreach (Transform arm in new[] { leftArm, rightArm })
            {
                Part("Sleeve", arm, new Vector3(0, -.12f, .105f), new Vector3(.15f, .27f, .22f), navy);
                Part("Forearm", arm, new Vector3(0, -.22f, .27f), new Vector3(.14f, .14f, .28f), navy);
                Part("Glove", arm, new Vector3(0, -.22f, .42f), new Vector3(.13f, .12f, .14f), armor);
            }
            weaponRoot = Pivot("Tactical rifle", model, new Vector3(.19f, 1.12f, .42f));
            MakeRifle(weaponRoot, armor, steel);
            Chest = Anchor("mixamorig:Spine2", model, new Vector3(0, 1.27f, 0));
            Muzzle = Anchor("Muzzle", weaponRoot, new Vector3(0, .035f, .49f));
        }

        void MakeRifle(Transform rifle, Material armor, Material steel)
        {
            Part("Rifle body", rifle, Vector3.zero, new Vector3(.11f, .16f, .47f), armor);
            Part("Rifle barrel", rifle, new Vector3(0, .035f, .34f), new Vector3(.045f, .045f, .28f), steel);
            Part("Rifle magazine", rifle, new Vector3(0, -.13f, -.02f), new Vector3(.08f, .19f, .12f), armor);
            Part("Rifle sight", rifle, new Vector3(0, .12f, .1f), new Vector3(.045f, .055f, .16f), steel);
        }

        void UpdateWeaponPose()
        {
            if (!weaponRoot) return;
            if (importedModel && rightHandBone)
                weaponRoot.position = rightHandBone.position + transform.forward * .06f;
            Vector3 aim = weaponAimDirection.sqrMagnitude > .001f ? weaponAimDirection : transform.forward;
            Vector3 up = Mathf.Abs(Vector3.Dot(aim.normalized, Vector3.up)) > .97f ? transform.right : Vector3.up;
            if (!IsAlive) aim = model ? model.forward : transform.forward;
            weaponRoot.rotation = Quaternion.LookRotation(aim, up) * Quaternion.Euler(-recoil * 3f, 0f, 0f);
        }

        Material MakeMaterial(Color color)
        {
            Material material;
            if (game.Assets && game.Assets.infectionMaterial) material = new Material(game.Assets.infectionMaterial);
            else material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "SWAT owned material";
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .3f);
            ownedMaterials.Add(material);
            return material;
        }

        void ApplyInfectionMaterials(Renderer[] renderers)
        {
            if (!game.Assets || !game.Assets.infectionMaterial) return;
            var copies = new Dictionary<Material, Material>();
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material original = materials[i];
                    if (!original) continue;
                    if (!copies.TryGetValue(original, out Material replacement))
                    {
                        replacement = new Material(game.Assets.infectionMaterial) { name = original.name + " - infection" };
                        Color baseColor = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white;
                        if (replacement.HasProperty("_BaseColor")) replacement.SetColor("_BaseColor", baseColor);
                        Texture texture = original.HasProperty("_BaseMap") ? original.GetTexture("_BaseMap") : original.HasProperty("_MainTex") ? original.GetTexture("_MainTex") : null;
                        // Explicit bundled textures also cover FBX material remaps that Unity cannot resolve automatically.
                        Texture bundled = original.name.ToLowerInvariant().Contains("head") ? game.Assets.headDiffuse : game.Assets.bodyDiffuse;
                        if (!texture && bundled) texture = bundled;
                        if (texture && replacement.HasProperty("_BaseMap")) replacement.SetTexture("_BaseMap", texture);
                        ownedMaterials.Add(replacement);
                        copies.Add(original, replacement);
                    }
                    materials[i] = replacement;
                }
                renderer.sharedMaterials = materials;
            }
        }

        static Transform Pivot(string name, Transform parent, Vector3 position)
        {
            return Anchor(name, parent, position);
        }

        static Transform Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.layer = 8;
            Collider collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            return part.transform;
        }

        void UpdateInfection()
        {
            propertyBlock.SetFloat("_PossessionAmount", PossessionAmount);
            for (int i = 0; i < bodyRenderers.Count; i++) if (bodyRenderers[i]) bodyRenderers[i].SetPropertyBlock(propertyBlock);
        }

        void AnimateVisual()
        {
            if (!model) return;
            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 8f);
            if (animator && animator.runtimeAnimatorController)
            {
                if (animatorParameters.Contains(speedParameter)) animator.SetFloat(speedParameter, signedAnimationSpeed, .12f, Time.deltaTime);
                if (animatorParameters.Contains(crouchParameter)) animator.SetBool(crouchParameter, crouched);
                if (animatorParameters.Contains(aimParameter)) animator.SetBool(aimParameter, IsPossessed || state == State.Engage || state == State.Cover);
            }
            else
            {
                model.localScale = new Vector3(modelScale.x, modelScale.y * motor.height / 1.8f, modelScale.z);
                float swing = Mathf.Sin(walkPhase) * Mathf.Min(1f, actualSpeed / 2f) * 24f;
                if (leftLeg) leftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
                if (rightLeg) rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
                if (leftArm) leftArm.localRotation = Quaternion.Euler(-recoil * 4f, 0, 0);
                if (rightArm) rightArm.localRotation = Quaternion.Euler(-recoil * 7f, 0, 0);
            }
        }

        void AnimateDeath()
        {
            if (!model) return;
            deathProgress = Mathf.Min(1f, deathProgress + Time.deltaTime * 2f);
            model.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(0, 0, 82f), Mathf.SmoothStep(0, 1, deathProgress));
            model.localPosition = Vector3.down * .55f * deathProgress;
        }

        void CreateDorsalTendrils()
        {
            if (dorsalTendrils != null) return;
            dorsalTendrils = new LineRenderer[4];
            Material material = game.Assets && game.Assets.slimeMaterial ? game.Assets.slimeMaterial : MakeMaterial(new Color(.025f, .015f, .04f));
            for (int i = 0; i < dorsalTendrils.Length; i++)
            {
                Transform tendril = Anchor("Dorsal biomass tendril", transform, Vector3.zero);
                var line = tendril.gameObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = tendrilPoints.Length;
                line.widthCurve = AnimationCurve.Linear(0f, .085f, 1f, .004f);
                line.sharedMaterial = material;
                line.numCapVertices = 3;
                line.numCornerVertices = 2;
                line.generateLightingData = true;
                line.shadowCastingMode = ShadowCastingMode.On;
                dorsalTendrils[i] = line;
            }
        }

        void LateUpdate()
        {
            if (!configured || !game || game.Paused) return;
            UpdateWeaponPose();
            if (dorsalTendrils == null || !Chest) return;
            for (int i = 0; i < dorsalTendrils.Length; i++)
            {
                LineRenderer line = dorsalTendrils[i];
                line.enabled = PossessionAmount > .03f;
                Vector3 start = Chest.position - transform.forward * .14f;
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 spread = transform.right * side * (.65f + i * .12f) - transform.forward * .55f;
                for (int p = 0; p < tendrilPoints.Length; p++)
                {
                    float t = p / (float)(tendrilPoints.Length - 1);
                    Vector3 wobble = transform.right * Mathf.Sin(Time.time * 2.5f + i * 1.8f + t * 5f) * t * .09f;
                    tendrilPoints[p] = start + (spread * t + Vector3.up * (Mathf.Sin(t * Mathf.PI * .8f) * .7f) + wobble) * PossessionAmount;
                }
                line.SetPositions(tendrilPoints);
            }
        }

        void OnDestroy()
        {
            foreach (Material material in ownedMaterials) if (material) Destroy(material);
        }
    }
}
