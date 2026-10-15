// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace HyperPdf.Benchmarks.Generator;

/// <summary>Reads the symbols the pipeline finds into the plain models it carries, so no symbol outlives the transform.</summary>
internal static class ModelReader
{
    /// <summary>Reads a benchmark method.</summary>
    /// <param name="context">The attribute context.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The model, or <see langword="null"/> when the method or its class cannot be called.</returns>
    internal static BenchmarkModel? Benchmark(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.TargetSymbol is not IMethodSymbol { Parameters.Length: 0, IsStatic: false, DeclaredAccessibility: Accessibility.Public } method || !CanCreate(method.ContainingType))
        {
            return null;
        }

        var operations = 1;
        foreach (var named in context.Attributes[0].NamedArguments)
        {
            if (named.Key == "OperationsPerInvoke" && named.Value.Value is int declared && declared > 0)
            {
                operations = declared;
            }
        }

        var type = method.ContainingType;
        return new BenchmarkModel(Global(type), $"{type.ToDisplayString()}.{method.Name}", method.Name, Classify(method.ReturnType), operations);
    }

    /// <summary>Reads a set-up or clean-up method.</summary>
    /// <param name="context">The attribute context.</param>
    /// <param name="kind">Which hook the attribute declares.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The model, or <see langword="null"/> when the method cannot be called.</returns>
    internal static HookModel? Hook(in GeneratorAttributeSyntaxContext context, HookKind kind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.TargetSymbol is not IMethodSymbol { Parameters.Length: 0, IsStatic: false } method || !CanCreate(method.ContainingType))
        {
            return null;
        }

        var returns = Classify(method.ReturnType);
        var awaits = returns is ReturnKind.Task or ReturnKind.ValueTask or ReturnKind.TaskOfResult or ReturnKind.ValueTaskOfResult;
        var hasResult = returns is ReturnKind.TaskOfResult or ReturnKind.ValueTaskOfResult;
        return new HookModel(kind, Global(method.ContainingType), method.Name, awaits, hasResult, Targets(context.Attributes[0]));
    }

    /// <summary>Reads a parameter that lists its values in a <c>Params</c> attribute.</summary>
    /// <param name="context">The attribute context.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The model, or <see langword="null"/> when the member cannot be set.</returns>
    internal static ParameterModel? Declared(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Member(context.TargetSymbol) is not { } member)
        {
            return null;
        }

        var argument = context.Attributes[0].ConstructorArguments.FirstOrDefault();
        var values = argument.Kind == TypedConstantKind.Array ? argument.Values : context.Attributes[0].ConstructorArguments;
        var literals = string.Join(", ", values.Select(value => Literal(value, member.Type)));
        return new ParameterModel(member.TypeName, member.Name, member.Type, $"new {member.Type}[] {{ {literals} }}");
    }

    /// <summary>Reads a parameter whose values come from a member of the class.</summary>
    /// <param name="context">The attribute context.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The model, or <see langword="null"/> when the member or its source cannot be used.</returns>
    internal static ParameterModel? FromSource(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var attribute = context.Attributes[0];
        if (Member(context.TargetSymbol) is not { } member || attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].Value is not string name)
        {
            return null;
        }

        var source = context.TargetSymbol.ContainingType.GetMembers(name).FirstOrDefault(static s => s is IPropertySymbol or IMethodSymbol { Parameters.Length: 0 });
        if (source is null)
        {
            return null;
        }

        var prefix = source.IsStatic ? member.TypeName : $"new {member.TypeName}()";
        var call = source is IMethodSymbol ? "()" : string.Empty;
        return new ParameterModel(member.TypeName, member.Name, member.Type, $"{prefix}.{name}{call}");
    }

    /// <summary>Reads a parameter that takes every value of its type.</summary>
    /// <param name="context">The attribute context.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The model, or <see langword="null"/> when the type is not a boolean or an enum.</returns>
    internal static ParameterModel? AllValues(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Member(context.TargetSymbol) is not { } member)
        {
            return null;
        }

        var symbolType = context.TargetSymbol is IFieldSymbol field ? field.Type : ((IPropertySymbol)context.TargetSymbol).Type;
        return symbolType.SpecialType == SpecialType.System_Boolean
            ? new ParameterModel(member.TypeName, member.Name, member.Type, "new[] { false, true }")
            : AllEnumValues(member, symbolType);
    }

    /// <summary>Makes the model of an enum parameter that takes every member.</summary>
    /// <param name="member">The parameter member.</param>
    /// <param name="type">The member's type.</param>
    /// <returns>The model, or <see langword="null"/> when the type is not an enum.</returns>
    private static ParameterModel? AllEnumValues(in ParameterMember member, ITypeSymbol type) =>
        type.TypeKind == TypeKind.Enum ? new ParameterModel(member.TypeName, member.Name, member.Type, $"global::System.Enum.GetValues<{member.Type}>()") : null;

    /// <summary>Gets the global name of a type.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The name, with the <c>global::</c> prefix.</returns>
    private static string Global(ISymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>Describes a settable public field or property.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>Its class name, its name and its type name; <see langword="null"/> when it cannot be set.</returns>
    private static ParameterMember? Member(ISymbol symbol) =>
        symbol.IsStatic || symbol.DeclaredAccessibility != Accessibility.Public || !CanCreate(symbol.ContainingType) ? null : ReadMember(symbol);

    /// <summary>Describes a public field or property that is known to be on a class the generated code can create.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>Its class name, its name and its type name; <see langword="null"/> when it cannot be set.</returns>
    private static ParameterMember? ReadMember(ISymbol symbol) => symbol switch
    {
        IFieldSymbol { IsReadOnly: false } field => new ParameterMember(Global(field.ContainingType), field.Name, Global(field.Type)),
        IPropertySymbol { SetMethod: not null } property => new ParameterMember(Global(property.ContainingType), property.Name, Global(property.Type)),
        _ => null,
    };

    /// <summary>Determines whether the generated code can create a class: it is a concrete, non-generic class reachable from the same assembly with a parameterless constructor.</summary>
    /// <param name="type">The class.</param>
    /// <returns><see langword="true"/> when it can be created.</returns>
    private static bool CanCreate(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsGenericType || !IsReachable(type))
        {
            return false;
        }

        return type.InstanceConstructors.Any(static c => c.Parameters.IsEmpty && c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal);
    }

    /// <summary>Determines whether a type and the types containing it are public or internal.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> when code in the same assembly can name it.</returns>
    private static bool IsReachable(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Sorts a return type into the kinds the generated code distinguishes.</summary>
    /// <param name="type">The return type.</param>
    /// <returns>The kind.</returns>
    private static ReturnKind Classify(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Void)
        {
            return ReturnKind.Void;
        }

        return type.IsRefLikeType ? ReturnKind.RefLike : ClassifyTask(type.OriginalDefinition.ToDisplayString());
    }

    /// <summary>Sorts a type by its metadata name into the task kinds, or an ordinary value.</summary>
    /// <param name="definition">The type's original definition name.</param>
    /// <returns>The kind.</returns>
    private static ReturnKind ClassifyTask(string definition) => definition switch
    {
        "System.Threading.Tasks.Task" => ReturnKind.Task,
        "System.Threading.Tasks.ValueTask" => ReturnKind.ValueTask,
        "System.Threading.Tasks.Task<TResult>" => ReturnKind.TaskOfResult,
        "System.Threading.Tasks.ValueTask<TResult>" => ReturnKind.ValueTaskOfResult,
        _ => ReturnKind.Value,
    };

    /// <summary>Gets the benchmark names a hook attribute is limited to.</summary>
    /// <param name="attribute">The attribute.</param>
    /// <returns>The names; empty when it applies to every benchmark.</returns>
    private static EquatableArray<string> Targets(AttributeData attribute)
    {
        var targets = new List<string>();
        foreach (var argument in attribute.ConstructorArguments)
        {
            AddTargets(argument, targets);
        }

        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key is "Target" or "Targets")
            {
                AddTargets(named.Value, targets);
            }
        }

        return new(targets.ToImmutableArray());
    }

    /// <summary>Adds the target names a constant holds.</summary>
    /// <param name="constant">A string or an array of strings.</param>
    /// <param name="targets">The names so far.</param>
    private static void AddTargets(TypedConstant constant, List<string> targets)
    {
        if (constant.Kind == TypedConstantKind.Array)
        {
            foreach (var value in constant.Values)
            {
                AddTargets(value, targets);
            }
        }
        else if (constant.Value is string name)
        {
            targets.Add(name);
        }
    }

    /// <summary>Writes a constant as a C# literal converted to the parameter's type.</summary>
    /// <param name="constant">The constant.</param>
    /// <param name="memberType">The parameter's global type name.</param>
    /// <returns>The expression.</returns>
    private static string Literal(TypedConstant constant, string memberType)
    {
        if (constant.IsNull)
        {
            return "default!";
        }

        var text = SymbolDisplay.FormatPrimitive(constant.Value!, quoteStrings: true, useHexadecimalNumbers: false);
        var literal = constant.Kind == TypedConstantKind.Enum ? $"({Global(constant.Type!)}){text}" : text;
        return $"unchecked(({memberType})({literal}))";
    }
}
