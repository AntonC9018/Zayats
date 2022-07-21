using System.Threading.Tasks;
using Kari.GeneratorCore.Workflow;
using Kari.Utils;

namespace Kari.Plugins.AdvancedEnum
{
    public class AdvancedEnumAdministrator : IAdministrator
    {
        public AdvancedEnumAnalyzer[] _analyzers;
        
        public void Initialize()
        {
            AdministratorHelpers.Initialize(ref _analyzers);
            var logger = new NamedLogger("AdvancedEnum");
            AdvancedEnumSymbols.Initialize(logger);
        }
        
        public Task Collect()
        {
            return AdministratorHelpers.CollectAsync(_analyzers);
        }
        
        public Task Generate()
        {
            AdministratorHelpers.AddCodeString(
                MasterEnvironment.Instance.CommonPseudoProject,
                "AdvancedEnumAnnotations.cs", "AdvancedEnum", GetAnnotations());

            return Task.WhenAll(
                AdministratorHelpers.GenerateAsync(_analyzers, "AdvancedEnum.cs")
            );
        }
        
        public string GetAnnotations() => DummyAdvancedEnumAnnotations.Text;
    }
}
