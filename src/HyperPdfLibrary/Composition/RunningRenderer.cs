// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Lays out the header and footer of a page, with its fields filled in, once the number of pages is known.</summary>
internal static class RunningRenderer
{
    /// <summary>Makes the commands that draw a page's header and footer.</summary>
    /// <param name="env">The document and its styles.</param>
    /// <param name="plan">The page.</param>
    /// <param name="pageCount">The number of pages in the document.</param>
    /// <param name="cancellationToken">Cancels the layout between blocks.</param>
    /// <returns>The commands, all artifacts.</returns>
    internal static List<DrawCommand> Build(LayoutEnv env, PagePlan plan, int pageCount, CancellationToken cancellationToken)
    {
        var section = plan.Section;
        var setup = section.Setup;
        var runEnv = env.WithoutTags();
        runEnv.Fields = new(plan.DisplayNumber, setup.PageNumberFormat, pageCount, section.Number, section.PageCount, env.Now, env.Document.Title, env.Document.Author);
        var header = RunningContentSelector.SelectHeader(section.Section, plan.SectionPage, plan.DisplayNumber);
        var footer = RunningContentSelector.SelectFooter(section.Section, plan.SectionPage, plan.DisplayNumber);
        var commands = new List<DrawCommand>();
        var geometry = PageGeometry.For(setup, plan.Number, 0F, 0F);
        if (header is not null)
        {
            var stack = StackLayout.Build(runEnv, header.Blocks, geometry.Width, PdfArtifactKind.Pagination, new(0F, null, null), cancellationToken);
            commands.AddRange(stack.PlaceAt(geometry.Left, setup.PageHeight - setup.HeaderDistance));
        }

        if (footer is not null)
        {
            var stack = StackLayout.Build(runEnv, footer.Blocks, geometry.Width, PdfArtifactKind.Pagination, new(0F, null, null), cancellationToken);
            commands.AddRange(stack.PlaceAt(geometry.Left, setup.FooterDistance + stack.Height));
        }

        return commands;
    }
}
