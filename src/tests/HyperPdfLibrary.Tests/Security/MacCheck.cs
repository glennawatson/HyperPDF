// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Security;

/// <summary>The independent check of one PDF MAC token in a file.</summary>
/// <param name="Position">Where the token's /AuthCode dictionary starts in the file.</param>
/// <param name="Location">The /MACLocation: Standalone or AttachedToSig.</param>
/// <param name="Failure">The reason the token does not hold, or empty text when it does.</param>
internal sealed record MacCheck(int Position, string Location, string Failure)
{
    /// <summary>Gets a value indicating whether the token holds.</summary>
    internal bool Holds => Failure.Length == 0;
}
