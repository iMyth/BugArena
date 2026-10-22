using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using BugArena;
using BigInteger = System.Numerics.BigInteger;

public static class SuperChecks
{
    static int checks;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static ArenaCombat Cast()
    {
        var c = new ArenaCombat { AiEnabled = false };
        c.Configure(ArenaCampaign.Stages[0]);
        for (int i = 0; i < 160; i++) c.Advance(1f / 120, new FightInput(0, false, true));
        c.Advance(1f / 120, new FightInput());
        Check(c.Orb != null && c.Orb.Damage == 24, "Full charge launches 24 damage orb");
        return c;
    }
    static void Contact(ArenaCombat c, bool terminal, bool mirror = false, bool facingAway = false)
    {
        var f = c.Orb.Target; float sign = mirror ? 1 : -1;
        c.Orb.Position = new Vector2(0, .8f);
        c.Orb.Owner.X = sign * 6;
        f.X = -sign * 1.36f; f.Y = 0; f.Facing = (int)sign;
        if (facingAway) f.Facing *= -1;
        f.State = BugState.Dashing; f.HasHit = false;
        f.DashStart = -sign * 3;
        f.DashEnd = terminal ? -sign * .8f : sign * 2;
        f.DashDuration = .4f;
        f.DashElapsed = (f.X - f.DashStart) / (f.DashEnd - f.DashStart) * f.DashDuration;
        c.Advance(1f / 120, new FightInput());
    }
    [MenuItem("Bug Arena/Run Super Checks")]
    public static void Run()
    {
        checks = 0;
        var c = new ArenaCombat { AiEnabled = false };
        c.Advance(.2f, new FightInput(0, false, true)); c.Advance(.01f, new FightInput());
        Check(c.Orb == null && c.Player.SuperCooldown == 0, "Undercharge cancels without cooldown");
        c = Cast(); Check(c.Player.SuperCooldown > 9, "Super starts cooldown");
        var pos = c.Orb.Position; c.Step(0, new FightInput()); Check(c.Orb.Position == pos, "Zero timestep freezes orb");
        c.Enemy.X = c.Orb.Position.x + 1.3f; c.Enemy.Ward = true; c.Enemy.Invulnerability = 100;
        c.Advance(.01f, new FightInput());
        Check(c.Orb == null && c.Enemy.Health == 56, "Ordinary contact damages even through ward and invulnerability");
        c = Cast(); Contact(c, false);
        Check(c.Orb == null && c.Enemy.Health == 56, "Early dash contact takes damage");
        c = Cast(); Contact(c, true, false, true);
        Check(c.Orb == null && c.Enemy.Health == 56, "A dash facing away cannot return the ball");
        c = Cast(); Contact(c, true);
        Check(c.Orb != null && c.Orb.Damage == 48 && c.Orb.Owner == c.Enemy && c.Enemy.Health == 80, "Terminal contact reflects and doubles");
        Contact(c, true, true);
        Check(c.Orb != null && c.Orb.Damage == 96 && c.Orb.Owner == c.Player, "Mirrored terminal contact doubles again");
        Contact(c, true);
        Check(c.Orb != null && c.Orb.Damage == 192 && c.Returns == 3, "Repeated rally doubles without reset");
        c.Orb.Target.State = BugState.Ready; c.Orb.Target.X = c.Orb.Position.x - 1;
        c.Advance(.01f, new FightInput()); Check(c.Finished && c.Orb == null, "Lethal orb instantly defeats on ordinary contact");
        c = Cast(); c.Orb.Target.State = BugState.Recovering; c.Orb.Target.Timer = 1; c.Orb.Target.X = c.Orb.Position.x + 1;
        c.Advance(.01f, new FightInput()); Check(c.Orb == null && c.Enemy.Health == 56, "Recovery contact cannot reflect");
        c = Cast();
        for (int i = 0; i < 2048; i++) c.Orb.Reflect(c.Enemy, c.Player);
        Check(c.Orb.Damage == new BigInteger(24) * BigInteger.Pow(2, 2048), "2048 doubles remain exact beyond float/double ranges");
        Check(c.Orb.DamageLabel.Contains("e"), "Huge damage display stays compact");
        c.Player.X = c.Orb.Position.x + 1; c.Player.State = BugState.Ready;
        c.Advance(.01f, new FightInput()); Check(c.Player.Health == 0 && !float.IsNaN(c.Player.Health), "Huge damage applies safely without conversion overflow");
        c = Cast(); c.AiEnabled = true;
        for (int i = 0; i < 900 && c.Returns == 0; i++) c.Advance(1f / 120, new FightInput());
        Check(c.Returns > 0, "Live AI can meet an approaching orb with a terminal dash");
        c.Reset(); Check(c.Orb == null && c.Returns == 0 && c.Player.SuperCooldown == 0, "Round reset clears super state");
        var owned = new System.Collections.Generic.List<UpgradeKind>();
        for (int i = 0; i < 4; i++)
        {
            var offers = ArenaUpgrade.Offer(owned, new System.Random(i));
            Check(offers.Length == 3 && offers.Select(o => o.Kind).Distinct().Count() == 3 && offers.All(o => !owned.Contains(o.Kind)), "Three distinct unowned upgrades at each break");
            owned.Add(offers[0].Kind);
        }
        c.Reset(); ArenaUpgrade.Apply(c.Player, ArenaUpgrade.All.Select(u => u.Kind));
        Check(c.Player.MaxHealth == 135 && c.Player.Ward && c.Player.MoveSpeed > 3.8f && c.Player.SuperChargeTime < 1.15f && c.Player.ReflectionHeal == 12, "Upgrade effects applied");
        c.Configure(ArenaCampaign.Stages[4]); c.AiEnabled = true; c.Enemy.Health = 499;
        c.Advance(1f / 120, new FightInput());
        Check(c.Enemy.Enraged && c.Enemy.Health == 499 && c.Enemy.Invulnerability == 0 && c.Enemy.State == BugState.Recovering, "Boss enrage exposes a punish window without healing or invulnerability");
        Check(c.Enemy.SuperChargeTime >= .9f && c.Enemy.SuperCooldownTime >= 7, "Boss keeps readable charge and cooldown");
        c = Cast(); c.Enemy.Health = c.Enemy.MaxHealth = 1000;
        for (int i = 0; i < 6; i++) Contact(c, true, i % 2 != 0);
        Check(c.Orb != null && c.Orb.Damage == 1536 && c.Orb.Target == c.Enemy, "Six actual terminal contacts produce a 1536-damage return toward boss");
        c.Enemy.State = BugState.Ready; c.Enemy.X = c.Orb.Position.x + 1;
        c.Advance(.01f, new FightInput()); Check(c.Finished, "1000-health boss remains killable by the shared orb rule");
        Directory.CreateDirectory("Library/BugArena");
        File.WriteAllText("Library/BugArena/super-checks.txt", "PASS: " + checks + " super checks");
        Debug.Log("[BugArena] PASS: " + checks + " super checks");
    }
}
