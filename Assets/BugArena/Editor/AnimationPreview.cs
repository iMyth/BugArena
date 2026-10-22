using System;
using System.IO;
using System.Linq;
using BugArena;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class AnimationPreview
{
    [MenuItem("Bug Arena/Render Animation Preview")]
    public static void Render()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode before rendering animation preview.");
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        const string folder = "/Users/mythchow/Desktop/projects/BugArenaModels/Previews/UnityFrames";
        Directory.CreateDirectory(folder);
        var root = new GameObject("Animation integration check");
        var visuals = new ArenaVisuals(root.transform);
        bool batching = GraphicsSettings.useScriptableRenderPipelineBatching;
        var oldTarget = RenderTexture.active;
        var target = new RenderTexture(640, 480, 24);
        var image = new Texture2D(640, 480, TextureFormat.RGB24, false);
        try
        {
            GraphicsSettings.useScriptableRenderPipelineBatching = false;
            var camera = visuals.BuildArena(); visuals.BuildFighters(); visuals.SetStage(ArenaCampaign.Stages[0]);
            camera.orthographicSize = 1.75f; camera.targetTexture = target;
            var combat = new ArenaCombat { AiEnabled = false }; combat.Enemy.X = 8;
            var knee = root.GetComponentsInChildren<Transform>().First(t => t.name == "Knee_-1_0");
            visuals.Update(combat, 0, 0); var startRotation = knee.localRotation;
            float maxAngle = 0;
            foreach (var clip in new[] { "Walk", "Idle" })
            {
                int frames = clip == "Walk" ? 24 : 48;
                float dt = clip == "Walk" ? 1f / 30 : .05f;
                // Let the transition settle before capturing the idle loop.
                if (clip == "Idle")
                    for (int j = 0; j < 12; j++) { combat.Advance(1f / 60, new FightInput()); visuals.Update(combat, 1f / 60, j / 60f); }
                for (int i = 0; i < frames; i++)
                {
                    combat.Advance(dt, new FightInput(clip == "Walk" ? 1 : 0, false));
                    visuals.Update(combat, dt, i * dt);
                    if (clip == "Walk") maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(startRotation, knee.localRotation));
                    camera.transform.position = new Vector3(combat.Player.X + 1.5f, 2.1f, -5);
                    camera.transform.LookAt(new Vector3(combat.Player.X, .7f, 0));
                    camera.Render(); if (i == 0) camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 640, 480), 0, 0); image.Apply();
                    File.WriteAllBytes(folder + "/" + clip + "_" + i.ToString("000") + ".png", image.EncodeToPNG());
                }
            }
            if (maxAngle < 10) throw new Exception("Gameplay movement did not drive the imported knee animation");
            var paused = knee.localRotation; visuals.Update(combat, 0, 5);
            if (Quaternion.Angle(paused, knee.localRotation) > .001f) throw new Exception("Paused gameplay altered skeleton pose");
            File.WriteAllText("Library/BugArena/animation-integration.txt", "PASS: gameplay movement drives knee (max " + maxAngle.ToString("F1") + " degrees); pause freezes pose; Unity frames rendered");
            Debug.Log("[BugArena] PASS: gameplay skeleton integration, max knee angle " + maxAngle);
        }
        finally
        {
            RenderTexture.active = oldTarget; GraphicsSettings.useScriptableRenderPipelineBatching = batching;
            visuals.Dispose(); UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
