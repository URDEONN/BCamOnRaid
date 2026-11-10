using UnityEngine;

namespace SymbioteRaid
{
    /// <summary>A swept sphere motor with local adhesion rather than a fixed world-up capsule.</summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class SymbioteMotor : MonoBehaviour
    {
        public Vector3 SurfaceNormal { get; private set; } = Vector3.up;
        public Vector3 Velocity { get; private set; }
        public float Speed01 { get; private set; }
        public bool Attached { get; private set; }
        const float Radius = .25f, Skin = .014f;
        const int WorldMask = 1;
        SphereCollider sphere;
        Vector3 input, airVelocity, travelHeading = Vector3.forward;
        bool sprint, crouch, jump, suspended;
        float detachTime;
        readonly Collider[] overlaps = new Collider[16];

        void Awake()
        {
            gameObject.layer = 9;
            sphere = GetComponent<SphereCollider>();
            if (!sphere) sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = Radius;
            sphere.center = Vector3.zero;
            var body = GetComponent<Rigidbody>();
            if (!body) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.FreezeRotation;
        }

        public void SetInput(Vector3 direction, bool run, bool crawl, bool detach)
        {
            input = Vector3.ClampMagnitude(direction, 1f);
            sprint = run;
            crouch = crawl;
            jump |= detach;
        }

        public void Teleport(Vector3 position, Vector3 normal)
        {
            transform.position = position;
            SurfaceNormal = SafeNormal(normal, Vector3.up);
            travelHeading = Tangent(transform.forward, SurfaceNormal);
            transform.rotation = Quaternion.LookRotation(travelHeading, SurfaceNormal);
            airVelocity = Velocity = input = Vector3.zero;
            Speed01 = 0f;
            detachTime = 0f;
            Attached = false;
            jump = false;
            ResolveOverlap();
        }

        public void Suspend(bool value)
        {
            suspended = value;
            if (sphere) sphere.enabled = !value;
            input = airVelocity = Velocity = Vector3.zero;
            jump = false;
        }

        void Update()
        {
            var game = SymbioteGame.I;
            if (suspended || (game && (game.Paused || game.GameOver || game.Won || game.Transitioning))) return;
            // Substeps preserve movement speed at ordinary low frame rates while keeping sweeps short.
            float remaining = Mathf.Min(Time.deltaTime, .2f);
            while (remaining > .00001f)
            {
                float step = Mathf.Min(remaining, .025f);
                Tick(step);
                remaining -= step;
            }
        }

        void Tick(float dt)
        {
            Vector3 before = transform.position;
            detachTime = Mathf.Max(0f, detachTime - dt);
            Vector3 desired = Vector3.ProjectOnPlane(input, SurfaceNormal);
            if (desired.sqrMagnitude > .0001f) travelHeading = desired.normalized;
            RaycastHit support = default;
            bool contact = detachTime <= 0f && ProbeSurface(travelHeading, input.sqrMagnitude > .01f, out support);
            if (contact)
            {
                Attached = true;
                Vector3 normal = AverageNormal(support, travelHeading);
                SurfaceNormal = SafeNormal(Vector3.Slerp(SurfaceNormal, normal, 1f - Mathf.Exp(-14f * dt)), normal);
                desired = Vector3.ProjectOnPlane(input, SurfaceNormal);
                // Transporting the camera normally prevents this degeneracy; preserve heading during a sharp corner.
                if (input.sqrMagnitude > .01f && desired.sqrMagnitude < .005f)
                    desired = Tangent(travelHeading, SurfaceNormal) * input.magnitude;
                if (desired.sqrMagnitude > 1f) desired.Normalize();
                float speed = crouch ? 1.45f : sprint ? 4.5f : 2.65f;
                SweepMove(desired * speed * dt);
                // Snap only along the selected plane's normal, avoiding sideways creep from diagonal probes.
                Vector3 anchor = support.point + support.normal * (Radius + Skin);
                float correction = Vector3.Dot(anchor - transform.position, support.normal);
                SweepMove(support.normal * Mathf.Clamp(correction, -5f * dt, 5f * dt));
                airVelocity = Vector3.zero;
                if (jump)
                {
                    Attached = false;
                    detachTime = .3f;
                    airVelocity = SurfaceNormal * 4.6f + desired * 2f;
                    SweepMove(SurfaceNormal * .025f);
                }
            }
            else
            {
                Attached = false;
                Vector3 steering = Vector3.ProjectOnPlane(input, Vector3.up) * (sprint ? 3.8f : 2.8f);
                Vector3 horizontal = Vector3.ProjectOnPlane(airVelocity, Vector3.up);
                horizontal = Vector3.MoveTowards(horizontal, steering, 6f * dt);
                airVelocity = horizontal + Vector3.up * Mathf.Max(-16f, airVelocity.y - 18f * dt);
                SweepMove(airVelocity * dt);
            }
            jump = false;
            ResolveOverlap();
            Velocity = (transform.position - before) / dt;
            Speed01 = Mathf.Clamp01(Vector3.ProjectOnPlane(Velocity, SurfaceNormal).magnitude / 4.5f);
            Vector3 forward = Vector3.ProjectOnPlane(Velocity, SurfaceNormal);
            if (forward.sqrMagnitude > .01f) travelHeading = forward.normalized;
            else travelHeading = Tangent(travelHeading, SurfaceNormal);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(travelHeading, SurfaceNormal),
                1f - Mathf.Exp(-18f * dt));
        }

        bool ProbeSurface(Vector3 forward, bool moving, out RaycastHit support)
        {
            Vector3 p = transform.position;
            // A new face in the travel direction takes priority at an inside corner.
            if (moving && Physics.SphereCast(p, .18f, forward, out support, .46f, WorldMask, QueryTriggerInteraction.Ignore)
                && Vector3.Dot(support.normal, SurfaceNormal) < .65f && Vector3.Dot(forward, support.normal) < -.2f)
                return true;
            if (Physics.SphereCast(p, .18f, -SurfaceNormal, out support, .58f, WorldMask, QueryTriggerInteraction.Ignore))
                return true;
            // Feel around a convex edge from a point slightly ahead and outside the current plane.
            Vector3 edgeOrigin = p + forward * .32f + SurfaceNormal * .22f;
            Vector3 diagonal = (-SurfaceNormal - forward).normalized;
            if (moving && Physics.SphereCast(edgeOrigin, .12f, diagonal, out support, .85f, WorldMask, QueryTriggerInteraction.Ignore))
                return Vector3.Distance(p, support.point) < .7f;
            // Reattach after a jump/fall, including a wall reached while airborne.
            if (!Attached && Physics.SphereCast(p, .18f, Vector3.down, out support, .58f, WorldMask, QueryTriggerInteraction.Ignore))
                return true;
            support = default;
            return false;
        }

        Vector3 AverageNormal(RaycastHit primary, Vector3 forward)
        {
            Vector3 sum = primary.normal * 3f;
            Vector3 right = Vector3.Cross(SurfaceNormal, forward).normalized;
            if (right.sqrMagnitude < .1f) right = Tangent(Vector3.right, SurfaceNormal);
            for (int i = 0; i < 4; i++)
            {
                Vector3 offset = i < 2 ? forward * (i == 0 ? .12f : -.12f) : right * (i == 2 ? .12f : -.12f);
                if (Physics.Raycast(transform.position + offset, -primary.normal, out RaycastHit sample, .75f, WorldMask, QueryTriggerInteraction.Ignore)
                    && Vector3.Dot(sample.normal, primary.normal) > .55f)
                    sum += sample.normal;
            }
            return SafeNormal(sum, primary.normal);
        }

        void SweepMove(Vector3 delta)
        {
            for (int i = 0; i < 3 && delta.sqrMagnitude > .0000001f; i++)
            {
                float distance = delta.magnitude;
                if (!Physics.SphereCast(transform.position, Radius, delta / distance, out RaycastHit hit,
                    distance + Skin, WorldMask, QueryTriggerInteraction.Ignore))
                {
                    transform.position += delta;
                    return;
                }
                float allowed = Mathf.Max(0f, hit.distance - Skin);
                Vector3 moved = delta / distance * Mathf.Min(allowed, distance);
                transform.position += moved;
                delta = Vector3.ProjectOnPlane(delta - moved, hit.normal);
                if (!Attached && Vector3.Dot(airVelocity, hit.normal) < 0f)
                    airVelocity = Vector3.ProjectOnPlane(airVelocity, hit.normal);
            }
        }

        void ResolveOverlap()
        {
            if (!sphere || !sphere.enabled) return;
            for (int iteration = 0; iteration < 3; iteration++)
            {
                int count = Physics.OverlapSphereNonAlloc(transform.position, Radius, overlaps, WorldMask, QueryTriggerInteraction.Ignore);
                bool corrected = false;
                for (int i = 0; i < count; i++)
                {
                    Collider obstacle = overlaps[i];
                    if (!obstacle || obstacle == sphere) continue;
                    if (Physics.ComputePenetration(sphere, transform.position, transform.rotation,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation, out Vector3 direction, out float distance))
                    {
                        transform.position += direction * (distance + Skin);
                        corrected = true;
                    }
                }
                if (!corrected) break;
            }
        }

        static Vector3 SafeNormal(Vector3 vector, Vector3 fallback) => vector.sqrMagnitude > .00001f ? vector.normalized : fallback;
        static Vector3 Tangent(Vector3 direction, Vector3 normal)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(direction, normal);
            if (tangent.sqrMagnitude < .0001f) tangent = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (tangent.sqrMagnitude < .0001f) tangent = Vector3.ProjectOnPlane(Vector3.right, normal);
            return tangent.normalized;
        }
    }
}
