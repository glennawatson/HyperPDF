// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// The appearance states of the buttons of a check box or radio group (ISO 32000-2 12.7.4.2.4 and 12.7.4.2.5). A button
/// is named by its export value when that is a plain name that no other button shares. Otherwise the buttons are named
/// by their position and the export values go in the field's <c>/Opt</c> array.
/// </summary>
/// <param name="Exports">The export value of each button.</param>
/// <param name="States">The on-state name of each button.</param>
/// <param name="UsesOptions">Whether the export values are kept in <c>/Opt</c>.</param>
[DebuggerDisplay("FormButtonStates: {Exports.Length} buttons")]
internal sealed record FormButtonStates(string[] Exports, string[] States, bool UsesOptions)
{
    /// <summary>The prefix of export values given to radio buttons that name none.</summary>
    internal const string DefaultPrefix = "Choice";

    /// <summary>Plans the states of buttons.</summary>
    /// <param name="exports">The export value of each button.</param>
    /// <returns>The states.</returns>
    internal static FormButtonStates Plan(string[] exports)
    {
        var seen = new HashSet<string>(exports.Length, StringComparer.Ordinal);
        var plain = true;
        foreach (var export in exports)
        {
            plain &= IsPlainName(export) && seen.Add(export);
        }

        if (plain)
        {
            return new(exports, exports, false);
        }

        var states = new string[exports.Length];
        for (var i = 0; i < states.Length; i++)
        {
            states[i] = i.ToString(CultureInfo.InvariantCulture);
        }

        return new(exports, states, true);
    }

    /// <summary>Gives the export value a radio button named none.</summary>
    /// <param name="position">The button's zero based position.</param>
    /// <returns>The export value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string DefaultExport(int position) => string.Concat(DefaultPrefix, (position + 1).ToString(CultureInfo.InvariantCulture));

    /// <summary>Determines whether an export value can be a name without escapes and is not <c>Off</c>.</summary>
    /// <param name="export">The export value.</param>
    /// <returns><see langword="true"/> for a plain name.</returns>
    internal static bool IsPlainName(string export)
    {
        if (export.Length == 0 || export.Length > PdfLimits.MaxNameLength || string.Equals(export, FormValues.OffState, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var c in export)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-' or '.' or '+'))
            {
                return false;
            }
        }

        return true;
    }
}
