using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SymbioteRaid
{
    /// <summary>Owns the player state, possession, combat, alarms and extraction objective.</summary>
    [DefaultExecutionOrder(0)]
    public sealed class SymbioteGame : MonoBehaviour
    {
        public static SymbioteGame I;
        public SymbioteAssets Assets;
        public SymbioteMotor Blob;
        public SymbioteCamera View;
        public SwatAgent Host;
        public readonly List<SwatAgent> Agents = new List<SwatAgent>();
        public bool Paused, GameOver, Won, Transitioning;
        public float Biomass = 100f, Alert01, RifleCooldown, WhipCooldown, BurstCooldown;
        public int TerminalsActivated;
        public string Prompt = "", StatusMessage = "";
        public bool IsPossessed => Host != null;
        public Vector3 TargetPoint => Host != null ? Host.Center : (Blob != null ? Blob.transform.position : transform.position);
        const int WorldMask = 1;
        const int AttackMask = (1 << 0) | (1 << 8);
        Vector3 spawn, exit;
        Vector3[] terminals;
        Bounds[] shadows;
        bool[] activated;
        float messageTime, lastDamageTime = -100f, noiseClock;
        bool ready, releasingHost;
        SwatAgent possessionCandidate;

        void Awake() { I = this; }

        public void Configure(SymbioteAssets assets, Vector3 spawnPoint, Vector3[] panels, Vector3 exitPoint, Bounds[] shadowZones)
        {
            Assets = assets;
            spawn = spawnPoint;
            exit = exitPoint;
            terminals = panels ?? new Vector3[0];
            shadows = shadowZones ?? new Bounds[0];
            activated = new bool[terminals.Length];
            var blobObject = new GameObject("Player - free symbiote");
            blobObject.layer = 9;
            Blob = blobObject.AddComponent<SymbioteMotor>();
            Blob.Teleport(spawn, Vector3.up);
            blobObject.AddComponent<SymbioteVisual>();
            var cameraObject = new GameObject("Third-person symbiote camera");
            View = cameraObject.AddComponent<SymbioteCamera>();
            View.Configure(this);
            gameObject.AddComponent<SymbioteHUD>();
            ready = true;
            Message("Infiltra la instalación. Posee un operario con E.", 7f);
            SetPaused(false);
        }

        public SwatAgent SpawnSwat(Vector3 position, Vector3[] route, Vector3[] cover)
        {
            var obj = new GameObject("SWAT " + (Agents.Count + 1));
            obj.layer = 8;
            obj.transform.position = position;
            var agent = obj.AddComponent<SwatAgent>();
            agent.Configure(this, route, cover);
            if (!Agents.Contains(agent)) Agents.Add(agent);
            return agent;
        }

        void Update()
        {
            if (!ready) return;
            if (Input.GetKeyDown(KeyCode.Escape) && !GameOver && !Won) SetPaused(!Paused);
            if (Input.GetKeyDown(KeyCode.R)) { Restart(); return; }
            if (Paused || GameOver || Won) return;
            float dt = Time.deltaTime;
            RifleCooldown = Mathf.Max(0f, RifleCooldown - dt);
            WhipCooldown = Mathf.Max(0f, WhipCooldown - dt);
            BurstCooldown = Mathf.Max(0f, BurstCooldown - dt);
            messageTime -= dt;
            if (messageTime <= 0f) StatusMessage = "";
            Alert01 = Mathf.MoveTowards(Alert01, 0f, dt * .025f);
            if (!IsPossessed && Time.time - lastDamageTime > 5f && IsShadowed(TargetPoint))
                Biomass = Mathf.Min(100f, Biomass + dt * 2.5f);
            if (Host != null) Host.RegenerateShield(dt);
            if (Transitioning) return;
            Vector3 direction = View.MoveRight * Axis(KeyCode.D, KeyCode.A) + View.MoveForward * Axis(KeyCode.W, KeyCode.S);
            direction = Vector3.ClampMagnitude(direction, 1f);
            bool sprint = Input.GetKey(KeyCode.LeftShift);
            bool crouch = Input.GetKey(KeyCode.LeftControl);
            bool jump = Input.GetKeyDown(KeyCode.Space);
            if (Host != null)
            {
                Host.MoveAsHost(direction, sprint, jump, crouch);
                Host.SetAimDirection(View.AimRay.direction);
            }
            else Blob.SetInput(direction, sprint, crouch, jump);
            UpdatePrompt();
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (Host != null) ExitHost(false);
                else if (possessionCandidate != null) StartCoroutine(Possess(possessionCandidate));
            }
            if (Input.GetKeyDown(KeyCode.G)) InteractPanel();
            if (Host != null && Input.GetMouseButton(0) && RifleCooldown <= 0f) FireRifle();
            if ((Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Q)) && WhipCooldown <= 0f) Whip();
            if (Host != null && Input.GetKeyDown(KeyCode.F) && BurstCooldown <= 0f) Burst();
            noiseClock -= dt;
            if (direction.sqrMagnitude > .25f && sprint && noiseClock <= 0f)
            {
                EmitNoise(TargetPoint, Host != null ? .45f : .18f);
                noiseClock = .45f;
            }
            if (TerminalsActivated >= terminals.Length && terminals.Length > 0 && Host == null && Vector3.Distance(TargetPoint, exit) < .55f && !Physics.Linecast(TargetPoint, exit, WorldMask, QueryTriggerInteraction.Ignore))
                Finish(true);
            if (Host == null && !Transitioning && TerminalsActivated < terminals.Length && Agents.Count > 0)
            {
                bool availableHost = false;
                foreach (var agent in Agents) if (agent && agent.IsAlive) { availableHost = true; break; }
                if (!availableHost)
                {
                    Message("No quedan huéspedes para abrir la contención. R para reintentar.", 30f);
                    Finish(false);
                }
            }
            if (TargetPoint.y < -12f)
            {
                if (Host != null) ExitHost(true);
                Blob.Teleport(spawn, Vector3.up);
                DamagePlayer(20f);
            }
        }

        static float Axis(KeyCode positive, KeyCode negative) => (Input.GetKey(positive) ? 1f : 0f) - (Input.GetKey(negative) ? 1f : 0f);

        void UpdatePrompt()
        {
            possessionCandidate = null;
            Prompt = "";
            if (Host != null) Prompt = "E  abandonar huésped";
            else
            {
                float best = float.MaxValue;
                foreach (var agent in Agents)
                {
                    if (!agent || !agent.IsAlive || agent.IsPossessed) continue;
                    Vector3 delta = agent.Chest.position - TargetPoint;
                    float distance = delta.magnitude;
                    if (distance > 4.5f || distance < .01f) continue;
                    if (Vector3.Dot(View.AimRay.direction, delta / distance) < .15f) continue;
                    if (Physics.Linecast(TargetPoint, agent.Chest.position, WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    if (distance < best) { best = distance; possessionCandidate = agent; }
                }
                if (possessionCandidate != null) Prompt = "E  poseer operario";
            }
            for (int i = 0; i < terminals.Length; i++)
            {
                if (activated[i] || Vector3.Distance(TargetPoint, terminals[i]) > 2.2f) continue;
                Prompt = Host != null ? "G  sabotear terminal " + (i + 1) : "Necesitas un huésped para este terminal";
                break;
            }
            if (TerminalsActivated >= terminals.Length && Vector3.Distance(TargetPoint, exit) < 4f)
                Prompt = Host != null ? "Abandona el huésped con E y escapa por el conducto" : "Entra en el conducto verde para escapar";
        }

        IEnumerator Possess(SwatAgent target)
        {
            if (Transitioning || !target || !target.IsAlive) yield break;
            Transitioning = true;
            Blob.SetInput(Vector3.zero, false, false, false);
            Blob.Suspend(true);
            Vector3 start = Blob.transform.position;
            bool arrived = false, blocked = false;
            for (float t = 0f; t < .3f; t += Time.deltaTime)
            {
                if (!target || !target.IsAlive) break;
                Vector3 destination = target.Chest.position;
                Vector3 next = Vector3.Lerp(start, destination, Mathf.SmoothStep(0f, 1f, t / .3f));
                Vector3 delta = next - Blob.transform.position;
                if (delta.sqrMagnitude > .0001f && Physics.SphereCast(Blob.transform.position, .2f, delta.normalized,
                    out RaycastHit obstruction, delta.magnitude, WorldMask, QueryTriggerInteraction.Ignore))
                { blocked = true; break; }
                Blob.transform.position = next;
                arrived = true;
                yield return null;
            }
            if (blocked || !arrived || !target || !target.IsAlive || Physics.Linecast(Blob.transform.position, target.Chest.position, WorldMask, QueryTriggerInteraction.Ignore))
            {
                Blob.Suspend(false);
                Transitioning = false;
                yield break;
            }
            Host = target;
            target.TakeControl();
            Blob.gameObject.SetActive(false);
            foreach (var observer in Agents)
                if (observer != target && observer && observer.IsAlive && observer.CanSeePoint(target.Chest.position, 16f))
                { RaiseAlarm(target.Center, observer); break; }
            SymbioteVfx.Tendril(start, target.Chest.position, .35f);
            Message("Huésped tomado. Sabotea los tres terminales con G.", 5f);
            Transitioning = false;
        }

        void ExitHost(bool destroyed)
        {
            if (Host == null) return;
            var previous = Host;
            Vector3 center = previous.Center;
            Host = null; // Clear before death callbacks; abandoning a host is not a biomass reward.
            releasingHost = true;
            previous.Release(true);
            releasingHost = false;
            // The smaller blob fits inside the old capsule; verify the fallback too.
            Vector3 escape = Physics.CheckSphere(center, .26f, WorldMask, QueryTriggerInteraction.Ignore) ? spawn : center;
            Vector3[] directions = { View.MoveRight, -View.MoveRight, View.MoveForward, -View.MoveForward, Vector3.up };
            foreach (Vector3 dir in directions)
            {
                Vector3 candidate = center + dir.normalized * .9f;
                if (Physics.CheckSphere(candidate, .28f, WorldMask, QueryTriggerInteraction.Ignore)) continue;
                Vector3 path = candidate - center;
                if (Physics.SphereCast(center, .26f, path.normalized, out RaycastHit blockedExit,
                    path.magnitude, WorldMask, QueryTriggerInteraction.Ignore)) continue;
                escape = candidate;
                break;
            }
            Blob.gameObject.SetActive(true);
            Blob.Teleport(escape, Vector3.up);
            Blob.Suspend(false);
            SymbioteVfx.Burst(center, 1.5f);
            if (destroyed) Message("Huésped perdido. La biomasa sigue viva.", 4f);
            else Message("Cuerpo abandonado. Busca otro huésped o vuelve a las sombras.", 4f);
            Transitioning = false;
        }

        public void HostDied(SwatAgent host) { if (Host == host) ExitHost(true); }

        public void OnAgentKilled(SwatAgent agent)
        {
            if (releasingHost || agent == Host || agent.IsPossessed) return;
            Biomass = Mathf.Min(100f, Biomass + 9f);
        }

        void FireRifle()
        {
            RifleCooldown = .12f;
            Ray aim = View.AimRay;
            Vector3 target = aim.GetPoint(55f);
            if (Physics.Raycast(aim, out RaycastHit cameraHit, 55f, AttackMask, QueryTriggerInteraction.Ignore)) target = cameraHit.point;
            // Muzzle trace prevents firing through close cover that only the camera can see past.
            Vector3 muzzle = Host.Muzzle.position;
            Vector3 barrelPath = muzzle - Host.Chest.position;
            if (Physics.CheckSphere(muzzle, .045f, WorldMask, QueryTriggerInteraction.Ignore) ||
                (barrelPath.sqrMagnitude > .0001f && Physics.SphereCast(Host.Chest.position, .06f,
                    barrelPath.normalized, out RaycastHit barrelBlock, barrelPath.magnitude, WorldMask, QueryTriggerInteraction.Ignore)))
            {
                Message("El arma está obstruida. Apártate de la pared.", 1f);
                return;
            }
            Vector3 delta = target - muzzle;
            if (Physics.Raycast(muzzle, delta.normalized, out RaycastHit hit, delta.magnitude + .1f, AttackMask, QueryTriggerInteraction.Ignore))
            {
                target = hit.point;
                var enemy = hit.collider.GetComponentInParent<SwatAgent>();
                if (enemy && enemy != Host) enemy.ApplyDamage(22f, muzzle);
            }
            Host.FireVisual(target, true);
            EmitNoise(muzzle, 1f);
        }

        void Whip()
        {
            float cost = Host != null ? 12f : 8f;
            if (Biomass <= cost + 1f) { Message("Biomasa insuficiente", 2f); return; }
            Vector3 origin = TargetPoint;
            float range = Host != null ? 10f : 3.8f;
            SwatAgent victim = null;
            float best = -1f;
            foreach (var enemy in Agents)
            {
                if (!enemy || !enemy.IsAlive || enemy == Host) continue;
                Vector3 delta = enemy.Center - origin;
                float distance = delta.magnitude;
                if (distance > range || distance < .05f || Physics.Linecast(origin, enemy.Center, WorldMask, QueryTriggerInteraction.Ignore)) continue;
                float score = Vector3.Dot(View.AimRay.direction, delta / distance);
                if (score > .45f && score > best) { best = score; victim = enemy; }
            }
            if (victim == null) { Message("Apunta a un operario a tu alcance", 1.5f); return; }
            Biomass -= cost;
            WhipCooldown = Host != null ? 3.5f : 1.6f;
            Vector3 end = victim.Center;
            victim.ApplyDamage(Host != null ? 55f : 20f, origin, 2.2f);
            if (victim.IsAlive) victim.AddImpulse((origin - end).normalized * 6f);
            SymbioteVfx.Tendril(origin, end, .3f);
            EmitNoise(origin, .55f);
        }

        void Burst()
        {
            if (Biomass <= 31f) { Message("Necesitas más de 30 de biomasa", 2f); return; }
            Biomass -= 30f;
            BurstCooldown = 10f;
            Vector3 origin = TargetPoint;
            foreach (var enemy in Agents)
            {
                if (!enemy || !enemy.IsAlive || enemy == Host) continue;
                Vector3 delta = enemy.Center - origin;
                if (delta.magnitude > 7f || Physics.Linecast(origin, enemy.Center, WorldMask, QueryTriggerInteraction.Ignore)) continue;
                enemy.ApplyDamage(45f, origin, 2f);
                if (enemy.IsAlive) enemy.AddImpulse(delta.normalized * 11f + Vector3.up * 2f);
            }
            SymbioteVfx.Burst(origin, 7f);
            EmitNoise(origin, 1f);
        }

        void InteractPanel()
        {
            for (int i = 0; i < terminals.Length; i++)
            {
                if (activated[i] || Vector3.Distance(TargetPoint, terminals[i]) > 2.2f) continue;
                if (Host == null) { Message("El terminal requiere un huésped", 2f); return; }
                if (Physics.Linecast(Host.Center, terminals[i], WorldMask, QueryTriggerInteraction.Ignore))
                { Message("Acércate a la cara del terminal", 2f); return; }
                activated[i] = true;
                TerminalsActivated++;
                Biomass = Mathf.Min(100f, Biomass + 15f);
                Message(TerminalsActivated < terminals.Length ? "Terminal saboteado: " + TerminalsActivated + "/" + terminals.Length : "Contención desactivada. Abandona el huésped y escapa por el conducto verde.", 6f);
                EmitNoise(terminals[i], .65f);
                return;
            }
        }

        public bool IsTerminalActivated(int index) => index >= 0 && index < activated.Length && activated[index];
        public bool IsShadowed(Vector3 position)
        {
            if (shadows == null) return false;
            foreach (Bounds region in shadows) if (region.Contains(position)) return true;
            return false;
        }
        public void RaiseAlarm(Vector3 position, SwatAgent witness = null)
        {
            Alert01 = 1f;
            foreach (var agent in Agents)
                if (agent && agent.IsAlive && agent != Host && agent != witness) agent.HearNoise(position, 1f);
        }
        public void EmitNoise(Vector3 position, float loudness)
        {
            foreach (var agent in Agents) if (agent && agent.IsAlive && agent != Host) agent.HearNoise(position, loudness);
            Alert01 = Mathf.Max(Alert01, loudness * .35f);
        }
        public void DamagePlayer(float damage)
        {
            if (GameOver || Won || Transitioning) return;
            lastDamageTime = Time.time;
            if (Host != null) Host.ApplyDamage(damage, TargetPoint);
            else
            {
                Biomass = Mathf.Max(0f, Biomass - damage);
                if (Biomass <= 0f) Finish(false);
            }
        }
        public void SetPaused(bool paused)
        {
            Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }
        public void Restart() { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
        void Finish(bool win)
        {
            Won = win;
            GameOver = !win;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        void Message(string text, float duration) { StatusMessage = text; messageTime = duration; }
        void OnApplicationFocus(bool focus) { if (!focus && ready && !GameOver && !Won) SetPaused(true); }
        void OnDestroy()
        {
            if (I == this) I = null;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
