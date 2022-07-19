using System.Threading.Tasks;
using Kari.GeneratorCore.Workflow;
using Kari.Utils;

namespace Kari.Plugins.EnumArray
{
    public class EnumArrayAdministrator : IAdministrator
    {
        public EnumArrayAnalyzer[] _analyzers;
        
        public void Initialize()
        {
            AdministratorHelpers.Initialize(ref _analyzers);
            var logger = new NamedLogger("EnumArray");
            EnumArraySymbols.Initialize(logger);
        }
        
        public Task Collect()
        {
            return AdministratorHelpers.CollectAsync(_analyzers);
        }
        
        public Task Generate()
        {
            AdministratorHelpers.AddCodeString(
                MasterEnvironment.Instance.CommonPseudoProject,
                "EnumArrayAnnotations.cs", "EnumArray", GetAnnotations());

            return Task.WhenAll(
                AdministratorHelpers.GenerateAsync(_analyzers, "EnumArray.cs")
            );
        }
        
        public string GetAnnotations() => DummyEnumArrayAnnotations.Text;
    }
}
