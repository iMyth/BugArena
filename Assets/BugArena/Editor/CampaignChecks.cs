using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using BugArena;

public static class CampaignChecks
{
    [InitializeOnLoadMethod]
    static void RunRequestedChecks()
    {
        const string request = "Library/BugArena/request-checks";
        if (!File.Exists(request)) return;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { Run(); TouchChecks.Run(); ArtValidation.Run(); }
            catch (Exception error) { Debug.LogException(error); }
        };
    }
    [MenuItem("Bug Arena/Run Campaign Checks")]
    public static void Run()
    {
        int count = 0;
        Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); count++; };
        ArenaSetup.CheckCombat();
        foreach (var stage in ArenaCampaign.Stages)
        {
            var c = new ArenaCombat(); c.Configure(stage); c.AiEnabled = false;
            check(c.Enemy.Health == stage.EnemyHealth && c.Enemy.MaxHealth == stage.EnemyHealth, "Stage health");
            for (int i = 0; i < 600; i++) c.Advance(1f / 120, new FightInput(1, false));
            check(c.Player.X <= stage.Edge, "Movement respects stage boundary");
            c.Player.X = stage.Edge - .1f; c.Enemy.X = -4;
            for (int i = 0; i < 150; i++) c.Advance(1f / 120, new FightInput(0, true));
            c.Advance(1f / 120, new FightInput());
            check(c.Player.DashEnd >= -stage.Edge && c.Player.DashEnd <= stage.Edge, "Dash boundary");
            c.Configure(stage); c.Player.Health = 1; c.Enemy.Health = 1; c.Reset();
            check(c.Player.Health == 100 && c.Enemy.Health == stage.EnemyHealth && c.Player.State == BugState.Ready, "Retry resets state");
            c.AiEnabled = true;
            for (int i = 0; i < 2400; i++) c.Advance(1f / 120, new FightInput());
            check(c.Player.Health < 100, "Stage AI attacks idle player");
        }
        var root = new GameObject("Campaign flow check");
        bool hadUnlock = PlayerPrefs.HasKey("BugArena.UnlockedStage");
        int previousUnlock = PlayerPrefs.GetInt("BugArena.UnlockedStage", 0);
        PlayerPrefs.SetInt("BugArena.UnlockedStage", 0);
        ArenaGame game = null;
        try
        {
            game = root.AddComponent<ArenaGame>();
            // Awake is invoked explicitly in edit mode because this component is not ExecuteAlways.
            game.Initialize();
            game.SelectStage(2); check(game.StageIndex == 0, "Locked stage cannot be selected");
            for (int stage = 0; stage < ArenaCampaign.Stages.Length; stage++)
            {
                check(game.StageIndex == stage, "Next-stage progression");
                game.Combat.Enemy.Health = 0; game.Continue();
                check(!game.RoundFinished && game.Countdown == 2 && game.Combat.Player.Health == 100, "Round starts fresh");
            }
            check(game.StageIndex == 0, "Champion can replay cup");
            game.Combat.Player.Health = 0; game.Continue();
            check(game.StageIndex == 0, "Defeat retries same stage");
            game.Restart(); check(!game.Paused, "Retry starts countdown");
            game.TogglePause(); check(game.Paused, "Pause");
            game.TogglePause(); check(!game.Paused, "Resume");
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(ArenaGame).GetField("<Countdown>k__BackingField", flags).SetValue(game, 0f);
            typeof(ArenaGame).GetField("<RoundTime>k__BackingField", flags).SetValue(game, 75f);
            game.Combat.Player.Health = game.Combat.Enemy.Health = 50;
            typeof(ArenaGame).GetMethod("Update", flags).Invoke(game, null);
            check(game.TimedOut && game.RoundFinished && !game.Won, "Timeout resolves ties as defeat");
            game.Continue(); check(!game.TimedOut && game.RoundTime == 0, "Retry clears timer");
            game.SelectStage(2); game.Combat.Player.Health = 60; game.Combat.Enemy.Health = 70;
            check(game.Won, "Timeout compares health ratios for unequal maximum health");
        }
        finally
        {
            if (game != null)
            {
                typeof(ArenaGame).GetMethod("OnDestroy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(game, null);
                UnityEngine.Object.DestroyImmediate(game);
            }
            UnityEngine.Object.DestroyImmediate(root);
            if (hadUnlock) PlayerPrefs.SetInt("BugArena.UnlockedStage", previousUnlock);
            else PlayerPrefs.DeleteKey("BugArena.UnlockedStage");
            PlayerPrefs.Save();
        }
        Directory.CreateDirectory("Library/BugArena");
        File.WriteAllText("Library/BugArena/campaign-checks.txt", "PASS: " + count + " campaign checks plus 15 combat checks");
        Debug.Log("[BugArena] PASS: " + count + " campaign checks plus 15 combat checks");
    }
}
