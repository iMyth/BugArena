using System;
using System.Collections.Generic;
using System.Linq;

namespace BugArena
{
    public enum UpgradeKind { QuickCore, WideHorn, FleetFeet, ReflectionHeal, Ward, Vitality, HeavyHorn, Siphon, Feedback, Overcharge }
    public sealed class ArenaUpgrade
    {
        public readonly UpgradeKind Kind;
        public readonly string Name, Detail;
        public ArenaUpgrade(UpgradeKind kind, string name, string detail) { Kind = kind; Name = name; Detail = detail; }
        public static readonly ArenaUpgrade[] All = {
            new ArenaUpgrade(UpgradeKind.QuickCore, "QUICK CORE", "Super charge 25% faster. Super cooldown 20% shorter. Pair with UNSTABLE CORE: orb 64, cooldown 5.6s."),
            new ArenaUpgrade(UpgradeKind.WideHorn, "PRECISION HORN", "Dash end-zone +0.35m. Easier launchers and orb returns."),
            new ArenaUpgrade(UpgradeKind.FleetFeet, "FLEET FEET", "Move 20% faster to set up your next dash."),
            new ArenaUpgrade(UpgradeKind.ReflectionHeal, "SECOND WIND", "Every orb return heals 12. Pair with PRECISION HORN: heal 20."),
            new ArenaUpgrade(UpgradeKind.Ward, "CARAPACE WARD", "Block the first dash hit in each round."),
            new ArenaUpgrade(UpgradeKind.Vitality, "IRON SHELL", "+35 maximum health for the rest of this run."),
            new ArenaUpgrade(UpgradeKind.HeavyHorn, "THUNDER HORN", "Dash end hits deal 40% more damage. Pair with HUNTER INSTINCT: +110% damage, heal 12."),
            new ArenaUpgrade(UpgradeKind.Siphon, "HUNTER INSTINCT", "A damaging dash end hit restores 8 health."),
            new ArenaUpgrade(UpgradeKind.Feedback, "FEEDBACK LOOP", "Each orb return cuts your super cooldown by 3 seconds."),
            new ArenaUpgrade(UpgradeKind.Overcharge, "UNSTABLE CORE", "Your orbs start at 32 damage. Returned fire is stronger too!")
        };
        public static ArenaUpgrade[] Offer(ICollection<UpgradeKind> owned, Random random)
        {
            var pool = All.Where(u => !owned.Contains(u.Kind)).ToList();
            for (int i = pool.Count - 1; i > 0; i--) { int j = random.Next(i + 1); var swap = pool[i]; pool[i] = pool[j]; pool[j] = swap; }
            // Always offer an available build complement, or a damage option if none is owned.
            UpgradeKind? wanted = null;
            foreach (var pair in Pairs)
            {
                if (owned.Contains(pair[0]) && !owned.Contains(pair[1])) { wanted = pair[1]; break; }
                if (owned.Contains(pair[1]) && !owned.Contains(pair[0])) { wanted = pair[0]; break; }
            }
            if (!wanted.HasValue && !owned.Contains(UpgradeKind.HeavyHorn) && !owned.Contains(UpgradeKind.Overcharge))
                wanted = random.Next(2) == 0 ? UpgradeKind.HeavyHorn : UpgradeKind.Overcharge;
            if (wanted.HasValue)
            {
                int at = pool.FindIndex(u => u.Kind == wanted.Value);
                if (at >= 0) { var pick = pool[at]; pool.RemoveAt(at); pool.Insert(0, pick); }
            }
            return pool.Take(3).ToArray();
        }
        public static readonly UpgradeKind[][] Pairs = {
            new[] { UpgradeKind.HeavyHorn, UpgradeKind.Siphon },
            new[] { UpgradeKind.QuickCore, UpgradeKind.Overcharge },
            new[] { UpgradeKind.WideHorn, UpgradeKind.ReflectionHeal }
        };
        public static string ComboText(ICollection<UpgradeKind> owned)
        {
            var names = new System.Collections.Generic.List<string>();
            if (Pairs[0].All(owned.Contains)) names.Add("RAMPAGE: end damage x2.1 + heal 12");
            if (Pairs[1].All(owned.Contains)) names.Add("NOVA: orb 64 + faster cooldown");
            if (Pairs[2].All(owned.Contains)) names.Add("REBIRTH: return heals 20");
            return string.Join(" / ", names);
        }
        public static void Apply(Fighter f, IEnumerable<UpgradeKind> upgrades, bool combos = true)
        {
            var owned = upgrades.ToArray();
            foreach (var kind in owned)
                switch (kind)
                {
                    case UpgradeKind.QuickCore: f.SuperChargeTime *= .75f; f.SuperCooldownTime *= .8f; break;
                    case UpgradeKind.WideHorn: f.EndWindow += .35f; break;
                    case UpgradeKind.FleetFeet: f.MoveSpeed *= 1.2f; break;
                    case UpgradeKind.ReflectionHeal: f.ReflectionHeal = 12; break;
                    case UpgradeKind.Ward: f.Ward = true; break;
                    case UpgradeKind.HeavyHorn: f.FinisherMultiplier = 1.4f; break;
                    case UpgradeKind.Siphon: f.FinisherHeal = 8; break;
                    case UpgradeKind.Feedback: f.ReflectionRefund = 3; break;
                    case UpgradeKind.Overcharge: f.SuperDamage = 32; break;
                    case UpgradeKind.Vitality: f.MaxHealth += 35; f.Health = f.MaxHealth; break;
                }
            if (!combos) return;
            if (Pairs[0].All(owned.Contains)) { f.FinisherMultiplier = 2.1f; f.FinisherHeal = 12; }
            if (Pairs[1].All(owned.Contains)) { f.SuperDamage = 64; f.SuperCooldownTime *= .7f; }
            if (Pairs[2].All(owned.Contains)) f.ReflectionHeal = 20;
        }
    }
}
