using System;
using UnityEngine;

namespace GTFOSoundReplacer
{
    /// <summary>热键修饰键。</summary>
    public enum HotkeyModifier
    {
        None = 0,
        Ctrl = 1,
        Shift = 2,
        Alt = 3,
    }

    /// <summary>
    /// 一个已解析的热键绑定：主键（KeyCode）+ 可选修饰键。
    /// 从 config 的字符串值解析，支持双键组合（如 Ctrl+F8）。
    /// </summary>
    public readonly struct HotkeyBinding
    {
        public readonly KeyCode Key;
        public readonly HotkeyModifier Modifier;

        public HotkeyBinding(KeyCode key, HotkeyModifier modifier)
        {
            Key = key;
            Modifier = modifier;
        }

        /// <summary>从 config 字符串解析（解析失败时回退到默认 F8 / None）。</summary>
        public static HotkeyBinding Parse(string keyName, string modifierName)
        {
            return new HotkeyBinding(ParseKey(keyName), ParseModifier(modifierName));
        }

        public static KeyCode ParseKey(string name)
        {
            if (string.IsNullOrEmpty(name))
                return KeyCode.F8;

            if (Enum.TryParse<KeyCode>(name, true, out var key))
                return key;

            return KeyCode.F8;
        }

        public static HotkeyModifier ParseModifier(string name)
        {
            if (string.IsNullOrEmpty(name))
                return HotkeyModifier.None;

            if (Enum.TryParse<HotkeyModifier>(name, true, out var mod))
                return mod;

            return HotkeyModifier.None;
        }

        /// <summary>本帧是否按下该热键（主键按下 + 修饰键保持）。</summary>
        public bool IsPressedThisFrame()
        {
            if (!Input.GetKeyDown(Key))
                return false;

            return IsModifierHeld();
        }

        /// <summary>修饰键当前是否处于按下状态（None 视为恒成立）。</summary>
        public bool IsModifierHeld()
        {
            switch (Modifier)
            {
                case HotkeyModifier.Ctrl:
                    return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                case HotkeyModifier.Shift:
                    return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                case HotkeyModifier.Alt:
                    return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                default:
                    return true;
            }
        }
    }
}
