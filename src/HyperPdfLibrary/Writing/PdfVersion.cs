// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;

namespace HyperPdfLibrary.Writing;

/// <summary>A PDF version such as 1.7 or 2.0, as the <c>%PDF-</c> header and the catalog's <c>/Version</c> name it (ISO 32000-2 §7.5.2, §7.2.2).</summary>
/// <param name="Major">The major number: 1 or 2.</param>
/// <param name="Minor">The minor number, 0 to 9.</param>
[DebuggerDisplay("PdfVersion: {Major}.{Minor}")]
public readonly record struct PdfVersion(int Major, int Minor) : IComparable<PdfVersion>, IComparable
{
    /// <summary>The length of text such as "1.7".</summary>
    private const int TextLength = 3;

    /// <summary>The position of the dot in text such as "1.7".</summary>
    private const int DotPosition = 1;

    /// <summary>The position of the minor digit in text such as "1.7".</summary>
    private const int MinorPosition = 2;

    /// <summary>The major number of PDF 2.0.</summary>
    private const int SecondMajor = 2;

    /// <summary>The minor number of PDF 1.4.</summary>
    private const int FourthMinor = 4;

    /// <summary>The minor number of PDF 1.5.</summary>
    private const int FifthMinor = 5;

    /// <summary>The minor number of PDF 1.7.</summary>
    private const int SeventhMinor = 7;

    /// <summary>Gets PDF 1.4.</summary>
    public static PdfVersion Pdf14 => new(1, FourthMinor);

    /// <summary>Gets PDF 1.5, the first version with object streams and cross-reference streams.</summary>
    public static PdfVersion Pdf15 => new(1, FifthMinor);

    /// <summary>Gets PDF 1.7 (ISO 32000-1).</summary>
    public static PdfVersion Pdf17 => new(1, SeventhMinor);

    /// <summary>Gets PDF 2.0 (ISO 32000-2).</summary>
    public static PdfVersion Pdf20 => new(SecondMajor, 0);

    /// <summary>Gets a value indicating whether this is the unset version, which is older than every real one.</summary>
    public bool IsUnset => Major == 0 && Minor == 0;

    /// <summary>Determines whether the left version is older than the right.</summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    /// <returns><see langword="true"/> when it is older.</returns>
    public static bool operator <(PdfVersion left, PdfVersion right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left version is newer than the right.</summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    /// <returns><see langword="true"/> when it is newer.</returns>
    public static bool operator >(PdfVersion left, PdfVersion right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left version is older than or the same as the right.</summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public static bool operator <=(PdfVersion left, PdfVersion right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left version is newer than or the same as the right.</summary>
    /// <param name="left">The left version.</param>
    /// <param name="right">The right version.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public static bool operator >=(PdfVersion left, PdfVersion right) => left.CompareTo(right) >= 0;

    /// <summary>Reads version text such as "1.7".</summary>
    /// <param name="text">The text.</param>
    /// <param name="version">The version, or the unset version when the text is not one.</param>
    /// <returns><see langword="true"/> when the text is a version: one digit, a dot and one digit.</returns>
    public static bool TryParse(ReadOnlySpan<char> text, out PdfVersion version)
    {
        version = default;
        if (text.Length != TextLength || text[DotPosition] != '.' || !char.IsAsciiDigit(text[0]) || !char.IsAsciiDigit(text[MinorPosition]))
        {
            return false;
        }

        version = new(text[0] - '0', text[MinorPosition] - '0');
        return true;
    }

    /// <summary>Reads version text such as "1.7", or the unset version when it is not one.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The version.</returns>
    public static PdfVersion ParseOrUnset(ReadOnlySpan<char> text) => TryParse(text, out var version) ? version : default;

    /// <summary>Gets the newer of two versions.</summary>
    /// <param name="first">The first version.</param>
    /// <param name="second">The second version.</param>
    /// <returns>The newer one.</returns>
    public static PdfVersion Max(PdfVersion first, PdfVersion second) => first >= second ? first : second;

    /// <inheritdoc/>
    public int CompareTo(PdfVersion other) => Major != other.Major ? Major.CompareTo(other.Major) : Minor.CompareTo(other.Minor);

    /// <inheritdoc/>
    int IComparable.CompareTo(object? obj) => obj is PdfVersion other ? CompareTo(other) : 1;

    /// <summary>Gets the version as it is written, such as "1.7".</summary>
    /// <returns>The text, or an empty string for the unset version.</returns>
    public override string ToString() => IsUnset ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}");
}
