using UnityEngine;

namespace BodycamRaid
{
    /// <summary>Asset-free, scalable training HUD and accessibility controls.</summary>
    [DisallowMultipleComponent]
    public sealed class RaidHUD : MonoBehaviour
    {
        RaidPlayer player;
        bool showControls = true;
        float sessionStarted;
        GUIStyle label, muted, heading, small, prompt, button;
        readonly Color ink = new Color(.91f, .95f, .94f, 1);
        readonly Color quiet = new Color(.59f, .68f, .69f, 1);
        readonly Color accent = new Color(.68f, .83f, .67f, 1);

        void Awake()
        {
            player = GetComponent<RaidPlayer>();
            sessionStarted = Time.unscaledTime;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) showControls = !showControls;
        }

        void PrepareStyles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleLeft };
            label.normal.textColor = ink;
            muted = new GUIStyle(label); muted.normal.textColor = quiet;
            heading = new GUIStyle(label) { fontSize = 28, fontStyle = FontStyle.Bold };
            small = new GUIStyle(muted) { fontSize = 13 };
            prompt = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            button = new GUIStyle(GUI.skin.button) { fontSize = 17, fixedHeight = 42 };
        }

        static void Box(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void OnGUI()
        {
            if (player == null) return;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            Color previousBackground = GUI.backgroundColor;
            Color previousContent = GUI.contentColor;
            int previousDepth = GUI.depth;
            try
            {
                PrepareStyles();
                float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
                scale = Mathf.Max(.1f, scale);
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
                GUI.color = Color.white;
                GUI.depth = -10;
                float width = Screen.width / scale;
                float height = Screen.height / scale;
                float margin = 34;

                Box(new Rect(margin, margin, 4, 67), accent);
                GUI.Label(new Rect(margin + 18, margin - 5, 440, 30), "UNIDAD 07 / ENTRENAMIENTO", label);
                GUI.Label(new Rect(margin + 18, margin + 25, 440, 25), "BODYCAM   ·   PROTOTIPO 02", small);
                bool blink = Mathf.Repeat(Time.unscaledTime, 1.4f) < 1;
                if (blink) Box(new Rect(width - 278, margin + 9, 9, 9), new Color(.93f, .25f, .21f));
                GUI.Label(new Rect(width - 259, margin - 1, 70, 27), "REC", label);
                int elapsed = Mathf.FloorToInt(Time.unscaledTime - sessionStarted);
                GUI.Label(new Rect(width - 180, margin - 1, 150, 27),
                    string.Format("{0:00}:{1:00}:{2:00}", elapsed / 3600, elapsed / 60 % 60, elapsed % 60), label);
                GUI.Label(new Rect(width - 278, margin + 29, 250, 24), "SIMULACIÓN / SIN MISIÓN ACTIVA", small);

                if (!player.Paused)
                {
                    float cx = width * .5f;
                    float cy = height * .5f;
                    Box(new Rect(cx - 1.5f, cy - 1.5f, 3, 3), new Color(1, 1, 1, .7f));
                    if (!string.IsNullOrEmpty(player.InteractionPrompt))
                    {
                        Box(new Rect(cx - 240, cy + 40, 480, 38), new Color(.025f, .045f, .05f, .79f));
                        GUI.Label(new Rect(cx - 237, cy + 40, 474, 38), player.InteractionPrompt, prompt);
                    }
                }

                float bottom = height - margin;
                string state = player.IsVaulting ? "SUPERANDO OBSTÁCULO" : player.IsCrouched ? "AGACHADO" :
                    player.Speed01 > .65f ? "CARRERA" : player.Speed01 > .05f ? "EN MOVIMIENTO" : "EN POSICIÓN";
                GUI.Label(new Rect(margin, bottom - 66, 350, 25), state, label);
                Box(new Rect(margin, bottom - 30, 205, 4), new Color(.3f, .35f, .35f, .7f));
                Box(new Rect(margin, bottom - 30, 205 * Mathf.Clamp01(player.Stamina01), 4), accent);
                GUI.Label(new Rect(margin, bottom - 17, 350, 24), "RESISTENCIA", small);
                GUI.Label(new Rect(width - 376, bottom - 24, 342, 24), "TAB  controles    /    ESC  ajustes", small);

                if (showControls && !player.Paused)
                {
                    Rect panel = new Rect(width - 376, height - 437, 342, 329);
                    Box(panel, new Color(.025f, .045f, .05f, .84f));
                    Box(new Rect(panel.x, panel.y, 342, 2), accent);
                    GUI.Label(new Rect(panel.x + 18, panel.y + 13, 310, 28), "CONTROLES", label);
                    string[] rows = {
                        "W A S D      Moverte", "RATÓN        Mirar", "SHIFT        Correr",
                        "CTRL / C     Agacharte (mantener / alternar)", "Q / E        Inclinarte (mantener)",
                        "ESPACIO      Saltar / superar obstáculo", "G            Abrir / cerrar puerta",
                        "F            Linterna", "F2           Efectos de lente", "F3           Cuerpo y manos", "R            Volver al inicio"
                    };
                    for (int i = 0; i < rows.Length; i++)
                        GUI.Label(new Rect(panel.x + 18, panel.y + 47 + i * 23, 312, 23), rows[i], small);
                    GUI.Label(new Rect(margin, 124, 550, 26), "Explora el edificio y prueba el movimiento", muted);
                }
                if (player.Paused) DrawPause(width, height);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                GUI.backgroundColor = previousBackground;
                GUI.contentColor = previousContent;
                GUI.depth = previousDepth;
            }
        }

        void DrawPause(float width, float height)
        {
            Box(new Rect(0, 0, width, height), new Color(.01f, .02f, .025f, .89f));
            float x = width * .5f - 250;
            float y = height * .5f - 280;
            GUI.Label(new Rect(x, y, 500, 45), "CÁMARA EN PAUSA", heading);
            GUI.Label(new Rect(x, y + 46, 500, 29), "Ajusta la sensación antes de continuar.", muted);
            GUI.Label(new Rect(x, y + 106, 500, 27), "Sensibilidad del ratón  " + player.mouseSensitivity.ToString("0.0"), label);
            player.mouseSensitivity = GUI.HorizontalSlider(new Rect(x, y + 145, 500, 22), player.mouseSensitivity, .2f, 4);
            GUI.Label(new Rect(x, y + 180, 500, 27), "Movimiento de cámara  " + Mathf.RoundToInt(player.cameraMotion * 100) + "%", label);
            player.cameraMotion = GUI.HorizontalSlider(new Rect(x, y + 219, 500, 22), player.cameraMotion, 0, 1);
            player.reducedMotion = GUI.Toggle(new Rect(x, y + 260, 500, 30), player.reducedMotion, "  Movimiento reducido");
            GUI.Label(new Rect(x, y + 299, 500, 30), "F2 activa o desactiva los efectos de lente.", small);
            if (GUI.Button(new Rect(x, y + 352, 500, 45), "CONTINUAR", button)) player.SetPaused(false);
            if (GUI.Button(new Rect(x, y + 410, 500, 45), "VOLVER A LA POSICIÓN INICIAL", button))
            {
                player.ResetToSpawn();
                player.SetPaused(false);
            }
            if (GUI.Button(new Rect(x, y + 468, 500, 45), "SALIR", button))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            GUI.Label(new Rect(x, y + 530, 500, 28), "ESC también reanuda la simulación.", small);
        }
    }
}
