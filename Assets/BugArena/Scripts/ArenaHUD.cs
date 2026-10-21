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
            return GUI.Button(new Rect(x, y, w, h), GUIContent.none, GUIStyle.none);
        }
        void Health(float x, string name, string tag, Fighter f, Color color, bool right)
        {
            Text(x, 116, 430, 30, name, 28, ink, right ? TextAnchor.UpperRight : TextAnchor.UpperLeft, true);
            Text(x, 151, 430, 24, tag, 12, color, right ? TextAnchor.UpperRight : TextAnchor.UpperLeft);
            Rect(x, 184, 430, 12, new Color(.12f, .20f, .22f));
            float fill = 430 * f.Health / 100;
            Rect(right ? x + 430 - fill : x, 184, fill, 12, color);
            Text(x, 204, 430, 24, Mathf.CeilToInt(f.Health) + " / 100", 13, muted, right ? TextAnchor.UpperRight : TextAnchor.UpperLeft);
        }
        void OnGUI()
        {
            if (game == null || game.Combat == null) return;
            var saved = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            var offset = new Vector3((Screen.width - 1440 * scale) / 2, (Screen.height - 900 * scale) / 2, 0);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one * scale);
            var c = game.Combat; var p = c.Player;
            Rect(52, 47, 33, 5, ArenaVisuals.Gold);
            Text(98, 31, 400, 35, "BUG ARENA", 27, ink, bold: true);
            Text(100, 66, 400, 20, "M O S S W O O D   /   D U E L   0 1", 11, muted);
            Text(1010, 41, 378, 25, "SIDE-VIEW COMBAT PROTOTYPE", 12, muted, TextAnchor.UpperRight);
            Rect(52, 96, 1336, 1, new Color(.27f, .38f, .38f, .45f));
            Health(56, "JADE", "YOU  /  RHINO BEETLE", p, ArenaVisuals.Teal, false);
            Health(954, "EMBER", "RIVAL  /  TRAINING AI", c.Enemy, ArenaVisuals.Coral, true);
            Text(580, 120, 280, 25, "MOSSWOOD RING", 12, muted, TextAnchor.UpperCenter);
            Text(580, 151, 280, 41, game.PlayerWins + "  :  " + game.EnemyWins, 33, ArenaVisuals.Gold, TextAnchor.UpperCenter, true);
            Text(580, 200, 280, 25, Mathf.FloorToInt(game.RoundTime / 60).ToString("00") + ":" + Mathf.FloorToInt(game.RoundTime % 60).ToString("00"), 15, muted, TextAnchor.UpperCenter);
            if (game.AnnouncementLife > 0 && !c.Finished && !game.Paused)
            {
                Color color = game.Announcement.Contains("LAUNCH") ? ArenaVisuals.Gold : ink;
                Text(250, 256, 940, 48, game.Announcement, 29, color, TextAnchor.UpperCenter, true);
                Text(250, 302, 940, 30, game.AnnouncementDetail, 14, muted, TextAnchor.UpperCenter);
            }
            if (c.Enemy.State == BugState.Charging && !c.Finished)
            {
                Text(980, 248, 408, 25, "RIVAL CHARGING", 14, ArenaVisuals.Coral, TextAnchor.UpperRight, true);
                Rect(1150, 278, 234, 4, new Color(.2f, .14f, .14f));
                Rect(1150, 278, c.Enemy.Power * 234, 4, ArenaVisuals.Coral);
            }
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
            Text(978, 731, 386, 25, "THE SWEET SPOT", 15, ArenaVisuals.Gold, bold: true);
            Text(978, 764, 386, 58, "Catch your rival in the gold end-zone.\nToo close? A short charge beats a long one.", 15, ink);
            Text(56, 860, 700, 25, "A / D   MOVE       HOLD + RELEASE SPACE   DASH", 13, muted);
            Text(920, 860, 464, 25, "R   REMATCH       ESC   PAUSE", 13, muted, TextAnchor.UpperRight);
            if (game.ImpactFlash > 0)
            {
                Color color = game.LastWasPlayer ? ArenaVisuals.Gold : ArenaVisuals.Coral; color.a = game.ImpactFlash * .8f;
                Rect(0, 0, 1440, 4, color); Rect(0, 896, 1440, 4, color);
            }
            if (game.Paused || c.Finished)
            {
                Rect(0, 0, 1440, 900, new Color(.015f, .035f, .045f, .78f));
                Rect(440, 282, 560, 320, panel); Rect(440, 282, 560, 3, ArenaVisuals.Gold);
                bool won = c.Enemy.Health <= 0;
                Text(465, 316, 510, 58, game.Paused ? (game.RoundTime == 0 ? "READY TO DUEL" : "PAUSED") : won ? "JADE WINS" : "EMBER WINS", 42, game.Paused ? ink : won ? ArenaVisuals.Teal : ArenaVisuals.Coral, TextAnchor.UpperCenter, true);
                Text(475, 391, 490, 65, game.Paused ? (game.RoundTime == 0 ? "A / D to move. Hold SPACE, release to dash.\nLand the gold end-zone to launch your rival." : "Take a breath. Your next charge counts.") : c.Finishers + " launchers  /  " + c.Hits + " hits\nAdjust your spacing. Make the last inch count.", 17, muted, TextAnchor.UpperCenter);
                if (Button(520, 493, 400, 57, game.Paused ? (game.RoundTime == 0 ? "ENTER THE RING  /  ENTER" : "RESUME  /  ESC") : "REMATCH  /  R", ArenaVisuals.Gold))
                { if (game.Paused) game.TogglePause(); else game.Restart(); }
            }
            GUI.matrix = saved;
        }
    }
}
