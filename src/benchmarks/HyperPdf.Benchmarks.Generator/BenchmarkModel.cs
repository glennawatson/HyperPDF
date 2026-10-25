// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Benchmarks.Generator;

/// <summary>One benchmark method of a class the generated code can create and call.</summary>
/// <param name="TypeName">The class's global name.</param>
/// <param name="FilterName">The namespace, class and method a filter is matched against.</param>
/// <param name="Method">The method name.</param>
/// <param name="Return">What the method returns.</param>
/// <param name="OperationsPerInvoke">The operations one call stands for.</param>
internal readonly record struct BenchmarkModel(string TypeName, string FilterName, string Method, ReturnKind Return, int OperationsPerInvoke);
