using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

			using MSBuildWorkspace workspace = MSBuildWorkspace.Create();

			// Print message for WorkspaceFailed event to help diagnosing project load failures.
			workspace.WorkspaceFailed += (o, e) => Console.WriteLine(e.Diagnostic.Message);

			string projectPath = GetProjectLocation();
			Console.WriteLine($"Loading project '{projectPath}'");

			// Attach progress reporter so we print projects as they are loaded.
			Project project = await workspace.OpenProjectAsync(projectPath);
			Console.WriteLine($"Finished loading project '{projectPath}'");
			int documentCount = project.Documents.Count();

			ProgressBar bar = ProgressBar.StartNew(documentCount);

			const byte chunkSize = 4;
			int i = 0;
			IEnumerable<IEnumerable<Document>> chunks = from document in project.Documents
				group document by i++ % chunkSize
				into part
				select part.AsEnumerable();

			List<Task> tasks = chunks.Select(chunk => Task.Run(() => ProcessChunk(chunk, bar))).ToList();

			await Task.WhenAll(tasks);
		}

		private static async Task ProcessChunk(IEnumerable<Document> chunk, IProgress<int> progress)
		{
			foreach (Document document in chunk)
			{
				SyntaxTree root = await document.GetSyntaxTreeAsync() ??
				                  throw new Exception("No syntax root - " + document.FilePath);

				SyntaxNode rootNode = await root.GetRootAsync();

				TileRefRewriter rewriter = new(await document.GetSemanticModelAsync());
				CompilationUnitSyntax result = rewriter.Visit(rootNode) as CompilationUnitSyntax;

				if (!result!.IsEquivalentTo(rootNode))
				{
					Console.WriteLine($"Changed {document.FilePath}");
					await File.WriteAllTextAsync(document.FilePath, result.ToFullString());
				}
				
				progress.Report(1);
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
