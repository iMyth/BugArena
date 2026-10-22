using UnityEngine;
using UnityEngine.InputSystem;

namespace BugArena
{
    // Poll each finger independently: dragging off a control releases it, and canceled
    // or missing touches cannot leave a button held after app focus changes.
    public sealed class ArenaTouchInput : MonoBehaviour
    {
        public static readonly Rect Left = new Rect(56, 725, 130, 120);
        public static readonly Rect Right = new Rect(202, 725, 130, 120);
        public static readonly Rect Charge = new Rect(1110, 710, 274, 140);
        public static Rect Viewport
        {
            get
            {
                var safe = Screen.safeArea;
                float scale = Mathf.Min(safe.width / 1440f, safe.height / 900f);
                return new Rect(safe.x + (safe.width - 1440 * scale) / 2,
                    Screen.height - safe.yMax + (safe.height - 900 * scale) / 2, 1440 * scale, 900 * scale);
            }
        }
        public static readonly Rect PrimaryAction = new Rect(520, 493, 400, 57);
        public static readonly Rect PauseAction = new Rect(660, 31, 120, 37);
        public Vector2? PressedPoint { get; private set; }
        public bool PreviewControls;
        public bool Visible => PreviewControls || Application.isMobilePlatform || (Touchscreen.current != null);
        public float Move { get; private set; }
        public bool Held { get; private set; }
        public bool LeftHeld { get; private set; }
        public bool RightHeld { get; private set; }
        public void Sample() { Sample(Application.isFocused); }
        public void Sample(bool hasFocus)
        {
            Clear();
            if (!hasFocus) return;
            if (PreviewControls && Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                var previewViewport = Viewport;
                var previewScreen = Mouse.current.position.ReadValue();
                var previewPoint = (new Vector2(previewScreen.x, Screen.height - previewScreen.y) - previewViewport.position) / (previewViewport.width / 1440);
                LeftHeld = Left.Contains(previewPoint); RightHeld = Right.Contains(previewPoint); Held = Charge.Contains(previewPoint);
                if (Mouse.current.leftButton.wasPressedThisFrame) PressedPoint = previewPoint;
            }
            if (Touchscreen.current == null)
            { Move = (RightHeld ? 1 : 0) - (LeftHeld ? 1 : 0); return; }
            var viewport = Viewport; float scale = viewport.width / 1440;
            foreach (var touch in Touchscreen.current.touches)
            {
                if (!touch.press.isPressed) continue;
                var screen = touch.position.ReadValue();
                var point = (new Vector2(screen.x, Screen.height - screen.y) - viewport.position) / scale;
                if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began) PressedPoint = point;
                LeftHeld |= Left.Contains(point); RightHeld |= Right.Contains(point); Held |= Charge.Contains(point);
            }
            Move = (RightHeld ? 1 : 0) - (LeftHeld ? 1 : 0);
        }
        public void Clear() { PressedPoint = null; Move = 0; Held = LeftHeld = RightHeld = false; }
        void OnDisable() { Clear(); }
    }
}
