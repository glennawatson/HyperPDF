// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The colour space a JPEG 2000 image declares in its colour specification box.</summary>
/// <param name="Family">DeviceCMYK, Lab or ICCBased.</param>
/// <param name="Components">The number of components.</param>
/// <param name="Profile">The embedded ICC profile, or null for an enumerated space.</param>
internal sealed record PdfAJpxSpace(KnownName Family, int Components, byte[]? Profile);
