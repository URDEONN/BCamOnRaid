using UnityEngine;

namespace SymbioteRaid
{
    /// <summary>Scalable original UI. Values come from the live session, never simulated counters.</summary>
    public sealed class SymbioteHUD : MonoBehaviour
    {
        bool controls = true;
        GUIStyle title, label, muted, small, centered, button;
        readonly Color acid = new Color(.68f, .94f, .43f);
        readonly Color ink = new Color(.9f, .96f, .94f);
        readonly Color quiet = new Color(.53f, .67f, .67f);
        readonly Color panel = new Color(.018f, .045f, .05f, .89f);
        readonly Color danger = new Color(.96f, .35f, .23f);
        static readonly string[] Keys = {
            "WASD / RATÓN    Moverse / mirar", "SHIFT / CTRL     Sprint / agacharse",
            "ESPACIO            Salto / soltarse", "E                         Poseer / abandonar",
            "G                        Activar relé", "CLIC IZQUIERDO   Rifle del huésped",
            "CLIC DERECHO / Q   Látigo", "F                         Explosión radial",
            "RUEDA                 Distancia de cámara", "TAB / ESC            Ayuda / pausa"
        };

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) controls = !controls;
        }

        void Styles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleLeft };
            label.normal.textColor = ink;
            title = new GUIStyle(label) { fontSize = 29, fontStyle = FontStyle.Bold };
            muted = new GUIStyle(label); muted.normal.textColor = quiet;
            small = new GUIStyle(muted) { fontSize = 13 };
            centered = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { fontSize = 17 };
        }

        static void RectFill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void Bar(float x, float y, float width, string caption, float value, float maximum, Color color)
        {
            GUI.Label(new Rect(x, y, width, 24), caption, small);
            RectFill(new Rect(x, y + 29, width, 6), new Color(.22f, .31f, .31f, .9f));
            RectFill(new Rect(x, y + 29, width * Mathf.Clamp01(value / maximum), 6), color);
            GUI.Label(new Rect(x, y + 40, width, 25), Mathf.CeilToInt(value) + " / " + Mathf.CeilToInt(maximum), label);
        }

        void Cooldown(float x, float y, string caption, float remaining, float duration, bool available)
        {
            RectFill(new Rect(x, y, 155, 61), panel);
            float fill = available ? 1f - Mathf.Clamp01(remaining / duration) : 0f;
            RectFill(new Rect(x, y + 57, 155 * fill, 4), available ? acid : quiet);
            GUI.Label(new Rect(x + 10, y + 5, 140, 24), caption, small);
            GUI.Label(new Rect(x + 10, y + 28, 140, 26), !available ? "REQUIERE HUÉSPED" : remaining <= 0 ? "LISTO" : remaining.ToString("0.0") + " s", remaining <= 0 && available ? label : muted);
        }

        void OnGUI()
        {
            var game = SymbioteGame.I;
            if (game == null) return;
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            int oldDepth = GUI.depth;
            try
            {
                Styles();
                float scale = Mathf.Max(.1f, Mathf.Min(Screen.width / 1440f, Screen.height / 900f));
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
                GUI.color = Color.white;
                GUI.depth = -10;
                float w = Screen.width / scale;
                float h = Screen.height / scale;
                const float margin = 32;
                RectFill(new Rect(margin, margin, 4, 71), acid);
                GUI.Label(new Rect(margin + 18, margin - 5, 600, 40), "UMBRA / SIMBIOSIS", title);
                GUI.Label(new Rect(margin + 18, margin + 36, 600, 25), "CONTENCIÓN ROTA   ·   PROTOTIPO", small);
                string form = game.Transitioning ? "VINCULANDO TEJIDO" : game.IsPossessed ? "HUÉSPED TÁCTICO" : "BIOMASA LIBRE";
                GUI.Label(new Rect(w - 315, margin - 2, 280, 30), form, label);
                string concealment = game.IsShadowed(game.TargetPoint) ? "EN SOMBRAS" : "ZONA EXPUESTA";
                GUI.Label(new Rect(w - 315, margin + 31, 280, 25), concealment, small);
                float alert = Mathf.Clamp01(game.Alert01);
                RectFill(new Rect(w - 315, margin + 69, 280, 4), new Color(.2f, .3f, .3f));
                RectFill(new Rect(w - 315, margin + 69, 280 * alert, 4), Color.Lerp(acid, danger, alert));
                GUI.Label(new Rect(w - 315, margin + 81, 280, 23), alert > .6f ? "ESCUADRÓN EN ALERTA" : alert > .15f ? "ACTIVIDAD SOSPECHOSA" : "ESCUADRÓN EN PATRULLA", small);

                RectFill(new Rect(margin, 135, 410, 84), panel);
                GUI.Label(new Rect(margin + 14, 143, 382, 27), "RELÉS DE SEGURIDAD    " + game.TerminalsActivated + " / 3", label);
                GUI.Label(new Rect(margin + 14, 177, 382, 30), game.TerminalsActivated >= 3 ? "Abandona el huésped y usa la extracción." : "Usa un huésped para activar los tres relés.", small);
                if (!game.Paused && !game.GameOver && !game.Won)
                {
                    DrawMarkers(game, scale, w, h);
                    if (game.IsPossessed)
                    {
                        float cx = w * .5f, cy = h * .5f;
                        RectFill(new Rect(cx - 9, cy - 1, 5, 2), ink);
                        RectFill(new Rect(cx + 4, cy - 1, 5, 2), ink);
                        RectFill(new Rect(cx - 1, cy - 9, 2, 5), ink);
                        RectFill(new Rect(cx - 1, cy + 4, 2, 5), ink);
                    }
                    if (!string.IsNullOrEmpty(game.Prompt))
                    {
                        RectFill(new Rect(w * .5f - 290, h * .61f, 580, 47), panel);
                        GUI.Label(new Rect(w * .5f - 280, h * .61f, 560, 47), game.Prompt, centered);
                    }
                }
                if (!string.IsNullOrEmpty(game.StatusMessage))
                    GUI.Label(new Rect(w * .5f - 380, h - 208, 760, 45), game.StatusMessage, centered);

                RectFill(new Rect(margin, h - 171, 334, 139), panel);
                Bar(margin + 16, h - 160, 302, "BIOMASA", game.Biomass, 100, acid);
                if (game.IsPossessed && game.Host != null)
                {
                    GUI.Label(new Rect(margin + 16, h - 91, 290, 23), "HUÉSPED  " + Mathf.CeilToInt(game.Host.Health) + " / 100", small);
                    RectFill(new Rect(margin + 16, h - 61, 142, 5), new Color(.2f, .3f, .3f));
                    RectFill(new Rect(margin + 16, h - 61, 142 * Mathf.Clamp01(game.Host.Health / 100), 5), ink);
                    RectFill(new Rect(margin + 176, h - 61, 142, 5), new Color(.2f, .3f, .3f));
                    RectFill(new Rect(margin + 176, h - 61, 142 * Mathf.Clamp01(game.Host.Shield / 60), 5), new Color(.3f, .72f, .92f));
                    GUI.Label(new Rect(margin + 176, h - 91, 142, 23), "ESCUDO " + Mathf.CeilToInt(game.Host.Shield), small);
                }
                else GUI.Label(new Rect(margin + 16, h - 85, 302, 34), "E  busca un operario para poseerlo", small);
                Cooldown(w * .5f - 164, h - 93, "Q / RMB   LÁTIGO", game.WhipCooldown, game.IsPossessed ? 3.5f : 1.6f, true);
                Cooldown(w * .5f + 9, h - 93, "F   EXPLOSIÓN", game.BurstCooldown, 10, game.IsPossessed);
                GUI.Label(new Rect(w - 345, h - 55, 310, 25), "TAB  controles   /   ESC  pausa", small);
                if (controls && !game.Paused && !game.GameOver && !game.Won)
                {
                    float x = w - 367;
                    float y = h - 420;
                    RectFill(new Rect(x, y, 335, 327), panel);
                    RectFill(new Rect(x, y, 335, 2), acid);
                    GUI.Label(new Rect(x + 15, y + 12, 310, 30), "CONTROLES", label);
                    for (int i = 0; i < Keys.Length; i++) GUI.Label(new Rect(x + 15, y + 48 + i * 25, 310, 25), Keys[i], small);
                }
                if (game.Paused || game.GameOver || game.Won) Overlay(game, w, h);
            }
            finally { GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.depth = oldDepth; }
        }

        void DrawMarkers(SymbioteGame game, float scale, float width, float height)
        {
            if (game.View == null || game.View.Camera == null) return;
            for (int i = 0; i < SymbioteFacility.TerminalPoints.Length; i++)
            {
                if (game.IsTerminalActivated(i)) continue;
                Vector3 point = SymbioteFacility.TerminalPoints[i];
                Marker(game.View.Camera, point + Vector3.up * .6f, "RELÉ " + (char)('A' + i), scale, width, height);
            }
            if (game.TerminalsActivated >= 3)
                Marker(game.View.Camera, SymbioteFacility.ExitPoint + Vector3.up * .7f, "EXTRACCIÓN", scale, width, height);
        }

        void Marker(Camera camera, Vector3 world, string text, float scale, float width, float height)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z < .2f) return;
            float x = screen.x / scale, y = (Screen.height - screen.y) / scale;
            if (x < 70 || x > width - 70 || y < 235 || y > height - 235) return;
            Color tint = new Color(acid.r, acid.g, acid.b, .82f);
            RectFill(new Rect(x - 3, y - 3, 6, 6), tint);
            GUI.Label(new Rect(x - 70, y + 9, 140, 25), text, centered);
        }

        void Overlay(SymbioteGame game, float width, float height)
        {
            RectFill(new Rect(0, 0, width, height), new Color(.012f, .028f, .034f, .94f));
            float x = width * .5f - 260;
            float y = height * .5f - 180;
            GUI.Label(new Rect(x, y, 520, 48), game.Won ? "CONTENCIÓN SUPERADA" : game.GameOver ? "BIOMASA PERDIDA" : "SIMBIOSIS EN PAUSA", title);
            GUI.Label(new Rect(x, y + 57, 520, 50), game.Won ? "Has recuperado el control y escapado de la instalación." : game.GameOver ? "La biomasa se agotó. Prueba otra ruta o cambia de huésped." : "ESC continúa. TAB oculta los controles durante la partida.", centered);
            float buttonY = y + 140;
            if (!game.GameOver && !game.Won)
            {
                if (GUI.Button(new Rect(x, buttonY, 520, 47), "CONTINUAR", button)) game.SetPaused(false);
                buttonY += 61;
            }
            if (GUI.Button(new Rect(x, buttonY, 520, 47), "REINICIAR OPERACIÓN", button)) game.Restart();
            if (GUI.Button(new Rect(x, buttonY + 61, 520, 47), "SALIR", button))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            GUI.Label(new Rect(x, buttonY + 131, 520, 48), "R también reinicia la operación.", centered);
        }
    }
}
