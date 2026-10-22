using UnityEngine;

namespace BugArena
{
    public sealed class ArenaStage
    {
        public readonly string Name, Rival, Hint;
        public readonly float Edge, EnemyHealth, ThinkDelay, AimError, PreferredGap;
        public readonly Color Accent;
        public ArenaStage(string name, string rival, string hint, float edge, float health, float delay, float error, float gap, Color accent)
        { Name = name; Rival = rival; Hint = hint; Edge = edge; EnemyHealth = health; ThinkDelay = delay; AimError = error; PreferredGap = gap; Accent = accent; }
    }

    public static class ArenaCampaign
    {
        public static readonly ArenaStage[] Stages =
        {
            new ArenaStage("MOSSWOOD", "EMBER", "Learn the gold end-zone. Bait a dash, then punish the recovery.", 8.1f, 80, 1.25f, 1.3f, 5.3f, new Color(.95f,.35f,.25f)),
            new ArenaStage("AMBER GROVE", "CINDER", "A close-range brawler. Create space before charging.", 7.1f, 100, .70f, .85f, 3.4f, new Color(1,.65f,.16f)),
            new ArenaStage("MOONLIT CROWN", "ONYX", "A precise champion in a narrow ring. Break its charge with a short dash.", 6.1f, 125, .50f, .45f, 5.1f, new Color(.55f,.38f,.95f))
        };
    }
}
