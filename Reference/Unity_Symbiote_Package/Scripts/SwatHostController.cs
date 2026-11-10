using System.Collections;
using UnityEngine;

namespace SymbioteGame
{
    /// <summary>
    /// Controlador dual para el Operario SWAT ("Swat.fbx" / "Swat.glb" / "Swat_Guy.glb"):
    /// - Modo IA: Patrulla y dispara al simbionte cuando lo detecta.
    /// - Modo POSEÍDO: El simbionte toma control de su cuerpo, infla su malla con el shader
    ///   "Custom/SwatSymbioteInfection", aumenta su velocidad/blindaje y desbloquea:
    ///   * Click Izquierdo: Disparo táctico potenciado con biomasa.
    ///   * Click Derecho / Q: Látigo de tentáculo simbiótico que empalar/atrae otros SWAT.
    ///   * Tecla F: Explosión radial de tentáculos desde el pecho ("mixamorig:Spine2").
    ///   * Tecla E: Abandonar el cuerpo del SWAT y volver a forma de masa simbiótica.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SwatHostController : MonoBehaviour
    {
        [Header("Estadísticas Base vs Poseído")]
        public float maxHealth = 100f;
        public float aiWalkSpeed = 2.8f;
        public float possessedMoveSpeed = 6.5f;
        public float possessedSprintSpeed = 10.5f;
        public float possessedJumpForce = 8.5f;

        [Header("Habilidades Especiales del SWAT Poseído")]
        public float tendrilWhipRange = 14f;
        public float tendrilWhipDamage = 65f;
        public float radialSpikesRadius = 7f;
        public float radialSpikesDamage = 85f;
        public KeyCode whipAbilityKey = KeyCode.Q;
        public KeyCode spikesAbilityKey = KeyCode.F;
        public KeyCode ejectKey = KeyCode.E;

        [Header("Referencias del Rig Mixamo (Auto-detectadas si están vacías)")]
        public Animator animator;
        public Transform spineBone;     // mixamorig:Spine2
        public Transform rightHandBone; // mixamorig:RightHand
        public Renderer swatSkinnedRenderer;
        public LineRenderer symbioticWhipRenderer;

        public bool IsPossessed { get; private set; }
        public bool IsDead { get; private set; }

        private float currentHealth;
        private float possessionShaderAmount;
        private CharacterController charController;
        private SymbioteController activeSymbiote;
        private float verticalVelocity;
        private MaterialPropertyBlock propBlock;

        private void Awake()
        {
            currentHealth = maxHealth;
            charController = GetComponent<CharacterController>();
            propBlock = new MaterialPropertyBlock();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (swatSkinnedRenderer == null)
                swatSkinnedRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

            // Auto-buscar los huesos estándar del rig Mixamo descargado
            if (spineBone == null)
                spineBone = FindBoneRecursive(transform, "mixamorig:Spine2") ?? transform;
            if (rightHandBone == null)
                rightHandBone = FindBoneRecursive(transform, "mixamorig:RightHand") ?? spineBone;
        }

        private Transform FindBoneRecursive(Transform parent, string boneName)
        {
            if (parent.name == boneName || parent.name.EndsWith(boneName))
                return parent;
            foreach (Transform child in parent)
            {
                Transform found = FindBoneRecursive(child, boneName);
                if (found != null) return found;
            }
            return null;
        }

        public Transform GetSymbioteChestSocket()
        {
            return spineBone != null ? spineBone : transform;
        }

        private void Update()
        {
            if (IsDead) return;

            if (IsPossessed)
            {
                UpdatePossessionVisuals(1f);
                UpdatePossessedPlayerControl();
            }
            else
            {
                UpdatePossessionVisuals(0f);
                UpdateSwatAI();
            }
        }

        /// <summary>
        /// Inicia la posesión simbiótica sobre este operario SWAT.
        /// </summary>
        public void BeginPossession(SymbioteController symbiote)
        {
            activeSymbiote = symbiote;
            IsPossessed = true;
            currentHealth = maxHealth * 1.5f; // +50% blindaje simbiótico al poseer
        }

        private void UpdatePossessionVisuals(float targetAmount)
        {
            possessionShaderAmount = Mathf.MoveTowards(possessionShaderAmount, targetAmount, Time.deltaTime * 2.5f);
            if (swatSkinnedRenderer != null)
            {
                swatSkinnedRenderer.GetPropertyBlock(propBlock);
                propBlock.SetFloat("_PossessionAmount", possessionShaderAmount);
                swatSkinnedRenderer.SetPropertyBlock(propBlock);
            }
        }

        private void UpdatePossessedPlayerControl()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Transform cam = Camera.main != null ? Camera.main.transform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            Vector3 moveDir = (forward * v + right * h).normalized;

            bool isSprinting = Input.GetKey(KeyCode.LeftShift);
            float speed = isSprinting ? possessedSprintSpeed : possessedMoveSpeed;

            if (moveDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 14f);
            }

            if (charController.isGrounded)
            {
                verticalVelocity = -1f;
                if (Input.GetButtonDown("Jump"))
                    verticalVelocity = possessedJumpForce;
            }
            else
            {
                verticalVelocity += Physics.gravity.y * Time.deltaTime;
            }

            Vector3 velocity = moveDir * speed + Vector3.up * verticalVelocity;
            charController.Move(velocity * Time.deltaTime);

            // Actualizar parámetros de animación del rig Mixamo
            if (animator != null)
            {
                animator.SetFloat("Speed", moveDir.magnitude * speed);
                animator.SetBool("IsFiring", Input.GetMouseButton(0));
            }

            // Habilidad 1: Látigo de Tentáculo Simbiótico (Click Derecho o Q)
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(whipAbilityKey))
            {
                StartCoroutine(ExecuteSymbioticTendrilWhip());
            }

            // Habilidad 2: Explosión Radial de Púas Simbióticas (Tecla F)
            if (Input.GetKeyDown(spikesAbilityKey))
            {
                ExecuteRadialBiomassBurst();
            }

            // Abandonar huésped voluntariamente (Tecla E)
            if (Input.GetKeyDown(ejectKey))
            {
                EjectSymbiote();
            }
        }

        private IEnumerator ExecuteSymbioticTendrilWhip()
        {
            Vector3 origin = rightHandBone.position;
            Vector3 dir = transform.forward;
            Vector3 hitPoint = origin + dir * tendrilWhipRange;

            if (Physics.SphereCast(origin, 0.5f, dir, out RaycastHit hit, tendrilWhipRange))
            {
                hitPoint = hit.point;
                SwatHostController enemySwat = hit.collider.GetComponentInParent<SwatHostController>();
                if (enemySwat != null && enemySwat != this)
                {
                    enemySwat.TakeDamage(tendrilWhipDamage);
                }
            }

            if (symbioticWhipRenderer != null)
            {
                symbioticWhipRenderer.enabled = true;
                symbioticWhipRenderer.positionCount = 2;
                symbioticWhipRenderer.SetPosition(0, origin);
                symbioticWhipRenderer.SetPosition(1, hitPoint);
                yield return new WaitForSeconds(0.18f);
                symbioticWhipRenderer.enabled = false;
            }
        }

        private void ExecuteRadialBiomassBurst()
        {
            Collider[] hits = Physics.OverlapSphere(spineBone.position, radialSpikesRadius);
            foreach (Collider col in hits)
            {
                SwatHostController otherSwat = col.GetComponentInParent<SwatHostController>();
                if (otherSwat != null && otherSwat != this)
                {
                    otherSwat.TakeDamage(radialSpikesDamage);
                }
            }
        }

        public void EjectSymbiote()
        {
            if (!IsPossessed || activeSymbiote == null) return;
            IsPossessed = false;
            Vector3 ejectPos = spineBone.position + Vector3.up * 0.6f;
            activeSymbiote.OnEjectedFromHost(ejectPos, transform.forward);
            activeSymbiote = null;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;
            currentHealth -= amount;
            if (currentHealth <= 0f)
            {
                if (IsPossessed)
                {
                    // Si matan el cuerpo del huésped, el simbionte sale expulsado vivo
                    EjectSymbiote();
                }
                IsDead = true;
                if (animator != null) animator.SetTrigger("Die");
            }
        }

        private void UpdateSwatAI()
        {
            // Comportamiento IA básico cuando no está poseído
            if (charController.isGrounded)
                verticalVelocity = -1f;
            else
                verticalVelocity += Physics.gravity.y * Time.deltaTime;

            charController.Move(Vector3.up * (verticalVelocity * Time.deltaTime));
        }
    }
}
