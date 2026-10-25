// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>A page's media box as veraPDF reads it, in points.</summary>
/// <param name="Left">The lower-left x.</param>
/// <param name="Bottom">The lower-left y.</param>
/// <param name="Right">The upper-right x.</param>
/// <param name="Top">The upper-right y.</param>
internal readonly record struct VeraPdfPage(double Left, double Bottom, double Right, double Top);
