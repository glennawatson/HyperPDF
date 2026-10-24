// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Benchmarks.Generator;

/// <summary>The values one parameter attribute gives a field or property of a benchmark class.</summary>
/// <param name="TypeName">The class's global name.</param>
/// <param name="Member">The field or property name.</param>
/// <param name="MemberType">The member's global type name.</param>
/// <param name="Values">A C# expression listing the values.</param>
internal readonly record struct ParameterModel(string TypeName, string Member, string MemberType, string Values);
