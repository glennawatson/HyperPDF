// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.TestAssets;

/// <summary>How the items written into a test /DSS store divide into certificates and OCSP responses; the rest are CRLs.</summary>
/// <param name="Certificates">The number of certificates.</param>
/// <param name="OcspResponses">The number of OCSP responses.</param>
[DebuggerDisplay("StoreCounts: {Certificates} certificates, {OcspResponses} OCSP")]
public sealed record StoreCounts(int Certificates, int OcspResponses);
