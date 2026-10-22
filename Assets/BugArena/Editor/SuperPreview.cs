using System.IO;
using BugArena;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SuperPreview
{
    [MenuItem("Bug Arena/Render Super Preview")]
    public static void Render()
    {
        var root = new GameObject("Super preview");
        var visuals = new ArenaVisuals(root.transform);
        bool batching = GraphicsSettings.useScriptableRenderPipelineBatching;
        bool asyncCompile = ShaderUtil.allowAsyncCompilation;
        var oldTarget = RenderTexture.active;
        var target = new RenderTexture(1440, 900, 24);
        var image = new Texture2D(1440, 900, TextureFormat.RGB24, false);
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            GraphicsSettings.useScriptableRenderPipelineBatching = false;
            var camera = visuals.BuildArena(); visuals.BuildFighters(); visuals.SetStage(ArenaCampaign.Stages[0]);
            camera.targetTexture = target; camera.orthographicSize = 9.1f;
            var c = new ArenaCombat { AiEnabled = false }; c.Configure(ArenaCampaign.Stages[0]);
            for (int i = 0; i < 160; i++) c.Advance(1f / 120, new FightInput(0, false, true));
            c.Advance(1f / 120, new FightInput());
            Directory.CreateDirectory("Library/BugArena");
            foreach (int returns in new[] { 0, 1, 3, 8 })
            {
                while (c.Orb.Reflections < returns) c.Orb.Reflect(c.Orb.Target, c.Orb.Owner);
                c.Orb.Position = new Vector2(0, 1.2f);
                visuals.Update(c, .2f, 1);
                camera.Render(); camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); image.Apply();
                File.WriteAllBytes("Library/BugArena/super-" + returns + ".png", image.EncodeToPNG());
            }
            Debug.Log("[BugArena] Super previews rendered: 0, 1, 3, 8 returns");
        }
        finally
        {
            RenderTexture.active = oldTarget;
            GraphicsSettings.useScriptableRenderPipelineBatching = batching;
            ShaderUtil.allowAsyncCompilation = asyncCompile;
            visuals.Dispose(); Object.DestroyImmediate(root);
            Object.DestroyImmediate(image); Object.DestroyImmediate(target);
        }
    }
}
