using System;
using System.IO;
using BugArena;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ArenaSetup
{
    public const string ScenePath = "Assets/BugArena/Scenes/BugArena.unity";
    static ArenaSetup() { EditorApplication.delayCall += EnsureScene; }
    static void EnsureScene()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += EnsureScene; return; }
        if (!File.Exists(ScenePath)) CreateScene();
    }
    [MenuItem("Bug Arena/Open Arena Scene")]
    public static void OpenScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(ScenePath)) CreateScene();
        EditorSceneManager.OpenScene(ScenePath);
    }
    [MenuItem("Bug Arena/Create Arena Scene")]
    public static void CreateScene()
    {
        if (File.Exists(ScenePath)) return;
        Directory.CreateDirectory("Assets/BugArena/Scenes"); AssetDatabase.Refresh();
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        new GameObject("Bug Arena / Runtime").AddComponent<ArenaGame>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.productName = "Bug Arena";
        PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.runInBackground = false;
        AssetDatabase.SaveAssets();
        if (previous.IsValid() && !previous.isDirty && (previous.name == "SampleScene" || string.IsNullOrEmpty(previous.path)))
            EditorSceneManager.CloseScene(previous, true);
        else if (previous.IsValid()) { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        Debug.Log("[BugArena] Arena scene created. Open Bug Arena > Open Arena Scene and press Play.");
    }
    [MenuItem("Bug Arena/Capture Game Screenshot")]
    public static void CaptureScreenshot()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("Enter Play Mode before capturing."); return; }
        Directory.CreateDirectory("Library/BugArena");
        ScreenCapture.CaptureScreenshot("Library/BugArena/gameplay.png");
        Debug.Log("[BugArena] Screenshot requested: Library/BugArena/gameplay.png");
    }
    [MenuItem("Bug Arena/Run Combat Checks")]
    public static void CheckCombat()
    {
        int passed = 0;
        Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception("[BugArena] FAILED: " + message); passed++; };
        check(CombatRules.Distance(0) < CombatRules.Distance(CombatRules.MaxCharge), "Charge increases dash distance");
        check(Mathf.Approximately(CombatRules.Distance(99), CombatRules.Distance(CombatRules.MaxCharge)), "Charge is capped");
        check(CombatRules.Endpoint(7, 1, 2) == CombatRules.RingEdge, "Dash respects ring edge");
        var c = new ArenaCombat { AiEnabled = false };
        Action<float, FightInput> tick = (seconds, input) => { for (int i = 0; i < Mathf.CeilToInt(seconds * 120); i++) c.Advance(1f / 120, input); };
        c.Player.X = -4; c.Enemy.X = 2;
        tick(.8f, new FightInput(0, true));
        check(c.Player.State == BugState.Charging && c.Enemy.Health == 100, "Holding charges without attacking");
        tick(.38f, new FightInput(0, false));
        check(c.Finishers == 1 && c.Enemy.Health < 75 && c.Enemy.Y > 0, "Endpoint hit deals bonus damage and launches");
        c.Reset(); c.Player.X = -4; c.Enemy.X = -1.6f;
        tick(.8f, new FightInput(0, true)); tick(.4f, new FightInput());
        check(c.Hits == 1 && c.Finishers == 0 && c.Enemy.Health > 80 && c.Enemy.Y == 0, "Early hit is weaker and grounded");
        c.Reset(); c.Player.X = -6; c.Enemy.X = 6;
        tick(.1f, new FightInput(0, true)); tick(.3f, new FightInput());
        check(c.Player.State == BugState.Recovering, "Missed dash has a recovery window");
        tick(.7f, new FightInput()); check(c.Player.State == BugState.Ready, "Recovery ends");
        c.Reset(); c.Player.X = 4; c.Enemy.X = -2;
        tick(.8f, new FightInput(0, true)); tick(.38f, new FightInput());
        check(c.Finishers == 1 && c.Player.Facing == -1, "Left-facing launcher is symmetric");
        tick(2, new FightInput()); check(c.Enemy.Y == 0 && c.Enemy.State == BugState.Ready, "Launched fighter lands and recovers");
        c.Reset(); c.Player.Health = 0; float enemyX = c.Enemy.X;
        tick(1, new FightInput(1, true)); check(c.Finished && c.Enemy.X == enemyX, "Finished match stops simulation");
        c.Reset(); check(!c.Finished && c.Player.Health == 100 && c.Finishers == 0, "Rematch resets combat state");
        c.AiEnabled = true;
        tick(12, new FightInput()); check(c.Player.Health < 100, "AI approaches, charges, and attacks");
        c.Reset(); c.AiEnabled = false; c.Player.X = -2; c.Enemy.X = 2;
        c.Enemy.State = BugState.Charging; c.Enemy.Charge = .4f;
        c.Player.State = BugState.Charging; c.Player.Charge = .4f;
        tick(.25f, new FightInput());
        check(c.Player.Health == 100 && c.Enemy.Health == 100 && c.Player.State == BugState.Recovering, "Opposing dashes clash without damage");
        c.Reset(); c.AiEnabled = true; c.Enemy.X = -1; c.Player.X = -4;
        c.Player.State = BugState.Dashing; c.Player.DashStart = -4; c.Player.DashEnd = -1; c.Player.DashDuration = .2f;
        c.Enemy.State = BugState.Charging;
        bool counterHit = false; c.Hit += impact => { if (impact.Counter) counterHit = true; };
        tick(.12f, new FightInput()); check(counterHit && c.Enemy.Health < 100, "Hitting a charging rival breaks charge and earns a counter bonus");
        string result = "[BugArena] PASS: " + passed + " combat checks";
        Directory.CreateDirectory("Library/BugArena"); File.WriteAllText("Library/BugArena/combat-checks.txt", result);
        Debug.Log(result);
    }
}
