using UnityEngine;
using UnityEngine.Rendering;

namespace SymbioteRaid
{
    /// <summary>Short-lived original combat cues, without imported effects.</summary>
    public static class SymbioteVfx
    {
        public static void Beam(Vector3 start, Vector3 end, Color color, float width = .035f, float duration = .1f)
        {
            LineRenderer line = NewLine("Energy tracer", color, width, duration);
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        public static void Tendril(Vector3 start, Vector3 end, float duration = .2f)
        {
            LineRenderer line = NewLine("Striking biomass tendril", new Color(.12f, .30f, .25f), .105f, duration);
            const int samples = 18;
            line.positionCount = samples;
            Vector3 path = end - start;
            Vector3 side = Vector3.Cross(path.normalized, Vector3.up);
            if (side.sqrMagnitude < .01f) side = Vector3.right;
            side.Normalize();
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples - 1);
                Vector3 bow = (Vector3.up * .35f + side * Mathf.Sin(t * Mathf.PI * 3f) * .13f) * Mathf.Sin(t * Mathf.PI);
                line.SetPosition(i, Vector3.Lerp(start, end, t) + bow);
            }
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.8f, .7f), new Keyframe(1f, .15f));
        }

        public static void Burst(Vector3 position, float radius)
        {
            const int spikes = 14;
            for (int i = 0; i < spikes; i++)
            {
                float a = i / (float)spikes * Mathf.PI * 2f;
                Vector3 direction = new Vector3(Mathf.Sin(a), (i % 3) * .12f, Mathf.Cos(a)).normalized;
                Vector3 end = position + direction * radius;
                if (Physics.Raycast(position, direction, out RaycastHit hit, radius, 1, QueryTriggerInteraction.Ignore)) end = hit.point;
                LineRenderer line = NewLine("Radial biomass spike", new Color(.14f, .40f, .31f), .12f, .32f);
                line.positionCount = 3;
                line.SetPosition(0, position);
                line.SetPosition(1, Vector3.Lerp(position, end, .45f) + Vector3.up * .25f);
                line.SetPosition(2, end);
                line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.7f, .55f), new Keyframe(1f, .03f));
            }
        }

        static LineRenderer NewLine(string name, Color color, float width, float duration)
        {
            var go = new GameObject(name, typeof(LineRenderer), typeof(SymbioteEffectLifetime));
            go.layer = 2;
            var line = go.GetComponent<LineRenderer>();
            Material template = SymbioteGame.I != null && SymbioteGame.I.Assets != null ? SymbioteGame.I.Assets.accentMaterial : null;
            // A saved scene material keeps this shader available in standalone builds.
            Material material;
            if (template != null) material = new Material(template);
            else material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            material.name = "Owned transient biomass effect";
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.5f);
            }
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.startColor = line.endColor = Color.white;
            line.widthMultiplier = width;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            go.GetComponent<SymbioteEffectLifetime>().Configure(material, Mathf.Max(.025f, duration));
            return line;
        }
    }

    public sealed class SymbioteEffectLifetime : MonoBehaviour
    {
        Material ownedMaterial;
        float remaining;
        public void Configure(Material material, float duration) { ownedMaterial = material; remaining = duration; }
        void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f) Destroy(gameObject);
        }
        void OnDestroy() { if (ownedMaterial != null) Destroy(ownedMaterial); }
    }
}
