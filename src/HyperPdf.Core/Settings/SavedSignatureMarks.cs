// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Annotations;

namespace HyperPdf.Core.Settings;

/// <summary>The signature and initials the user chose to remember; either may be absent.</summary>
[DebuggerDisplay("SavedSignatureMarks: Signature: {Signature != null}, Initials: {Initials != null}")]
public sealed class SavedSignatureMarks
{
    /// <summary>Gets or sets the remembered signature, or <see langword="null"/>.</summary>
    public SignatureMark? Signature { get; set; }

    /// <summary>Gets or sets the remembered initials, or <see langword="null"/>.</summary>
    public SignatureMark? Initials { get; set; }

    /// <summary>Gets the remembered mark of a kind.</summary>
    /// <param name="kind">Signature or initials.</param>
    /// <returns>The mark, or <see langword="null"/>.</returns>
    public SignatureMark? Get(SignatureMarkKind kind) => kind == SignatureMarkKind.Initials ? Initials : Signature;

    /// <summary>Remembers or forgets the mark of a kind.</summary>
    /// <param name="kind">Signature or initials.</param>
    /// <param name="mark">The mark to remember, or <see langword="null"/> to forget it.</param>
    public void Set(SignatureMarkKind kind, SignatureMark? mark)
    {
        if (kind == SignatureMarkKind.Initials)
        {
            Initials = mark;
        }
        else
        {
            Signature = mark;
        }
    }
}
