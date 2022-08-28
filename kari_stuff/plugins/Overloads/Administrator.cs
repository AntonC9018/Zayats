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

namespace Kari.Plugins.Overloads
{
    public class OverloadsAdministrator : IAdministrator
    {
        public OverloadsAnalyzer[] _analyzers;
        
        public void Initialize()
        {
            AdministratorHelpers.Initialize(ref _analyzers);
            var logger = new NamedLogger("Overloads");
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
                "OverloadsAnnotations.cs", "Overloads", DummyOverloadsAnnotations.Text);

            var tasks = AdministratorHelpers.GenerateSyntax(_analyzers, "Overloads.cs");

            return Task.WhenAll(tasks);
        }

        public string GetAnnotations() => DummyOverloadsAnnotations.Text;
    }
}
