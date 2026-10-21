using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BugArena
{
    [DisallowMultipleComponent]
    public sealed class ArenaGame : MonoBehaviour
    {
        public ArenaCombat Combat { get; private set; }
        public bool Paused { get; private set; }
        public string Announcement { get; private set; } = "FIND YOUR DISTANCE";
        public string AnnouncementDetail { get; private set; } = "Release SPACE when the gold zone meets your rival";
        public float AnnouncementLife { get; private set; } = 3;
        public float ImpactFlash { get; private set; }
        public float LastDamage { get; private set; }
        public bool LastWasPlayer { get; private set; }
        public int PlayerWins { get; private set; }
        public int EnemyWins { get; private set; }
        public float RoundTime { get; private set; }
        public Camera ArenaCamera { get; private set; }
        public ArenaVisuals Visuals { get; private set; }
        float accumulator, hitStop, shake;
        bool counted;
        Vector3 cameraHome;
        AudioSource audioSource;
        AudioClip hitClip, launchClip, clashClip;
        readonly List<AudioClip> clips = new List<AudioClip>();

        void Awake()
        {
            Combat = new ArenaCombat();
            Paused = true;
            Visuals = new ArenaVisuals(transform);
            ArenaCamera = Visuals.BuildArena();
            cameraHome = ArenaCamera.transform.position;
            Visuals.BuildFighters();
            gameObject.AddComponent<ArenaHUD>();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            hitClip = Tone("Shell impact", 110, .12f);
            launchClip = Tone("Horn launcher", 320, .23f);
            clashClip = Tone("Shell clash", 620, .16f);
            Combat.Hit += OnHit;
        }
        AudioClip Tone(string name, float hz, float duration)
        {
            int sampleRate = 22050, count = Mathf.RoundToInt(duration * sampleRate);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate, envelope = Mathf.Pow(1 - i / (float)count, 2);
                samples[i] = (Mathf.Sin(2 * Mathf.PI * hz * t * (1 - t)) + .3f * Mathf.Sin(2 * Mathf.PI * hz * 2.3f * t)) * envelope * .23f;
            }
            var clip = AudioClip.Create(name, count, 1, sampleRate, false); clip.SetData(samples, 0); clips.Add(clip); return clip;
        }
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) Restart();
                if (keyboard.escapeKey.wasPressedThisFrame || (Paused && keyboard.enterKey.wasPressedThisFrame)) TogglePause();
            }
            float realDt = Mathf.Min(Time.unscaledDeltaTime, .08f);
            AnnouncementLife = Mathf.Max(0, AnnouncementLife - (Paused ? 0 : realDt));
            ImpactFlash = Mathf.Max(0, ImpactFlash - realDt * 3);
            if (!Paused)
            {
                if (Combat.Finished)
                {
                    if (!counted) { counted = true; if (Combat.Enemy.Health <= 0) PlayerWins++; else EnemyWins++; }
                }
                else
                {
                    RoundTime += realDt;
                    if (hitStop > 0) hitStop -= realDt;
                    else
                    {
                        float move = 0; bool held = false;
                        if (keyboard != null && Application.isFocused)
                        {
                            move = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                            held = keyboard.spaceKey.isPressed;
                        }
                        accumulator = Mathf.Min(accumulator + realDt, .1f);
                        while (accumulator >= 1f / 120)
                        {
                            Combat.Advance(1f / 120, new FightInput(move, held)); accumulator -= 1f / 120;
                            if (hitStop > 0 || Combat.Finished) { accumulator = 0; break; }
                        }
                    }
                }
            }
            shake = Mathf.MoveTowards(shake, 0, realDt * 2);
            ArenaCamera.transform.position = cameraHome + new Vector3(Mathf.Sin(Time.unscaledTime * 81), Mathf.Cos(Time.unscaledTime * 67), 0) * shake * .14f;
            Visuals.Update(Combat, Paused ? 0 : realDt, Time.unscaledTime);
        }
        void OnHit(Impact impact)
        {
            shake = impact.Finisher ? 1 : .4f; hitStop = impact.Finisher ? .095f : .045f;
            ImpactFlash = impact.Finisher ? .75f : .3f; LastDamage = impact.Damage; LastWasPlayer = impact.Attacker == Combat.Player;
            if (impact.Clash) { Announcement = "SHELL CLASH"; AnnouncementDetail = "Both attacks deflected. Reset your spacing."; }
            else if (impact.Finisher) { Announcement = LastWasPlayer ? "HORN LAUNCH!" : "CAUGHT AT THE EDGE"; AnnouncementDetail = "END-ZONE HIT  /  BONUS DAMAGE + LAUNCH"; }
            else if (impact.Counter) { Announcement = "CHARGE BREAK"; AnnouncementDetail = "Caught while charging  /  +4 damage"; }
            else { Announcement = LastWasPlayer ? "SHELL HIT" : "RIVAL HIT"; AnnouncementDetail = "Land the gold end-zone for a launcher"; }
            AnnouncementLife = impact.Finisher ? 1.6f : .95f;
            Visuals.Burst(new Vector3(impact.Target.X, impact.Target.Y + .8f, -.2f), impact.Finisher ? ArenaVisuals.Gold : Color.white, impact.Finisher ? 22 : 10);
            audioSource.PlayOneShot(impact.Clash ? clashClip : impact.Finisher ? launchClip : hitClip, .7f);
        }
        void OnApplicationFocus(bool focused) { if (!focused && Combat != null && !Combat.Finished) Paused = true; }
        public void TogglePause() { if (!Combat.Finished) { Paused = !Paused; accumulator = 0; } }
        public void Restart()
        {
            Combat.Reset(); Paused = false; accumulator = hitStop = 0; counted = false; RoundTime = 0;
            Announcement = "FIND YOUR DISTANCE"; AnnouncementDetail = "Release SPACE when the gold zone meets your rival"; AnnouncementLife = 2.5f;
        }
        void OnDestroy()
        {
            if (Combat != null) Combat.Hit -= OnHit;
            Visuals?.Dispose();
            foreach (var clip in clips) if (clip != null) Destroy(clip);
        }
    }
}
