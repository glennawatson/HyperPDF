// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace HyperPdf.Benchmarks.Generator;

/// <summary>
/// Lists every benchmark of the assembly for the allocation harness, so no reflection finds, sets up or calls a
/// benchmark. One attribute-driven provider reads each BenchmarkDotNet attribute into small value-equatable models; the
/// models are collected and the source is written from them alone, so an edit that changes no benchmark regenerates
/// nothing.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class BenchmarkCaseGenerator : IIncrementalGenerator
{
    /// <summary>The BenchmarkDotNet attribute namespace.</summary>
    private const string Attributes = "BenchmarkDotNet.Attributes.";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var benchmarks = context.SyntaxProvider
            .ForAttributeWithMetadataName($"{Attributes}BenchmarkAttribute", static (node, _) => node is MethodDeclarationSyntax, ModelReader.Benchmark)
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .WithTrackingName("Benchmarks")
            .Collect()
            .Select(static (all, _) => new EquatableArray<BenchmarkModel>(all.ToArray()))
            .WithTrackingName("BenchmarkList");

        var hooks = Hooks(in context, "GlobalSetupAttribute", HookKind.GlobalSetup)
            .Combine(Hooks(in context, "GlobalCleanupAttribute", HookKind.GlobalCleanup))
            .Combine(Hooks(in context, "IterationSetupAttribute", HookKind.IterationSetup))
            .Combine(Hooks(in context, "IterationCleanupAttribute", HookKind.IterationCleanup))
            .Select(static (all, _) => Join(all.Left.Left.Left, all.Left.Left.Right, all.Left.Right, all.Right))
            .WithTrackingName("HookList");

        var parameters = Members(in context, "ParamsAttribute", ModelReader.Declared)
            .Combine(Members(in context, "ParamsSourceAttribute", ModelReader.FromSource))
            .Combine(Members(in context, "ParamsAllValuesAttribute", ModelReader.AllValues))
            .Select(static (all, _) => Join(all.Left.Left, all.Left.Right, all.Right))
            .WithTrackingName("ParameterList");

        var everything = benchmarks.Combine(hooks).Combine(parameters);
        context.RegisterSourceOutput(everything, static (output, all) =>
            output.AddSource("GeneratedCases.g.cs", SourceText.From(CaseEmitter.Emit(all.Left.Left.Items, all.Left.Right.Items, all.Right.Items), Encoding.UTF8)));
    }

    /// <summary>Reads one kind of hook into a collected list.</summary>
    /// <param name="context">The initialization context.</param>
    /// <param name="attribute">The hook attribute's class name.</param>
    /// <param name="kind">The hook kind.</param>
    /// <returns>The collected hooks.</returns>
    private static IncrementalValueProvider<EquatableArray<HookModel>> Hooks(in IncrementalGeneratorInitializationContext context, string attribute, HookKind kind) =>
        context.SyntaxProvider
            .ForAttributeWithMetadataName($"{Attributes}{attribute}", static (node, _) => node is MethodDeclarationSyntax, (ctx, ct) => ModelReader.Hook(ctx, kind, ct))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .Collect()
            .Select(static (all, _) => new EquatableArray<HookModel>(all.ToArray()));

    /// <summary>Reads one kind of parameter attribute into a collected list.</summary>
    /// <param name="context">The initialization context.</param>
    /// <param name="attribute">The parameter attribute's class name.</param>
    /// <param name="read">Reads one member.</param>
    /// <returns>The collected parameter models.</returns>
    private static IncrementalValueProvider<EquatableArray<ParameterModel>> Members(
        in IncrementalGeneratorInitializationContext context,
        string attribute,
        System.Func<GeneratorAttributeSyntaxContext, System.Threading.CancellationToken, ParameterModel?> read) =>
        context.SyntaxProvider
            .ForAttributeWithMetadataName($"{Attributes}{attribute}", static (node, _) => node is PropertyDeclarationSyntax or VariableDeclaratorSyntax, read)
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .Collect()
            .Select(static (all, _) => new EquatableArray<ParameterModel>(all.ToArray()));

    /// <summary>Joins collected hook lists.</summary>
    /// <param name="lists">The lists.</param>
    /// <returns>One list.</returns>
    private static EquatableArray<HookModel> Join(params EquatableArray<HookModel>[] lists) =>
        new(lists.SelectMany(static l => l.Items).ToArray());

    /// <summary>Joins collected parameter lists.</summary>
    /// <param name="lists">The lists.</param>
    /// <returns>One list.</returns>
    private static EquatableArray<ParameterModel> Join(params EquatableArray<ParameterModel>[] lists) =>
        new(lists.SelectMany(static l => l.Items).ToArray());
}
