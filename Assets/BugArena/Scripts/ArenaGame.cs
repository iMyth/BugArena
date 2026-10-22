using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BugArena
{
    [DisallowMultipleComponent]
    public sealed class ArenaGame : MonoBehaviour
    {
        public ArenaCombat Combat { get; private set; }
        public readonly List<UpgradeKind> Upgrades = new List<UpgradeKind>();
        public ArenaUpgrade[] Offers { get; private set; }
        public bool ChoosingUpgrade => Offers != null;
        System.Random upgradeRandom;
        public int RunSeed { get; private set; }
        public int Assistance { get; private set; }
        public int DefeatStreak { get; private set; }
        ArenaStage[] runStages;
        void GenerateRun()
        {
            RunSeed = System.Guid.NewGuid().GetHashCode(); Assistance = Mathf.Clamp(DefeatStreak - 1, 0, 2);
            runStages = ArenaCampaign.Generate(RunSeed, Assistance);
            upgradeRandom = new System.Random(RunSeed ^ 0x5A17);
        }
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
        public ArenaTouchInput Touch { get; private set; }
        public int StageIndex { get; private set; }
        public int UnlockedStage { get; private set; }
        public ArenaStage Stage => runStages[StageIndex];
        public ArenaStage NextStage => runStages[Mathf.Min(StageIndex + 1, ArenaCampaign.RunLength - 1)];
        public bool TimedOut { get; private set; }
        public bool RoundFinished => Combat.Finished || TimedOut;
        public bool Won => Stage.IsBoss
            ? Combat.Enemy.Health <= 0 && Combat.Player.Health > 0
            : Combat.Player.Health / Combat.Player.MaxHealth > Combat.Enemy.Health / Combat.Enemy.MaxHealth;
        public bool CampaignComplete => RoundFinished && Won && StageIndex == ArenaCampaign.RunLength - 1;
        public float TimeRemaining => Mathf.Max(0, Stage.TimeLimit - RoundTime);
        public float Countdown { get; private set; } = 2;
        float accumulator, hitStop, shake;
        bool counted, bossAnnounced;
        Vector3 cameraHome;
        AudioSource audioSource;
        AudioClip hitClip, launchClip, clashClip;
        readonly List<AudioClip> clips = new List<AudioClip>();

        void Awake() { Initialize(); }
        public void Initialize()
        {
            if (Combat != null) return;
            GenerateRun();
            Combat = new ArenaCombat();
            Combat.Configure(Stage);
            UnlockedStage = Mathf.Clamp(PlayerPrefs.GetInt("BugArena.UnlockedStage", 0), 0, ArenaCampaign.RunLength - 1);
            Touch = gameObject.AddComponent<ArenaTouchInput>();
            if (Application.isMobilePlatform)
            {
                Screen.orientation = ScreenOrientation.AutoRotation;
                Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
            }
            Paused = true;
            Visuals = new ArenaVisuals(transform);
            ArenaCamera = Visuals.BuildArena();
            cameraHome = ArenaCamera.transform.position;
            Visuals.BuildFighters();
            Visuals.SetStage(Stage);
            gameObject.AddComponent<ArenaHUD>();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            hitClip = Tone("Shell impact", 110, .12f);
            launchClip = Tone("Horn launcher", 320, .23f);
            clashClip = Tone("Shell clash", 620, .16f);
            Combat.Hit += OnHit; Combat.OrbChanged += OnOrb;
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
            Touch.Sample();
            if (Touch.PressedPoint.HasValue)
            {
                var point = Touch.PressedPoint.Value;
                if (ChoosingUpgrade)
                { for (int i = 0; i < 3; i++) if (ArenaTouchInput.UpgradeCard(i).Contains(point)) { ChooseUpgrade(i); break; } }
                else if ((Paused || RoundFinished) && ArenaTouchInput.PrimaryAction.Contains(point))
                { if (RoundFinished) Continue(); else TogglePause(); }
                else if (!RoundFinished && ArenaTouchInput.PauseAction.Contains(point)) TogglePause();
                else if (Paused && RoundTime == 0)
                    for (int i = 0; i < ArenaCampaign.RunLength; i++)
                        if (new Rect(245 + (i % 5) * 190, 625 + (i / 5) * 50, 180, 43).Contains(point)) SelectStage(i);
            }
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) RestartRun();
                if (ChoosingUpgrade)
                {
                    if (keyboard.digit1Key.wasPressedThisFrame) ChooseUpgrade(0);
                    else if (keyboard.digit2Key.wasPressedThisFrame) ChooseUpgrade(1);
                    else if (keyboard.digit3Key.wasPressedThisFrame) ChooseUpgrade(2);
                }
                if (RoundFinished && keyboard.enterKey.wasPressedThisFrame) Continue();
                if (keyboard.escapeKey.wasPressedThisFrame || (Paused && keyboard.enterKey.wasPressedThisFrame)) TogglePause();
            }
            float realDt = Mathf.Min(Time.unscaledDeltaTime, .08f);
            AnnouncementLife = Mathf.Max(0, AnnouncementLife - (Paused || RoundFinished || Countdown > 0 ? 0 : realDt));
            ImpactFlash = Mathf.Max(0, ImpactFlash - realDt * 3);
            if (!Paused)
            {
                if (RoundFinished)
                {
                    RecordResult();
                }
                else if (Countdown > 0) Countdown = Mathf.Max(0, Countdown - realDt);
                else
                {
                    RoundTime += realDt;
                    if (RoundTime >= Stage.TimeLimit) { TimedOut = true; accumulator = 0; }
                    else if (hitStop > 0) hitStop -= realDt;
                    else
                    {
                        float move = Touch.Move; bool held = Touch.Held, super = Touch.SuperHeld;
                        if (keyboard != null && Application.isFocused)
                        {
                            move += (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                            held |= keyboard.spaceKey.isPressed; super |= keyboard.qKey.isPressed;
                        }
                        accumulator = Mathf.Min(accumulator + realDt, .1f);
                        while (accumulator >= 1f / 120)
                        {
                            Combat.Advance(1f / 120, new FightInput(Mathf.Clamp(move, -1, 1), held, super)); accumulator -= 1f / 120;
                            if (hitStop > 0 || Combat.Finished) { accumulator = 0; break; }
                        }
                    }
                }
            }
            RecordResult();
            if (Combat.Enemy.Enraged && !bossAnnounced)
            {
                bossAnnounced = true; Announcement = Stage.Rival + " ENRAGED";
                AnnouncementDetail = "FASTER SUPER / STILL VULNERABLE DURING RECOVERY"; AnnouncementLife = 2;
                Visuals.Burst(new Vector3(Combat.Enemy.X, 1, 0), ArenaVisuals.Coral, 24);
            }
            ArenaCamera.orthographicSize = Mathf.Max(6.6f, 14.5f / Mathf.Max(.7f, ArenaCamera.aspect));
            shake = Mathf.MoveTowards(shake, 0, realDt * 2);
            ArenaCamera.transform.position = cameraHome + new Vector3(Mathf.Sin(Time.unscaledTime * 81), Mathf.Cos(Time.unscaledTime * 67), 0) * shake * .14f;
            Visuals.Update(Combat, Paused || RoundFinished || Countdown > 0 ? 0 : realDt, Time.unscaledTime);
        }
        void OnOrb(OrbEvent e)
        {
            Announcement = e.Reflected ? "RETURN!  " + e.Damage + " DAMAGE" : "SUPER ORB!";
            AnnouncementDetail = e.Reflected ? "DOUBLE DAMAGE / ONLY A DASH END-ZONE CAN RETURN IT" : "Watch the ball. Charge a dash to meet it at the end.";
            AnnouncementLife = 1.8f; shake = e.Reflected ? .8f : .35f;
            Visuals.Burst(new Vector3(e.Position.x, e.Position.y, 0), e.Reflected ? ArenaVisuals.Gold : ArenaVisuals.Teal, e.Reflected ? 28 : 16);
            audioSource.pitch = e.Reflected ? 1.3f : .8f;
            audioSource.PlayOneShot(launchClip, .8f);
        }
        void OnHit(Impact impact)
        {
            audioSource.pitch = 1;
            shake = impact.Finisher ? 1 : .4f; hitStop = impact.Finisher ? .095f : .045f;
            ImpactFlash = impact.Finisher ? .75f : .3f; LastDamage = impact.Damage; LastWasPlayer = impact.Attacker == Combat.Player;
            if (impact.Orb) { Announcement = impact.Target.Health <= 0 ? "ORB FINISH!" : "ORB IMPACT"; AnnouncementDetail = impact.DamageText + " DAMAGE / MISSED THE DASH END-ZONE"; }
            else if (impact.Blocked) { Announcement = "WARD BROKEN"; AnnouncementDetail = "First dash hit absorbed"; }
            else if (impact.Clash) { Announcement = "SHELL CLASH"; AnnouncementDetail = "Both attacks deflected. Reset your spacing."; }
            else if (impact.Finisher) { Announcement = LastWasPlayer ? "HORN LAUNCH!" : "CAUGHT AT THE EDGE"; AnnouncementDetail = "END-ZONE HIT  /  BONUS DAMAGE + LAUNCH"; }
            else if (impact.Counter) { Announcement = "CHARGE BREAK"; AnnouncementDetail = "Caught while charging  /  +4 damage"; }
            else { Announcement = LastWasPlayer ? "SHELL HIT" : "RIVAL HIT"; AnnouncementDetail = "Land the gold end-zone for a launcher"; }
            AnnouncementLife = impact.Finisher ? 1.6f : .95f;
            Visuals.Burst(new Vector3(impact.Target.X, impact.Target.Y + .8f, -.2f), impact.Finisher ? ArenaVisuals.Gold : Color.white, impact.Finisher ? 22 : 10);
            audioSource.PlayOneShot(impact.Clash ? clashClip : impact.Finisher ? launchClip : hitClip, .7f);
        }
        void OnApplicationFocus(bool focused) { if (!focused && Combat != null && !RoundFinished) Paused = true; }
        void OnApplicationPause(bool paused) { if (paused && Combat != null && !RoundFinished) { Paused = true; Touch?.Clear(); } }
        public void TogglePause() { if (!RoundFinished) { Paused = !Paused; accumulator = 0; } }
        public void Restart()
        {
            Combat.Configure(Stage); Combat.Player.DashDamageMultiplier = 1 + StageIndex * .25f; ArenaUpgrade.Apply(Combat.Player, Upgrades); Visuals.SetStage(Stage);
            Touch.Clear(); TimedOut = false; Countdown = 2;
            Paused = false; accumulator = hitStop = 0; counted = bossAnnounced = false; RoundTime = 0;
            Announcement = Stage.Name; AnnouncementDetail = Stage.Hint; AnnouncementLife = 2.5f;
        }
        void RecordResult()
        {
            if (counted || !RoundFinished) return;
            counted = true;
            if (Won)
            {
                PlayerWins++;
                if (CampaignComplete) DefeatStreak = 0;
                UnlockedStage = Mathf.Max(UnlockedStage, Mathf.Min(StageIndex + 1, ArenaCampaign.RunLength - 1));
                PlayerPrefs.SetInt("BugArena.UnlockedStage", UnlockedStage); PlayerPrefs.Save();
            }
            else { EnemyWins++; DefeatStreak = Mathf.Min(3, DefeatStreak + 1); }
        }
        public void Continue()
        {
            if (!RoundFinished) return;
            RecordResult();
            if (ChoosingUpgrade) return;
            if (Won && !CampaignComplete) { Offers = ArenaUpgrade.Offer(Upgrades, upgradeRandom); Touch.Clear(); return; }
            RestartRun();
        }
        public void ChooseUpgrade(int index)
        {
            if (!ChoosingUpgrade || index < 0 || index >= Offers.Length) return;
            Upgrades.Add(Offers[index].Kind); Offers = null; StageIndex++; Restart();
        }
        public void RestartRun() { Offers = null; Upgrades.Clear(); StageIndex = 0; GenerateRun(); Restart(); }
        public void SelectStage(int index)
        {
            if (index < 0 || index > UnlockedStage || index >= ArenaCampaign.RunLength) return;
            Offers = null; Upgrades.Clear(); StageIndex = index; Restart(); Paused = true;
        }
        void OnDestroy()
        {
            if (Combat != null) { Combat.Hit -= OnHit; Combat.OrbChanged -= OnOrb; }
            Visuals?.Dispose(); Visuals = null;
            foreach (var clip in clips) if (clip != null) { if (Application.isPlaying) Destroy(clip); else DestroyImmediate(clip); }
            clips.Clear();
        }
    }
}
