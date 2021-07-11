using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace TileStructRefactorer
{
    public class TileRefRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _model;

        public TileRefRewriter(SemanticModel model) => _model = model;
        
        // TODO: Remove "if (tile == null)" by converting:
        // "tile == null" to "false"
        // "tile != null" to "true"
        
        public override SyntaxNode VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            // Converts "Tile tile = x;" to "ref Tile tile = ref x;"
            
            // Check if the type of the local variable is Terraria.Tile
            if (!IsTile(node.Declaration.Type))
                return base.VisitLocalDeclarationStatement(node);

            if (node.Declaration.Type is RefTypeSyntax)
                return base.VisitLocalDeclarationStatement(node);
            
            // Change the type to 'ref type' instead of 'type'
            TypeSyntax newType = RefType(node.Declaration.Type.WithoutTrivia());
            VariableDeclarationSyntax newDeclaration = node.Declaration.WithType(newType);
        
            // Make the variables also be ref
            SeparatedSyntaxList<VariableDeclaratorSyntax> newVariables = new();
            foreach (VariableDeclaratorSyntax variableDeclarator in newDeclaration.Variables)
            {
                if (variableDeclarator.Initializer == null || variableDeclarator.Initializer.Value.ToFullString() == "null")
                {
                    VariableDeclaratorSyntax a =
                        variableDeclarator.WithInitializer(EqualsValueClause(RefExpression(ParseExpression("Tile.Dummy"))));

                    newVariables = newVariables.Add(a);
                    continue;
                }

                var newValue = RefExpression(variableDeclarator.Initializer.Value);
                var newInitializer = variableDeclarator.Initializer.WithValue(newValue);
                var newDeclarator = variableDeclarator.WithInitializer(newInitializer);
                
                newVariables = newVariables.Add(newDeclarator.NormalizeWhitespace());
            }
            newDeclaration = newDeclaration.WithVariables(newVariables);
            
            var newNode = node.WithDeclaration(newDeclaration.NormalizeWhitespace().WithTriviaFrom(node.Declaration));
            return newNode;
        }

        public override SyntaxNode VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            // Add "ref" to the right side of an assignment

            if (!IsTile(node.Left) || node.Right is ObjectCreationExpressionSyntax)
                return base.VisitAssignmentExpression(node);

            if (node.Right is RefExpressionSyntax)
                return base.VisitAssignmentExpression(node);

            RefExpressionSyntax newRight = RefExpression(node.Right);
            AssignmentExpressionSyntax newNode = node.WithRight(newRight.NormalizeWhitespace()).WithTriviaFrom(node);

            if (node.Left is ElementAccessExpressionSyntax leftAccess && leftAccess.ArgumentList.Arguments.Count == 2)
                newNode = newNode.WithLeadingTrivia(newNode.GetLeadingTrivia().Add(Comment("// "))).WithTrailingTrivia(Comment("// Commented out by Tile Struct Refactorer"));
            
            return newNode;
        }

        public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (node.Right.Kind() != SyntaxKind.NullLiteralExpression) return base.VisitBinaryExpression(node);

            if (node.OperatorToken.Kind() != SyntaxKind.EqualsEqualsToken && node.OperatorToken.Kind() != SyntaxKind.ExclamationEqualsToken)
                return base.VisitBinaryExpression(node);

            if (!IsTile(node.Left)) return base.VisitBinaryExpression(node);

            SyntaxKind n = node.OperatorToken.Kind() switch
            {
                SyntaxKind.EqualsEqualsToken => SyntaxKind.FalseLiteralExpression,
                SyntaxKind.ExclamationEqualsToken => SyntaxKind.TrueLiteralExpression,
                _ => throw new ArgumentOutOfRangeException(nameof(node))
            };

            LiteralExpressionSyntax newNullCheck = LiteralExpression(n).WithTriviaFrom(node);

            return newNullCheck;
        }

        private bool IsTile(SyntaxNode node)
        {
            // Check if the type of the node is Terraria.Tile
            ITypeSymbol type = _model.GetTypeInfo(node).Type;
            if (type != null)
                return type.ToDisplayString() == "Terraria.Tile";

            ISymbol symbol = _model.GetSymbolInfo(node).Symbol;

            // Check if the symbol of the node is Terraria.Tile
            return symbol != null && symbol.ToDisplayString() == "Terraria.Tile";
        }
    }
}