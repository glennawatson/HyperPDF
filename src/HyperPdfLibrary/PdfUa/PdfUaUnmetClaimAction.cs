// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.PdfUa;

/// <summary>What a save does when a file claims PDF/UA-1 and a problem it cannot fix would make the claim false.</summary>
public enum PdfUaUnmetClaimAction
{
    /// <summary>Stop the save with an explanation, and write nothing. This is the default.</summary>
    Refuse = 0,

    /// <summary>Remove the PDF/UA claim from the file, then save it. The user chooses this after being told why the claim cannot be kept.</summary>
    RemoveClaim = 1,
}
