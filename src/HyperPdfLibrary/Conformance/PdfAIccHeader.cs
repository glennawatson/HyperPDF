// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The fields of an ICC profile header the PDF/A rules read (ISO 15076-1 7.2).</summary>
/// <param name="DeviceClass">The profile/device class signature, such as <c>prtr</c>; zero when missing.</param>
/// <param name="ColourSpace">The data colour space signature, such as <c>RGB </c>; zero when missing.</param>
/// <param name="MajorVersion">The major version; -1 when the header is too short to hold it.</param>
internal readonly record struct PdfAIccHeader(uint DeviceClass, uint ColourSpace, int MajorVersion);
