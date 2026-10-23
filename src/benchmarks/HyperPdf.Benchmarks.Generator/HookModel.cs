// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Benchmarks.Generator;

/// <summary>One set-up or clean-up method.</summary>
/// <param name="Kind">Which hook.</param>
/// <param name="TypeName">The class's global name.</param>
/// <param name="Method">The method name.</param>
/// <param name="Awaits">Whether the method returns a task.</param>
/// <param name="HasResult">Whether the task carries a result to drop.</param>
/// <param name="Targets">The benchmark names the hook is limited to; empty for all.</param>
internal readonly record struct HookModel(HookKind Kind, string TypeName, string Method, bool Awaits, bool HasResult, EquatableArray<string> Targets);
