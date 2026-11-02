// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Media.U3d;

/// <summary>
/// The compression contexts of ECMA-363 clause 9. A context tells the bit decoder which frequency counts to use. Context 0 is the
/// plain 8-bit context; contexts below <see cref="StaticBase"/> are adaptive and named after the fields that use them; a context of
/// <see cref="StaticBase"/> plus a range holds every value below that range equally likely.
/// </summary>
internal static class U3dContexts
{
    /// <summary>The context of an uncompressed 8-bit value.</summary>
    internal const uint Plain = 0;

    /// <summary>The adaptive context of a shading list index.</summary>
    internal const uint Shading = 1;

    /// <summary>The adaptive context of the count of unique knots in the U direction.</summary>
    internal const uint KnotCountU = 2;

    /// <summary>The adaptive context of the multiplicity of a knot in the U direction.</summary>
    internal const uint KnotMultiplicityU = 3;

    /// <summary>The adaptive context of the count of unique knots in the V direction.</summary>
    internal const uint KnotCountV = 4;

    /// <summary>The adaptive context of the multiplicity of a knot in the V direction.</summary>
    internal const uint KnotMultiplicityV = 5;

    /// <summary>The number of adaptive contexts a context set keeps.</summary>
    internal const int AdaptiveCount = 6;

    /// <summary>The first value of a static context: this plus the range.</summary>
    internal const uint StaticBase = 0x400;

    /// <summary>The first context that is too large to compress: values written with it are plain.</summary>
    internal const uint StaticLimit = StaticBase + 0x3FFF;

    /// <summary>Makes the static context of a range.</summary>
    /// <param name="range">The number of values the field can hold.</param>
    /// <returns>The context.</returns>
    internal static uint Range(uint range) => range >= StaticLimit - StaticBase ? StaticLimit : StaticBase + range;
}
