// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>One round of MD5: the non-linear function and the position of the round, fixed when the code is compiled.</summary>
internal interface IMd5Round
{
    /// <summary>Gets the round, zero to three.</summary>
    static abstract int Index { get; }

    /// <summary>The non-linear function of the round.</summary>
    /// <param name="b">The second state word.</param>
    /// <param name="c">The third state word.</param>
    /// <param name="d">The fourth state word.</param>
    /// <returns>The mixed value.</returns>
    static abstract uint Mix(uint b, uint c, uint d);
}
