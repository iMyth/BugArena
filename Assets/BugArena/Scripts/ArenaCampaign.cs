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
        public float TimeLimit => IsBoss ? 150 : 75;
        public ArenaStage(string name, string rival, string hint, float edge, float health, float delay, float error, float gap, Color accent, float superDistance = 7, float reflectRange = 4.4f, bool boss = false)
        { Name = name; Rival = rival; Hint = hint; Edge = edge; EnemyHealth = health; ThinkDelay = delay; AimError = error; PreferredGap = gap; Accent = accent; SuperDistance = superDistance; ReflectRange = reflectRange; IsBoss = boss; }
    }

    public static class ArenaCampaign
    {
        public static readonly ArenaStage[] Stages =
        {
            new ArenaStage("MOSSWOOD", "EMBER", "Learn the gold end-zone. Bait a dash, then punish the recovery.", 10.5f, 80, 1.25f, 1.3f, 5.3f, new Color(.95f,.35f,.25f)),
            new ArenaStage("AMBER GROVE", "CINDER", "A close-range brawler. Create space before charging.", 11.5f, 140, .70f, .85f, 3.4f, new Color(1,.65f,.16f)),
            new ArenaStage("SPORE BANK", "SPORE", "A ranged rival. Use the end of your dash to return its orb.", 12, 220, .65f, .8f, 8, new Color(.55f,.8f,.25f), 5.5f),
            new ArenaStage("CRYSTAL GUARD", "QUARTZ", "This rival returns fire. Prepare to rally.", 11.5f, 350, .55f, .5f, 6, new Color(.35f,.7f,1), 6, 5),
            new ArenaStage("MOONLIT CROWN", "ONYX", "Half-health enrage. Punish recovery; return its own power.", 12, 1000, .50f, .45f, 5.1f, new Color(.55f,.38f,.95f), boss: true)
        };
    }
}
