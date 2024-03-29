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
                
            var tasks = AdministratorHelpers.GenerateSyntax(_analyzers, "Forward.cs");
            return Task.WhenAll(tasks);
        }

        public string GetAnnotations() => DummyForwardAnnotations.Text;
    }
}
