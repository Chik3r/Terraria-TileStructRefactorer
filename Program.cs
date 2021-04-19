using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TileStructRefactorer
{
	class Program
	{
		static async Task Main(string[] args)
		{
			MSBuildLocator.RegisterDefaults();

			using (var workspace = MSBuildWorkspace.Create())
			{
				// Print message for WorkspaceFailed event to help diagnosing project load failures.
				workspace.WorkspaceFailed += (o, e) => Console.WriteLine(e.Diagnostic.Message);

				string projectPath = GetProjectLocation();
				Console.WriteLine($"Loading project '{projectPath}'");

				
				
				// Attach progress reporter so we print projects as they are loaded.
				Project solution = await workspace.OpenProjectAsync(projectPath);
				Console.WriteLine($"Finished loading project '{projectPath}'");

				// TODO: Do analysis on the projects in the loaded solution
				foreach (Document document in solution.Documents)
				{
					if (!document.FilePath.Contains("Collision.cs"))
						continue;
					
					SyntaxTree root = await document.GetSyntaxTreeAsync() ?? 
					                  throw new Exception("No syntax root - " + document.FilePath);

					SyntaxNode rootNode = await root.GetRootAsync();

					var rewriter = new TileRefRewriter(await document.GetSemanticModelAsync());
					var result = rewriter.Visit(rootNode) as CompilationUnitSyntax;
					
					if (!result!.IsEquivalentTo(rootNode))
					{
						Console.WriteLine($"Changed {document.FilePath}");
						// Console.WriteLine(result.ToFullString());
						await File.WriteAllTextAsync(document.FilePath,
							result.ToFullString());
					}
				}
			}
		}

		private static string GetProjectLocation()
		{
			while (true)
			{
				Console.WriteLine("Enter the path of the csproj to load");
				string path = Console.ReadLine();

				if (Path.GetExtension(path) != ".csproj")
					path += ".csproj";

				if (!File.Exists(path))
				{
					Console.WriteLine("File doesn't exist");
					continue;
				}

				Console.Clear();
				return path;
			}
		}
	}
}
