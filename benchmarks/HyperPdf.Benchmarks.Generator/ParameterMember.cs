// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Benchmarks.Generator;

/// <summary>A settable parameter member of a benchmark class: the class, the member and its type, as global names.</summary>
/// <param name="TypeName">The class's global name.</param>
/// <param name="Name">The member name.</param>
/// <param name="Type">The member's global type name.</param>
internal readonly record struct ParameterMember(string TypeName, string Name, string Type);
