﻿/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2015-2017 SonarSource SA
 * mailto: contact AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [Rule(DiagnosticId)]
    public sealed class ExpressionComplexity : ExpressionComplexityBase<ExpressionSyntax>
    {
        private static readonly DiagnosticDescriptor rule =
            DiagnosticDescriptorBuilder.GetDescriptor(DiagnosticId, MessageFormat, RspecStrings.ResourceManager)
                                       .DisabledByDefault();
        protected override DiagnosticDescriptor Rule => rule;

        public override GeneratedCodeRecognizer GeneratedCodeRecognizer => Helpers.CSharp.GeneratedCodeRecognizer.Instance;

        private static readonly ISet<SyntaxKind> CompoundExpressionKinds = ImmutableHashSet.Create(
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.AnonymousMethodExpression,

            SyntaxKind.ArrayInitializerExpression,
            SyntaxKind.CollectionInitializerExpression,
            SyntaxKind.ComplexElementInitializerExpression,
            SyntaxKind.ObjectInitializerExpression,

            SyntaxKind.InvocationExpression);

        private static readonly ISet<SyntaxKind> ComplexityIncreasingKinds = ImmutableHashSet.Create(
            SyntaxKind.ConditionalExpression,
            SyntaxKind.LogicalAndExpression,
            SyntaxKind.LogicalOrExpression);

        protected override bool IsComplexityIncreasingKind(SyntaxNode node) =>
            ComplexityIncreasingKinds.Contains(node.Kind());

        protected override bool IsCompoundExpression(SyntaxNode node) =>
            CompoundExpressionKinds.Contains(node.Kind());
    }
}
