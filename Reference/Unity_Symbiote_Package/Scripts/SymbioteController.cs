using System.Collections.Generic;
using UnityEngine;

namespace SymbioteGame
{
    /// <summary>
    /// Controlador principal del Simbionte (Estilo Venom / Carrion).
    /// Permite:
    /// 1. Movimiento 360 grados por suelos, paredes y techos (Surface Raycast Snapping).
    /// 2. Deformacion procedural de su malla/shader y tentaculos de agarre en tiempo real.
    /// 3. Saltar sobre un operario SWAT ("Swat.fbx" / "Swat.glb") y poseer su cuerpo.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class SymbioteController : MonoBehaviour
    {
        [Header("Movimiento y Trepar Paredes (360°)")]
        public float moveSpeed = 7.5f;
        public float sprintMultiplier = 1.6f;
        public float surfaceStickForce = 25f;
        public float surfaceDetectionRadius = 1.6f;
        public float normalAlignSpeed = 12f;
        public LayerMask climbableLayers = ~0;

        [Header("Tentáculos Procedurales (Estilo Carrion)")]
        public int tentacleCount = 8;
        public float tentacleReach = 2.4f;
        public float tentacleStepDistance = 1.8f;
        public Material tentacleMaterial;

        [Header("Posesión de Operarios SWAT")]
        public float possessionRange = 4.5f;
        public float leapSpeed = 22f;
        public KeyCode possessKey = KeyCode.E;

        [Header("Referencias Visuales")]
        public Renderer symbioteRenderer;
        public Transform visualRoot;

        private Rigidbody rb;
        private Vector3 currentSurfaceNormal = Vector3.up;
        private SwatHostController currentHost;
        private SwatHostController targetHostLatching;

        // Estado interno de cada tentáculo procedural
        private class ProceduralTentacle
        {
            public LineRenderer line;
            public Vector3 currentAnchor;
            public Vector3 targetAnchor;
            public float lerpProgress;
            public Vector3 rayDirectionOffset;
        }

        private readonly List<ProceduralTentacle> tentacles = new List<ProceduralTentacle>();
        private MaterialPropertyBlock propBlock;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false; // Usamos gravedad dirigida hacia la pared/suelo actual
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            propBlock = new MaterialPropertyBlock();
            if (symbioteRenderer == null)
                symbioteRenderer = GetComponentInChildren<Renderer>();

            InitializeProceduralTentacles();
        }

        private void InitializeProceduralTentacles()
        {
            for (int i = 0; i < tentacleCount; i++)
            {
                GameObject tObj = new GameObject($"Symbiote_Tentacle_{i}");
                tObj.transform.SetParent(transform);
                tObj.transform.localPosition = Vector3.zero;

                LineRenderer lr = tObj.AddComponent<LineRenderer>();
                lr.positionCount = 8;
                lr.startWidth = 0.16f;
                lr.endWidth = 0.02f;
                lr.useWorldSpace = true;
                if (tentacleMaterial != null)
                    lr.material = tentacleMaterial;
                else if (symbioteRenderer != null)
                    lr.material = symbioteRenderer.sharedMaterial;

                float angle = (i / (float)tentacleCount) * Mathf.PI * 2f;
                Vector3 dirOffset = new Vector3(Mathf.Cos(angle), -0.35f, Mathf.Sin(angle)).normalized;

                tentacles.Add(new ProceduralTentacle
                {
                    line = lr,
                    currentAnchor = transform.position + dirOffset * 0.6f,
                    targetAnchor = transform.position + dirOffset * 0.6f,
                    lerpProgress = 1f,
                    rayDirectionOffset = dirOffset
                });
            }
        }

        private void Update()
        {
            // Si estamos dentro de un SWAT poseído, el seguimiento lo maneja el huésped
            if (currentHost != null)
            {
                transform.position = currentHost.GetSymbioteChestSocket().position;
                return;
            }

            // Si estamos saltando hacia un SWAT para poseerlo
            if (targetHostLatching != null)
            {
                UpdateLatchTransition();
                return;
            }

            // Detectar si el jugador presiona E para poseer un SWAT cercano
            if (Input.GetKeyDown(possessKey))
            {
                TryFindAndLatchSwat();
            }

            UpdateProceduralTentacles();
            UpdateShaderDeformation();
        }

        private void FixedUpdate()
        {
            if (currentHost != null || targetHostLatching != null)
                return;

            // 1. Escanear 360 grados alrededor del simbionte para hallar suelo, pared o techo
            Vector3 detectedNormal = SampleEnvironmentSurfaceNormal();
            currentSurfaceNormal = Vector3.Slerp(currentSurfaceNormal, detectedNormal, Time.fixedDeltaTime * normalAlignSpeed).normalized;

            // 2. Alinear orientación del simbionte a la superficie
            Quaternion targetRot = Quaternion.FromToRotation(transform.up, currentSurfaceNormal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * normalAlignSpeed);

            // 3. Calcular dirección de movimiento relativa a la cámara y proyectada sobre la pared/suelo
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Transform cam = Camera.main != null ? Camera.main.transform : transform;

            Vector3 camForward = Vector3.ProjectOnPlane(cam.forward, currentSurfaceNormal).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(cam.right, currentSurfaceNormal).normalized;
            Vector3 moveDir = (camForward * v + camRight * h).normalized;

            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);
            Vector3 desiredVelocity = moveDir * speed;

            // 4. Aplicar adhesión a la superficie (permite caminar por paredes y techos sin caer)
            rb.velocity = desiredVelocity - currentSurfaceNormal * surfaceStickForce * Time.fixedDeltaTime;
        }

        /// <summary>
        /// Lanza rayos en múltiples direcciones para promediar la normal del terreno/pared/esquina.
        /// </summary>
        private Vector3 SampleEnvironmentSurfaceNormal()
        {
            Vector3 avgNormal = Vector3.zero;
            int hits = 0;

            Vector3[] sampleDirs =
            {
                -transform.up,
                transform.forward, -transform.forward,
                transform.right, -transform.right,
                (-transform.up + transform.forward).normalized,
                (-transform.up - transform.forward).normalized,
                (-transform.up + transform.right).normalized,
                (-transform.up - transform.right).normalized,
                Vector3.down
            };

            foreach (Vector3 dir in sampleDirs)
            {
                if (Physics.Raycast(transform.position, dir, out RaycastHit hit, surfaceDetectionRadius, climbableLayers))
                {
                    float weight = 1f / (hit.distance + 0.1f);
                    avgNormal += hit.normal * weight;
                    hits++;
                }
            }

            return hits > 0 ? avgNormal.normalized : Vector3.up;
        }

        /// <summary>
        /// Actualiza los tentáculos que se agarran proceduralmente a las paredes y el suelo.
        /// </summary>
        private void UpdateProceduralTentacles()
        {
            Vector3 vel = rb.velocity;
            for (int i = 0; i < tentacles.Count; i++)
            {
                ProceduralTentacle t = tentacles[i];
                float dist = Vector3.Distance(transform.position, t.currentAnchor);

                // Si el tentáculo quedó muy atrás, buscar un nuevo agarre adelante en la pared/suelo
                if (dist > tentacleStepDistance && t.lerpProgress >= 1f)
                {
                    Vector3 worldRayDir = transform.TransformDirection(t.rayDirectionOffset) + vel.normalized * 0.55f;
                    if (Physics.Raycast(transform.position, worldRayDir.normalized, out RaycastHit hit, tentacleReach, climbableLayers))
                    {
                        t.targetAnchor = hit.point;
                        t.lerpProgress = 0f;
                    }
                    else
                    {
                        t.targetAnchor = transform.position + worldRayDir.normalized * (tentacleReach * 0.6f);
                        t.lerpProgress = 0f;
                    }
                }

                if (t.lerpProgress < 1f)
                {
                    t.lerpProgress = Mathf.Min(1f, t.lerpProgress + Time.deltaTime * 8f);
                    t.currentAnchor = Vector3.Lerp(t.currentAnchor, t.targetAnchor, t.lerpProgress);
                }

                // Dibujar curva orgánica Bézier entre el núcleo y el punto de agarre
                int pts = t.line.positionCount;
                Vector3 start = transform.position;
                Vector3 end = t.currentAnchor;
                Vector3 mid = (start + end) * 0.5f + currentSurfaceNormal * 0.25f
                              + Mathf.Sin(Time.time * 7f + i) * 0.12f * transform.right;

                for (int p = 0; p < pts; p++)
                {
                    float u = p / (float)(pts - 1);
                    Vector3 bezier = Mathf.Pow(1 - u, 2) * start + 2 * (1 - u) * u * mid + u * u * end;
                    t.line.SetPosition(p, bezier);
                }
            }
        }

        private void UpdateShaderDeformation()
        {
            if (symbioteRenderer == null) return;
            symbioteRenderer.GetPropertyBlock(propBlock);
            propBlock.SetVector("_Velocity", rb.velocity);
            propBlock.SetVector("_SurfaceNormal", currentSurfaceNormal);
            symbioteRenderer.SetPropertyBlock(propBlock);
        }

        private void TryFindAndLatchSwat()
        {
            Collider[] cols = Physics.OverlapSphere(transform.position, possessionRange);
            float bestDist = float.MaxValue;
            SwatHostController bestTarget = null;

            foreach (Collider col in cols)
            {
                SwatHostController swat = col.GetComponentInParent<SwatHostController>();
                if (swat != null && !swat.IsDead && !swat.IsPossessed)
                {
                    float d = Vector3.Distance(transform.position, swat.transform.position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestTarget = swat;
                    }
                }
            }

            if (bestTarget != null)
            {
                targetHostLatching = bestTarget;
                rb.isKinematic = true;
            }
        }

        private void UpdateLatchTransition()
        {
            Transform chest = targetHostLatching.GetSymbioteChestSocket();
            transform.position = Vector3.MoveTowards(transform.position, chest.position, leapSpeed * Time.deltaTime);

            // Estirar todos los tentáculos hacia los huesos del SWAT objetivo
            for (int i = 0; i < tentacles.Count; i++)
            {
                tentacles[i].targetAnchor = chest.position + Random.insideUnitSphere * 0.35f;
                tentacles[i].currentAnchor = Vector3.Lerp(tentacles[i].currentAnchor, tentacles[i].targetAnchor, Time.deltaTime * 15f);
                tentacles[i].line.SetPosition(0, transform.position);
                tentacles[i].line.SetPosition(tentacles[i].line.positionCount - 1, tentacles[i].currentAnchor);
            }

            if (Vector3.Distance(transform.position, chest.position) < 0.25f)
            {
                CompletePossession(targetHostLatching);
                targetHostLatching = null;
            }
        }

        private void CompletePossession(SwatHostController host)
        {
            currentHost = host;
            SetSymbioteVisibility(false);
            host.BeginPossession(this);
        }

        /// <summary>
        /// Llamado por SwatHostController cuando el jugador presiona E para salir del cuerpo
        /// o cuando el cuerpo del SWAT poseído muere.
        /// </summary>
        public void OnEjectedFromHost(Vector3 ejectPosition, Vector3 ejectDirection)
        {
            currentHost = null;
            transform.position = ejectPosition;
            rb.isKinematic = false;
            SetSymbioteVisibility(true);
            rb.velocity = ejectDirection.normalized * 12f + Vector3.up * 5f;
        }

        private void SetSymbioteVisibility(bool visible)
        {
            if (symbioteRenderer != null) symbioteRenderer.enabled = visible;
            foreach (var t in tentacles)
                t.line.enabled = visible;
        }
    }
}
