using UnityEngine;
using UnityEngine.Rendering;

namespace SymbioteRaid
{
    /// <summary>Original containment facility. Geometry is assembled once when entering Play.</summary>
    public sealed class SymbioteFacility : MonoBehaviour
    {
        public SymbioteAssets assets;
        public static readonly Vector3[] TerminalPoints = {
            new Vector3(-14.8f, 1.05f, 5.65f),
            new Vector3(14.8f, 1.05f, 17.65f),
            new Vector3(0f, 1.05f, 26.4f)
        };
        public static readonly Vector3 ExitPoint = new Vector3(-18f, .32f, 27.2f);
        Transform generated;
        Material wall, floor, metal, accent, warm, lightStrip;
        bool ownsFallback;

        void Awake()
        {
            generated = new GameObject("Containment site / generated").transform;
            generated.SetParent(transform, false);
            wall = assets != null ? assets.wallMaterial : null;
            floor = assets != null ? assets.floorMaterial : null;
            metal = assets != null ? assets.metalMaterial : null;
            accent = assets != null ? assets.accentMaterial : null;
            if (!wall || !floor || !metal || !accent)
            {
                ownsFallback = true;
                wall = Material("Concrete", new Color(.22f, .27f, .29f));
                floor = Material("Floor", new Color(.11f, .15f, .17f));
                metal = Material("Metal", new Color(.055f, .075f, .085f), .65f);
                accent = Material("Containment green", new Color(.37f, .75f, .30f));
            }
            warm = Material("Hazard amber", new Color(.9f, .43f, .12f));
            lightStrip = Material("Cool light", new Color(.31f, .67f, .72f), .1f);
            if (lightStrip.HasProperty("_EmissionColor"))
            {
                lightStrip.EnableKeyword("_EMISSION");
                lightStrip.SetColor("_EmissionColor", new Color(.18f, .55f, .62f) * 2f);
            }
            BuildShell();
            BuildRooms();
            BuildVents();
            BuildProps();
            BuildObjectives();
            BuildLights();
            Physics.SyncTransforms();
            var gameObjectRoot = new GameObject("Symbiote session");
            gameObjectRoot.transform.SetParent(generated, false);
            var game = gameObjectRoot.AddComponent<SymbioteGame>();
            game.Configure(assets, new Vector3(-15.4f, .32f, -9.2f), TerminalPoints, ExitPoint,
                new[] {
                    new Bounds(new Vector3(-16, 2.4f, -8), new Vector3(7, 4.8f, 10)),
                    new Bounds(new Vector3(-18, 2.4f, 10), new Vector3(3, 4.8f, 36)),
                    new Bounds(new Vector3(17, 2.4f, 10), new Vector3(5, 4.8f, 7)),
                    new Bounds(new Vector3(-9, 2.4f, 21), new Vector3(9, 4.8f, 5))
                });
            SpawnGuards(game);
        }

        Material Material(string title, Color color, float metallic = 0f)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = title, color = color };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .31f);
            return material;
        }

        GameObject Box(string title, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = title;
            go.layer = 0;
            go.transform.SetParent(generated, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid)
            {
                var collider = go.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
            }
            return go;
        }

        void BuildShell()
        {
            Box("Continuous floor", new Vector3(0, -.2f, 8), new Vector3(40.4f, .4f, 44.4f), floor);
            Box("Climbable west exterior", new Vector3(-20, 2.4f, 8), new Vector3(.3f, 4.8f, 44), wall);
            Box("Climbable east exterior", new Vector3(20, 2.4f, 8), new Vector3(.3f, 4.8f, 44), wall);
            Box("South exterior", new Vector3(0, 2.4f, -14), new Vector3(40, 4.8f, .3f), wall);
            Box("North exterior", new Vector3(0, 2.4f, 30), new Vector3(40, 4.8f, .3f), wall);
            Box("Continuous climbable ceiling", new Vector3(0, 4.9f, 8), new Vector3(40.4f, .2f, 44.4f), metal);
            for (int z = -10; z < 30; z += 5)
            {
                Box("Floor seam", new Vector3(0, .003f, z), new Vector3(39.5f, .006f, .025f), metal, false);
                Box("Central ceiling rib", new Vector3(0, 4.63f, z), new Vector3(7.2f, .18f, .18f), metal);
            }
            for (int z = -11; z < 29; z += 3)
                Box("Guide lane dash", new Vector3(0, .008f, z), new Vector3(.11f, .012f, .8f), accent, false);
        }

        void CorridorWall(float x, float from, float to)
        {
            Box("Corridor partition", new Vector3(x, 2.4f, (from + to) * .5f), new Vector3(.22f, 4.8f, to - from), wall);
            Box("Partition lower band", new Vector3(x - Mathf.Sign(x) * .12f, .35f, (from + to) * .5f), new Vector3(.025f, .17f, to - from), metal, false);
        }

        void BuildRooms()
        {
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 3.8f;
                CorridorWall(x, -4f, 2.6f);
                CorridorWall(x, 5.4f, 15.6f);
                CorridorWall(x, 18.4f, 30f);
                foreach (float doorZ in new[] { 4f, 17f })
                {
                    Box("Open tactical portal lintel", new Vector3(x, 3.75f, doorZ), new Vector3(.28f, 2.1f, 2.8f), metal);
                    Box("Portal cool marker", new Vector3(x - side * .15f, 2.6f, doorZ), new Vector3(.035f, .07f, 2.7f), lightStrip, false);
                }
                // West end remains open to the low ventilation lane at x=-18.
                float outer = side < 0 ? -17.15f : 19.85f;
                float inner = side * 11.6f;
                float mid = (outer + inner) * .5f;
                Box("Room divider outer", new Vector3(mid, 2.4f, 11), new Vector3(Mathf.Abs(outer - inner), 4.8f, .22f), wall);
                float innerEdge = side * 8.4f;
                float corridorEdge = side * 3.9f;
                Box("Room divider inner", new Vector3((innerEdge + corridorEdge) * .5f, 2.4f, 11), new Vector3(Mathf.Abs(innerEdge - corridorEdge), 4.8f, .22f), wall);
                Box("Internal portal lintel", new Vector3(side * 10f, 3.75f, 11), new Vector3(3.2f, 2.1f, .28f), metal);
            }
            foreach (Vector3 p in new[] { new Vector3(-8, 2.4f, 6), new Vector3(8, 2.4f, 18) })
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "Climbable column";
                go.transform.SetParent(generated, false);
                go.transform.localPosition = p;
                go.transform.localScale = new Vector3(1.05f, 2.4f, 1.05f);
                go.GetComponent<Renderer>().sharedMaterial = metal;
            }
        }

        void BuildVents()
        {
            // Width 1.32m, clear height .76m: blob only, with open ends.
            Box("West vent inner side", new Vector3(-17.28f, .42f, 9), new Vector3(.12f, .84f, 37.6f), metal);
            Box("West vent outer side", new Vector3(-18.72f, .42f, 9), new Vector3(.12f, .84f, 37.6f), metal);
            Box("West vent roof", new Vector3(-18, .82f, 9), new Vector3(1.56f, .12f, 37.6f), metal);
            foreach (float z in new[] { -9.8f, 27.8f })
            {
                Box("Vent opening green header", new Vector3(-18, .82f, z), new Vector3(1.55f, .10f, .13f), accent, false);
                foreach (float x in new[] { -18.72f, -17.28f })
                    Box("Vent opening side marker", new Vector3(x, .4f, z), new Vector3(.1f, .8f, .13f), accent, false);
            }
            // A low maintenance bridge gives the blob an elevated ceiling connection.
            Box("Upper maintenance shelf", new Vector3(14.7f, 3.78f, 7.5f), new Vector3(9.8f, .16f, 1.25f), metal);
            Box("Upper shelf crawl roof", new Vector3(14.7f, 4.65f, 7.5f), new Vector3(9.8f, .12f, 1.25f), metal);
            Box("Upper shelf back", new Vector3(14.7f, 4.2f, 8.2f), new Vector3(9.8f, .8f, .12f), metal);
            Box("Upper shelf outer rail", new Vector3(14.7f, 4.2f, 6.8f), new Vector3(9.8f, .8f, .12f), metal);
        }

        void Crate(Vector3 center, Vector3 size)
        {
            Box("Ballistic crate", center, size, metal);
            foreach (float dx in new[] { -.32f, .32f })
                Box("Crate reinforcing band", center + Vector3.right * (dx * size.x), new Vector3(.065f, size.y + .01f, size.z + .01f), wall, false);
        }

        void BuildProps()
        {
            Crate(new Vector3(-11, .6f, 1), new Vector3(2.4f, 1.2f, 1.2f));
            Crate(new Vector3(-7, .75f, 8.4f), new Vector3(2, 1.5f, 1.4f));
            Crate(new Vector3(11, .6f, 14), new Vector3(2.4f, 1.2f, 1.3f));
            Crate(new Vector3(16, .85f, 22), new Vector3(1.5f, 1.7f, 2));
            Crate(new Vector3(-10, .6f, 19), new Vector3(2.4f, 1.2f, 1.3f));
            Crate(new Vector3(-6, .6f, 26), new Vector3(1.4f, 1.2f, 2.3f));
            Crate(new Vector3(2.4f, .6f, 23), new Vector3(1.2f, 1.2f, 2.5f));
            for (int i = 0; i < 4; i++)
            {
                float x = -15.8f + i * 1.9f;
                Box("Dark server rack", new Vector3(x, 1.35f, 14), new Vector3(1.3f, 2.7f, 1.1f), metal);
                for (int j = 0; j < 3; j++)
                    Box("Server status strip", new Vector3(x, .7f + j * .55f, 13.435f), new Vector3(.55f, .035f, .02f), accent, false);
            }
            Box("Entry decontamination console", new Vector3(9, .9f, -8), new Vector3(4, 1.8f, .8f), metal);
            Box("Entry amber warning", new Vector3(9, 1.9f, -8), new Vector3(4.1f, .07f, .8f), warm, false);
            // Visual route bars: one, two and three marks identify the relay rooms.
            for (int i = 0; i < TerminalPoints.Length; i++)
                for (int mark = 0; mark <= i; mark++)
                    Box("Relay route identifier", TerminalPoints[i] + new Vector3((mark - i * .5f) * .2f, 1.3f, .36f), new Vector3(.09f, .37f, .035f), accent, false);
        }

        void BuildObjectives()
        {
            for (int i = 0; i < TerminalPoints.Length; i++)
            {
                Vector3 p = TerminalPoints[i];
                // Interaction target is .45m in front of the solid terminal face.
                Box("Relay console " + (i + 1), p + Vector3.forward * .55f + Vector3.down * .1f, new Vector3(1.2f, 1.7f, .6f), metal);
                Box("Relay illuminated screen", p + new Vector3(0, .15f, .235f), new Vector3(.88f, .55f, .025f), accent, false);
                Box("Relay key shelf", p + new Vector3(0, -.45f, .1f), new Vector3(1.15f, .08f, .3f), wall);
            }
            Box("Extraction floor marker", new Vector3(-18, .014f, 28.5f), new Vector3(1.25f, .018f, 2.5f), accent, false);
            Box("Extraction rear green bar", new Vector3(-18, 1.25f, 29.78f), new Vector3(2.2f, .12f, .02f), accent, false);
        }

        void BuildLights()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.095f, .135f, .15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.025f, .05f, .065f);
            RenderSettings.fogDensity = .012f;
            foreach (Vector3 p in new[] { new Vector3(0, 4.35f, -8), new Vector3(0, 4.35f, 6), new Vector3(0, 4.35f, 20), new Vector3(-10, 4.35f, 4), new Vector3(11, 4.35f, 17), new Vector3(0, 4.35f, 27) })
            {
                var lamp = new GameObject("Ceiling area light").AddComponent<Light>();
                lamp.transform.SetParent(generated, false);
                lamp.transform.localPosition = p;
                lamp.type = LightType.Spot;
                lamp.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                lamp.spotAngle = 112f;
                lamp.innerSpotAngle = 82f;
                lamp.color = new Color(.50f, .76f, .85f);
                lamp.range = 16f;
                lamp.intensity = 4.5f;
                lamp.shadows = LightShadows.Soft;
                Box("Ceiling luminous bar", p + Vector3.up * .08f, new Vector3(2.5f, .045f, .2f), lightStrip, false);
            }
            foreach (float z in new[] { -8.5f, 9f, 26.8f })
            {
                var ventLight = new GameObject("Vent guidance light").AddComponent<Light>();
                ventLight.transform.SetParent(generated, false);
                ventLight.transform.localPosition = new Vector3(-18, .44f, z);
                ventLight.type = LightType.Point;
                ventLight.color = new Color(.38f, .78f, .28f);
                ventLight.range = 6f;
                ventLight.intensity = .75f;
                ventLight.shadows = LightShadows.None;
            }
            var sun = new GameObject("Low indirect key light").AddComponent<Light>();
            sun.transform.SetParent(generated, false);
            sun.type = LightType.Directional;
            sun.color = new Color(.35f, .53f, .63f);
            sun.intensity = .5f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(57, -25, 0);
        }

        void SpawnGuards(SymbioteGame game)
        {
            Vector3[] westCover = { new Vector3(-11, 0, 2.1f), new Vector3(-7, 0, 7), new Vector3(-14.5f, 0, 8.5f) };
            Vector3[] eastCover = { new Vector3(11, 0, 15.2f), new Vector3(14.5f, 0, 22), new Vector3(7, 0, 21) };
            Vector3[] rearCover = { new Vector3(-8.2f, 0, 19), new Vector3(-6, 0, 24.4f), new Vector3(.9f, 0, 23) };
            game.SpawnSwat(new Vector3(-10, .04f, 4), new[] { new Vector3(-13, 0, 4), new Vector3(-6, 0, 4), new Vector3(-6, 0, 6.9f), new Vector3(-13, 0, 6.9f) }, westCover);
            game.SpawnSwat(new Vector3(-9, .04f, 5.6f), new[] { new Vector3(-13, 0, 7.1f), new Vector3(-6, 0, 7.1f), new Vector3(-6, 0, 4.2f), new Vector3(-13, 0, 4.2f) }, westCover);
            game.SpawnSwat(new Vector3(10, .04f, 17), new[] { new Vector3(7, 0, 17), new Vector3(13.4f, 0, 17), new Vector3(13.4f, 0, 19.5f), new Vector3(7, 0, 19.5f) }, eastCover);
            game.SpawnSwat(new Vector3(11, .04f, 19), new[] { new Vector3(7, 0, 20), new Vector3(13.4f, 0, 20), new Vector3(13.4f, 0, 19), new Vector3(7, 0, 19) }, eastCover);
            game.SpawnSwat(new Vector3(-10, .04f, 23), new[] { new Vector3(-14, 0, 23), new Vector3(-8, 0, 23), new Vector3(-8, 0, 27), new Vector3(-14, 0, 27) }, rearCover);
            game.SpawnSwat(new Vector3(0, .04f, 20), new[] { new Vector3(0, 0, 13), new Vector3(0, 0, 25), new Vector3(-1.8f, 0, 25), new Vector3(-1.8f, 0, 13) }, rearCover);
        }

        void OnDestroy()
        {
            if (ownsFallback) { Destroy(wall); Destroy(floor); Destroy(metal); Destroy(accent); }
            Destroy(warm);
            Destroy(lightStrip);
        }
    }
}
