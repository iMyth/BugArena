using System;
using System.IO;
using System.Linq;
using BugArena;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class ArtOptimizationChecks
{
    [InitializeOnLoadMethod]
    static void Requested()
    {
        const string request = "Library/BugArena/request-art-optimization";
        if (!File.Exists(request)) return;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(request) || EditorApplication.isPlaying) return;
            string mode = File.ReadAllText(request).Trim(); File.Delete(request);
            try
            {
                if (mode == "baseline") RecordScene("before");
                else ValidateAll();
            }
            catch (Exception error) { Debug.LogException(error); }
        };
    }
    public static void ValidateAll()
    {
        Run(); AnimationChecks.Run(); SuperChecks.Run(); CampaignChecks.Run(); TouchChecks.Run();
        ArtValidation.Run(); AnimationPreview.Render(); RecordScene("after");
    }
    public static void RecordScene(string label)
    {
        var root = new GameObject("Art cost audit"); var visuals = new ArenaVisuals(root.transform);
        try
        {
            visuals.BuildArena(); visuals.BuildFighters(); visuals.SetStage(ArenaCampaign.Stages[0]);
            visuals.Update(new ArenaCombat(), 0, 0);
            int triangles = 0, slots = 0, renderers = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy))
            {
                Mesh mesh = r is SkinnedMeshRenderer skin ? skin.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                triangles += mesh.triangles.Length / 3; slots += mesh.subMeshCount; renderers++;
            }
            string text = $"Active whole-scene mesh triangles: {triangles}; renderers: {renderers}; submesh submissions before culling/additional passes: {slots}. These are inventory counts, not measured frame draw calls or frame time.";
            Directory.CreateDirectory("Library/BugArena"); File.WriteAllText("Library/BugArena/art-cost-" + label + ".txt", text);
            Debug.Log("[BugArena cost " + label + "] " + text);
        }
        finally { visuals.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
    }
    [MenuItem("Bug Arena/Run Art Optimization Checks")]
    public static void Run()
    {
        int checks = 0;
        Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); checks++; };
        foreach (var name in new[] { "JadeBeetle", "EmberBeetle", "MosswoodArena" })
        {
            var asset = Resources.Load<GameObject>("BugArenaArt/" + name);
            check(asset != null, "Asset loads: " + name);
            var renderers = asset.GetComponentsInChildren<Renderer>();
            check(renderers.Length == 1, "One renderer per imported asset");
            var r = renderers[0];
            var mesh = r is SkinnedMeshRenderer skin ? skin.sharedMesh : r.GetComponent<MeshFilter>().sharedMesh;
            check(mesh.subMeshCount == 1 && r.sharedMaterials.Length == 1, "One submesh and one palette material");
            check(mesh.uv.All(uv => uv.x > 0 && uv.x < 1 && Mathf.Abs(uv.y - .5f) < .0001f), "Palette UV centers");
            check(name != "MosswoodArena" || mesh.triangles.Length / 3 <= 2400, "Arena triangle budget");
            if (name == "MosswoodArena") continue;
            var model = UnityEngine.Object.Instantiate(asset); var bake = new Mesh();
            BeetleAnimationPlayer player = null;
            try
            {
                var clips = Resources.LoadAll<AnimationClip>("BugArenaArt/" + name);
                player = new BeetleAnimationPlayer(model, clips.Single(c => c.name == "Idle"), clips.Single(c => c.name == "Walk"));
                var renderer = model.GetComponentInChildren<SkinnedMeshRenderer>();
                float minimum = float.MaxValue, maximum = float.MinValue;
                foreach (float speed in new[] { 0f, 1.2f, 3.8f, 0f, -3.8f, 0f })
                    for (int i = 0; i < 144; i++)
                    {
                        player.Tick(1f / 120, speed, true, true); renderer.BakeMesh(bake, true);
                        float bottom = bake.vertices.Min(v => renderer.transform.TransformPoint(v).y);
                        minimum = Mathf.Min(minimum, bottom); maximum = Mathf.Max(maximum, bottom);
                    }
                check(minimum >= .0265f, "Imported animation/blend penetrates sand: " + minimum);
                check(maximum < .055f, "Imported planted pose floats: " + maximum);
                Debug.Log($"[BugArena sole] {name}: {minimum:F5} to {maximum:F5}m, including start/stop/backward blends");
            }
            finally { player?.Dispose(); UnityEngine.Object.DestroyImmediate(bake); UnityEngine.Object.DestroyImmediate(model); }
        }
        var palette = Resources.Load<Texture2D>("BugArenaArt/JadePalette");
        check(palette != null && palette.width == 8 && palette.height == 1, "Tiny portable palette texture");
        check(palette.filterMode == FilterMode.Point && palette.mipmapCount == 1, "Palette has no filtering/mipmap bleed");
        var root = new GameObject("Palette stage check"); var visuals = new ArenaVisuals(root.transform);
        try
        {
            visuals.BuildArena(); visuals.BuildFighters();
            foreach (var stage in ArenaCampaign.Stages)
            {
                visuals.SetStage(stage); visuals.Update(new ArenaCombat(), 0, 0);
                foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    check(renderer.sharedMaterials.Length == 1, "Stage retains one draw submesh");
                    var material = renderer.sharedMaterial;
                    check(material.GetTexture("_BaseMap") != null && material.GetFloat("_Surface") == 0, "Opaque palette survives stage tint");
                    check(material.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"), "Palette roughness survives runtime material conversion");
                }
            }
        }
        finally { visuals.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
        Directory.CreateDirectory("Library/BugArena");
        File.WriteAllText("Library/BugArena/art-optimization-checks.txt", "PASS: " + checks + " art optimization checks");
        Debug.Log("[BugArena] PASS: " + checks + " art optimization checks");
    }
}
