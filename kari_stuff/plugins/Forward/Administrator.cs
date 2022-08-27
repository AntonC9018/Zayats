using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kari.Arguments;
using Kari.GeneratorCore.Workflow;
using Kari.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Kari.Plugins.Forward
{
    public class ForwardAdministrator : IAdministrator
    {
        public ForwardAnalyzer[] _analyzers;
        
        public void Initialize()
        {
            AdministratorHelpers.Initialize(ref _analyzers);
            var logger = new NamedLogger("Forward");
        }
        
        public Task Collect()
        {
            return AdministratorHelpers.CollectAsync(_analyzers);
        }
        
        public Task Generate()
        {
            var master = MasterEnvironment.Instance;

            AdministratorHelpers.AddCodeString(
                master.CommonPseudoProject,
                "ForwardAnnotations.cs", "Forward", DummyForwardAnnotations.Text);

            var tasks = new Task[_analyzers.Length];
            for (int i = 0; i < _analyzers.Length; i++)
            {
                var analyzer = _analyzers[i];
                var project = master.Projects[i];
                tasks[i] = Task.Run(() => Run(project.Data, analyzer));
                
                static void Run(ProjectEnvironmentData project, ForwardAnalyzer analyzer)
                {
                    var nodes = analyzer.GenerateSyntax(project);
                    if (!nodes.Any())
                        return;
                    var compilationUnit = SyntaxFactory.CompilationUnit().WithMembers(new(nodes));
                    var workspace = new AdhocWorkspace();
                    compilationUnit = (CompilationUnitSyntax) Formatter.Format(compilationUnit, workspace);
                    AdministratorHelpers.AddCodeString(project, "Forward.cs", "Forward", compilationUnit.ToString()); 
                }
            }

            return Task.WhenAll(tasks);
        }

        public string GetAnnotations() => DummyForwardAnnotations.Text;
    }
}
