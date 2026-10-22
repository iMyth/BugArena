using System.IO;
using UnityEditor;
using UnityEngine;
using BugArena;

[InitializeOnLoad]
public static class TouchPreview
{
    static int frames;
    static TouchPreview() { EditorApplication.update += Update; }
    [MenuItem("Bug Arena/Preview Touch Controls")]
    public static void Start()
    {
        if (!EditorApplication.isPlaying)
        {
            SessionState.SetBool("BugArena.TouchPreview", true);
            EditorApplication.isPlaying = true;
        }
        else
        {
            var game = Object.FindAnyObjectByType<ArenaGame>();
            if (game != null) game.Touch.PreviewControls = !game.Touch.PreviewControls;
        }
    }
    static void Update()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool("BugArena.TouchPreview", false)) return;
        var game = Object.FindAnyObjectByType<ArenaGame>();
        if (game == null) return;
        game.Touch.PreviewControls = true;
        // Pause AI so the screenshot captures a stable ready-to-play state.
        game.Combat.AiEnabled = false;
        if (game.Paused) game.TogglePause();
        // Wait for real shader variants rather than saving Unity's cyan compile placeholders.
        if (ShaderUtil.anythingCompiling) { frames = 0; return; }
        if (game.Countdown > 0 || ++frames < 30) return;
        SessionState.SetBool("BugArena.TouchPreview", false); frames = 0;
        game.Combat.AiEnabled = true;
        Directory.CreateDirectory("Library/BugArena");
        ScreenCapture.CaptureScreenshot("Library/BugArena/mobile-gameplay.png");
        Debug.Log("[BugArena] Touch preview ready; screenshot requested.");
    }
}
