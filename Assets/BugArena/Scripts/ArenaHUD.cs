using UnityEngine;

namespace BugArena
{
    public sealed class ArenaHUD : MonoBehaviour
    {
        ArenaGame game;
        Font font;
        GUIStyle style;
        readonly Color ink = new Color(.90f, .95f, .91f);
        readonly Color muted = new Color(.53f, .65f, .65f);
        readonly Color panel = new Color(.025f, .065f, .08f, .94f);
        void Awake() { game = GetComponent<ArenaGame>(); font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
        void Rect(float x, float y, float w, float h, Color color)
        {
            Color previous = GUI.color; GUI.color = color; GUI.DrawTexture(new Rect(x, y, Mathf.Max(0, w), h), Texture2D.whiteTexture); GUI.color = previous;
        }
        void Text(float x, float y, float w, float h, string text, int size, Color color, TextAnchor alignment = TextAnchor.UpperLeft, bool bold = false)
        {
            if (style == null) style = new GUIStyle();
            style.font = font; style.fontSize = size; style.normal.textColor = color; style.alignment = alignment;
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; style.wordWrap = true;
            GUI.Label(new Rect(x, y, w, h), text, style);
        }
        bool Button(float x, float y, float w, float h, string text, Color color)
        {
            bool hover = new Rect(x, y, w, h).Contains(Event.current.mousePosition);
            Rect(x, y, w, h, hover ? color : color * .8f);
            Text(x, y, w, h, text, 18, new Color(.03f, .07f, .07f), TextAnchor.MiddleCenter, true);
            return GUI.Button(new Rect(x, y, w, h), GUIContent.none, GUIStyle.none) && !game.Touch.Visible;
        }
        void TouchControl(Rect rect, string label, bool held, Color color)
        {
            Rect(rect.x, rect.y, rect.width, rect.height, held ? color : panel);
            Rect(rect.x, rect.y, rect.width, 3, color);
            Text(rect.x, rect.y, rect.width, rect.height, label, 22, held ? panel : color, TextAnchor.MiddleCenter, true);
        }
        void Health(float x, string name, string tag, Fighter f, Color color, bool right)
        {
            Text(x, 116, 430, 30, name, 28, ink, right ? TextAnchor.UpperRight : TextAnchor.UpperLeft, true);
            Text(x, 151, 430, 24, tag, 12, color, right ? TextAnchor.UpperRight : TextAnchor.UpperLeft);
            Rect(x, 184, 430, 12, new Color(.12f, .20f, .22f));
            float fill = 430 * f.Health / f.MaxHealth;
            Rect(right ? x + 430 - fill : x, 184, fill, 12, color);
            Text(x, 204, 430, 24, Mathf.CeilToInt(f.Health) + " / " + Mathf.CeilToInt(f.MaxHealth), 13, muted, right ? TextAnchor.UpperRight : TextAnchor.UpperLeft);
        }
        void OnGUI()
        {
            if (game == null || game.Combat == null) return;
            var saved = GUI.matrix;
            var viewport = ArenaTouchInput.Viewport;
            float scale = viewport.width / 1440;
            GUI.matrix = Matrix4x4.TRS(viewport.position, Quaternion.identity, Vector3.one * scale);
            var c = game.Combat; var p = c.Player;
            Rect(52, 47, 33, 5, ArenaVisuals.Gold);
            Text(98, 31, 400, 35, "BUG ARENA", 27, ink, bold: true);
            Text(100, 66, 400, 20, game.Stage.Name + "  /  STAGE " + (game.StageIndex + 1) + " OF " + ArenaCampaign.Stages.Length, 11, muted);
            Text(1010, 41, 378, 25, "MOSSWOOD RUN  /  FIVE RIVALS", 12, muted, TextAnchor.UpperRight);
            Rect(52, 96, 1336, 1, new Color(.27f, .38f, .38f, .45f));
            Rect(52, 108, 1336, 130, new Color(.025f, .065f, .08f, .82f));
            Health(56, "JADE", "YOU  /  HORN POWER x" + p.DashDamageMultiplier.ToString("0.00"), p, ArenaVisuals.Teal, false);
            Health(954, game.Stage.Rival, c.Enemy.Enraged ? "BOSS / ENRAGED - PUNISH RECOVERY" : "RIVAL  /  STAGE " + (game.StageIndex + 1), c.Enemy, game.Stage.Accent, true);
            Text(580, 120, 280, 25, game.Stage.Name, 12, muted, TextAnchor.UpperCenter);
            Text(580, 151, 280, 41, game.PlayerWins + "  :  " + game.EnemyWins, 33, ArenaVisuals.Gold, TextAnchor.UpperCenter, true);
            Text(580, 200, 280, 25, Mathf.FloorToInt(game.TimeRemaining / 60).ToString("00") + ":" + Mathf.FloorToInt(game.TimeRemaining % 60).ToString("00"), 15, muted, TextAnchor.UpperCenter);
            if (game.AnnouncementLife > 0 && !game.RoundFinished && !game.Paused)
            {
                Color color = game.Announcement.Contains("LAUNCH") ? ArenaVisuals.Gold : ink;
                Text(250, 256, 940, 48, game.Announcement, 29, color, TextAnchor.UpperCenter, true);
                Text(250, 302, 940, 30, game.Touch.Visible ? game.AnnouncementDetail.Replace("SPACE", "CHARGE") : game.AnnouncementDetail, 14, muted, TextAnchor.UpperCenter);
            }
            if (game.Upgrades.Count > 0)
                Text(56, 605, 1328, 25, "BUILD / " + string.Join("  +  ", game.Upgrades.ConvertAll(k => System.Array.Find(ArenaUpgrade.All, u => u.Kind == k).Name)), 12, ArenaVisuals.Teal, TextAnchor.MiddleCenter);
            if (c.Orb != null && !game.RoundFinished)
            {
                bool lethal = c.Orb.Damage >= new System.Numerics.BigInteger(System.Math.Ceiling(c.Orb.Target.Health));
                Text(360, 635, 720, 36, (lethal ? "LETHAL!  " : "") + c.Orb.DamageLabel + " DAMAGE  /  RETURN " + c.Orb.Reflections, 24, lethal ? ArenaVisuals.Coral : ArenaVisuals.Gold, TextAnchor.MiddleCenter, true);
                Text(360, 674, 720, 24, "ONLY DASH END CONTACT REFLECTS / EVERY RETURN DOUBLES DAMAGE", 12, ink, TextAnchor.MiddleCenter);
            }
            if (c.Enemy.State == BugState.SuperCharging)
            {
                Text(980, 248, 408, 25, "RIVAL SUPER CHARGING " + Mathf.RoundToInt(c.Enemy.SuperPower * 100) + "%", 14, ArenaVisuals.Coral, TextAnchor.UpperRight, true);
                Rect(1150, 278, 234 * c.Enemy.SuperPower, 4, ArenaVisuals.Coral);
            }
            if (c.Enemy.State == BugState.Charging && !game.RoundFinished)
            {
                Text(980, 248, 408, 25, "RIVAL CHARGING", 14, ArenaVisuals.Coral, TextAnchor.UpperRight, true);
                Rect(1150, 278, 234, 4, new Color(.2f, .14f, .14f));
                Rect(1150, 278, c.Enemy.Power * 234, 4, ArenaVisuals.Coral);
            }
            if (!game.Touch.Visible)
            {
            Rect(52, 714, 496, 126, panel); Rect(52, 714, 3, 126, ArenaVisuals.Teal);
            string status = p.State == BugState.Charging ? p.Power >= 1 ? "MAX CHARGE / RELEASE" : "CHARGING / HOLD SPACE" : p.State == BugState.Recovering ? "RECOVERING" : p.State == BugState.Dashing ? "DASH!" : p.State == BugState.Airborne ? "AIRBORNE" : "HOLD SPACE TO CHARGE";
            Text(76, 731, 385, 28, status, 15, p.State == BugState.Charging ? ArenaVisuals.Gold : ink, bold: true);
            Text(444, 731, 80, 26, Mathf.RoundToInt(p.Power * 100) + "%", 17, ArenaVisuals.Gold, TextAnchor.UpperRight, true);
            Rect(76, 770, 448, 8, new Color(.13f, .22f, .23f));
            Rect(76, 770, 448 * (p.State == BugState.Charging ? p.Power : 0), 8, ArenaVisuals.Teal);
            Text(76, 794, 448, 24, "DASH RANGE  " + CombatRules.Distance(p.Charge).ToString("0.0") + " m    /    GOLD = LAUNCH ZONE", 12, muted);
            Text(589, 743, 330, 30, c.Finishers.ToString("00") + "   LAUNCHERS", 23, ArenaVisuals.Gold, TextAnchor.UpperCenter, true);
            Text(589, 785, 330, 24, c.Hits.ToString("00") + " HITS LANDED", 12, muted, TextAnchor.UpperCenter);
            Rect(954, 714, 434, 126, panel);
            Text(978, 731, 386, 25, "SUPER CORE / HOLD Q", 15, ArenaVisuals.Gold, bold: true);
            Text(978, 764, 386, 58, c.Orb != null ? "ORB IN PLAY / " + c.Orb.DamageLabel + " DAMAGE" : p.SuperCooldown > 0 ? "RECHARGING  " + p.SuperCooldown.ToString("0.0") + "s" : p.State == BugState.SuperCharging ? "CHARGE " + Mathf.RoundToInt(p.SuperPower * 100) + "% / RELEASE AT 100%" : "Hold Q to fully charge, then release.\nReturn with the END of a dash.", 15, ink);
            Text(56, 860, 700, 25, "A / D   MOVE       HOLD + RELEASE SPACE   DASH", 13, muted);
            Text(920, 860, 464, 25, "R   NEW RUN       ESC   PAUSE", 13, muted, TextAnchor.UpperRight);
            }
            else
            {
                TouchControl(ArenaTouchInput.Left, "<", game.Touch.LeftHeld, ArenaVisuals.Teal);
                TouchControl(ArenaTouchInput.Right, ">", game.Touch.RightHeld, ArenaVisuals.Teal);
                TouchControl(ArenaTouchInput.Super, p.SuperCooldown > 0 ? "SUPER\n" + p.SuperCooldown.ToString("0.0") + "s" : "HOLD / RELEASE\nSUPER", game.Touch.SuperHeld, ArenaVisuals.Teal);
                Rect(890, 858, 200 * p.SuperPower, 7, ArenaVisuals.Teal);
                TouchControl(ArenaTouchInput.Charge, "HOLD / RELEASE\nCHARGE + DASH", game.Touch.Held, ArenaVisuals.Gold);
                Rect(1110, 858, 274 * (p.State == BugState.Charging ? p.Power : 0), 7, ArenaVisuals.Gold);
                Text(360, 755, 500, 55, game.Stage.Hint, 17, ink, TextAnchor.MiddleCenter);
            }
            if (!game.RoundFinished && Button(660, 31, 120, 37, game.Paused ? "RESUME" : "PAUSE", ArenaVisuals.Gold)) game.TogglePause();
            if (!game.Paused && !game.RoundFinished && game.Countdown > 0)
                Text(580, 330, 280, 90, Mathf.CeilToInt(game.Countdown).ToString(), 72, ArenaVisuals.Gold, TextAnchor.MiddleCenter, true);
            if (game.ImpactFlash > 0)
            {
                Color color = game.LastWasPlayer ? ArenaVisuals.Gold : ArenaVisuals.Coral; color.a = game.ImpactFlash * .8f;
                Rect(0, 0, 1440, 4, color); Rect(0, 896, 1440, 4, color);
            }
            if ((game.Paused || game.RoundFinished) && !game.ChoosingUpgrade)
            {
                Rect(0, 0, 1440, 900, new Color(.015f, .035f, .045f, .78f));
                Rect(440, 282, 560, 320, panel); Rect(440, 282, 560, 3, ArenaVisuals.Gold);
                bool won = game.Won;
                Text(465, 316, 510, 58, game.Paused ? (game.RoundTime == 0 ? "READY TO DUEL" : "PAUSED") : game.CampaignComplete ? "CUP CHAMPION!" : won ? "STAGE CLEARED" : game.TimedOut ? "TIME UP" : "TRY AGAIN", 42, game.Paused ? ink : won ? ArenaVisuals.Teal : ArenaVisuals.Coral, TextAnchor.UpperCenter, true);
                Text(475, 391, 490, 65, game.Paused ? (game.RoundTime == 0 ? game.Stage.Hint : "Take a breath. Your next charge counts.") : c.Finishers + " launchers  /  " + c.Hits + " hits\n" + (game.CampaignComplete ? "Run complete. Try a different build." : won ? "Choose an evolution for the next ring." : (game.Stage.IsBoss ? "Start a new run. Defeat the boss before time runs out." : "Start a new run. Ties at time-up favor the rival.")), 17, muted, TextAnchor.UpperCenter);
                if (Button(520, 493, 400, 57, game.Paused ? (game.RoundTime == 0 ? (game.Touch.Visible ? "ENTER THE RING" : "ENTER THE RING  /  ENTER") : (game.Touch.Visible ? "RESUME" : "RESUME  /  ESC")) : game.CampaignComplete ? "PLAY CUP AGAIN" : won ? (game.Touch.Visible ? "CHOOSE UPGRADE" : "CHOOSE UPGRADE  /  ENTER") : (game.Touch.Visible ? "NEW RUN" : "NEW RUN  /  ENTER"), ArenaVisuals.Gold))
                { if (game.Paused) game.TogglePause(); else game.Continue(); }
            }
            if (game.Paused && game.RoundTime == 0)
                for (int i = 0; i < ArenaCampaign.Stages.Length; i++)
                    if (Button(245 + i * 190, 625, 180, 43, i <= game.UnlockedStage ? "STAGE " + (i + 1) : "LOCKED", i == game.StageIndex ? ArenaVisuals.Teal : muted) && i <= game.UnlockedStage) game.SelectStage(i);
            if (game.ChoosingUpgrade)
            {
                Rect(0, 0, 1440, 900, new Color(.015f,.035f,.045f,.94f));
                Text(180, 245, 1080, 60, "EVOLVE YOUR BEETLE", 42, ArenaVisuals.Gold, TextAnchor.MiddleCenter, true);
                Text(180, 310, 1080, 30, "CHOOSE ONE / LASTS FOR THIS RUN / " + game.Upgrades.Count + " EVOLUTIONS OWNED", 15, ink, TextAnchor.MiddleCenter);
                for (int i = 0; i < game.Offers.Length; i++)
                {
                    var rect = ArenaTouchInput.UpgradeCard(i); var offer = game.Offers[i];
                    if (Button(rect.x, rect.y, rect.width, rect.height, "[" + (i + 1) + "]  " + offer.Name + "\n\n" + offer.Detail, ArenaVisuals.Teal)) { game.ChooseUpgrade(i); break; }
                }
            }
            GUI.matrix = saved;
        }
    }
}
