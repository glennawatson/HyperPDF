// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The reference pictures the current picture can use, in the three groups reference lists are built from (8.3.2).</summary>
/// <param name="StCurrBefore">The short-term pictures before the current one in output order, nearest first.</param>
/// <param name="StCurrAfter">The short-term pictures after the current one in output order, nearest first.</param>
/// <param name="LtCurr">The long-term pictures.</param>
internal sealed record H265RefPicSet(H265Picture[] StCurrBefore, H265Picture[] StCurrAfter, H265Picture[] LtCurr)
{
    /// <summary>Gets the empty set.</summary>
    internal static H265RefPicSet Empty { get; } = new([], [], []);
}
