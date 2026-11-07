using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BodycamRaid
{
    /// <summary>
    /// Original provisional officer visuals. This lightweight procedural rig provides
    /// feet, uniform, equipment and gloved hands without imported character assets.
    /// It is not a replacement for an authored skinned character or animation set.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RaidPlayer))]
    public sealed class OfficerBody : MonoBehaviour
    {
        public bool visibleBody = true;
        public bool visibleHands = true;

        sealed class Leg
        {
            public float side;
            public Transform thigh, shin, boot, kneePad;
        }

        RaidPlayer player;
        CharacterController motor;
        Transform bodyRoot, torso, handsRoot;
        readonly Leg[] legs = new Leg[2];
        readonly List<Material> ownedMaterials = new List<Material>(5);
        Material uniform, vest, rubber, detail, glove;
        Vector3 handsOffset;
        Quaternion handsRotation = Quaternion.identity;

        void Awake()
        {
            player = GetComponent<RaidPlayer>();
            motor = GetComponent<CharacterController>();
            if (player.viewCamera == null)
            {
                Debug.LogWarning("OfficerBody necesita la cámara de RaidPlayer antes de iniciarse.", this);
                enabled = false;
                return;
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) { enabled = false; return; }
            uniform = MakeMaterial(shader, "Officer navy cloth", new Color(.055f, .085f, .11f), 0f, .15f);
            vest = MakeMaterial(shader, "Officer equipment", new Color(.075f, .09f, .095f), .03f, .2f);
            rubber = MakeMaterial(shader, "Officer boots", new Color(.025f, .03f, .035f), 0f, .12f);
            glove = MakeMaterial(shader, "Officer gloves", new Color(.105f, .12f, .125f), 0f, .2f);
            detail = MakeMaterial(shader, "Officer fasteners", new Color(.29f, .32f, .33f), .55f, .35f);

            bodyRoot = new GameObject("Officer body - provisional procedural rig").transform;
            bodyRoot.SetParent(transform, false);
            bodyRoot.localPosition = new Vector3(0f, 0f, -.11f);
            bodyRoot.gameObject.layer = 2;
            torso = new GameObject("Pelvis and equipment").transform;
            torso.SetParent(bodyRoot, false);
            torso.gameObject.layer = 2;
            CreateTorso();
            for (int i = 0; i < legs.Length; i++)
            {
                var leg = new Leg { side = i == 0 ? -1f : 1f };
                leg.thigh = Primitive("Trouser thigh", PrimitiveType.Capsule, bodyRoot, uniform, true);
                leg.shin = Primitive("Trouser shin", PrimitiveType.Capsule, bodyRoot, uniform, true);
                leg.boot = Primitive("Tactical boot", PrimitiveType.Cube, bodyRoot, rubber, true);
                leg.kneePad = Primitive("Knee pad", PrimitiveType.Cube, bodyRoot, vest, true);
                leg.kneePad.localScale = new Vector3(.135f, .15f, .055f);
                // Child dimensions are relative to the normalized boot cube.
                Box("Boot sole", leg.boot, new Vector3(0f, -.405f, 0f), new Vector3(1.035f, .18f, 1.02f), rubber);
                Box("Reinforced toe", leg.boot, new Vector3(0f, .30f, .27f), new Vector3(.97f, .22f, .43f), vest);
                legs[i] = leg;
            }
            handsRoot = new GameObject("First person gloved hands").transform;
            handsRoot.SetParent(player.viewCamera.transform, false);
            handsRoot.gameObject.layer = 2;
            CreateHand(-1f);
            CreateHand(1f);
            ApplyPose(0f);
        }

        Material MakeMaterial(Shader shader, string label, Color color, float metallic, float smoothness)
        {
            var material = new Material(shader) { name = label, color = color };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            ownedMaterials.Add(material);
            return material;
        }

        static Transform Primitive(string label, PrimitiveType shape, Transform parent, Material material, bool shadows)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = label;
            // Disable the automatically created collider before any subsequent world query.
            // All visuals also use Ignore Raycast, matching the player capsule's layer.
            var collider = go.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            go.layer = 2;
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = shadows;
            return go.transform;
        }

        Transform Box(string label, Transform parent, Vector3 position, Vector3 size, Material material, bool shadows = true)
        {
            Transform part = Primitive(label, PrimitiveType.Cube, parent, material, shadows);
            part.localPosition = position;
            part.localScale = size;
            return part;
        }

        void CreateTorso()
        {
            // Chest pieces stay behind the lens and below chest height in both postures.
            Box("Uniform hips", torso, new Vector3(0f, -.025f, -.015f), new Vector3(.33f, .18f, .21f), uniform);
            Box("Vest lower section", torso, new Vector3(0f, .18f, -.055f), new Vector3(.36f, .29f, .19f), vest);
            Box("Duty belt", torso, new Vector3(0f, .035f, 0f), new Vector3(.39f, .065f, .235f), rubber);
            Box("Belt clasp", torso, new Vector3(0f, .035f, .125f), new Vector3(.065f, .045f, .025f), detail);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Belt pouch", torso, new Vector3(side * .205f, -.015f, .015f), new Vector3(.09f, .14f, .11f), vest);
                Box("Vest webbing", torso, new Vector3(side * .095f, .19f, .045f), new Vector3(.055f, .24f, .018f), uniform);
            }
            Box("Rear utility pouch", torso, new Vector3(0f, .015f, -.15f), new Vector3(.18f, .13f, .09f), vest);
        }

        void CreateHand(float side)
        {
            var sleeve = Primitive("Sleeve", PrimitiveType.Capsule, handsRoot, uniform, false);
            SetSegment(sleeve, new Vector3(side * .29f, -.39f, .08f), new Vector3(side * .245f, -.285f, .285f), .105f);
            var cuff = Primitive("Glove cuff", PrimitiveType.Capsule, handsRoot, glove, false);
            SetSegment(cuff, new Vector3(side * .245f, -.285f, .285f), new Vector3(side * .205f, -.252f, .405f), .085f);
            var hand = Box("Gloved hand", handsRoot, new Vector3(side * .20f, -.25f, .44f),
                new Vector3(.092f, .073f, .145f), glove, false);
            hand.localRotation = Quaternion.Euler(-8f, side * -10f, side * 7f);
            Box("Glove knuckle reinforcement", hand, new Vector3(0f, .48f, -.03f),
                new Vector3(.8f, .16f, .53f), vest, false);
            Box("Glove thumb", hand, new Vector3(-side * .48f, -.03f, .13f),
                new Vector3(.34f, .66f, .45f), glove, false);
        }

        void LateUpdate()
        {
            if (player == null || bodyRoot == null || player.Paused) return;
            ApplyPose(Time.deltaTime);
        }

        void ApplyPose(float dt)
        {
            if (bodyRoot.gameObject.activeSelf != visibleBody) bodyRoot.gameObject.SetActive(visibleBody);
            if (handsRoot.gameObject.activeSelf != visibleHands) handsRoot.gameObject.SetActive(visibleHands);
            float crouch = motor != null ? 1f - Mathf.InverseLerp(1.12f, 1.8f, motor.height) : (player.IsCrouched ? 1f : 0f);
            float vault = player.IsVaulting ? Mathf.Sin(Mathf.Clamp01(player.VaultProgress) * Mathf.PI) : 0f;
            float gait = player.GaitWeight;
            float pelvisHeight = Mathf.Lerp(.93f, .57f, crouch) - vault * .07f;
            torso.localPosition = Vector3.up * pelvisHeight;
            torso.localRotation = Quaternion.Euler(crouch * 8f, 0f, -player.LocalPlanarVelocity.x * 1.2f);
            bodyRoot.localRotation = Quaternion.Euler(0f, -Mathf.Clamp(player.TurnRate * .018f, -6f, 6f), 0f);
            if (visibleBody)
            {
                float stride = Mathf.Lerp(.15f, .30f, player.Speed01) * gait * Mathf.Lerp(1f, .65f, crouch);
                float lateral = Mathf.Clamp(player.LocalPlanarVelocity.x / 5.2f, -1f, 1f);
                float forward = Mathf.Clamp(player.LocalPlanarVelocity.z / Mathf.Max(player.LocalPlanarVelocity.magnitude, .01f), -1f, 1f);
                for (int i = 0; i < legs.Length; i++)
                {
                    Leg leg = legs[i];
                    float phase = player.GaitPhase + i * Mathf.PI;
                    float swing = Mathf.Max(0f, Mathf.Sin(phase));
                    float travel = -Mathf.Cos(phase) * stride;
                    Vector3 hip = new Vector3(leg.side * .115f, pelvisHeight - .025f, 0f);
                    Vector3 ankle = new Vector3(leg.side * .135f + travel * lateral,
                        .115f + swing * .095f * gait + vault * .18f,
                        .035f + travel * forward - vault * .13f);
                    PoseLeg(leg, hip, ankle, crouch);
                }
            }
            if (visibleHands)
            {
                float motion = player.reducedMotion ? 0f : player.cameraMotion;
                float run = Mathf.InverseLerp(.6f, 1f, player.Speed01);
                Vector3 desired = new Vector3(-Mathf.Clamp(player.TurnRate, -250f, 250f) * .00007f * motion,
                    Mathf.Sin(player.GaitPhase * 2f) * .008f * gait * motion - run * .045f + vault * .10f,
                    Mathf.Cos(player.GaitPhase) * .012f * gait * motion - run * .025f + vault * .11f);
                // Retract the first-person hands near walls rather than let them protrude.
                if (Physics.SphereCast(player.viewCamera.transform.position, 0.1f,
                    player.viewCamera.transform.forward, out RaycastHit wall, 0.65f,
                    ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    float obstruction = 1f - Mathf.InverseLerp(0.12f, 0.65f, wall.distance);
                    desired += new Vector3(0f, -0.24f * obstruction, -0.2f * obstruction);
                }
                Quaternion rotation = Quaternion.Euler(vault * -12f + run * 6f, 0f,
                    Mathf.Sin(player.GaitPhase) * 1.8f * gait * motion);
                float blend = dt <= 0f ? 1f : 1f - Mathf.Exp(-12f * dt);
                handsOffset = Vector3.Lerp(handsOffset, desired, blend);
                handsRotation = Quaternion.Slerp(handsRotation, rotation, blend);
                handsRoot.localPosition = handsOffset;
                handsRoot.localRotation = handsRotation;
            }
        }

        static void PoseLeg(Leg leg, Vector3 hip, Vector3 requestedAnkle, float crouch)
        {
            const float upperLength = .425f;
            const float lowerLength = .425f;
            Vector3 delta = requestedAnkle - hip;
            float distance = Mathf.Clamp(delta.magnitude, .08f, upperLength + lowerLength - .008f);
            Vector3 direction = delta.sqrMagnitude > .00001f ? delta.normalized : Vector3.down;
            Vector3 ankle = hip + direction * distance;
            Vector3 pole = Vector3.ProjectOnPlane(Vector3.forward, direction).normalized;
            if (pole.sqrMagnitude < .01f) pole = Vector3.forward;
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float bend = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 knee = hip + direction * along + pole * bend;
            SetSegment(leg.thigh, hip, knee, .155f);
            SetSegment(leg.shin, knee, ankle, .125f);
            leg.kneePad.localPosition = knee + pole * .062f;
            leg.kneePad.localRotation = Quaternion.Euler(-crouch * 8f, 0f, 0f);
            leg.boot.localPosition = ankle + new Vector3(0f, -.045f, .07f);
            leg.boot.localRotation = Quaternion.identity;
            leg.boot.localScale = new Vector3(.155f, .13f, .29f);
        }

        static void SetSegment(Transform segment, Vector3 start, Vector3 end, float width)
        {
            Vector3 delta = end - start;
            segment.localPosition = (start + end) * .5f;
            segment.localRotation = delta.sqrMagnitude > .00001f ? Quaternion.FromToRotation(Vector3.up, delta) : Quaternion.identity;
            // Unity's built-in capsule is two units tall and one unit wide.
            segment.localScale = new Vector3(width, Mathf.Max(.01f, delta.magnitude) * .5f, width);
        }

        void OnDestroy()
        {
            if (bodyRoot != null) Destroy(bodyRoot.gameObject);
            if (handsRoot != null) Destroy(handsRoot.gameObject);
            foreach (Material material in ownedMaterials) if (material != null) Destroy(material);
        }
    }
}
