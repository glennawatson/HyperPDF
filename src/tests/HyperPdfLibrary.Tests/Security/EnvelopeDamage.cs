// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Security;

/// <summary>The kinds of damage the ECDH tests make to an envelope.</summary>
public enum EnvelopeDamage
{
    /// <summary>The envelope's last bytes are cut, so it cannot be read.</summary>
    Truncated = 0,

    /// <summary>One bit of the wrapped content key is flipped, so the key unwrap check fails.</summary>
    WrappedKey = 1,

    /// <summary>One bit of the originator's ephemeral point is flipped, so it is off the curve.</summary>
    EphemeralPoint = 2,
}
