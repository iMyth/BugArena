using System;
using System.IO;
using BugArena;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class TouchChecks
{
    [MenuItem("Bug Arena/Run Touch Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Run touch checks outside Play Mode.");
        var device = InputSystem.AddDevice<Touchscreen>();
        var root = new GameObject("Touch check"); var controls = root.AddComponent<ArenaTouchInput>();
        int passed = 0;
        Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); passed++; };
        Action<int, Vector2, UnityEngine.InputSystem.TouchPhase> touch = (id, point, phase) =>
        {
            var viewport = ArenaTouchInput.Viewport;
            var pos = viewport.position + point * viewport.width / 1440;
            InputSystem.QueueStateEvent(device, new TouchState { touchId = id, position = new Vector2(pos.x, Screen.height - pos.y), phase = phase });
            InputSystem.Update(); controls.Sample(true);
        };
        try
        {
            touch(1, ArenaTouchInput.Right.center, UnityEngine.InputSystem.TouchPhase.Began);
            check(controls.Move == 1 && !controls.Held, "Right finger moves");
            touch(2, ArenaTouchInput.Charge.center, UnityEngine.InputSystem.TouchPhase.Began);
            check(controls.Move == 1 && controls.Held, "Two fingers move and charge together");
            var combat = new ArenaCombat { AiEnabled = false };
            for (int i = 0; i < 80; i++) combat.Advance(1f / 120, new FightInput(controls.Move, controls.Held));
            check(combat.Player.State == BugState.Charging, "Touch starts combat charge");
            touch(2, ArenaTouchInput.Charge.center, UnityEngine.InputSystem.TouchPhase.Ended);
            combat.Advance(1f / 120, new FightInput(controls.Move, controls.Held));
            check(!controls.Held && controls.Move == 1 && combat.Player.State == BugState.Dashing, "Charge release dashes while movement finger remains held");
            touch(1, new Vector2(600, 500), UnityEngine.InputSystem.TouchPhase.Moved);
            check(controls.Move == 0, "Dragging off releases movement");
            touch(1, new Vector2(600, 500), UnityEngine.InputSystem.TouchPhase.Canceled);
            touch(3, ArenaTouchInput.Left.center, UnityEngine.InputSystem.TouchPhase.Began);
            check(controls.Move == -1, "Left movement");
            touch(3, ArenaTouchInput.Left.center, UnityEngine.InputSystem.TouchPhase.Canceled);
            check(controls.Move == 0 && !controls.Held, "Canceled touch releases controls");
            touch(4, ArenaTouchInput.PrimaryAction.center, UnityEngine.InputSystem.TouchPhase.Began);
            check(controls.PressedPoint.HasValue && ArenaTouchInput.PrimaryAction.Contains(controls.PressedPoint.Value), "Menu tap mapping");
            controls.Sample(false); check(!controls.Held && controls.Move == 0 && !controls.PressedPoint.HasValue, "Focus cleanup");
            touch(5, ArenaTouchInput.Super.center, UnityEngine.InputSystem.TouchPhase.Began);
            check(controls.SuperHeld && !controls.Held, "Separate super control");
            combat.Reset();
            for (int i = 0; i < 160; i++) combat.Advance(1f / 120, new FightInput(0, controls.Held, controls.SuperHeld));
            touch(5, ArenaTouchInput.Super.center, UnityEngine.InputSystem.TouchPhase.Ended);
            combat.Advance(1f / 120, new FightInput(0, controls.Held, controls.SuperHeld));
            check(combat.Orb != null && !controls.SuperHeld, "Touch full charge release fires super");
            touch(6, ArenaTouchInput.Super.center, UnityEngine.InputSystem.TouchPhase.Began);
            controls.Sample(false); check(!controls.SuperHeld, "Focus clears super touch");
            Directory.CreateDirectory("Library/BugArena");
            File.WriteAllText("Library/BugArena/touch-checks.txt", "PASS: " + passed + " synthetic touchscreen checks");
            Debug.Log("[BugArena] PASS: " + passed + " synthetic touchscreen checks");
        }
        finally { InputSystem.RemoveDevice(device); UnityEngine.Object.DestroyImmediate(root); }
    }
}
