using System;
using System.Collections.Generic;
using System.Linq;
using BugArena;
using UnityEditor;
using UnityEngine;

public static class RandomRunChecks
{
    static string Signature(ArenaStage s) => s.Rival + ":" + s.EnemyHealth + ":" + s.DamageScale + ":" + string.Join(",", s.Traits);
    [MenuItem("Bug Arena/Run Random Run Checks")]
    public static void Run()
    {
        int checks = 0; var variants = new HashSet<string>();
        Action<bool, string> check = (ok, why) => { if (!ok) throw new Exception(why); checks++; };
        for (int seed = 0; seed < 100; seed++)
        {
            var run = ArenaCampaign.Generate(seed, 0); var repeat = ArenaCampaign.Generate(seed, 0); var aid = ArenaCampaign.Generate(seed, 2);
            check(run.Length == 9, "Nine-stage run");
            for (int i = 0; i < run.Length; i++)
            {
                var s = run[i]; variants.Add(Signature(s));
                check(Signature(s) == Signature(repeat[i]), "Seed reproduces enemy rolls");
                check(s.IsBoss == (i % 3 == 2), "Exactly every third encounter is a boss");
                check(s.Traits.Distinct().Count() == s.Traits.Length && s.Traits.Length == (s.IsBoss ? 2 : 1), "Skill budget is bounded and unique");
                check(!(s.Traits.Contains(UpgradeKind.Ward) && s.Traits.Contains(UpgradeKind.Siphon)), "No shield plus sustain stacking");
                check(aid[i].EnemyHealth < s.EnemyHealth && aid[i].DamageScale < s.DamageScale && aid[i].ThinkDelay > s.ThinkDelay, "Assistance reduces pressure for same seed");
                var c = new ArenaCombat(); c.Configure(s);
                check(c.Enemy.DashDamageMultiplier * c.Enemy.FinisherMultiplier * 38 < 80, "Enemy maximum dash including charge break cannot one-shot base player");
                check(c.Enemy.SuperDamage <= 27 && c.Enemy.SuperChargeTime >= .9f, "Initial orb damage and cast tell remain bounded");
            }
            var owned = new List<UpgradeKind>(); var rng = new System.Random(seed);
            for (int stage = 0; stage < 8; stage++)
            {
                var offers = ArenaUpgrade.Offer(owned, rng);
                check(offers.Length == 3 && offers.Select(u => u.Kind).Distinct().Count() == 3 && offers.All(u => !owned.Contains(u.Kind)), "All eight rewards provide three distinct unowned choices");
                bool partial = false;
                foreach (var pair in ArenaUpgrade.Pairs)
                    if (owned.Contains(pair[0]) != owned.Contains(pair[1])) { partial = true; break; }
                if (partial)
                    check(ArenaUpgrade.Pairs.Any(pair => owned.Contains(pair[0]) && offers.Any(u => u.Kind == pair[1]) || owned.Contains(pair[1]) && offers.Any(u => u.Kind == pair[0])), "Partial build has a guaranteed complement");
                else if (!owned.Contains(UpgradeKind.HeavyHorn) && !owned.Contains(UpgradeKind.Overcharge))
                    check(offers.Any(u => u.Kind == UpgradeKind.HeavyHorn || u.Kind == UpgradeKind.Overcharge), "Damage safety option when no damage upgrade owned");
                owned.Add(offers[rng.Next(3)].Kind);
            }
        }
        check(variants.Count > 500, "Seeds meaningfully vary enemy health, skills and strength");
        var fighter = new Fighter(); ArenaUpgrade.Apply(fighter, new[] { UpgradeKind.HeavyHorn, UpgradeKind.Siphon });
        check(fighter.FinisherMultiplier > 2 && fighter.FinisherHeal == 12, "Melee synergy creates a power spike");
        fighter = new Fighter(); ArenaUpgrade.Apply(fighter, new[] { UpgradeKind.QuickCore, UpgradeKind.Overcharge });
        check(fighter.SuperDamage == 64 && fighter.SuperCooldownTime < 6, "Super synergy creates a power spike");
        SuperChecks.Run(); CampaignChecks.Run(); TouchChecks.Run();
        Debug.Log("[BugArena] PASS: " + checks + " seeded generation and reward assertions across 100 runs");
    }
}
