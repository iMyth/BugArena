using System;
using UnityEngine;

namespace BugArena
{
    // Combat is independent of rendering and Input System so it can be stepped deterministically.
    public static class CombatRules
    {
        public const float MaxCharge = 1.45f;
        public const float RingEdge = 8.1f;
        public const float HitRadius = 2.15f;
        public const float EndWindow = 1.05f;
        public static float Power(float charge) => Mathf.Clamp01(charge / MaxCharge);
        public static float Distance(float charge) => Mathf.Lerp(1.65f, 6.8f, Power(charge));
        public static float Duration(float charge) => Mathf.Lerp(.19f, .40f, Power(charge));
        public static float Endpoint(float x, int facing, float charge) => Mathf.Clamp(x + facing * Distance(charge), -RingEdge, RingEdge);
    }

    public enum BugState { Ready, Charging, Dashing, Recovering, Airborne, Defeated }

    [Serializable]
    public sealed class Fighter
    {
        public float X, Y, VelocityX, VelocityY, Health = 100, Charge, Timer;
        public float DashStart, DashEnd, DashElapsed, DashDuration, Invulnerability;
        public int Facing = 1;
        public bool HasHit;
        public BugState State;
        public float Power => CombatRules.Power(Charge);
        public bool Grounded => Y <= .001f;
        public bool CanAct => Health > 0 && Grounded && (State == BugState.Ready || State == BugState.Charging);
    }

    public struct FightInput
    {
        public float Move;
        public bool HoldCharge;
        public FightInput(float move, bool hold) { Move = move; HoldCharge = hold; }
    }

    public struct Impact
    {
        public Fighter Attacker, Target;
        public bool Finisher, Counter, Clash;
        public float Damage;
    }

    public sealed class ArenaCombat
    {
        public readonly Fighter Player = new Fighter();
        public readonly Fighter Enemy = new Fighter();
        public event Action<Impact> Hit;
        public bool Finished => Player.Health <= 0 || Enemy.Health <= 0;
        public int Finishers { get; private set; }
        public int Hits { get; private set; }
        public float Elapsed { get; private set; }
        public bool AiEnabled = true;
        float aiWait, aiTargetCharge;
        uint seed = 17;

        public ArenaCombat() { Reset(); }
        float Random01() { seed = 1664525 * seed + 1013904223; return (seed & 0xFFFFFF) / (float)0x1000000; }
        public void Reset()
        {
            ResetFighter(Player, -4, 1); ResetFighter(Enemy, 4, -1);
            Finishers = Hits = 0; Elapsed = 0; aiWait = 1.2f; aiTargetCharge = .65f; seed = 17;
        }
        static void ResetFighter(Fighter f, float x, int facing)
        {
            f.X = x; f.Y = f.VelocityX = f.VelocityY = f.Charge = f.Timer = f.Invulnerability = 0;
            f.Health = 100; f.Facing = facing; f.State = BugState.Ready; f.HasHit = false;
        }
        public void Step(float dt, FightInput input)
        {
            if (Finished) return;
            dt = Mathf.Clamp(dt, 0, .025f); stepDelta = dt; Elapsed += dt;
            TickFighter(Player, Enemy, input, dt);
            TickFighter(Enemy, Player, AiEnabled ? Think(dt) : new FightInput(), dt);
            ResolveDash(Player, Enemy); ResolveDash(Enemy, Player);
            // Keep grounded idle bodies apart without stopping a dash or an aerial juggle.
            if (Player.Grounded && Enemy.Grounded && Player.State == BugState.Ready && Enemy.State == BugState.Ready)
            {
                float gap = Enemy.X - Player.X;
                if (Mathf.Abs(gap) < CombatRules.HitRadius)
                {
                    float push = (CombatRules.HitRadius - Mathf.Abs(gap)) * .5f;
                    int direction = gap >= 0 ? 1 : -1;
                    Player.X = Mathf.Clamp(Player.X - push * direction, -CombatRules.RingEdge, CombatRules.RingEdge);
                    Enemy.X = Mathf.Clamp(Enemy.X + push * direction, -CombatRules.RingEdge, CombatRules.RingEdge);
                }
            }
        }
        FightInput Think(float dt)
        {
            if (!Enemy.CanAct) return new FightInput();
            if (Enemy.State == BugState.Charging) return new FightInput(0, Enemy.Charge < aiTargetCharge);
            aiWait -= dt;
            float gap = Mathf.Abs(Player.X - Enemy.X);
            if (aiWait <= 0 && Player.Grounded && gap < 7.7f)
            {
                // Aim near the opponent, with readable variation rather than perfect tracking.
                float desired = Mathf.Clamp(gap - CombatRules.HitRadius + CombatRules.EndWindow * .5f + (Random01() - .5f) * .8f, 1.65f, 6.8f);
                aiTargetCharge = Mathf.Clamp((desired - 1.65f) / 5.15f * CombatRules.MaxCharge, .16f, 1.35f);
                aiWait = .75f + Random01() * .9f;
                return new FightInput(0, true);
            }
            float direction = Mathf.Sign(Player.X - Enemy.X);
            float move = gap > 5.7f ? direction * .65f : gap < 3.2f ? -direction * .6f : 0;
            return new FightInput(move, false);
        }
        static void TickFighter(Fighter f, Fighter opponent, FightInput input, float dt)
        {
            f.Invulnerability = Mathf.Max(0, f.Invulnerability - dt);
            if (f.Health <= 0) { f.State = BugState.Defeated; return; }
            if (f.State == BugState.Airborne)
            {
                f.VelocityY -= 18 * dt; f.Y += f.VelocityY * dt;
                f.X = Mathf.Clamp(f.X + f.VelocityX * dt, -CombatRules.RingEdge, CombatRules.RingEdge);
                f.VelocityX = Mathf.MoveTowards(f.VelocityX, 0, dt * 2);
                if (f.Y <= 0) { f.Y = 0; f.State = BugState.Recovering; f.Timer = .28f; }
                return;
            }
            if (f.State == BugState.Dashing) return; // Sweep and movement are resolved together below.
            if (f.State == BugState.Recovering)
            {
                f.Timer -= dt;
                f.X = Mathf.Clamp(f.X + f.VelocityX * dt, -CombatRules.RingEdge, CombatRules.RingEdge);
                f.VelocityX = Mathf.MoveTowards(f.VelocityX, 0, 20 * dt);
                if (f.Timer <= 0) { f.State = BugState.Ready; f.Charge = 0; }
                return;
            }
            if (Mathf.Abs(opponent.X - f.X) > .1f) f.Facing = opponent.X > f.X ? 1 : -1;
            if (input.HoldCharge)
            {
                f.State = BugState.Charging; f.Charge = Mathf.Min(CombatRules.MaxCharge, f.Charge + dt);
                return;
            }
            if (f.State == BugState.Charging)
            {
                f.State = BugState.Dashing; f.DashStart = f.X;
                f.DashEnd = CombatRules.Endpoint(f.X, f.Facing, f.Charge);
                f.DashDuration = CombatRules.Duration(f.Charge); f.DashElapsed = 0; f.HasHit = false;
                return;
            }
            f.X = Mathf.Clamp(f.X + input.Move * 3.8f * dt, -CombatRules.RingEdge, CombatRules.RingEdge);
        }
        float stepDelta;
        void ResolveDash(Fighter attacker, Fighter target)
        {
            if (attacker.State != BugState.Dashing) return;
            float oldX = attacker.X;
            attacker.DashElapsed += stepDelta;
            float progress = Mathf.Clamp01(attacker.DashElapsed / attacker.DashDuration);
            attacker.X = Mathf.Lerp(attacker.DashStart, attacker.DashEnd, progress);
            float sweepMin = Mathf.Min(oldX, attacker.X) - CombatRules.HitRadius;
            float sweepMax = Mathf.Max(oldX, attacker.X) + CombatRules.HitRadius;
            if (!attacker.HasHit && target.Invulnerability <= 0 && target.Health > 0 && target.Y < 1.65f && target.X >= sweepMin && target.X <= sweepMax)
            {
                attacker.HasHit = true;
                if (target.State == BugState.Dashing)
                {
                    attacker.State = target.State = BugState.Recovering;
                    attacker.Timer = target.Timer = .4f;
                    attacker.VelocityX = -attacker.Facing * 4; target.VelocityX = -target.Facing * 4;
                    attacker.Invulnerability = target.Invulnerability = .25f;
                    Hit?.Invoke(new Impact { Attacker = attacker, Target = target, Clash = true });
                    return;
                }
                // Use the contact position, rather than the body's centre, for endpoint timing.
                float travel = Mathf.Abs(attacker.DashEnd - attacker.DashStart);
                float contact = Mathf.Max(0, Mathf.Abs(target.X - attacker.DashStart) - CombatRules.HitRadius);
                bool finisher = travel >= 1.3f && Mathf.Abs(travel - contact) <= CombatRules.EndWindow;
                bool counter = target.State == BugState.Charging;
                float damage = (finisher ? 23 : 10) + attacker.Power * (finisher ? 11 : 6) + (counter ? 4 : 0);
                target.Health = Mathf.Max(0, target.Health - damage);
                target.Charge = 0; target.Invulnerability = .24f;
                target.State = finisher ? BugState.Airborne : BugState.Recovering;
                target.Timer = .32f; target.VelocityX = attacker.Facing * (finisher ? 4.2f : 5.5f);
                target.VelocityY = finisher ? 8.2f : 0;
                if (target.Health <= 0) target.State = BugState.Defeated;
                attacker.State = BugState.Recovering; attacker.Timer = finisher ? .15f : .25f;
                if (attacker == Player) { Hits++; if (finisher) Finishers++; }
                Hit?.Invoke(new Impact { Attacker = attacker, Target = target, Damage = damage, Finisher = finisher, Counter = counter });
                return;
            }
            if (progress >= 1) { attacker.State = BugState.Recovering; attacker.Timer = .43f; }
        }
        public void Advance(float dt, FightInput input)
        {
            // Substeps prevent tunnelling on slower frames and preserve deterministic input handling.
            int count = Mathf.Max(1, Mathf.CeilToInt(dt / .0125f));
            stepDelta = dt / count;
            for (int i = 0; i < count; i++) Step(stepDelta, input);
        }
    }
}
