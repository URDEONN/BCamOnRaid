using UnityEngine;
using UnityEngine.Rendering;

namespace SymbioteRaid
{
    /// <summary>Original procedural stand-in; no imported models or animations are assumed.</summary>
    [DefaultExecutionOrder(75)]
    [RequireComponent(typeof(SymbioteMotor))]
    public sealed class SymbioteVisual : MonoBehaviour
    {
        const int Rings = 10, Sides = 16, LimbCount = 8, LimbSegments = 9, LimbSides = 6;
        sealed class Limb
        {
            public Vector3 anchor, previous, goal;
            public float step = 1f;
            public float angle;
        }
        readonly Limb[] limbs = new Limb[LimbCount];
        SymbioteMotor motor;
        Mesh core, tubes;
        Vector3[] basis, coreVertices, tubeVertices;
        Vector2[] coreUV, tubeUV;
        Material ownedMaterial;
        Transform coreTransform, tubeTransform;
        Transform importedCore;
        Transform[] deformBones;
        Vector3[] restBonePositions;

        void Awake()
        {
            motor = GetComponent<SymbioteMotor>();
            Material material = SymbioteGame.I != null && SymbioteGame.I.Assets != null ? SymbioteGame.I.Assets.slimeMaterial : null;
            if (material == null)
            {
                Shader shader = Shader.Find("SymbioteRaid/Slime") ?? Shader.Find("Universal Render Pipeline/Lit");
                ownedMaterial = new Material(shader) { name = "Procedural petroleum biomass", color = new Color(.012f, .017f, .019f) };
                if (ownedMaterial.HasProperty("_Smoothness")) ownedMaterial.SetFloat("_Smoothness", .93f);
                if (ownedMaterial.HasProperty("_Metallic")) ownedMaterial.SetFloat("_Metallic", .18f);
                material = ownedMaterial;
            }
            if (!TryImportedCore(material))
            {
                core = CreateCore();
                coreTransform = RenderMesh("Deforming biomass core", core, material);
            }
            tubes = CreateTubes();
            tubeTransform = RenderMesh("Eight surface tendrils", tubes, material);
            for (int i = 0; i < LimbCount; i++)
            {
                limbs[i] = new Limb { angle = i * Mathf.PI * 2f / LimbCount, step = 1f };
                limbs[i].anchor = FindFoot(limbs[i].angle);
                limbs[i].goal = limbs[i].previous = limbs[i].anchor;
            }
            Animate(0f);
        }


        bool TryImportedCore(Material material)
        {
            var game = SymbioteGame.I;
            GameObject prefab = game != null && game.Assets != null ? game.Assets.symbioteCoreModel : null;
            if (prefab == null) return false;
            var root = new GameObject("Supplied seven-bone symbiote core");
            root.layer = 9;
            root.transform.SetParent(transform, false);
            importedCore = root.transform;
            GameObject model = Instantiate(prefab, importedCore, false);
            model.name = "Symbiote_Core_Blob - supplied model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 9;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            { collider.enabled = false; Destroy(collider); }
            foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.detectCollisions = false; Destroy(body); }
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { Destroy(root); importedCore = null; return false; }
            bool hasBounds = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (Renderer renderer in renderers)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                var skin = renderer as SkinnedMeshRenderer;
                if (skin != null) skin.updateWhenOffscreen = true;
                Bounds world = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = world.center + Vector3.Scale(world.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    point = importedCore.InverseTransformPoint(point);
                    if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                    else bounds.Encapsulate(point);
                }
            }
            Vector3 size = bounds.size;
            if (!hasBounds || size.x < .0001f || size.y < .0001f || size.z < .0001f)
            { Destroy(root); importedCore = null; return false; }
            float horizontal = .68f / Mathf.Max(size.x, size.z);
            Vector3 scale = new Vector3(horizontal, .42f / size.y, horizontal);
            model.transform.localScale = scale;
            model.transform.localPosition = new Vector3(0f, -.012f, 0f) - Vector3.Scale(bounds.center, scale);

            string[] names = { "Deform_Bottom", "Deform_Top", "Deform_Front", "Deform_Back", "Deform_Right", "Deform_Left" };
            deformBones = new Transform[names.Length];
            restBonePositions = new Vector3[names.Length];
            Transform[] all = model.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < names.Length; i++)
            {
                foreach (Transform bone in all)
                    if (bone.name.EndsWith(names[i], System.StringComparison.OrdinalIgnoreCase))
                    { deformBones[i] = bone; restBonePositions[i] = bone.localPosition; break; }
            }
            return true;
        }

        void AnimateImportedCore(float time, float speed)
        {
            if (importedCore == null || deformBones == null) return;
            for (int i = 0; i < deformBones.Length; i++)
            {
                Transform bone = deformBones[i];
                if (bone == null) continue;
                Vector3 rest = restBonePositions[i];
                Vector3 localVelocity = bone.parent != null ? bone.parent.InverseTransformDirection(motor.Velocity) : motor.Velocity;
                Vector3 direction = localVelocity.sqrMagnitude > .001f ? localVelocity.normalized : Vector3.forward;
                float breathe = Mathf.Sin(time * 3.7f + i * .7f) * .018f;
                Vector3 next = rest * (1f + breathe);
                if (i < 2) next *= 1f - speed * .16f; // top/bottom compress toward the center
                else next += direction * (Vector3.Dot(rest, direction) * speed * .22f);
                bone.localPosition = next;
            }
        }

        Transform RenderMesh(string name, Mesh mesh, Material material)
        {
            var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            child.layer = 9;
            child.transform.SetParent(transform, false);
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            return child.transform;
        }

        Mesh CreateCore()
        {
            int count = (Rings + 1) * (Sides + 1);
            basis = new Vector3[count];
            coreVertices = new Vector3[count];
            coreUV = new Vector2[count];
            for (int r = 0; r <= Rings; r++)
            {
                float latitude = Mathf.Lerp(-Mathf.PI * .5f, Mathf.PI * .5f, r / (float)Rings);
                for (int s = 0; s <= Sides; s++)
                {
                    float longitude = s / (float)Sides * Mathf.PI * 2f;
                    int index = r * (Sides + 1) + s;
                    basis[index] = new Vector3(Mathf.Cos(latitude) * Mathf.Sin(longitude), Mathf.Sin(latitude), Mathf.Cos(latitude) * Mathf.Cos(longitude));
                    coreUV[index] = new Vector2(s / (float)Sides, r / (float)Rings);
                    coreVertices[index] = Vector3.Scale(basis[index], new Vector3(.34f, .22f, .34f));
                }
            }
            int[] triangles = new int[Rings * Sides * 6];
            int at = 0;
            for (int r = 0; r < Rings; r++)
                for (int s = 0; s < Sides; s++)
                {
                    int a = r * (Sides + 1) + s;
                    triangles[at++] = a; triangles[at++] = a + 1; triangles[at++] = a + Sides + 1;
                    triangles[at++] = a + 1; triangles[at++] = a + Sides + 2; triangles[at++] = a + Sides + 1;
                }
            var mesh = new Mesh { name = "Original deformable blob", vertices = coreVertices, uv = coreUV, triangles = triangles };
            mesh.MarkDynamic();
            mesh.RecalculateNormals();
            return mesh;
        }

        Mesh CreateTubes()
        {
            int perLimb = (LimbSegments + 1) * LimbSides;
            tubeVertices = new Vector3[LimbCount * perLimb];
            tubeUV = new Vector2[tubeVertices.Length];
            int[] triangles = new int[LimbCount * LimbSegments * LimbSides * 6];
            int at = 0;
            for (int limb = 0; limb < LimbCount; limb++)
                for (int segment = 0; segment <= LimbSegments; segment++)
                    for (int side = 0; side < LimbSides; side++)
                    {
                        int a = limb * perLimb + segment * LimbSides + side;
                        tubeUV[a] = new Vector2(side / (float)LimbSides, segment / (float)LimbSegments);
                        if (segment == LimbSegments) continue;
                        int b = limb * perLimb + segment * LimbSides + (side + 1) % LimbSides;
                        triangles[at++] = a; triangles[at++] = b; triangles[at++] = a + LimbSides;
                        triangles[at++] = b; triangles[at++] = b + LimbSides; triangles[at++] = a + LimbSides;
                    }
            var mesh = new Mesh { name = "Original stepping tentacles", vertices = tubeVertices, uv = tubeUV, triangles = triangles };
            mesh.MarkDynamic();
            return mesh;
        }

        Vector3 FindFoot(float angle)
        {
            Vector3 radial = transform.right * Mathf.Sin(angle) + transform.forward * Mathf.Cos(angle);
            Vector3 velocityLead = Vector3.ClampMagnitude(motor.Velocity, 3f) * .12f;
            Vector3 desired = transform.position + radial * .66f + velocityLead;
            Vector3 up = motor.SurfaceNormal.sqrMagnitude > .1f ? motor.SurfaceNormal.normalized : transform.up;
            if (Physics.Raycast(desired + up * .42f, -up, out RaycastHit hit, 1.2f, 1, QueryTriggerInteraction.Ignore))
                return hit.point + hit.normal * .025f;
            // Around corners, seek an adjacent wall before using a trailing air pose.
            if (Physics.Raycast(transform.position + up * .1f, radial, out hit, .95f, 1, QueryTriggerInteraction.Ignore))
                return hit.point + hit.normal * .025f;
            return desired - up * .2f;
        }

        void LateUpdate()
        {
            var game = SymbioteGame.I;
            if (game != null && (game.Paused || game.GameOver || game.Won)) return;
            Animate(Time.deltaTime);
        }

        void Animate(float dt)
        {
            float time = Time.time;
            float speed = motor.Speed01;
            Vector3 localVelocity = transform.InverseTransformDirection(motor.Velocity);
            Vector3 direction = new Vector3(localVelocity.x, 0f, localVelocity.z);
            direction = direction.sqrMagnitude > .001f ? direction.normalized : Vector3.forward;
            if (core != null)
            {
                for (int i = 0; i < basis.Length; i++)
                {
                    Vector3 b = basis[i];
                    float wave = Mathf.Sin(time * 3.7f + b.x * 7f + b.z * 5f) * .025f + Mathf.Sin(time * 5.1f + b.y * 8f - b.z * 6f) * .012f;
                    Vector3 point = Vector3.Scale(b, new Vector3(.34f, .21f - speed * .035f, .34f));
                    point += direction * (Vector3.Dot(b, direction) * .10f * speed);
                    point += b * wave;
                    point.y -= .012f;
                    coreVertices[i] = point;
                }
                core.vertices = coreVertices;
                core.RecalculateNormals();
                core.RecalculateBounds();
            }
            AnimateImportedCore(time, speed);
            for (int i = 0; i < LimbCount; i++)
            {
                Limb limb = limbs[i];
                float reach = Vector3.Distance(limb.anchor, transform.position);
                // Possession can move the hidden player far away; reset old world anchors on return.
                if (reach > 2f)
                {
                    limb.anchor = FindFoot(limb.angle);
                    limb.previous = limb.goal = limb.anchor;
                    limb.step = 1f;
                    reach = Vector3.Distance(limb.anchor, transform.position);
                }
                if (limb.step >= 1f && (reach > .94f || reach < .30f || (motor.Velocity.sqrMagnitude > .15f && Mathf.Repeat(time + i * .19f, 1.5f) < dt)))
                {
                    limb.previous = limb.anchor;
                    limb.goal = FindFoot(limb.angle);
                    limb.step = 0f;
                }
                if (limb.step < 1f)
                {
                    limb.step = Mathf.Min(1f, limb.step + dt * Mathf.Lerp(4f, 7f, speed));
                    limb.anchor = Vector3.Lerp(limb.previous, limb.goal, Mathf.SmoothStep(0f, 1f, limb.step)) + motor.SurfaceNormal * (Mathf.Sin(limb.step * Mathf.PI) * .11f);
                }
                Vector3 radial = new Vector3(Mathf.Sin(limb.angle), 0f, Mathf.Cos(limb.angle));
                Vector3 start = radial * .19f + Vector3.up * -.015f;
                Vector3 end = transform.InverseTransformPoint(limb.anchor);
                Vector3 controlA = start + radial * .22f + Vector3.up * .12f;
                Vector3 controlB = Vector3.Lerp(start, end, .72f) + Vector3.up * .13f;
                int offset = i * (LimbSegments + 1) * LimbSides;
                for (int segment = 0; segment <= LimbSegments; segment++)
                {
                    float t = segment / (float)LimbSegments;
                    Vector3 point = Bezier(start, controlA, controlB, end, t);
                    Vector3 tangent = BezierTangent(start, controlA, controlB, end, t);
                    tangent = tangent.sqrMagnitude < .0001f ? radial : tangent.normalized;
                    Vector3 normal = Vector3.Cross(tangent, Vector3.up);
                    if (normal.sqrMagnitude < .0001f) normal = Vector3.Cross(tangent, Vector3.right);
                    normal.Normalize();
                    Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
                    float radius = Mathf.Lerp(.058f, .009f, t) * (1f + .08f * Mathf.Sin(time * 4f + t * 12f + i));
                    for (int side = 0; side < LimbSides; side++)
                    {
                        float a = side / (float)LimbSides * Mathf.PI * 2f;
                        tubeVertices[offset + segment * LimbSides + side] = point + (normal * Mathf.Cos(a) + binormal * Mathf.Sin(a)) * radius;
                    }
                }
            }
            tubes.vertices = tubeVertices;
            tubes.RecalculateNormals();
            tubes.RecalculateBounds();
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float s = 1f - t;
            return s * s * s * a + 3f * s * s * t * b + 3f * s * t * t * c + t * t * t * d;
        }
        static Vector3 BezierTangent(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float s = 1f - t;
            return 3f * s * s * (b - a) + 6f * s * t * (c - b) + 3f * t * t * (d - c);
        }
        void OnDestroy()
        {
            if (core != null) Destroy(core);
            if (tubes != null) Destroy(tubes);
            if (ownedMaterial != null) Destroy(ownedMaterial);
        }
    }
}
