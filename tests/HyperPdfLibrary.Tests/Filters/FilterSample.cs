// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>A named block of sample data.</summary>
/// <param name="Name">What the data is, shown when a check fails.</param>
/// <param name="Data">The bytes.</param>
internal sealed record FilterSample(string Name, byte[] Data);
