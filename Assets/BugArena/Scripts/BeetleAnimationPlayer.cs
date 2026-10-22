using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BugArena
{
    // Blender-authored skeletal clips; manually sampled so pause/hit stop really freeze pose.
    public sealed class BeetleAnimationPlayer : IDisposable
    {
        // Must match the Blender stance travel: .60m over half a cycle.
        public const float WalkCycleDistance = 1.20f;
        readonly PlayableGraph graph;
        readonly AnimationMixerPlayable mixer;
        readonly AnimationClipPlayable idle, walk;
        readonly float idleLength, walkLength;
        readonly Transform supportRoot;
        readonly Vector3 supportRestPosition;
        float idleTime, walkTime, walkWeight;
        public float WalkWeight => walkWeight;

        public BeetleAnimationPlayer(GameObject model, AnimationClip idleClip, AnimationClip walkClip)
        {
            var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var bone in model.GetComponentsInChildren<Transform>())
                if (bone.name == "Root") { supportRoot = bone; break; }
            if (supportRoot != null) supportRestPosition = supportRoot.localPosition;
            graph = PlayableGraph.Create(model.name + " / Idle + Walk");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            idle = AnimationClipPlayable.Create(graph, idleClip);
            walk = AnimationClipPlayable.Create(graph, walkClip);
            idle.SetApplyFootIK(false); walk.SetApplyFootIK(false);
            idleLength = idleClip.length; walkLength = walkClip.length;
            mixer = AnimationMixerPlayable.Create(graph, 2);
            graph.Connect(idle, 0, mixer, 0); graph.Connect(walk, 0, mixer, 1);
            AnimationPlayableOutput.Create(graph, "Beetle skeleton", animator).SetSourcePlayable(mixer);
            graph.Play(); Reset();
        }
        public void Reset()
        {
            if (supportRoot != null) supportRoot.localPosition = supportRestPosition;
            idleTime = walkTime = walkWeight = 0;
            idle.SetTime(0); walk.SetTime(0);
            mixer.SetInputWeight(0, 1); mixer.SetInputWeight(1, 0);
            graph.Evaluate(0);
        }
        public void Tick(float dt, float localSpeed, bool canWalk, bool animate)
        {
            if (dt <= 0 || !animate) return;
            float target = canWalk && Mathf.Abs(localSpeed) > .1f ? 1 : 0;
            // Movement starts immediately in combat. A long fade makes the beetle
            // travel half a metre before its legs catch up at the normal 3.8m/s.
            walkWeight = Mathf.MoveTowards(walkWeight, target, dt / .04f);
            idleTime = Mathf.Repeat(idleTime + dt, idleLength);
            // Advance by travelled distance, so a longer readable stride keeps planted feet still.
            if (Mathf.Abs(localSpeed) > .1f && canWalk)
                walkTime = Mathf.Repeat(walkTime + dt * localSpeed * walkLength / WalkCycleDistance, walkLength);
            idle.SetTime(idleTime); walk.SetTime(walkTime);
            mixer.SetInputWeight(0, 1 - walkWeight); mixer.SetInputWeight(1, walkWeight);
            if (supportRoot != null) supportRoot.localPosition = supportRestPosition;
            graph.Evaluate(0);
            // Rotation blending bends planted knees below either endpoint pose.
            // A small transition-only support arc preserves sole clearance without
            // per-frame mesh baking or extra IK bones on mobile.
            if (supportRoot != null)
                supportRoot.position += Vector3.up * (Mathf.Sin(walkWeight * Mathf.PI) * .02f);
        }
        public void Dispose() { if (graph.IsValid()) graph.Destroy(); }
    }
}
