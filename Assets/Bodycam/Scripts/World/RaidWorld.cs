using UnityEngine;
using UnityEngine.Rendering;

namespace BodycamRaid
{
    /// <summary>Small, original training environment assembled when the scene starts.</summary>
    public sealed class RaidWorld : MonoBehaviour
    {
        [Header("Palette (saved in the project so shaders are included in builds)")]
        public Material concrete, darkConcrete, paint, yellow, metal, wood, cyan, orange;
        private Transform generated;
        private static readonly Vector3 Spawn = new Vector3(0f, .06f, -6f);

        private void Awake() { BuildWorld(); }

        public static void Build()
        {
            var world = Object.FindFirstObjectByType<RaidWorld>();
            if (world == null) world = new GameObject("Training World").AddComponent<RaidWorld>();
            world.BuildWorld();
        }

        public void BuildWorld()
        {
            if (generated != null) return;
            generated = new GameObject("Generated training site").transform;
            generated.SetParent(transform, false);
            EnsurePalette();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.36f, .42f, .48f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.13f, .17f, .21f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .008f;
            Camera.main?.gameObject.SetActive(false);
            FloorAndShell();
            Rooms();
            Details();
            Lighting();
            var player = new GameObject("Officer - WASD / Mouse");
            player.transform.position = Spawn;
            player.AddComponent<CharacterController>();
            player.AddComponent<RaidPlayer>();
            player.transform.SetParent(generated, true);
        }

        private void EnsurePalette()
        {
            concrete = concrete != null ? concrete : MakeMaterial("Concrete", new Color(.38f, .40f, .40f));
            darkConcrete = darkConcrete != null ? darkConcrete : MakeMaterial("Dark concrete", new Color(.15f, .18f, .19f));
            paint = paint != null ? paint : MakeMaterial("Chalk paint", new Color(.62f, .65f, .61f));
            yellow = yellow != null ? yellow : MakeMaterial("Safety yellow", new Color(.89f, .63f, .12f));
            metal = metal != null ? metal : MakeMaterial("Gunmetal", new Color(.12f, .15f, .17f), .7f);
            wood = wood != null ? wood : MakeMaterial("Packing wood", new Color(.38f, .27f, .16f));
            cyan = cyan != null ? cyan : MakeMaterial("Cyan paint", new Color(.10f, .52f, .61f));
            orange = orange != null ? orange : MakeMaterial("Warm marker", new Color(.85f, .30f, .10f));
        }

        private static Material MakeMaterial(string name, Color color, float metallic = 0f)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name };
            material.color = color;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .23f);
            return material;
        }

        private GameObject Box(string name, Vector3 position, Vector3 size, Material material, bool collision = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(generated, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Object.Destroy(box.GetComponent<Collider>());
            return box;
        }
        private void FloorAndShell()
        {
            Box("Continuous concrete slab", new Vector3(0, -.2f, 10), new Vector3(28.4f, .4f, 40.4f), concrete);
            Box("West exterior", new Vector3(-14, 2, 10), new Vector3(.3f, 4, 40), darkConcrete);
            Box("East exterior", new Vector3(14, 2, 10), new Vector3(.3f, 4, 40), darkConcrete);
            Box("Entry boundary", new Vector3(0, 2, -10), new Vector3(28, 4, .3f), darkConcrete);
            Box("Rear boundary", new Vector3(0, 2, 30), new Vector3(28, 4, .3f), darkConcrete);
            // Side roofs leave a long skylight above the central corridor.
            Box("West roof", new Vector3(-8.1f, 4.05f, 10), new Vector3(11.8f, .2f, 40), darkConcrete);
            Box("East roof", new Vector3(8.1f, 4.05f, 10), new Vector3(11.8f, .2f, 40), darkConcrete);
            for (int z = -8; z <= 28; z += 6)
                Box("Ceiling crossbeam", new Vector3(0, 3.83f, z), new Vector3(28, .25f, .18f), metal);
            for (int z = -9; z < 30; z += 3)
                Box("Floor expansion joint", new Vector3(0, .003f, z), new Vector3(27.7f, .006f, .025f), darkConcrete, false);
            Box("Left lane marking", new Vector3(-1.72f, .009f, 10), new Vector3(.07f, .012f, 38), yellow, false);
            Box("Right lane marking", new Vector3(1.72f, .009f, 10), new Vector3(.07f, .012f, 38), yellow, false);
        }

        private void Rooms()
        {
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 2.2f;
                float start = 0;
                foreach (float doorCenter in new[] { 5f, 17f, 27f })
                {
                    float openingStart = doorCenter - .65f;
                    WallSegment(x, start, openingStart);
                    Box("Door lintel", new Vector3(x, 3.18f, doorCenter), new Vector3(.24f, 1.64f, 1.3f), paint);
                    AddDoor(side, x, doorCenter);
                    start = doorCenter + .65f;
                }
                WallSegment(x, start, 30);
                foreach (float z in new[] { 0f, 12f, 24f })
                {
                    Box("Room divider", new Vector3(side * 8.15f, 2, z), new Vector3(11.7f, 4, .24f), paint);
                    Box("Divider skirting", new Vector3(side * 8.15f, .22f, z - .132f), new Vector3(11.7f, .44f, .02f), darkConcrete, false);
                }
                for (int i = 0; i < 3; i++)
                {
                    float z = new[] { 5f, 17f, 27f }[i];
                    Box("Room colour marker", new Vector3(side * 2.05f, 2.85f, z), new Vector3(.025f, .15f, .75f), i == 1 ? orange : cyan, false);
                }
            }
        }
        private void WallSegment(float x, float from, float to)
        {
            if (to <= from) return;
            Box("Corridor wall", new Vector3(x, 2, (from + to) * .5f), new Vector3(.24f, 4, to - from), paint);
            Box("Corridor skirting", new Vector3(x - Mathf.Sign(x) * .132f, .22f, (from + to) * .5f), new Vector3(.02f, .44f, to - from), darkConcrete, false);
        }
        private void AddDoor(float side, float x, float z)
        {
            var hinge = new GameObject("Door hinge - G to open");
            hinge.transform.SetParent(generated, false);
            hinge.transform.localPosition = new Vector3(x, 0, z - .65f);
            var panel = Box("Door", new Vector3(x, 1.15f, z), new Vector3(.12f, 2.3f, 1.25f), metal);
            panel.transform.SetParent(hinge.transform, true);
            var handle = Box("Door handle", new Vector3(x - side * .10f, 1.07f, z + .37f), new Vector3(.10f, .05f, .18f), yellow, false);
            handle.transform.SetParent(hinge.transform, true);
            hinge.AddComponent<RaidDoor>().openAngle = side * 95f;
        }
        private void Details()
        {
            // Compact starting area, clear route through the middle.
            Box("Equipment bench", new Vector3(-7, .8f, -6), new Vector3(4, .15f, .9f), metal);
            foreach (float x in new[] { -8.7f, -5.3f })
                Box("Bench support", new Vector3(x, .4f, -6), new Vector3(.12f, .8f, .65f), metal);
            Box("Equipment case", new Vector3(-7.7f, 1.02f, -6), new Vector3(1.1f, .3f, .65f), darkConcrete);
            Box("Entry wall accent", new Vector3(0, 2.4f, -9.82f), new Vector3(7, .8f, .025f), cyan, false);
            Box("Vault barrier", new Vector3(0, .36f, 1.2f), new Vector3(2.1f, .72f, .48f), yellow);
            Box("Vault barrier dark top", new Vector3(0, .735f, 1.2f), new Vector3(2.1f, .03f, .48f), darkConcrete);
            // Clear landing space on both sides of each obstacle.
            Box("Low training crate", new Vector3(-6, .45f, 5), new Vector3(2, .9f, 1.25f), wood);
            Box("Climb platform", new Vector3(-9, .68f, 8.5f), new Vector3(2.4f, 1.36f, 2.2f), concrete);
            Box("Platform edge marker", new Vector3(-9, 1.37f, 7.43f), new Vector3(2.4f, .025f, .07f), yellow, false);
            Crate(new Vector3(7, .5f, 4), new Vector3(1.6f, 1, 1.6f));
            Crate(new Vector3(9, .5f, 4), new Vector3(1.6f, 1, 1.6f));
            Crate(new Vector3(9, 1.5f, 4), new Vector3(1.6f, 1, 1.6f));
            Shelf(new Vector3(12.8f, 0, 8));
            Shelf(new Vector3(-12.8f, 0, 19));
            Box("Office desk", new Vector3(7, .82f, 18), new Vector3(3, .14f, 1.3f), wood);
            foreach (float dx in new[] { -1.3f, 1.3f })
                Box("Desk leg", new Vector3(7 + dx, .4f, 18), new Vector3(.12f, .8f, 1.1f), metal);
            Box("Monitor", new Vector3(7, 1.18f, 18.3f), new Vector3(.85f, .56f, .09f), darkConcrete);
            Box("Monitor screen", new Vector3(7, 1.18f, 18.245f), new Vector3(.74f, .44f, .012f), cyan, false);
            Crate(new Vector3(-6, .4f, 16), new Vector3(2, .8f, 2));
            Crate(new Vector3(-9, .65f, 21), new Vector3(2, 1.3f, 1.5f));
            foreach (float x in new[] { -9f, -6f, 6f, 9f })
                Box("Rear training cover", new Vector3(x, .6f, 27.8f), new Vector3(1.5f, 1.2f, .65f), concrete);
            for (int z = 3; z < 30; z += 6)
                Box("Corridor safety dash", new Vector3(0, .011f, z), new Vector3(.10f, .01f, .6f), paint, false);
        }
        private void Crate(Vector3 position, Vector3 size)
        {
            Box("Shipping crate", position, size, wood);
            foreach (float dx in new[] { -.32f, .32f })
                Box("Crate band", position + new Vector3(dx * size.x, 0, 0), new Vector3(.06f, size.y + .02f, size.z + .02f), metal, false);
        }
        private void Shelf(Vector3 position)
        {
            foreach (float z in new[] { -1.5f, 1.5f })
                foreach (float x in new[] { -.45f, .45f })
                    Box("Shelf post", position + new Vector3(x, 1.4f, z), new Vector3(.08f, 2.8f, .08f), metal);
            foreach (float y in new[] { .2f, 1.3f, 2.5f })
                Box("Shelf board", position + new Vector3(0, y, 0), new Vector3(1, .08f, 3.2f), metal);
            Crate(position + new Vector3(0, .65f, -.75f), new Vector3(.8f, .8f, .9f));
            Crate(position + new Vector3(0, 1.7f, .7f), new Vector3(.8f, .7f, .9f));
        }
        private void Lighting()
        {
            var sun = new GameObject("Overcast skylight").AddComponent<Light>();
            sun.transform.SetParent(generated, false);
            sun.type = LightType.Directional;
            sun.color = new Color(.68f, .80f, 1f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55, -30, 0);
            for (int z = -6; z <= 27; z += 11)
            {
                AddLight(new Vector3(0, 3.5f, z), new Color(.75f, .87f, 1), 2f, 11);
                AddLight(new Vector3(-8, 3.25f, z + 2), new Color(1, .79f, .60f), 2.4f, 11);
                AddLight(new Vector3(8, 3.25f, z + 2), new Color(.75f, .87f, 1), 2.4f, 11);
            }
        }
        private void AddLight(Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject("Ceiling lamp");
            go.transform.SetParent(generated, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            Box("Lamp housing", position + Vector3.up * .15f, new Vector3(1.1f, .08f, .25f), paint, false);
        }
    }
}
