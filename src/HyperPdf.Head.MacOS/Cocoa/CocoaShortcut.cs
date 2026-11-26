// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Head.MacOS.Cocoa;

/// <summary>A shortcut as a Cocoa menu item shows it: the key and the modifier keys held with it.</summary>
/// <param name="Key">The key's character, or empty when the item shows no shortcut.</param>
/// <param name="Modifiers">The <c>NSEventModifierFlags</c> of the keys held with it.</param>
internal readonly record struct CocoaShortcut(string Key, ulong Modifiers)
{
    /// <summary>The Command key; the viewer reads Command as Control, so a Control shortcut is shown with Command.</summary>
    private const ulong CommandKey = 1 << 20;

    /// <summary>The Shift key.</summary>
    private const ulong ShiftKey = 1 << 17;

    /// <summary>The Option key, which the viewer reads as Alt.</summary>
    private const ulong OptionKey = 1 << 19;

    /// <summary>The joiner between the keys of a shortcut.</summary>
    private const char Joiner = '+';

    /// <summary>The characters AppKit shows for keys that have no character of their own.</summary>
    private static readonly Dictionary<string, string> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Delete"] = "\uF728",
        ["Del"] = "\uF728",
        ["Backspace"] = "\b",
        ["Home"] = "\uF729",
        ["End"] = "\uF72B",
        ["PageUp"] = "\uF72C",
        ["PageDown"] = "\uF72D",
        ["Up"] = "\uF700",
        ["Down"] = "\uF701",
        ["Left"] = "\uF702",
        ["Right"] = "\uF703",
        ["Tab"] = "\t",
        ["Enter"] = "\r",
        ["Esc"] = "\u001B",
        ["Escape"] = "\u001B",
        ["Space"] = " ",
        ["Plus"] = "+",
        ["Minus"] = "-",
        ["F1"] = "\uF704",
        ["F2"] = "\uF705",
        ["F3"] = "\uF706",
        ["F4"] = "\uF707",
        ["F5"] = "\uF708",
        ["F6"] = "\uF709",
        ["F7"] = "\uF70A",
        ["F8"] = "\uF70B",
        ["F9"] = "\uF70C",
        ["F10"] = "\uF70D",
        ["F11"] = "\uF70E",
        ["F12"] = "\uF70F",
    };

    /// <summary>Gets the shortcut of an item that shows none; its key is empty rather than <see langword="null"/>, as AppKit needs a string.</summary>
    internal static CocoaShortcut None { get; } = new(string.Empty, 0);

    /// <summary>Reads a shortcut as the viewer writes it, such as "Ctrl+Shift+Z", "Ctrl++" or "Delete".</summary>
    /// <param name="gesture">The shortcut, or <see langword="null"/>.</param>
    /// <returns>The shortcut; empty when there is none or its key has no character AppKit shows.</returns>
    internal static CocoaShortcut From(string? gesture)
    {
        if (string.IsNullOrEmpty(gesture))
        {
            return None;
        }

        // A plus as the key itself is written as a second plus at the end.
        var keyStart = gesture.EndsWith("++", StringComparison.Ordinal) ? gesture.Length - 1 : gesture.LastIndexOf(Joiner) + 1;
        var key = KeyOf(gesture[keyStart..]);
        return key.Length == 0 ? None : new(key, ModifiersOf(gesture.AsSpan(0, Math.Max(keyStart - 1, 0))));
    }

    /// <summary>Finds the character of a key.</summary>
    /// <param name="name">The key as the shortcut names it.</param>
    /// <returns>The character, lower case for a letter; empty when AppKit has none for it.</returns>
    private static string KeyOf(string name) => name.Length == 1 ? name.ToLowerInvariant() : NamedKeys.GetValueOrDefault(name, string.Empty);

    /// <summary>Reads the modifier keys before the key.</summary>
    /// <param name="modifiers">The modifier keys, joined by plus signs.</param>
    /// <returns>The modifier flags.</returns>
    private static ulong ModifiersOf(ReadOnlySpan<char> modifiers)
    {
        ulong flags = 0;
        foreach (var range in modifiers.Split(Joiner))
        {
            var name = modifiers[range];
            flags |= name switch
            {
                "Ctrl" or "Cmd" => CommandKey,
                "Shift" => ShiftKey,
                "Alt" or "Option" => OptionKey,
                _ => 0,
            };
        }

        return flags;
    }
}
