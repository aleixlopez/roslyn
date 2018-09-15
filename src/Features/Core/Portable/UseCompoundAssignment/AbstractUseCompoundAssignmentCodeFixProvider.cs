﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Extensions;

namespace Microsoft.CodeAnalysis.CSharp.UseCompoundAssignment
{
    internal abstract class AbstractUseCompoundAssignmentCodeFixProvider<TSyntaxKind, TExpressionSyntax> 
        : SyntaxEditorBasedCodeFixProvider
        where TSyntaxKind : struct
        where TExpressionSyntax : SyntaxNode
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(IDEDiagnosticIds.UseCompoundAssignmentDiagnosticId);

        protected readonly ImmutableDictionary<TSyntaxKind, TSyntaxKind> BinaryToAssignmentMap;
        protected readonly ImmutableDictionary<TSyntaxKind, TSyntaxKind> AssignmentToTokenMap;

        protected AbstractUseCompoundAssignmentCodeFixProvider(
            ImmutableDictionary<TSyntaxKind, TSyntaxKind> binaryToAssignmentMap,
            ImmutableDictionary<TSyntaxKind, TSyntaxKind> assignmentToTokenMap)
        {
            BinaryToAssignmentMap = binaryToAssignmentMap;
            AssignmentToTokenMap = assignmentToTokenMap;
        }

        protected abstract TSyntaxKind GetSyntaxKind(int rawKind);
        protected abstract SyntaxToken Token(TSyntaxKind kind);
        protected abstract SyntaxNode AssignmentExpression(
            TSyntaxKind assignmentOpKind, TExpressionSyntax left, SyntaxToken syntaxToken, TExpressionSyntax right);

        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var diagnostic = context.Diagnostics[0];
            
            context.RegisterCodeFix(new MyCodeAction(
                c => FixAsync(document, diagnostic, c)),
                context.Diagnostics);

            return Task.CompletedTask;
        }

        protected override Task FixAllAsync(
            Document document, ImmutableArray<Diagnostic> diagnostics,
            SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var syntaxFacts = document.GetLanguageService<ISyntaxFactsService>();

            foreach (var diagnostic in diagnostics)
            {
                var assignment = diagnostic.AdditionalLocations[0].FindNode(getInnermostNodeForTie: true, cancellationToken);

                editor.ReplaceNode(assignment,
                    (currentAssignment, generator) =>
                    {
                        syntaxFacts.GetPartsOfAssignmentStatement(currentAssignment, 
                            out var leftOfAssign, out var equalsToken, out var rightOfAssign);

                        syntaxFacts.GetPartsOfBinaryExpression(rightOfAssign,
                           out _, out var opToken, out var rightExpr);

                    var assignmentOpKind = BinaryToAssignmentMap[GetSyntaxKind(rightOfAssign.RawKind)];
                        var compoundOperator = Token(AssignmentToTokenMap[assignmentOpKind]);
                        return AssignmentExpression(
                            assignmentOpKind,
                            (TExpressionSyntax)leftOfAssign,
                            compoundOperator.WithTriviaFrom(equalsToken),
                            (TExpressionSyntax)rightExpr);
                    });
            }

            return Task.CompletedTask;
        }

        private class MyCodeAction : CodeAction.DocumentChangeAction
        {
            public MyCodeAction(Func<CancellationToken, Task<Document>> createChangedDocument) 
                : base(FeaturesResources.Use_compound_assignment, createChangedDocument, FeaturesResources.Use_compound_assignment)
            {
            }
        }
    }
}
