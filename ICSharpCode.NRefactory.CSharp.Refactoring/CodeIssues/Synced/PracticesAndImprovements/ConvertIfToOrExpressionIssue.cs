//
// ConvertIfToOrExpressionIssue.cs
//
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
//
// Copyright (c) 2013 Xamarin Inc. (http://xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;
using System.Threading;
using ICSharpCode.NRefactory6.CSharp.Refactoring;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.FindSymbols;

namespace ICSharpCode.NRefactory6.CSharp.Refactoring
{
	[DiagnosticAnalyzer]
	[NRefactoryCodeDiagnosticAnalyzer(AnalysisDisableKeyword = "ConvertIfToOrExpression")]
	public class ConvertIfToOrExpressionIssue : GatherVisitorCodeIssueProvider
	{
		internal const string DiagnosticId = "ConvertIfToOrExpressionIssue";
		const string Description = "Convert 'if' to '||' expression";
		const string MessageFormat = "{0}";
		const string Category = IssueCategories.PracticesAndImprovements;

		static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, DiagnosticSeverity.Info, true, "'if' statement can be re-written as '||' expression");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
		{
			get
			{
				return ImmutableArray.Create(Rule);
			}
		}

		protected override CSharpSyntaxWalker CreateVisitor(SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, CancellationToken cancellationToken)
		{
			return new GatherVisitor(semanticModel, addDiagnostic, cancellationToken);
		}

		//		internal static bool CheckTarget(Expression target, Expression expr)
		//		{
		//			return !target.DescendantNodesAndSelf().Any(
		//				n => (n is IdentifierExpression || n is MemberReferenceExpression) && 
		//				expr.DescendantNodesAndSelf().Any(n2 => ((INode)n).IsMatch(n2))
		//			);
		//		}

		internal static bool MatchIfElseStatement(IfStatementSyntax ifStatement, out ExpressionSyntax assignmentTarget, out SyntaxTriviaList assignmentTrailingTriviaList)
		{
			assignmentTarget = null;
			assignmentTrailingTriviaList = SyntaxFactory.TriviaList(SyntaxFactory.SyntaxTrivia(SyntaxKind.DisabledTextTrivia, ""));
			var trueExpression = ifStatement.Statement as ExpressionStatementSyntax;
			if (trueExpression != null)
			{
				return CheckForAssignmentOfTrue(trueExpression, out assignmentTarget, out assignmentTrailingTriviaList);
			}

			var blockExpression = ifStatement.Statement as BlockSyntax;
			if (blockExpression != null)
			{
				if (blockExpression.Statements.Count != 1)
					return false;
				return CheckForAssignmentOfTrue(blockExpression.Statements[0], out assignmentTarget, out assignmentTrailingTriviaList);
			}

			return false;
		}

		static bool CheckForAssignmentOfTrue(StatementSyntax statement, out ExpressionSyntax assignmentTarget, out SyntaxTriviaList assignmentTrailingTriviaList)
		{
			assignmentTarget = null;
			assignmentTrailingTriviaList = SyntaxFactory.TriviaList(SyntaxFactory.SyntaxTrivia(SyntaxKind.DisabledTextTrivia, ""));
			var expressionStatement = statement as ExpressionStatementSyntax;
			if (expressionStatement == null)
				return false;
			var assignmentExpression = expressionStatement.Expression as AssignmentExpressionSyntax;
			if (assignmentExpression == null)
				return false;
			assignmentTarget = assignmentExpression.Left as IdentifierNameSyntax;
			assignmentTrailingTriviaList = assignmentExpression.OperatorToken.TrailingTrivia;
			if (assignmentTarget == null)
				assignmentTarget = assignmentExpression.Left as MemberAccessExpressionSyntax;
			var rightAssignment = assignmentExpression.Right as LiteralExpressionSyntax;
			return (assignmentTarget != null) && (rightAssignment != null) && (rightAssignment.IsKind(SyntaxKind.TrueLiteralExpression));
		}

		internal static LocalDeclarationStatementSyntax FindPreviousVarDeclaration(StatementSyntax statement)
		{
			var siblingStatements = statement.Parent.ChildNodes().OfType<StatementSyntax>();
			StatementSyntax lastSibling = null;
			foreach (var sibling in siblingStatements)
			{
				if (sibling == statement)
				{
					return lastSibling as LocalDeclarationStatementSyntax;
				}
				lastSibling = sibling;
			}

			return null;
		}

		static bool CheckTarget(ExpressionSyntax target, ExpressionSyntax expr)
		{
			if (target.IsKind(SyntaxKind.IdentifierName))
				return !expr.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(n => ((IdentifierNameSyntax)target).Identifier.ValueText == n.Identifier.ValueText);
			if (target.IsKind(SyntaxKind.SimpleMemberAccessExpression))
				return !expr.DescendantNodesAndSelf().Any(
						n =>
						{
							if (n.IsKind(SyntaxKind.IdentifierName))
								return ((MemberAccessExpressionSyntax)target).Expression.ToString() == ((IdentifierNameSyntax)n).Identifier.ValueText;
							if (n.IsKind(SyntaxKind.SimpleMemberAccessExpression))
								return ((MemberAccessExpressionSyntax)target).Expression.ToString() == ((MemberAccessExpressionSyntax)n).Expression.ToString();
							return false;
						}
					);
			return false;
		}

		class GatherVisitor : GatherVisitorBase<ConvertIfToOrExpressionIssue>
		{
			public GatherVisitor(SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, CancellationToken cancellationToken)
				: base(semanticModel, addDiagnostic, cancellationToken)
			{
			}

			//			static readonly AstNode ifPattern = 
			//				new IfElseStatement(
			//					new AnyNode ("condition"),
			//					PatternHelper.EmbeddedStatement (
			//						new AssignmentExpression(
			//							new AnyNode("target"),
			//							new PrimitiveExpression (true)
			//						)
			//					)
			//				);
			//
			//			static readonly AstNode varDelarationPattern = 
			//				new VariableDeclarationStatement(new AnyNode("type"), Pattern.AnyString, new AnyNode("initializer"));
			//
			//			void AddTo(IfElseStatement ifElseStatement, VariableDeclarationStatement varDeclaration, Expression expr)
			//			{
			//			}

			public override void VisitIfStatement(IfStatementSyntax node)
			{
				base.VisitIfStatement(node);

				ExpressionSyntax target;
				SyntaxTriviaList assignmentTrailingTriviaList;
				if (MatchIfElseStatement(node, out target, out assignmentTrailingTriviaList))
				{
					var varDeclaration = FindPreviousVarDeclaration(node);
					if (varDeclaration != null)
					{
						var targetIdentifier = target as IdentifierNameSyntax;
						if (targetIdentifier == null)
							return;
						var declaredVarName = varDeclaration.Declaration.Variables.First().Identifier.Value;
						var assignedVarName = targetIdentifier.Identifier.Value;
						if (declaredVarName != assignedVarName)
							return;
						if (!CheckTarget(targetIdentifier, node.Condition))
							return;
						AddIssue(Diagnostic.Create(Rule, node.IfKeyword.GetLocation(), "Convert to '||' expression"));
					}
					else
					{
						if (!CheckTarget(target, node.Condition))
							return;
						AddIssue(Diagnostic.Create(Rule, node.IfKeyword.GetLocation(), "Replace with '|='"));
					}
				}
			}

			//
			//			public override void VisitIfElseStatement(IfElseStatement ifElseStatement)
			//			{
			//				base.VisitIfElseStatement(ifElseStatement);
			//
			//				var match = ifPattern.Match(ifElseStatement);
			//				if (match.Success) {
			//					var varDeclaration = ifElseStatement.GetPrevSibling(s => s.Role == BlockStatement.StatementRole) as VariableDeclarationStatement;
			//					var target = match.Get<Expression>("target").Single();
			//					var match2 = varDelarationPattern.Match(varDeclaration);
			//					if (match2.Success) {
			//						if (varDeclaration == null || target == null)
			//							return;
			//						var initializer = varDeclaration.Variables.FirstOrDefault();
			//						if (initializer == null || !(target is IdentifierExpression) || ((IdentifierExpression)target).Identifier != initializer.Name)
			//							return;
			//						var expr = match.Get<Expression>("condition").Single();
			//						if (!CheckTarget(target, expr))
			//							return;
			//						AddIssue(new CodeIssue(
			//							ifElseStatement.IfToken,
			//							ctx.TranslateString("Convert to '||' expresssion"),
			//							ctx.TranslateString(""),
			//							script => {
			//								var variable = varDeclaration.Variables.First();
			//								script.Replace(
			//									varDeclaration, 
			//									new VariableDeclarationStatement(
			//									varDeclaration.Type.Clone(),
			//									variable.Name,
			//									new BinaryOperatorExpression(variable.Initializer.Clone(), BinaryOperatorType.ConditionalOr, expr.Clone()) 
			//									)
			//									);
			//								script.Remove(ifElseStatement); 
			//							}
			//						) { IssueMarker = IssueMarker.DottedLine });
			//						return;
			//					} else {
			//						var expr = match.Get<Expression>("condition").Single();
			//						if (!CheckTarget(target, expr))
			//							return;
			//						AddIssue(new CodeIssue(
			//							ifElseStatement.IfToken,
			//							ctx.TranslateString(""),
			//							ctx.TranslateString("Replace with '|='"),
			//							script => {
			//								script.Replace(
			//									ifElseStatement, 
			//									new ExpressionStatement(
			//										new AssignmentExpression(
			//											target.Clone(),
			//											AssignmentOperatorType.BitwiseOr,
			//											expr.Clone()) 
			//										)
			//									);
			//							}
			//						));
			//					}
			//				}
			//			}
		}
	}

	[ExportCodeFixProvider(ConvertIfToOrExpressionIssue.DiagnosticId, LanguageNames.CSharp)]
	public class ConvertIfToOrExpressionFixProvider : NRefactoryCodeFixProvider
	{
		protected override IEnumerable<string> InternalGetFixableDiagnosticIds()
		{
			yield return ConvertIfToOrExpressionIssue.DiagnosticId;
		}

		public override async Task ComputeFixesAsync(CodeFixContext context)
		{
			var document = context.Document;
			var cancellationToken = context.CancellationToken;
			var span = context.Span;
			var diagnostics = context.Diagnostics;
			var root = await document.GetSyntaxRootAsync(cancellationToken);
			var result = new List<CodeAction>();
			foreach (var diagnostic in diagnostics)
			{
				var node = root.FindNode(diagnostic.Location.SourceSpan) as IfStatementSyntax;
				ExpressionSyntax target;
				SyntaxTriviaList assignmentTrailingTriviaList;
				ConvertIfToOrExpressionIssue.MatchIfElseStatement(node, out target, out assignmentTrailingTriviaList);
				SyntaxNode newRoot = null;
				var varDeclaration = ConvertIfToOrExpressionIssue.FindPreviousVarDeclaration(node);
				if (varDeclaration != null)
				{
					var varDeclarator = varDeclaration.Declaration.Variables[0];
					newRoot = root.ReplaceNodes(new SyntaxNode[] { varDeclaration, node }, (arg, arg2) =>
					{
						if (arg is LocalDeclarationStatementSyntax)
							return SyntaxFactory.LocalDeclarationStatement(
									SyntaxFactory.VariableDeclaration(varDeclaration.Declaration.Type,
										SyntaxFactory.SeparatedList(
											new[] {
												SyntaxFactory.VariableDeclarator(varDeclarator.Identifier.ValueText)
													.WithInitializer(
														SyntaxFactory.EqualsValueClause(
															SyntaxFactory.BinaryExpression(SyntaxKind.LogicalOrExpression, varDeclarator.Initializer.Value, node.Condition))
																.WithAdditionalAnnotations(Formatter.Annotation)
													)
											}
										))
								).WithLeadingTrivia(node.GetLeadingTrivia()).WithTrailingTrivia(node.GetTrailingTrivia());
						return null;
					});
				}
				else
				{
					newRoot = root.ReplaceNode((SyntaxNode)node,
						SyntaxFactory.ExpressionStatement(
							SyntaxFactory.AssignmentExpression(
								SyntaxKind.OrAssignmentExpression,
								target,
								node.Condition.WithLeadingTrivia(assignmentTrailingTriviaList).WithoutTrailingTrivia()
							)
						).WithLeadingTrivia(node.GetLeadingTrivia()).WithTrailingTrivia(node.GetTrailingTrivia()));
				}

				context.RegisterFix(CodeActionFactory.Create(node.Span, diagnostic.Severity, "Replace with '||'", document.WithSyntaxRoot(newRoot)), diagnostic);
			}
		}
	}
}