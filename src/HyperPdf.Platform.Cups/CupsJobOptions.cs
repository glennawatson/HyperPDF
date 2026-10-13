// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Platform.Cups;

/// <summary>The values of the CUPS options sent with a print job, held in a value so building them allocates nothing.</summary>
/// <param name="Copies">The <c>copies</c> value.</param>
/// <param name="Media">The <c>media</c> value, a PWG media name.</param>
/// <param name="Sides">The <c>sides</c> value.</param>
/// <param name="ColorMode">The <c>print-color-mode</c> value.</param>
/// <param name="Scaling">The <c>print-scaling</c> value.</param>
internal readonly record struct CupsJobOptions(string Copies, string Media, string Sides, string ColorMode, string Scaling)
{
    /// <summary>The name of the copies option.</summary>
    internal const string CopiesName = "copies";

    /// <summary>The name of the media option.</summary>
    internal const string MediaName = "media";

    /// <summary>The name of the sides option.</summary>
    internal const string SidesName = "sides";

    /// <summary>The name of the colour mode option.</summary>
    internal const string ColorModeName = "print-color-mode";

    /// <summary>The name of the scaling option.</summary>
    internal const string ScalingName = "print-scaling";
}
