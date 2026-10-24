using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace PangGolf
{
    public enum GKey
    {
        Shot, Confirm, Back,
        AimLeft, AimRight, ClubUp, ClubDown,
        SpinUp, SpinDown, SpinLeft, SpinRight, SpinReset,
        PowerShot, SpecialShot,
        ViewLanding, ViewGreen,
        Restart, Help, Fine, Graphics
    }

    /// <summary>
    /// Thin keyboard wrapper so the game runs with either the legacy Input Manager
    /// or the new Input System package (Unity 6 default).
    /// </summary>
    public static class GInput
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        static ButtonControl[] Controls(GKey k)
        {
            var kb = Keyboard.current;
            if (kb == null) return System.Array.Empty<ButtonControl>();
            switch (k)
            {
                case GKey.Shot: return new ButtonControl[] { kb.spaceKey };
                case GKey.Confirm: return new ButtonControl[] { kb.enterKey, kb.numpadEnterKey, kb.spaceKey };
                case GKey.Back: return new ButtonControl[] { kb.escapeKey };
                case GKey.AimLeft: return new ButtonControl[] { kb.leftArrowKey };
                case GKey.AimRight: return new ButtonControl[] { kb.rightArrowKey };
                case GKey.ClubUp: return new ButtonControl[] { kb.upArrowKey };
                case GKey.ClubDown: return new ButtonControl[] { kb.downArrowKey };
                case GKey.SpinUp: return new ButtonControl[] { kb.wKey };
                case GKey.SpinDown: return new ButtonControl[] { kb.sKey };
                case GKey.SpinLeft: return new ButtonControl[] { kb.aKey };
                case GKey.SpinRight: return new ButtonControl[] { kb.dKey };
                case GKey.SpinReset: return new ButtonControl[] { kb.xKey };
                case GKey.PowerShot: return new ButtonControl[] { kb.eKey };
                case GKey.SpecialShot: return new ButtonControl[] { kb.qKey };
                case GKey.ViewLanding: return new ButtonControl[] { kb.vKey };
                case GKey.ViewGreen: return new ButtonControl[] { kb.cKey };
                case GKey.Restart: return new ButtonControl[] { kb.rKey };
                case GKey.Help: return new ButtonControl[] { kb.hKey };
                case GKey.Fine: return new ButtonControl[] { kb.leftShiftKey, kb.rightShiftKey };
                case GKey.Graphics: return new ButtonControl[] { kb.gKey };
            }
            return System.Array.Empty<ButtonControl>();
        }

        public static bool Down(GKey k)
        {
            foreach (var c in Controls(k)) if (c.wasPressedThisFrame) return true;
            if ((k == GKey.Shot || k == GKey.Confirm) && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            return false;
        }

        public static bool Held(GKey k)
        {
            foreach (var c in Controls(k)) if (c.isPressed) return true;
            return false;
        }
#else
        static KeyCode[] Keys(GKey k)
        {
            switch (k)
            {
                case GKey.Shot: return new[] { KeyCode.Space };
                case GKey.Confirm: return new[] { KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Space };
                case GKey.Back: return new[] { KeyCode.Escape };
                case GKey.AimLeft: return new[] { KeyCode.LeftArrow };
                case GKey.AimRight: return new[] { KeyCode.RightArrow };
                case GKey.ClubUp: return new[] { KeyCode.UpArrow };
                case GKey.ClubDown: return new[] { KeyCode.DownArrow };
                case GKey.SpinUp: return new[] { KeyCode.W };
                case GKey.SpinDown: return new[] { KeyCode.S };
                case GKey.SpinLeft: return new[] { KeyCode.A };
                case GKey.SpinRight: return new[] { KeyCode.D };
                case GKey.SpinReset: return new[] { KeyCode.X };
                case GKey.PowerShot: return new[] { KeyCode.E };
                case GKey.SpecialShot: return new[] { KeyCode.Q };
                case GKey.ViewLanding: return new[] { KeyCode.V };
                case GKey.ViewGreen: return new[] { KeyCode.C };
                case GKey.Restart: return new[] { KeyCode.R };
                case GKey.Help: return new[] { KeyCode.H };
                case GKey.Fine: return new[] { KeyCode.LeftShift, KeyCode.RightShift };
                case GKey.Graphics: return new[] { KeyCode.G };
            }
            return new KeyCode[0];
        }

        public static bool Down(GKey k)
        {
            foreach (var c in Keys(k)) if (Input.GetKeyDown(c)) return true;
            if ((k == GKey.Shot || k == GKey.Confirm) && Input.GetMouseButtonDown(0)) return true;
            return false;
        }

        public static bool Held(GKey k)
        {
            foreach (var c in Keys(k)) if (Input.GetKey(c)) return true;
            return false;
        }
#endif
    }
}
