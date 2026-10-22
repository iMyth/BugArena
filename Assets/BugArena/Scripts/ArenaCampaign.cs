using UnityEngine;

namespace BugArena
{
    public sealed class ArenaStage
    {
        public readonly string Name, Rival, Hint;
        public readonly float Edge, EnemyHealth, ThinkDelay, AimError, PreferredGap;
        public readonly float SuperDistance, ReflectRange;
        public readonly Color Accent;
        public readonly bool IsBoss;
        public float DamageScale = 1, MoveSpeed = 3.8f, ChargeTime = 1.15f, Cooldown = 10;
        public int OrbDamage = 24;
        public UpgradeKind[] Traits = new UpgradeKind[0];
        public float TimeLimit => IsBoss ? 150 : 75;
        public ArenaStage(string name, string rival, string hint, float edge, float health, float delay, float error, float gap, Color accent, float superDistance = 7, float reflectRange = 4.4f, bool boss = false)
        { Name = name; Rival = rival; Hint = hint; Edge = edge; EnemyHealth = health; ThinkDelay = delay; AimError = error; PreferredGap = gap; Accent = accent; SuperDistance = superDistance; ReflectRange = reflectRange; IsBoss = boss; }
    }

    public static class ArenaCampaign
    {
        public const int RunLength = 9;
        // Stable fixture for art previews and isolated combat checks; live runs own their seed.
        public static readonly ArenaStage[] Stages = Generate(1729, 0);
        public static ArenaStage[] Generate(int seed, int assistance)
        {
            var random = new System.Random(seed);
            var stages = new ArenaStage[RunLength];
            string[] habitats = { "MOSSWOOD", "AMBER GROVE", "SPORE BANK" };
            string[] kinds = { "BRAWLER", "CASTER", "GUARD" };
            var traits = new[] { UpgradeKind.HeavyHorn, UpgradeKind.QuickCore, UpgradeKind.Ward, UpgradeKind.Siphon };
            for (int i = 0; i < stages.Length; i++)
            {
                int act = i / 3, kind = random.Next(3); bool boss = i % 3 == 2;
                float roll = (float)random.NextDouble();
                float baseHealth = boss ? new[] { 600, 1000, 1600 }[act] : 80 + i * 35;
                bool fragile = !boss && random.NextDouble() < .22;
                float health = baseHealth * (.85f + .3f * roll) * (fragile ? .65f : 1) * (1 - .1f * assistance);
                var chosen = new System.Collections.Generic.List<UpgradeKind>();
                chosen.Add(traits[random.Next(traits.Length)]);
                if (boss)
                {
                    // Bosses get two distinct skills, never both sustain and a shield.
                    var second = traits[random.Next(2)];
                    if (chosen.Contains(second)) second = second == UpgradeKind.HeavyHorn ? UpgradeKind.QuickCore : UpgradeKind.HeavyHorn;
                    chosen.Add(second);
                }
                var stage = new ArenaStage(habitats[act], (boss ? "BOSS " : fragile ? "FRAIL " : "") + kinds[kind],
                    (kind == 0 ? "Close-range pressure. Punish missed dashes." : kind == 1 ? "Ranged attacks. Return the orb at your dash end." : "Strong returns. Pressure with dash end hits.") +
                    " Skills: " + string.Join(" / ", chosen.ConvertAll(k => System.Array.Find(ArenaUpgrade.All, u => u.Kind == k).Name)),
                    10.5f + (float)random.NextDouble() * 1.5f, Mathf.Round(health),
                    Mathf.Max(.65f, 1.25f - i * .06f) + assistance * .12f, .65f + (float)random.NextDouble() * .7f,
                    kind == 0 ? 3.5f : kind == 1 ? 8 : 5.8f,
                    kind == 0 ? new Color(1,.4f,.25f) : kind == 1 ? new Color(.5f,.8f,.3f) : new Color(.5f,.45f,1),
                    kind == 1 ? 5.5f : 7, kind == 2 ? 5 : 4.2f, boss);
                // More health consumes the damage budget; independent max rolls cannot stack.
                stage.DamageScale = (.9f + i * .035f + (1 - roll) * .2f) * (1 - assistance * .08f);
                stage.MoveSpeed = 3.4f + (float)random.NextDouble() * .7f;
                stage.ChargeTime = 1.15f + assistance * .15f;
                stage.OrbDamage = 18 + act * 3 + random.Next(4);
                stage.Traits = chosen.ToArray(); stages[i] = stage;
            }
            return stages;
        }
    }
}
