//
// NonReadonlyReferencedInGetHashCodeIssue.cs
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
	[ExportDiagnosticAnalyzer("Non-readonly field referenced in 'GetHashCode()'", LanguageNames.CSharp)]
	[NRefactoryCodeDiagnosticAnalyzer(Description = "Non-readonly field referenced in 'GetHashCode()'", AnalysisDisableKeyword = "NonReadonlyReferencedInGetHashCode")]
	public class NonReadonlyReferencedInGetHashCodeIssue : GatherVisitorCodeIssueProvider
	{	
		internal const string DiagnosticId  = "NonReadonlyReferencedInGetHashCodeIssue";
		const string Description            = "";
		const string MessageFormat          = "Non-readonly field referenced in 'GetHashCode()'";
		const string Category               = IssueCategories.CodeQualityIssues;

		static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor (DiagnosticId, Description, MessageFormat, Category, DiagnosticSeverity.Warning, true);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics {
			get {
				return ImmutableArray.Create(Rule);
			}
		}

		protected override CSharpSyntaxWalker CreateVisitor (SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, CancellationToken cancellationToken)
		{
			return new GatherVisitor(semanticModel, addDiagnostic, cancellationToken);
		}

		class GatherVisitor : GatherVisitorBase<NonReadonlyReferencedInGetHashCodeIssue>
		{
			public GatherVisitor(SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, CancellationToken cancellationToken)
				: base (semanticModel, addDiagnostic, cancellationToken)
			{
			}
//
//			#region Skipped declarations
//			public override void VisitConstructorDeclaration(ConstructorDeclaration constructorDeclaration)
//			{
//			}
//
//			public override void VisitPropertyDeclaration(PropertyDeclaration propertyDeclaration)
//			{
//			}
//
//			public override void VisitFieldDeclaration(FieldDeclaration fieldDeclaration)
//			{
//			}
//
//			public override void VisitIndexerDeclaration(IndexerDeclaration indexerDeclaration)
//			{
//			}
//
//			public override void VisitOperatorDeclaration(OperatorDeclaration operatorDeclaration)
//			{
//			}
//
//			public override void VisitCustomEventDeclaration(CustomEventDeclaration eventDeclaration)
//			{
//			}
//
//			public override void VisitEventDeclaration(EventDeclaration eventDeclaration)
//			{
//			}
//
//			public override void VisitDestructorDeclaration(DestructorDeclaration destructorDeclaration)
//			{
//			}
//
//			public override void VisitFixedFieldDeclaration(FixedFieldDeclaration fixedFieldDeclaration)
//			{
//			}
//
//			public override void VisitDelegateDeclaration(DelegateDeclaration delegateDeclaration)
//			{
//			}
//			#endregion
//
//			public override void VisitMethodDeclaration(MethodDeclaration methodDeclaration)
//			{
//				if (methodDeclaration.Name != "GetHashCode" || !methodDeclaration.HasModifier(Modifiers.Override) || methodDeclaration.Parameters.Any())
//					return;
//				if (!ctx.Resolve(methodDeclaration.ReturnType).Type.IsKnownType(KnownTypeCode.Int32))
//					return;
//				base.VisitMethodDeclaration(methodDeclaration);
//			}
//
//			public override void VisitMemberReferenceExpression(MemberReferenceExpression memberReferenceExpression)
//			{
//				base.VisitMemberReferenceExpression(memberReferenceExpression);
//				CheckNode(memberReferenceExpression, memberReferenceExpression.MemberNameToken);
//			}
//
//			public override void VisitIdentifierExpression(IdentifierExpression identifierExpression)
//			{
//				base.VisitIdentifierExpression(identifierExpression);
//				CheckNode(identifierExpression, identifierExpression);
//			}
//
//			void CheckNode(AstNode expr, AstNode nodeToMark)
//			{
//				var resolvedResult = ctx.Resolve(expr);
//				var mrr = resolvedResult as MemberResolveResult;
//				if (mrr == null)
//					return;
//				var member = mrr.Member;
//				var field = member as IField;
//				if (field != null) {
//					if (!field.IsReadOnly && !field.IsConst)
//						AddIssue(new CodeIssue(nodeToMark, "Non-readonly field referenced in 'GetHashCode()'"));
//				}
//			}
		}
	}

	[ExportCodeFixProvider(NonReadonlyReferencedInGetHashCodeIssue.DiagnosticId, LanguageNames.CSharp)]
	public class NonReadonlyReferencedInGetHashCodeIssueFixProvider : ICodeFixProvider
	{
		public IEnumerable<string> GetFixableDiagnosticIds()
		{
			yield return NonReadonlyReferencedInGetHashCodeIssue.DiagnosticId;
		}

		public async Task<IEnumerable<CodeAction>> GetFixesAsync(Document document, TextSpan span, IEnumerable<Diagnostic> diagnostics, CancellationToken cancellationToken)
		{
			var root = await document.GetSyntaxRootAsync(cancellationToken);
			var result = new List<CodeAction>();
			foreach (var diagonstic in diagnostics) {
				var node = root.FindNode(diagonstic.Location.SourceSpan);
				//if (!node.IsKind(SyntaxKind.BaseList))
				//	continue;
				var newRoot = root.RemoveNode(node, SyntaxRemoveOptions.KeepNoTrivia);
				result.Add(CodeActionFactory.Create(node.Span, diagonstic.Severity, diagonstic.GetMessage(), document.WithSyntaxRoot(newRoot)));
			}
			return result;
		}
	}
}