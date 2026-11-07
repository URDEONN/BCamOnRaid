using UnityEngine;

namespace BodycamRaid
{
    public sealed class RaidDoor : MonoBehaviour
    {
        public float openAngle = 95f;
        public float swingSpeed = 110f;
        public string Prompt => targetOpen ? "Cerrar puerta" : "Abrir puerta";
        bool targetOpen;
        float angle;
        Quaternion closedRotation;
        BoxCollider slab;
        void Awake()
        {
            closedRotation = transform.localRotation;
            slab = GetComponentInChildren<BoxCollider>();
        }
        public void Toggle() { targetOpen = !targetOpen; }
        void Update()
        {
            float next = Mathf.MoveTowards(angle, targetOpen ? openAngle : 0f, swingSpeed * Time.deltaTime);
            // Angular substeps also protect the player during a slow frame.
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(next - angle) / 4f));
            float safeAngle = angle;
            for (int i = 1; i <= steps; i++)
            {
                float testAngle = Mathf.Lerp(angle, next, i / (float)steps);
                Quaternion proposedLocal = closedRotation * Quaternion.Euler(0f, testAngle, 0f);
                Quaternion proposedWorld = transform.parent ? transform.parent.rotation * proposedLocal : proposedLocal;
                if (slab)
                {
                    Quaternion delta = proposedWorld * Quaternion.Inverse(transform.rotation);
                    Vector3 center = slab.transform.TransformPoint(slab.center);
                    Vector3 proposedCenter = transform.position + delta * (center - transform.position);
                    Vector3 scale = slab.transform.lossyScale;
                    Vector3 half = Vector3.Scale(slab.size * 0.5f,
                        new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                    if (Physics.CheckBox(proposedCenter, half + Vector3.one * 0.025f,
                        delta * slab.transform.rotation, 1 << 2, QueryTriggerInteraction.Ignore)) break;
                }
                safeAngle = testAngle;
            }
            angle = safeAngle;
            transform.localRotation = closedRotation * Quaternion.Euler(0f, angle, 0f);
        }
    }
}
