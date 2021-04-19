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
                if (variableDeclarator.Initializer == null)
                {
                    // TODO: do "ref Tile a = new Tile();" instead of "ref Tile a;"
                    newVariables = newVariables.Add(variableDeclarator);
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
            if (!IsTile(node.Left) || node.Right is ObjectCreationExpressionSyntax)
            {
                // var a = _model.GetSymbolInfo(node.Right);
                // var aa = _model.GetSymbolInfo(node);
                // var b = _model.GetDeclaredSymbol(node.Right);
                // var bb = _model.GetDeclaredSymbol(node);
                // var c = _model.GetSpeculativeTypeInfo(0, node.Right, SpeculativeBindingOption.BindAsTypeOrNamespace);
                // var d = _model.GetTypeInfo(node.Right);
                // var dd = _model.GetTypeInfo(node.Left);
                // var ee = _model.GetSymbolInfo(node.Left);

                return base.VisitAssignmentExpression(node);
            }
            
            if (node.Right is RefExpressionSyntax)
                return base.VisitAssignmentExpression(node);

            var newRight = RefExpression(node.Right);
            var newNode = node.WithRight(newRight.NormalizeWhitespace()).WithTriviaFrom(node);
            // var newNode = node.WithRight(RefExpression(node.Right.WithoutTrivia()).WithTriviaFrom(node.Right));
            // var newNode = node;
            // var symbol = _model.GetSymbolInfo(node);
            // var symbol2 = _model.GetSymbolInfo(node.Left);
            // var symbol3 = _model.GetSymbolInfo(node.Right);
            // var f = _model.GetTypeInfo(node.Left);
            // var ff = _model.GetTypeInfo(node.Right);
            // var a = node.GetLeadingTrivia();
            // var b = newNode.GetLeadingTrivia();
            //
            return newNode;
        }

        private bool IsTile(SyntaxNode node)
        {
            var symbol = _model.GetSymbolInfo(node).Symbol;

            // Check if the type of the node is Terraria.Tile
            var type = _model.GetTypeInfo(node).Type;
            if (type != null)
                return type.ToDisplayString() == "Terraria.Tile";
            
            // Check if the symbol of the node is Terraria.Tile
            return symbol != null && symbol.ToDisplayString() == "Terraria.Tile";
        }
    }
}