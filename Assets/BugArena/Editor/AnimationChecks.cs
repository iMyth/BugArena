using System;
using System.IO;
using System.Linq;
using BugArena;
using UnityEditor;
using UnityEngine;

public static class AnimationChecks
{
    [InitializeOnLoadMethod]
    static void Requested()
    {
        const string path = "Library/BugArena/request-animation-checks";
        if (!File.Exists(path)) return;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(path)) return;
            File.Delete(path);
            try { Run(); AnimationPreview.Render(); }
            catch (Exception error) { Debug.LogException(error); }
        };
    }
    [MenuItem("Bug Arena/Run Animation Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode before checking imported clips.");
        int count = 0;
        Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); count++; };
        foreach (var name in new[] { "JadeBeetle", "EmberBeetle" })
        {
            var asset = Resources.Load<GameObject>("BugArenaArt/" + name);
            var clips = Resources.LoadAll<AnimationClip>("BugArenaArt/" + name);
            Debug.Log("[BugArena Animation] " + name + " clips: " + string.Join(", ", clips.Select(c => c.name + " " + c.length)));
            var idle = clips.Single(c => c.name == "Idle"); var walk = clips.Single(c => c.name == "Walk");
            check(idle.isLooping && walk.isLooping, "Clips loop");
            check(Mathf.Abs(walk.length - .8f) < .05f && Mathf.Abs(idle.length - 2.4f) < .05f, "Clip durations");
            var model = UnityEngine.Object.Instantiate(asset);
            BeetleAnimationPlayer player = null;
            var bake = new Mesh();
            try
            {
                var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                Debug.Log("[BugArena Animation] skin bones: " + (skin != null ? skin.bones.Length : 0));
                check(skin != null && skin.bones.Length >= 15 && skin.bones.Length <= 16, "One skinned mesh with lightweight beetle skeleton");
                var antenna = model.GetComponentsInChildren<Transform>().First(t => t.name == "Antenna_1");
                var knee = model.GetComponentsInChildren<Transform>().First(t => t.name == "Knee_-1_0");
                model.transform.localScale = new Vector3(-1, 1, 1);
                player = new BeetleAnimationPlayer(model, idle, walk);
                check(model.transform.localScale.x == -1, "Animation preserves game facing correction");
                var restAntenna = antenna.localRotation;
                skin.BakeMesh(bake); var rest = bake.vertices;
                player.Tick(.6f, 0, true, true);
                Debug.Log("[BugArena Animation] Idle antenna delta: " + Quaternion.Angle(restAntenna, antenna.localRotation));
                check(Quaternion.Angle(restAntenna, antenna.localRotation) > 2, "Idle antenna animation actually plays");
                player.Reset(); var restKnee = knee.localRotation;
                player.Tick(.2f, 1.2f, true, true);
                skin.BakeMesh(bake); var forward = bake.vertices;
                check(Quaternion.Angle(restKnee, knee.localRotation) > 5, "Walk flexes knee");
                check(forward.Zip(rest, (a,b) => Vector3.Distance(a,b)).Max() > .08f, "Walk visibly deforms mesh");
                player.Tick(0, 0, false, true); skin.BakeMesh(bake);
                check(bake.vertices.Zip(forward, (a,b) => Vector3.Distance(a,b)).Max() < .0001f, "Pause freezes pose");
                player.Reset(); player.Tick(.2f, -1.2f, true, true); skin.BakeMesh(bake);
                check(bake.vertices.Zip(forward, (a,b) => Vector3.Distance(a,b)).Max() > .08f, "Backward movement reverses stride");
                player.Tick(.2f, 0, true, true); check(player.WalkWeight == 0, "Stopping blends back to idle");
                var frozen = knee.localRotation; player.Tick(.3f, 2, true, false);
                check(Quaternion.Angle(frozen, knee.localRotation) < .001f, "Airborne/defeat pose does not keep walking");
                player.Reset(); check(player.WalkWeight == 0, "Stage reset clears walk state");
                player.Tick(.04f, 3.8f, true, true);
                check(player.WalkWeight == 1, "Full-speed movement reaches the walk pose within 40ms");
                var cyclePose = knee.localRotation;
                player.Tick(BeetleAnimationPlayer.WalkCycleDistance / 3.8f, 3.8f, true, true);
                check(Quaternion.Angle(cyclePose, knee.localRotation) < .05f,
                    "One authored stride distance completes exactly one cycle at gameplay speed");
                player.Reset(); skin.BakeMesh(bake, true);
                var resting = bake.vertices.Select(v => skin.transform.TransformPoint(v)).ToArray();
                int footBone = Array.FindIndex(skin.bones, b => b.name == "Knee_-1_0");
                var weights = skin.sharedMesh.boneWeights;
                var footIndices = Enumerable.Range(0, resting.Length).Where(i => resting[i].y < .15f &&
                    weights[i].boneIndex0 == footBone && weights[i].weight0 > .99f).ToArray();
                player.Tick(.2f, BeetleAnimationPlayer.WalkCycleDistance / walk.length, true, true);
                skin.BakeMesh(bake, true);
                check(footIndices.Length > 5 && footIndices.Min(i => skin.transform.TransformPoint(bake.vertices[i]).y) > .2f,
                    "Swinging foot visibly clears the ground by at least 20cm");
                foreach (float speed in new[] { BeetleAnimationPlayer.WalkCycleDistance / walk.length, -BeetleAnimationPlayer.WalkCycleDistance / walk.length })
                {
                    player.Reset(); player.Tick(.5f, speed, true, true); skin.BakeMesh(bake, true);
                    var planted = bake.vertices.Select(v => skin.transform.TransformPoint(v)).ToArray();
                    player.Tick(.05f, speed, true, true); skin.BakeMesh(bake, true);
                    var moved = bake.vertices.Select(v => skin.transform.TransformPoint(v)).ToArray();
                    var drift = planted.Zip(moved, (a,b) => new { a, b })
                        .Where(v => v.a.y < .055f && v.b.y < .055f)
                        .Select(v => Mathf.Abs(v.b.x - v.a.x + speed * .05f)).OrderBy(v => v).ToArray();
                    check(drift.Length > 20 && drift[drift.Length / 2] < .01f,
                        "Planted soles follow ground within 1cm in either direction");
                }
                Quaternion reference = Quaternion.identity;
                foreach (int fps in new[] { 30, 60, 120 })
                {
                    player.Reset();
                    for (int frame = 0; frame < fps; frame++) player.Tick(1f / fps, 3.8f, true, true);
                    if (fps == 30) reference = knee.localRotation;
                    else check(Quaternion.Angle(reference, knee.localRotation) < .05f,
                        "Same travelled distance produces the same gait at 30/60/120fps");
                }
                foreach (int fps in new[] { 240, 1000 })
                {
                    player.Reset();
                    var combat = new ArenaCombat { AiEnabled = false };
                    float accumulator = 0, dt = 1f / fps;
                    bool stayedWalking = true;
                    for (int frame = 0; frame < fps / 2; frame++)
                    {
                        float before = combat.Player.X;
                        accumulator += dt;
                        while (accumulator >= 1f / 120)
                        {
                            combat.Advance(1f / 120, new FightInput(1, false));
                            accumulator -= 1f / 120;
                        }
                        player.Tick(dt, (combat.Player.X - before) / dt, true, true);
                        if (frame * dt > .08f && player.WalkWeight < .99f) stayedWalking = false;
                    }
                    check(stayedWalking, "120Hz combat must retain Walk between simulation steps at " + fps + "fps");
                    player.Tick(.08f, 0, true, true);
                    check(player.WalkWeight == 0, "A real stop still returns to idle promptly at " + fps + "fps");
                }
            }
            finally { player?.Dispose(); UnityEngine.Object.DestroyImmediate(bake); UnityEngine.Object.DestroyImmediate(model); }
        }
        ArenaSetup.CheckCombat();
        Directory.CreateDirectory("Library/BugArena");
        File.WriteAllText("Library/BugArena/animation-checks.txt", "PASS: " + count + " animation checks plus 15 combat checks");
        Debug.Log("[BugArena] PASS: " + count + " animation checks plus 15 combat checks");
    }
}
