using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BugArena
{
    // Imported Blender art with procedural effects and a fallback for missing assets.
    public sealed class ArenaVisuals
    {
        public static readonly Color Teal = new Color(.16f, .88f, .72f);
        public static readonly Color Coral = new Color(1, .36f, .30f);
        public static readonly Color Gold = new Color(1, .78f, .28f);
        readonly Transform root;
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        readonly Dictionary<Color, Material> paletteMaterials = new Dictionary<Color, Material>();
        readonly List<Texture2D> paletteTextures = new List<Texture2D>();
        Mesh detailSphere, detailCylinder, pollenMesh;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Spark> sparks = new List<Spark>();
        readonly List<Transform> motes = new List<Transform>();
        BugView player, enemy;
        readonly List<Transform> boundaries = new List<Transform>();
        readonly Dictionary<Renderer, Material[]> enemyMaterials = new Dictionary<Renderer, Material[]>();
        float ringEdge = CombatRules.RingEdge;
        Transform previewRoot, previewLine, previewZone, previewTip;
        Material previewMaterial;
        float clock;
        struct Spark { public Transform Transform; public Vector3 Velocity; public float Life, MaxLife; }
        sealed class BugView
        {
            public Transform Root, Body, Shadow, ChargeRing;
            public readonly List<Transform> Legs = new List<Transform>();
            public readonly List<Quaternion> LegRest = new List<Quaternion>();
            public readonly List<Transform> Streaks = new List<Transform>();
            public Vector3 Previous;
            public float Gait;
            public BeetleAnimationPlayer Animation;
        }
        public ArenaVisuals(Transform root) { this.root = root; }
        Material Mat(Color color)
        {
            if (materials.TryGetValue(color, out var material)) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .38f);
            materials.Add(color, material); return material;
        }
        Material PaletteMat(Color shell)
        {
            if (paletteMaterials.TryGetValue(shell, out var cached)) return cached;
            var source = Resources.Load<Texture2D>("BugArenaArt/JadePalette");
            if (source == null) throw new System.InvalidOperationException("Missing art palette texture");
            var pixels = source.GetPixels();
            var encoded = shell.gamma; encoded.a = pixels[0].a; pixels[0] = encoded;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
            texture.name = "BugArena palette texture"; texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels(pixels); texture.Apply(false, true); paletteTextures.Add(texture);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "BugArena palette " + shell;
            material.SetColor("_BaseColor", Color.white); material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 1); material.SetFloat("_Metallic", 0);
            material.SetFloat("_SmoothnessTextureChannel", 1);
            material.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            paletteMaterials.Add(shell, material); return material;
        }
        Mesh DetailMesh(PrimitiveType primitive, bool pollen)
        {
            if (pollen && pollenMesh != null) return pollenMesh;
            if (!pollen && primitive == PrimitiveType.Sphere && detailSphere != null) return detailSphere;
            if (!pollen && primitive == PrimitiveType.Cylinder && detailCylinder != null) return detailCylinder;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var indices = new List<int>();
            if (primitive == PrimitiveType.Sphere)
            {
                int segments = pollen ? 4 : 12, rings = pollen ? 2 : 6;
                for (int j = 0; j <= rings; j++)
                    for (int i = 0; i <= segments; i++)
                    {
                        float lat = j * Mathf.PI / rings, lon = i * Mathf.PI * 2 / segments;
                        var normal = new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));
                        vertices.Add(normal * .5f); normals.Add(normal);
                    }
                for (int j = 0; j < rings; j++)
                    for (int i = 0; i < segments; i++)
                    {
                        int a = j * (segments + 1) + i, b = a + segments + 1;
                        if (j != 0) { indices.Add(a); indices.Add(a + 1); indices.Add(b); }
                        if (j != rings - 1) { indices.Add(a + 1); indices.Add(b + 1); indices.Add(b); }
                    }
            }
            else
            {
                const int segments = 8;
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2 / segments;
                    var n = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    vertices.Add(n * .5f + Vector3.down); vertices.Add(n * .5f + Vector3.up); normals.Add(n); normals.Add(n);
                    if (i < segments)
                    {
                        int a = i * 2; indices.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
                    }
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    int start = vertices.Count; vertices.Add(Vector3.up * side); normals.Add(Vector3.up * side);
                    for (int i = 0; i <= segments; i++)
                    {
                        float angle = i * Mathf.PI * 2 / segments;
                        vertices.Add(new Vector3(Mathf.Cos(angle) * .5f, side, Mathf.Sin(angle) * .5f)); normals.Add(Vector3.up * side);
                        if (i < segments) indices.AddRange(side > 0 ? new[] { start, start + i + 2, start + i + 1 } : new[] { start, start + i + 1, start + i + 2 });
                    }
                }
            }
            var mesh = new Mesh { name = pollen ? "Pollen octahedron" : "Low detail " + primitive };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds(); meshes.Add(mesh);
            if (pollen) pollenMesh = mesh;
            else if (primitive == PrimitiveType.Sphere) detailSphere = mesh; else detailCylinder = mesh;
            return mesh;
        }
        void BatchScenery(Transform scenery)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var renderer in scenery.GetComponentsInChildren<MeshRenderer>())
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var material = renderer.sharedMaterial;
                if (!groups.TryGetValue(material, out var instances)) groups[material] = instances = new List<CombineInstance>();
                instances.Add(new CombineInstance { mesh = mesh, transform = scenery.worldToLocalMatrix * renderer.localToWorldMatrix });
                renderer.enabled = false; Release(renderer.gameObject);
            }
            foreach (var pair in groups)
            {
                var mesh = new Mesh { name = "Combined forest decoration" }; mesh.CombineMeshes(pair.Value.ToArray()); meshes.Add(mesh);
                var group = Group("Forest material batch", scenery);
                group.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = group.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = pair.Key;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
        Transform Group(string name, Transform parent = null)
        {
            var t = new GameObject(name).transform; t.SetParent(parent != null ? parent : root, false); return t;
        }
        Transform Shape(string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Color color, Transform parent = null, bool lowDetail = false)
        {
            var go = GameObject.CreatePrimitive(primitive); go.name = name;
            var t = go.transform; t.SetParent(parent != null ? parent : root, false); t.localPosition = position; t.localScale = scale;
            var collider = go.GetComponent<Collider>(); if (collider != null) Release(collider);
            if (lowDetail && (primitive == PrimitiveType.Sphere || primitive == PrimitiveType.Cylinder))
                go.GetComponent<MeshFilter>().sharedMesh = DetailMesh(primitive, name == "Drifting pollen");
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            return t;
        }
        Transform Rod(string name, Vector3 a, Vector3 b, float radius, Color color, Transform parent)
        {
            var t = Shape(name, PrimitiveType.Cylinder, (a + b) / 2, new Vector3(radius, Vector3.Distance(a, b) / 2, radius), color, parent);
            t.localRotation = Quaternion.FromToRotation(Vector3.up, b - a); return t;
        }
        Transform Ring(string name, float rx, float rz, float tube, Color color, Transform parent)
        {
            const int segments = 80, sides = 6;
            var vertices = new Vector3[(segments + 1) * sides]; var normals = new Vector3[vertices.Length]; var tris = new int[segments * sides * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                for (int j = 0; j < sides; j++)
                {
                    float v = j * Mathf.PI * 2 / sides;
                    var n = new Vector3(Mathf.Cos(angle) * Mathf.Cos(v), Mathf.Sin(v), Mathf.Sin(angle) * Mathf.Cos(v));
                    vertices[i * sides + j] = new Vector3(Mathf.Cos(angle) * rx, 0, Mathf.Sin(angle) * rz) + n * tube;
                    normals[i * sides + j] = n;
                    if (i == segments) continue;
                    int k = (i * sides + j) * 6, a = i * sides + j, b = i * sides + (j + 1) % sides;
                    tris[k] = a; tris[k + 1] = a + sides; tris[k + 2] = b;
                    tris[k + 3] = b; tris[k + 4] = a + sides; tris[k + 5] = b + sides;
                }
            }
            var mesh = new Mesh { name = name, vertices = vertices, normals = normals, triangles = tris }; mesh.RecalculateBounds(); meshes.Add(mesh);
            var t = Group(name, parent); t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mat(color); return t;
        }
        Transform ImportArt(GameObject asset, Transform parent)
        {
            var model = Object.Instantiate(asset, parent, false);
            // FBX materials are converted to the project's URP shader and cached by color.
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    var source = slots[i];
                    var color = source != null && source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                    if (source != null && source.name.Contains("Palette"))
                        slots[i] = PaletteMat(source.name.Contains("Coral") ? new Color(.95f, .22f, .16f) : new Color(.08f, .65f, .44f));
                    else slots[i] = Mat(color);
                }
                renderer.sharedMaterials = slots;
            }
            return model.transform;
        }
        public Camera BuildArena()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.31f, .43f, .48f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.035f, .085f, .12f);
            RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .018f;
            var cameraGo = Group("Arena Camera"); var camera = cameraGo.gameObject.AddComponent<Camera>();
            cameraGo.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 6.6f;
            camera.transform.position = new Vector3(0, 5.8f, -22);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0, 1.4f, 0) - camera.transform.position);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .055f, .08f);
            camera.nearClipPlane = .1f; camera.farClipPlane = 110;
            camera.gameObject.AddComponent<AudioListener>();
            var lightGo = Group("Warm overhead light"); var light = lightGo.gameObject.AddComponent<Light>();
            light.type = LightType.Directional; light.color = new Color(1, .88f, .65f); light.intensity = 1.5f;
            light.shadows = LightShadows.Soft; lightGo.rotation = Quaternion.Euler(40, -35, 0);
            var fillGo = Group("Moon fill"); var fill = fillGo.gameObject.AddComponent<Light>();
            fill.type = LightType.Directional; fill.color = new Color(.3f, .7f, .85f); fill.intensity = .65f; fillGo.rotation = Quaternion.Euler(20, 140, 0);
            var arena = Group("Mosswood fighting ring");
            var arenaAsset = Resources.Load<GameObject>("BugArenaArt/MosswoodArena");
            if (arenaAsset != null) ImportArt(arenaAsset, arena);
            else
            {
            Shape("Clay plinth", PrimitiveType.Cylinder, new Vector3(0, -.48f, .3f), new Vector3(20, .46f, 7.2f), new Color(.19f, .15f, .13f), arena);
            Shape("Moss edge", PrimitiveType.Cylinder, new Vector3(0, -.07f, .3f), new Vector3(20.1f, .09f, 7.25f), new Color(.18f, .32f, .23f), arena);
            Shape("Sand fighting surface", PrimitiveType.Cylinder, new Vector3(0, -.025f, 0), new Vector3(18.9f, .04f, 5.8f), new Color(.40f, .34f, .23f), arena);
            }
            Ring("Outer gold inlay", 9.25f, 2.73f, .035f, new Color(.64f, .56f, .33f), arena).localPosition = new Vector3(0, .025f, 0);
            Ring("Inner ring", 4.3f, 1.9f, .024f, new Color(.57f, .47f, .3f), arena).localPosition = new Vector3(0, .025f, 0);
            Ring("Centre mark", .7f, .48f, .022f, new Color(.62f, .53f, .35f), arena).localPosition = new Vector3(0, .028f, 0);
            for (int i = -8; i <= 8; i++)
                Shape("Range tick", PrimitiveType.Cube, new Vector3(i, .035f, -1.8f), new Vector3(.025f, .015f, i % 2 == 0 ? .25f : .12f), new Color(.67f, .58f, .39f), arena);
            // Seeded environment generation avoids affecting combat randomness.
            var scenery = Group("Batched forest scenery");
            var random = new System.Random(48);
            for (int i = 0; i < 34; i++)
            {
                float x = (float)random.NextDouble() * 25 - 12.5f, z = 4 + (float)random.NextDouble() * 9;
                float height = 3 + (float)random.NextDouble() * 7;
                Shape("Distant reed", PrimitiveType.Cylinder, new Vector3(x, height / 2 - 1, z), new Vector3(.14f, height / 2, .14f), new Color(.045f, .16f, .15f), scenery, true);
                var leaf = Shape("Canopy leaf", PrimitiveType.Sphere, new Vector3(x + .6f, height * .7f, z), new Vector3(3, .4f, 1), new Color(.065f, .22f, .18f), scenery, true);
                leaf.localRotation = Quaternion.Euler(0, 15 * i, 25 * (i % 2 == 0 ? 1 : -1));
            }
            for (int i = 0; i < 12; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                float x = side * (8.8f + (float)random.NextDouble() * 3), z = .5f + (float)random.NextDouble() * 6;
                float h = .6f + (float)random.NextDouble() * 1.9f;
                var mushroom = Group("Lantern mushroom", scenery);
                Shape("Ivory stem", PrimitiveType.Cylinder, new Vector3(x, h / 2, z), new Vector3(.17f, h / 2, .17f), new Color(.58f, .67f, .48f), mushroom, true);
                Shape("Luminous cap", PrimitiveType.Sphere, new Vector3(x, h, z), new Vector3(1.2f, .4f, 1), i % 3 == 0 ? new Color(.91f, .5f, .29f) : new Color(.28f, .59f, .52f), mushroom, true);
            }
            for (int i = 0; i < 44; i++)
            {
                float x = (float)random.NextDouble() * 22 - 11, z = i % 2 == 0 ? -3.4f : 3.1f;
                float h = .2f + (float)random.NextDouble() * .55f;
                var blade = Shape("Moss blade", PrimitiveType.Cube, new Vector3(x, h / 2 - .05f, z), new Vector3(.075f, h, .04f), new Color(.18f, .4f, .27f), scenery);
                blade.localRotation = Quaternion.Euler(0, 0, (float)random.NextDouble() * 60 - 30);
                if (i % 4 == 0) Shape("River pebble", PrimitiveType.Sphere, new Vector3(x, .05f, z + .2f), new Vector3(.5f, .22f, .38f), new Color(.33f, .39f, .36f), scenery, true);
            }
            BatchScenery(scenery);
            for (int i = 0; i < 22; i++)
                motes.Add(Shape("Drifting pollen", PrimitiveType.Sphere, new Vector3((float)random.NextDouble() * 22 - 11, (float)random.NextDouble() * 6 + .3f, (float)random.NextDouble() * 6 + 1), Vector3.one * .045f, new Color(.82f, .9f, .46f), null, true));
            previewRoot = Group("Player charge trajectory");
            previewLine = Shape("Dash distance", PrimitiveType.Cube, Vector3.zero, Vector3.one, new Color(.25f, .62f, .51f), previewRoot);
            previewZone = Shape("Launcher contact zone", PrimitiveType.Cube, Vector3.zero, Vector3.one, Gold, previewRoot);
            previewTip = Ring("Dash endpoint", .18f, .18f, .035f, Gold, previewRoot);
            previewMaterial = new Material(Mat(Gold)); previewZone.GetComponent<Renderer>().sharedMaterial = previewMaterial;
            previewRoot.gameObject.SetActive(false);
            return camera;
        }
        public void BuildFighters()
        {
            player = BuildBug("Jade / player", Teal); enemy = BuildBug("Ember / rival", Coral);
        }
        BugView BuildBug(string name, Color shell)
        {
            var v = new BugView { Root = Group(name) };
            v.Body = Group("Animated body", v.Root);
            var dark = new Color(.075f, .14f, .16f);
            v.Shadow = Shape("Contact shadow", PrimitiveType.Sphere, new Vector3(0, .025f, 0), new Vector3(1.9f, .025f, 1.05f), new Color(.16f, .19f, .15f), v.Root);
            var bugAsset = Resources.Load<GameObject>(shell == Teal ? "BugArenaArt/JadeBeetle" : "BugArenaArt/EmberBeetle");
            if (bugAsset != null)
            {
                var model = ImportArt(bugAsset, v.Body);
                // FBX converts Blender handedness by reflecting the authored X axis.
                model.localScale = new Vector3(-1, 1, 1);
                AnimationClip idle = null, walk = null;
                string assetPath = shell == Teal ? "BugArenaArt/JadeBeetle" : "BugArenaArt/EmberBeetle";
                foreach (var clip in Resources.LoadAll<AnimationClip>(assetPath))
                {
                    if (clip.name == "Idle") idle = clip;
                    if (clip.name == "Walk") walk = clip;
                }
                if (idle != null && walk != null) v.Animation = new BeetleAnimationPlayer(model.gameObject, idle, walk);
                else
                {
                    // Only the original rigid meshes use the procedural fallback.
                    foreach (var child in model.GetComponentsInChildren<Transform>())
                        if (child.name.StartsWith("Leg_") && child.GetComponent<Renderer>() != null) v.Legs.Add(child);
                    Debug.LogWarning("[BugArena] Beetle Idle/Walk clips are missing.");
                }
            }
            else
            {
            Shape("Abdomen", PrimitiveType.Sphere, new Vector3(-.3f, .65f, 0), new Vector3(1.65f, .85f, 1.2f), dark, v.Body);
            for (int i = -1; i <= 1; i += 2)
            {
                Shape("Split elytron", PrimitiveType.Sphere, new Vector3(-.35f, .82f, i * .29f), new Vector3(1.43f, .69f, .64f), shell, v.Body);
                var stripe = Shape("Shell gold trim", PrimitiveType.Sphere, new Vector3(-.35f, 1.11f, i * .12f), new Vector3(.96f, .045f, .07f), Gold, v.Body);
                stripe.localRotation = Quaternion.Euler(0, 0, -5);
            }
            Shape("Head armor", PrimitiveType.Sphere, new Vector3(.55f, .65f, 0), new Vector3(.87f, .68f, .97f), shell * .67f, v.Body);
            Shape("Horn base", PrimitiveType.Sphere, new Vector3(.98f, .71f, 0), new Vector3(.5f, .2f, .28f), new Color(.92f, .83f, .56f), v.Body);
            Rod("Rhino horn", new Vector3(.9f, .7f, 0), new Vector3(1.38f, 1.02f, 0), .15f, new Color(.96f, .87f, .62f), v.Body);
            Rod("Horn tip", new Vector3(1.38f, 1.02f, 0), new Vector3(1.38f, 1.27f, 0), .075f, Gold, v.Body);
            foreach (int side in new[] { -1, 1 })
            {
                Shape("Eye", PrimitiveType.Sphere, new Vector3(.74f, .81f, side * .42f), new Vector3(.25f, .25f, .12f), new Color(.98f, .98f, .8f), v.Body);
                Shape("Pupil", PrimitiveType.Sphere, new Vector3(.80f, .82f, side * .474f), new Vector3(.1f, .14f, .045f), dark, v.Body);
                Rod("Antenna", new Vector3(.65f, .93f, side * .25f), new Vector3(.94f, 1.28f, side * .42f), .04f, dark, v.Body);
                Shape("Antenna bead", PrimitiveType.Sphere, new Vector3(.94f, 1.28f, side * .42f), Vector3.one * .1f, Gold, v.Body);
                for (int i = 0; i < 3; i++)
                {
                    var leg = Group("Articulated leg", v.Body); leg.localPosition = new Vector3(-.72f + i * .56f, .45f, side * .35f);
                    Rod("Upper leg", Vector3.zero, new Vector3(-.16f, -.17f, side * .48f), .09f, dark, leg);
                    Rod("Lower leg", new Vector3(-.16f, -.17f, side * .48f), new Vector3(.07f, -.39f, side * .63f), .06f, dark, leg);
                    v.Legs.Add(leg);
                }
            }
            }
            v.Legs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (var leg in v.Legs) v.LegRest.Add(leg.localRotation);
            v.ChargeRing = Ring("Charge aura", 1.2f, .8f, .025f, shell, v.Root); v.ChargeRing.localPosition = new Vector3(0, .06f, 0);
            for (int i = 0; i < 4; i++)
                v.Streaks.Add(Shape("Dash speed line", PrimitiveType.Cube, new Vector3(-1.5f - i * .3f, .35f + i * .2f, -.6f), new Vector3(1.3f, .035f, .035f), shell, v.Body));
            return v;
        }
        public void SetStage(ArenaStage stage)
        {
            ringEdge = stage.Edge;
            foreach (var spark in sparks) if (spark.Transform != null) Release(spark.Transform.gameObject);
            sparks.Clear();
            player.Previous = new Vector3(-4, 0, 0); enemy.Previous = new Vector3(4, 0, 0);
            player.Gait = enemy.Gait = 0;
            player.Animation?.Reset(); enemy.Animation?.Reset();
            if (boundaries.Count == 0)
                foreach (int side in new[] { -1, 1 })
                    boundaries.Add(Shape("Combat boundary", PrimitiveType.Cube, Vector3.zero, new Vector3(.07f, .035f, 4.4f), Gold));
            for (int i = 0; i < boundaries.Count; i++) boundaries[i].localPosition = new Vector3((i == 0 ? -1 : 1) * ringEdge, .065f, 0);
            if (enemyMaterials.Count == 0)
                foreach (var renderer in enemy.Body.GetComponentsInChildren<Renderer>()) enemyMaterials[renderer] = renderer.sharedMaterials;
            foreach (var pair in enemyMaterials)
            {
                var slots = (Material[])pair.Value.Clone();
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].name.StartsWith("BugArena palette")) { slots[i] = PaletteMat(stage.Accent); continue; }
                    var color = slots[i].GetColor("_BaseColor");
                    if (color.r > .7f && color.g < .45f && color.b < .4f) slots[i] = Mat(stage.Accent);
                }
                pair.Key.sharedMaterials = slots;
            }
            RenderSettings.fogColor = Color.Lerp(new Color(.035f, .085f, .12f), stage.Accent * .13f, .4f);
        }
        public void Update(ArenaCombat combat, float dt, float time)
        {
            clock += dt;
            UpdateBug(player, combat.Player, dt); UpdateBug(enemy, combat.Enemy, dt);
            for (int i = 0; i < motes.Count; i++)
            {
                var t = motes[i]; var p = t.localPosition; p.y += Mathf.Sin(clock * .7f + i) * dt * .08f; p.x += Mathf.Cos(clock * .4f + i) * dt * .06f; t.localPosition = p;
            }
            bool preview = combat.Player.State == BugState.Charging && !combat.Finished;
            previewRoot.gameObject.SetActive(preview);
            if (preview)
            {
                var f = combat.Player;
                float end = Mathf.Clamp(f.X + f.Facing * CombatRules.Distance(f.Charge), -ringEdge, ringEdge);
                float zoneEnd = end + f.Facing * CombatRules.HitRadius;
                float zoneStart = zoneEnd - f.Facing * CombatRules.EndWindow;
                previewLine.localPosition = new Vector3((f.X + end) / 2, .07f, -.9f);
                previewLine.localScale = new Vector3(Mathf.Abs(end - f.X), .025f, .055f);
                previewZone.localPosition = new Vector3((zoneStart + zoneEnd) / 2, .09f, -.9f);
                previewZone.localScale = new Vector3(CombatRules.EndWindow, .045f, .27f);
                previewTip.localPosition = new Vector3(end, .10f, -.9f);
                bool inZone = combat.Enemy.Grounded && combat.Enemy.X >= Mathf.Min(zoneStart, zoneEnd) && combat.Enemy.X <= Mathf.Max(zoneStart, zoneEnd);
                previewMaterial.SetColor("_BaseColor", inZone ? Color.Lerp(Gold, Color.white, .35f + Mathf.Sin(time * 12) * .2f) : Gold);
            }
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i]; s.Life -= dt;
                if (s.Life <= 0) { Release(s.Transform.gameObject); sparks.RemoveAt(i); continue; }
                s.Velocity += Vector3.down * dt * 8; s.Transform.position += s.Velocity * dt;
                s.Transform.localScale = Vector3.one * (.09f * s.Life / s.MaxLife);
                sparks[i] = s;
            }
        }
        void UpdateBug(BugView view, Fighter f, float dt)
        {
            Vector3 position = new Vector3(f.X, f.Y, 0);
            float movement = Mathf.Abs(position.x - view.Previous.x);
            view.Gait += Mathf.Min(movement, .15f) * 12;
            bool teleported = movement > 1;
            if (teleported) movement = 0; // Teleports on stage restart do not animate a stride.
            view.Root.localPosition = position;
            float charge = f.State == BugState.Charging ? f.Power : 0;
            float squash = 1 - charge * .16f;
            float wobble = f.State == BugState.Charging ? Mathf.Sin(clock * (30 + charge * 35)) * .018f * charge : 0;
            float breathe = view.Animation == null && f.State == BugState.Ready ? Mathf.Sin(clock * 2.8f) * .012f : 0;
            view.Body.localPosition = new Vector3(wobble, breathe + (view.Animation == null ? Mathf.Sin(view.Gait) * Mathf.Min(movement * 2, .06f) : 0), 0);
            view.Body.localScale = new Vector3(1 + charge * .12f, squash, 1);
            float angle = f.State == BugState.Airborne ? -35 : f.State == BugState.Defeated ? 140 : f.State == BugState.Charging ? 5 : 0;
            view.Body.localRotation = Quaternion.Euler(0, f.Facing == 1 ? 0 : 180, angle);
            view.Shadow.localPosition = new Vector3(0, .025f - f.Y, 0);
            view.Shadow.localScale = new Vector3(1.9f, .025f, 1.05f) / (1 + f.Y * .25f);
            view.ChargeRing.gameObject.SetActive(f.State == BugState.Charging);
            view.ChargeRing.localScale = Vector3.one * (1 + charge * .3f + Mathf.Sin(clock * 8) * .03f);
            float localSpeed = dt > 0 && !teleported ? (position.x - view.Previous.x) * f.Facing / dt : 0;
            view.Animation?.Tick(dt, localSpeed, f.State == BugState.Ready,
                f.State != BugState.Defeated && f.State != BugState.Airborne);
            for (int i = 0; i < view.Legs.Count; i++)
                view.Legs[i].localRotation = view.LegRest[i] * Quaternion.Euler(0, Mathf.Sin(view.Gait + ((i % 3 + i / 3) % 2) * Mathf.PI) * (movement > .001f ? 22 : 3), 0);
            foreach (var streak in view.Streaks) streak.gameObject.SetActive(f.State == BugState.Dashing);
            view.Previous = position;
        }
        public void Burst(Vector3 origin, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                var t = Shape("Impact spark", PrimitiveType.Sphere, origin, Vector3.one * .09f, color);
                float life = .25f + (i % 4) * .08f;
                sparks.Add(new Spark { Transform = t, Velocity = new Vector3(Mathf.Cos(angle) * (3 + i % 3), Mathf.Sin(angle) * 3 + 2, -.3f), Life = life, MaxLife = life });
            }
        }
        static void Release(Object value)
        {
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
        public void Dispose()
        {
            player?.Animation?.Dispose(); enemy?.Animation?.Dispose();
            foreach (var material in paletteMaterials.Values) if (material != null) Release(material);
            foreach (var texture in paletteTextures) if (texture != null) Release(texture);
            foreach (var material in materials.Values) if (material != null) Release(material);
            if (previewMaterial != null) Release(previewMaterial);
            foreach (var mesh in meshes) if (mesh != null) Release(mesh);
        }
    }
}
