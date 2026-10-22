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

    public enum BugState { Ready, Charging, SuperCharging, Dashing, Recovering, Airborne, Defeated }

    [Serializable]
    public sealed class Fighter
    {
        public float X, Y, VelocityX, VelocityY, Health = 100, MaxHealth = 100, Charge, Timer;
        public float DashStart, DashEnd, DashElapsed, DashDuration, Invulnerability;
        public int Facing = 1;
        public bool HasHit;
        public float SuperCharge, SuperCooldown, SuperChargeTime = 1.15f, SuperCooldownTime = 10f;
        public float MoveSpeed = 3.8f, EndWindow = CombatRules.EndWindow, ReflectionHeal;
        public bool Ward, Enraged;
        public int SuperDamage = 24;
        public float DashDamageMultiplier = 1, FinisherMultiplier = 1, FinisherHeal, ReflectionRefund;
        public float SuperPower => Mathf.Clamp01(SuperCharge / SuperChargeTime);
        public BugState State;
        public float Power => CombatRules.Power(Charge);
        public bool Grounded => Y <= .001f;
        public bool CanAct => Health > 0 && Grounded && (State == BugState.Ready || State == BugState.Charging || State == BugState.SuperCharging);
    }

    public struct FightInput
    {
        public float Move;
        public bool HoldCharge, HoldSuper;
        public FightInput(float move, bool hold, bool super = false) { Move = move; HoldCharge = hold; HoldSuper = super; }
    }

    public struct Impact
    {
        public Fighter Attacker, Target;
        public bool Finisher, Counter, Clash, Orb, Blocked;
        public string DamageText;
        public float Damage;
    }

    public sealed class ArenaCombat
    {
        public readonly Fighter Player = new Fighter();
        public readonly Fighter Enemy = new Fighter();
        public event Action<Impact> Hit;
        public event Action<OrbEvent> OrbChanged;
        public EnergyOrb Orb { get; private set; }
        public System.Numerics.BigInteger Returns { get; private set; }
        public bool Finished => Player.Health <= 0 || Enemy.Health <= 0;
        public int Finishers { get; private set; }
        public int Hits { get; private set; }
        public float Elapsed { get; private set; }
        public bool AiEnabled = true;
        public ArenaStage Stage { get; private set; }
        public float RingEdge => Stage != null ? Stage.Edge : CombatRules.RingEdge;
        public void Configure(ArenaStage stage) { Stage = stage; Reset(); }
        float aiWait, aiTargetCharge;
        uint seed = 17;

        public ArenaCombat() { Reset(); }
        float Random01() { seed = 1664525 * seed + 1013904223; return (seed & 0xFFFFFF) / (float)0x1000000; }
        public void Reset()
        {
            ResetFighter(Player, -4, 1); ResetFighter(Enemy, 4, -1);
            if (Stage != null) Enemy.Health = Enemy.MaxHealth = Stage.EnemyHealth;
            Orb = null; Returns = 0;
            if (Stage != null) { Player.X = -6; Enemy.X = 6; }
            Finishers = Hits = 0; Elapsed = 0; aiWait = 1.2f; aiTargetCharge = .65f; seed = 17;
        }
        static void ResetFighter(Fighter f, float x, int facing)
        {
            f.X = x; f.Y = f.VelocityX = f.VelocityY = f.Charge = f.Timer = f.Invulnerability = 0;
            f.SuperCharge = f.SuperCooldown = f.ReflectionHeal = 0; f.SuperChargeTime = 1.15f; f.SuperCooldownTime = 10;
            f.MoveSpeed = 3.8f; f.EndWindow = CombatRules.EndWindow; f.Ward = f.Enraged = false; f.SuperDamage = 24; f.DashDamageMultiplier = f.FinisherMultiplier = 1; f.FinisherHeal = f.ReflectionRefund = 0;
            f.Health = f.MaxHealth = 100; f.Facing = facing; f.State = BugState.Ready; f.HasHit = false;
        }
        public void Step(float dt, FightInput input)
        {
            if (Finished || dt <= 0) return;
            dt = Mathf.Clamp(dt, 0, .025f); stepDelta = dt; Elapsed += dt;
            float oldPlayer = Player.X, oldEnemy = Enemy.X;
            float oldPlayerY = Player.Y, oldEnemyY = Enemy.Y;
            TickFighter(Player, Enemy, input, dt);
            TickFighter(Enemy, Player, AiEnabled ? Think(dt) : new FightInput(), dt);
            MoveDash(Player); MoveDash(Enemy);
            ResolveOrb(new Vector2(oldPlayer, oldPlayerY + .8f), new Vector2(oldEnemy, oldEnemyY + .8f));
            if (!Finished) { ResolveDash(Player, Enemy, oldPlayer); ResolveDash(Enemy, Player, oldEnemy); }
            // Keep grounded idle bodies apart without stopping a dash or an aerial juggle.
            if (Player.Grounded && Enemy.Grounded && Player.State == BugState.Ready && Enemy.State == BugState.Ready)
            {
                float gap = Enemy.X - Player.X;
                if (Mathf.Abs(gap) < CombatRules.HitRadius)
                {
                    float push = (CombatRules.HitRadius - Mathf.Abs(gap)) * .5f;
                    int direction = gap >= 0 ? 1 : -1;
                    Player.X = Mathf.Clamp(Player.X - push * direction, -RingEdge, RingEdge);
                    Enemy.X = Mathf.Clamp(Enemy.X + push * direction, -RingEdge, RingEdge);
                }
            }
        }
        FightInput Think(float dt)
        {
            if (!Enemy.CanAct) return new FightInput();
            if (Stage != null && Stage.IsBoss && !Enemy.Enraged && Enemy.Health <= Enemy.MaxHealth * .5f)
            {
                Enemy.Enraged = true; Enemy.SuperChargeTime = .95f; Enemy.SuperCooldownTime = 7;
                Enemy.MoveSpeed = 4.2f; Enemy.Charge = Enemy.SuperCharge = 0;
                // A readable transition and punish window, with no healing or invulnerability.
                Enemy.State = BugState.Recovering; Enemy.Timer = .9f;
                aiWait = .8f; return new FightInput();
            }
            if (Enemy.State == BugState.SuperCharging) return new FightInput(0, false, Enemy.SuperPower < 1);
            if (Orb != null && Orb.Target == Enemy && Enemy.Grounded)
            {
                // Predict a short dash meeting the approaching ball inside the terminal zone.
                float gapToBall = Mathf.Abs(Orb.Position.x - Enemy.X);
                float desired = CombatRules.Distance(Enemy.Charge) + EnergyOrb.ContactRadius + EnergyOrb.Speed * CombatRules.Duration(Enemy.Charge) - Enemy.EndWindow * .5f;
                if (Enemy.State == BugState.Charging) return new FightInput(0, gapToBall > desired);
                float reaction = Stage != null ? Stage.ReflectRange : 4.4f;
                if (gapToBall < reaction) return new FightInput(0, true);
            }
            if (Enemy.State == BugState.Charging) return new FightInput(0, Enemy.Charge < aiTargetCharge);
            aiWait -= dt;
            float gap = Mathf.Abs(Player.X - Enemy.X);
            if (aiWait <= 0 && Orb == null && Enemy.SuperCooldown <= 0 && Stage != null && gap >= Stage.SuperDistance)
            { aiWait = Stage.ThinkDelay + 1; return new FightInput(0, false, true); }
            if (aiWait <= 0 && Player.Grounded && gap < 7.7f)
            {
                // Aim near the opponent, with readable variation rather than perfect tracking.
                float desired = Mathf.Clamp(gap - CombatRules.HitRadius + CombatRules.EndWindow * .5f + (Random01() - .5f) * (Stage != null ? Stage.AimError : .8f), 1.65f, 6.8f);
                aiTargetCharge = Mathf.Clamp((desired - 1.65f) / 5.15f * CombatRules.MaxCharge, .16f, 1.35f);
                aiWait = (Stage != null ? Stage.ThinkDelay : .75f) + Random01() * .6f;
                return new FightInput(0, true);
            }
            float direction = Mathf.Sign(Player.X - Enemy.X);
            float preferred = Stage != null ? Stage.PreferredGap : 4.5f;
            float move = gap > preferred + .65f ? direction * .7f : gap < preferred - .65f ? -direction * .6f : 0;
            return new FightInput(move, false);
        }
        void TickFighter(Fighter f, Fighter opponent, FightInput input, float dt)
        {
            f.Invulnerability = Mathf.Max(0, f.Invulnerability - dt);
            f.SuperCooldown = Mathf.Max(0, f.SuperCooldown - dt);
            if (f.Health <= 0) { f.State = BugState.Defeated; return; }
            if (f.State == BugState.Airborne)
            {
                f.VelocityY -= 18 * dt; f.Y += f.VelocityY * dt;
                f.X = Mathf.Clamp(f.X + f.VelocityX * dt, -RingEdge, RingEdge);
                f.VelocityX = Mathf.MoveTowards(f.VelocityX, 0, dt * 2);
                if (f.Y <= 0) { f.Y = 0; f.State = BugState.Recovering; f.Timer = .28f; }
                return;
            }
            if (f.State == BugState.Dashing) return; // Sweep and movement are resolved together below.
            if (f.State == BugState.Recovering)
            {
                f.Timer -= dt;
                f.X = Mathf.Clamp(f.X + f.VelocityX * dt, -RingEdge, RingEdge);
                f.VelocityX = Mathf.MoveTowards(f.VelocityX, 0, 20 * dt);
                if (f.Timer <= 0) { f.State = BugState.Ready; f.Charge = 0; }
                return;
            }
            if (Mathf.Abs(opponent.X - f.X) > .1f) f.Facing = opponent.X > f.X ? 1 : -1;
            if (f.State == BugState.SuperCharging)
            {
                if (input.HoldSuper) { f.SuperCharge = Mathf.Min(f.SuperChargeTime, f.SuperCharge + dt); return; }
                if (f.SuperPower >= 1 && Orb == null)
                {
                    Orb = new EnergyOrb { Owner = f, Target = opponent, Damage = new System.Numerics.BigInteger(f.SuperDamage), Position = new Vector2(f.X + f.Facing * 1.45f, f.Y + .8f) };
                    f.SuperCooldown = f.SuperCooldownTime;
                    OrbChanged?.Invoke(new OrbEvent { Actor = f, Position = Orb.Position, Damage = Orb.DamageLabel });
                    f.State = BugState.Recovering; f.Timer = .55f;
                }
                else f.State = BugState.Ready;
                f.SuperCharge = 0; return;
            }
            if (input.HoldSuper && f.State == BugState.Ready && f.SuperCooldown <= 0 && Orb == null)
            { f.State = BugState.SuperCharging; f.SuperCharge = dt; return; }
            if (input.HoldCharge)
            {
                f.State = BugState.Charging; f.Charge = Mathf.Min(CombatRules.MaxCharge, f.Charge + dt);
                return;
            }
            if (f.State == BugState.Charging)
            {
                f.State = BugState.Dashing; f.DashStart = f.X;
                f.DashEnd = Mathf.Clamp(f.X + f.Facing * CombatRules.Distance(f.Charge), -RingEdge, RingEdge);
                f.DashDuration = CombatRules.Duration(f.Charge); f.DashElapsed = 0; f.HasHit = false;
                return;
            }
            f.X = Mathf.Clamp(f.X + input.Move * f.MoveSpeed * dt, -RingEdge, RingEdge);
        }
        float stepDelta;
        void MoveDash(Fighter f)
        {
            if (f.State != BugState.Dashing) return;
            f.DashElapsed += stepDelta;
            f.X = Mathf.Lerp(f.DashStart, f.DashEnd, Mathf.Clamp01(f.DashElapsed / f.DashDuration));
        }
        void ResolveDash(Fighter attacker, Fighter target, float oldX)
        {
            if (attacker.State != BugState.Dashing) return;
            float progress = Mathf.Clamp01(attacker.DashElapsed / attacker.DashDuration);
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
                bool finisher = travel >= 1.3f && Mathf.Abs(travel - contact) <= attacker.EndWindow;
                bool counter = target.State == BugState.Charging || target.State == BugState.SuperCharging;
                float damage = (finisher ? 23 : 10) + attacker.Power * (finisher ? 11 : 6) + (counter ? 4 : 0);
                damage *= attacker.DashDamageMultiplier;
                if (finisher) damage *= attacker.FinisherMultiplier;
                bool blocked = target.Ward; if (blocked) { target.Ward = false; damage = 0; }
                target.Health = Mathf.Max(0, target.Health - damage);
                if (finisher && damage > 0) attacker.Health = Mathf.Min(attacker.MaxHealth, attacker.Health + attacker.FinisherHeal);
                target.Charge = target.SuperCharge = 0; target.Invulnerability = .24f;
                target.State = finisher ? BugState.Airborne : BugState.Recovering;
                target.Timer = .32f; target.VelocityX = attacker.Facing * (finisher ? 4.2f : 5.5f);
                target.VelocityY = finisher ? 8.2f : 0;
                if (target.Health <= 0) target.State = BugState.Defeated;
                attacker.State = BugState.Recovering; attacker.Timer = finisher ? .15f : .25f;
                if (attacker == Player) { Hits++; if (finisher) Finishers++; }
                Hit?.Invoke(new Impact { Attacker = attacker, Target = target, Damage = damage, Finisher = finisher, Counter = counter, Blocked = blocked });
                return;
            }
            if (progress >= 1) { attacker.State = BugState.Recovering; attacker.Timer = .43f; }
        }
        void ResolveOrb(Vector2 oldPlayer, Vector2 oldEnemy)
        {
            if (Orb == null) return;
            var ball = Orb; var target = ball.Target;
            Vector2 oldTarget = target == Player ? oldPlayer : oldEnemy;
            var targetPosition = new Vector2(target.X, target.Y + .8f);
            Vector2 start = ball.Position;
            Vector2 end = Vector2.MoveTowards(start, targetPosition, EnergyOrb.Speed * stepDelta);
            // Swept relative circle contact prevents fast dashes crossing through the ball.
            Vector2 relative = start - oldTarget, delta = (end - targetPosition) - relative;
            float c = relative.sqrMagnitude - EnergyOrb.ContactRadius * EnergyOrb.ContactRadius;
            float t = 0;
            if (c > 0)
            {
                float a = delta.sqrMagnitude, b = 2 * Vector2.Dot(relative, delta);
                float disc = b * b - 4 * a * c;
                if (a < .0000001f || disc < 0) { ball.Position = end; return; }
                t = (-b - Mathf.Sqrt(disc)) / (2 * a);
                if (t < 0 || t > 1) { ball.Position = end; return; }
            }
            ball.Position = Vector2.Lerp(start, end, t);
            float contactX = Mathf.Lerp(oldTarget.x, target.X, t);
            bool facingBall = (ball.Position.x - contactX) * target.Facing > 0;
            float travel = Mathf.Abs(target.DashEnd - target.DashStart);
            bool terminal = target.State == BugState.Dashing && !target.HasHit && target.Grounded && facingBall &&
                target.DashElapsed - stepDelta + stepDelta * t <= target.DashDuration + .00001f &&
                travel >= 1.3f && Mathf.Abs(target.DashEnd - contactX) <= target.EndWindow;
            if (terminal)
            {
                var opponent = ball.Owner;
                ball.Reflect(target, opponent); Returns++;
                target.HasHit = true; target.State = BugState.Recovering; target.Timer = .15f;
                target.Health = Mathf.Min(target.MaxHealth, target.Health + target.ReflectionHeal);
                target.SuperCooldown = Mathf.Max(0, target.SuperCooldown - target.ReflectionRefund);
                OrbChanged?.Invoke(new OrbEvent { Actor = target, Position = ball.Position, Reflected = true, Damage = ball.DamageLabel, Returns = ball.Reflections });
                return;
            }
            bool blocked = false; // Orbs always damage on non-terminal contact, including wards.
            // Keep unbounded exact damage on the orb; only clamp the applied health loss.
            float damage = ball.Damage >= new System.Numerics.BigInteger(Math.Ceiling(target.Health)) ? target.Health : (float)ball.Damage;
            target.Health = Mathf.Max(0, target.Health - damage);
            target.Charge = target.SuperCharge = 0;
            target.State = target.Health <= 0 ? BugState.Defeated : BugState.Airborne;
            target.VelocityX = Mathf.Sign(target.X - ball.Owner.X) * 4; target.VelocityY = 7;
            target.Invulnerability = .24f;
            if (ball.Owner == Player) Hits++;
            Orb = null;
            Hit?.Invoke(new Impact { Attacker = ball.Owner, Target = target, Orb = true, Damage = damage, DamageText = ball.DamageLabel, Finisher = true, Blocked = blocked });
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
