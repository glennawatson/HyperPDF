// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Reads and compares the unlimited decimal numbers in Hn structure names.</summary>
internal static class NumberedHeadings
{
    /// <summary>The length of H followed by one digit.</summary>
    private const int SingleDigitNameLength = 2;

    /// <summary>The longest canonical heading name whose number may fit in Int32.</summary>
    private const int IntegerNameLength = 11;

    /// <summary>Checks the canonical Hn spelling without converting its number.</summary>
    /// <param name="name">The UTF-8 structure name.</param>
    /// <returns>True for H followed by decimal digits without leading zeroes.</returns>
    internal static bool IsName(ReadOnlySpan<byte> name) =>
        name.Length > 1 && name[0] == (byte)'H' && name[1] is >= (byte)'1' and <= (byte)'9'
        && name[1..].IndexOfAnyExceptInRange((byte)'0', (byte)'9') < 0;

    /// <summary>Checks the canonical Hn spelling without converting its number.</summary>
    /// <param name="name">The structure name.</param>
    /// <returns>True for H followed by decimal digits without leading zeroes.</returns>
    internal static bool IsName(ReadOnlySpan<char> name) =>
        name.Length > 1 && name[0] == 'H' && name[1] is >= '1' and <= '9'
        && name[1..].IndexOfAnyExceptInRange('0', '9') < 0;

    /// <summary>Checks for an Hn name deeper than the six built-in levels.</summary>
    /// <param name="name">The structure name.</param>
    /// <returns>True for an extended numbered heading.</returns>
    internal static bool IsExtendedName(ReadOnlySpan<char> name) =>
        name.Length > 1 && (name.Length > SingleDigitNameLength || name[1] > '6') && IsName(name);

    /// <summary>Gets the exact numbered name after namespace and role-map resolution.</summary>
    /// <param name="element">The resolved element.</param>
    /// <returns>The retained name, or empty for an unnumbered or non-heading element.</returns>
    internal static string GetName(PdfStructureElement element)
    {
        if (element.Type == PdfStructureType.NumberedHeading)
        {
            return element.MappedType;
        }

        if (element.Type is < PdfStructureType.Heading1 or > PdfStructureType.Heading6)
        {
            return string.Empty;
        }

        // PDF/UA-1 extended headings retain their number while mapping to a PDF 1.7 heading.
        return IsPdf17Extension(element)
            ? element.RawType
            : element.MappedType;
    }

    /// <summary>Gets a heading's integer level without changing its retained exact number.</summary>
    /// <param name="element">The resolved element.</param>
    /// <returns>The level, saturated at Int32.MaxValue; zero for a non-heading.</returns>
    internal static int GetLevel(PdfStructureElement element)
    {
        if (element.Type != PdfStructureType.NumberedHeading
            && (element.Type is < PdfStructureType.Heading1 or > PdfStructureType.Heading6 || !IsPdf17Extension(element)))
        {
            return PdfStructureTypes.GetHeadingLevel(element.Type);
        }

        var name = element.Type == PdfStructureType.NumberedHeading ? element.MappedType : element.RawType;
        if (name.Length == SingleDigitNameLength)
        {
            return name[1] - '0';
        }

        return name.Length > IntegerNameLength || !int.TryParse(name.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var level)
            ? int.MaxValue
            : level;
    }

    /// <summary>Compares canonical numbered names without converting them to bounded integers.</summary>
    /// <param name="previous">The preceding name, or empty before the first heading.</param>
    /// <param name="current">The current numbered heading name.</param>
    /// <returns>True when the current level exceeds the previous level plus one.</returns>
    internal static bool SkipsLevel(string previous, string current)
    {
        if (previous.Length == 0)
        {
            return current != "H1";
        }

        var before = previous.AsSpan(1);
        var after = current.AsSpan(1);
        if (after.Length < before.Length || (after.Length == before.Length && after.SequenceCompareTo(before) <= 0))
        {
            return false;
        }

        return !IsSuccessor(before, after);
    }

    /// <summary>Checks whether an original PDF 1.7 name carries an extended heading number.</summary>
    /// <param name="element">The resolved heading element.</param>
    /// <returns>True when its original name supplies the level.</returns>
    private static bool IsPdf17Extension(PdfStructureElement element) =>
        element.Namespace is null or PdfStructureTypes.Pdf17Uri && IsExtendedName(element.RawType.AsSpan());

    /// <summary>Tests decimal succession without allocating a formatted number.</summary>
    /// <param name="before">The previous number's canonical digits.</param>
    /// <param name="after">The current number's canonical digits.</param>
    /// <returns>True when the second number is exactly one greater.</returns>
    private static bool IsSuccessor(ReadOnlySpan<char> before, ReadOnlySpan<char> after)
    {
        var pivot = before.LastIndexOfAnyExcept('9');
        return pivot < 0
            ? after.Length == before.Length + 1 && after[0] == '1' && after[1..].IndexOfAnyExcept('0') < 0
            : after.Length == before.Length && after[..pivot].SequenceEqual(before[..pivot])
                && after[pivot] == before[pivot] + 1 && after[(pivot + 1)..].IndexOfAnyExcept('0') < 0;
    }
}
