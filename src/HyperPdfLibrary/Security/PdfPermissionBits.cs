// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>Converts between <see cref="PdfAccessPermissions"/> and the 32-bit permission word of ISO 32000-2 Tables 22 and 24.</summary>
internal static class PdfPermissionBits
{
    /// <summary>
    /// The bits a writer sets whatever the caller chooses: bits 7 and 8 (reserved, must be 1) and bits 13 to 32 (reserved,
    /// must be 1). Bit 13 stays set, which also means the file needs no PDF MAC (ISO/TS 32004).
    /// </summary>
    internal const int Reserved = unchecked((int)0xFFFFF0C0);

    /// <summary>The bits that carry a permission in the standard security handler: bits 3, 4, 5, 6, 9, 10, 11 and 12.</summary>
    private const int StandardMask = 0xF3C;

    /// <summary>Bit 2, which only public-key recipients use.</summary>
    private const int ChangeEncryptionBit = 2;

    /// <summary>Bit 3, printing.</summary>
    private const int PrintBit = 4;

    /// <summary>Bit 10, the accessibility bit ISO 32000-2 retired.</summary>
    private const int AccessibilityBit = 512;

    /// <summary>Bit 12, high quality printing.</summary>
    private const int HighQualityBit = 2048;

    /// <summary>Builds the <c>/P</c> value of the standard security handler.</summary>
    /// <param name="permissions">What the user may do.</param>
    /// <param name="keepAccessibilityBit">Whether bit 10 follows the flags; it is always written set when <see langword="false"/> (PDF 2.0 files).</param>
    /// <returns>The permission word, a negative number because the reserved high bits are set.</returns>
    internal static int ToStandard(PdfAccessPermissions permissions, bool keepAccessibilityBit)
    {
        var bits = Normalise(permissions) & StandardMask;
        if (!keepAccessibilityBit)
        {
            bits |= AccessibilityBit;
        }

        return Reserved | bits;
    }

    /// <summary>Builds the permission word held in a public-key recipient list (Table 24): bit 2 and the standard bits, bit 10 always set.</summary>
    /// <param name="permissions">What the recipients may do.</param>
    /// <returns>The permission word.</returns>
    internal static int ToPublicKey(PdfAccessPermissions permissions) =>
        Reserved | ((Normalise(permissions) & (StandardMask | ChangeEncryptionBit)) | AccessibilityBit);

    /// <summary>Reads the permissions a word allows.</summary>
    /// <param name="word">The <c>/P</c> value or the public-key permission word.</param>
    /// <returns>The flags.</returns>
    internal static PdfAccessPermissions FromWord(int word) => (PdfAccessPermissions)(word & (StandardMask | ChangeEncryptionBit));

    /// <summary>Determines whether a word allows every permission a user can be given.</summary>
    /// <param name="word">The permission word.</param>
    /// <returns><see langword="true"/> when printing, modifying, copying, annotating, forms, assembling and high quality printing are all allowed.</returns>
    internal static bool AllowsEverything(int word)
    {
        const PdfAccessPermissions Required = PdfAccessPermissions.All & ~PdfAccessPermissions.ExtractForAccessibility;
        return (FromWord(word) & Required) == Required;
    }

    /// <summary>High quality printing means nothing without printing, so it is cleared with it, as Acrobat does.</summary>
    /// <param name="permissions">The flags.</param>
    /// <returns>The flags as bits.</returns>
    private static int Normalise(PdfAccessPermissions permissions)
    {
        var bits = (int)permissions;
        return (bits & PrintBit) == 0 ? bits & ~HighQualityBit : bits;
    }
}
