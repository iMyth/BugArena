using System.IO;
using UnityEditor;
using UnityEngine;
using BugArena;

public static class ArtValidation
{
    [MenuItem("Bug Arena/Validate Imported Art")]
    public static void Run()
    {
        foreach (var name in new[] { "JadeBeetle", "EmberBeetle", "MosswoodArena" })
        {
            var asset = Resources.Load<GameObject>("BugArenaArt/" + name);
            if (asset == null) throw new System.Exception("Missing FBX: " + name);
            int triangles = 0, legs = 0;
            foreach (var filter in asset.GetComponentsInChildren<MeshFilter>()) triangles += filter.sharedMesh.triangles.Length / 3;
            foreach (var skin in asset.GetComponentsInChildren<SkinnedMeshRenderer>()) triangles += skin.sharedMesh.triangles.Length / 3;
            foreach (var t in asset.GetComponentsInChildren<Transform>()) if (t.name.StartsWith("Leg_")) legs++;
            if (name != "MosswoodArena" && legs != 6) throw new System.Exception("Expected six animated legs: " + name);
            Debug.Log($"[BugArena Art] {name}: {triangles} triangles, {legs} legs");
        }
        ArenaSetup.CheckCombat();
        bool previousBatching = UnityEngine.Rendering.GraphicsSettings.useScriptableRenderPipelineBatching;
        var root = new GameObject("Art smoke check");
        var visuals = new ArenaVisuals(root.transform);
        try
        {
            var camera = visuals.BuildArena(); visuals.BuildFighters();
            var combat = new ArenaCombat(); visuals.Update(combat, .016f, 0);
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                var target = new RenderTexture(1400, 850, 24);
                camera.targetTexture = target;
                UnityEngine.Rendering.GraphicsSettings.useScriptableRenderPipelineBatching = false;
                camera.Render(); camera.Render();
                var old = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(1400, 850, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1400, 850), 0, 0); image.Apply();
                File.WriteAllBytes("/Users/mythchow/Desktop/projects/BugArenaModels/Unity-preview.png", image.EncodeToPNG());
                camera.targetTexture = null; RenderTexture.active = old;
                Object.DestroyImmediate(image); Object.DestroyImmediate(target);
            }
            if (root.GetComponentsInChildren<MeshRenderer>().Length == 0) throw new System.Exception("No renderers");
            Directory.CreateDirectory("Library/BugArena");
            File.WriteAllText("Library/BugArena/art-checks.txt", "PASS: three imported FBX assets, six legs per fighter, scene construction and animation update");
        }
        finally
        {
            UnityEngine.Rendering.GraphicsSettings.useScriptableRenderPipelineBatching = previousBatching;
            visuals.Dispose(); Object.DestroyImmediate(root);
        }
    }
}
