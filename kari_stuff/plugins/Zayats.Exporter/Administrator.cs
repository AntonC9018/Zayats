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

namespace Kari.Zayats.Exporter
{
    using static Kari.Zayats.Exporter.ExportCategory;
    using static System.Diagnostics.Debug;

    public class ExporterAdministrator : IAdministrator
    {
        private ProjectEnvironment _coreProject;
        private ProjectEnvironment _serializationProject;
        private string[] _interfaceNames;

        public void Initialize()
        {
            var master = MasterEnvironment.Instance;
            var logger = new NamedLogger("Zayats.Exporter");

            const string serializationProjectName = "Zayats.Serialization";
            var serializationProject = master.Projects.FirstOrDefault(p => p.Data.Name == serializationProjectName);
            if (serializationProject is null)
                logger.LogError("The exporter plugin could not find the serialization project " + serializationProjectName);
            _serializationProject = serializationProject;
            
            const string coreProjectName = "Zayats.Core";
            var coreProject = master.Projects.FirstOrDefault(p => p.Data.Name == coreProjectName);
            if (coreProject is null)
                logger.LogError("The exporter plugin could not find the core project " + coreProjectName);
            _coreProject = coreProject;

            var names = new string[4];
            names[(int) PickupEffect] = "Zayats.Core.IPickupEffect";
            names[(int) PickupInteraction] = "Zayats.Core.IPickupInteraction";
            names[(int) ActivatedAction] = "Zayats.Core.ITargetedActivatedAction";
            names[(int) TargetFilter] = "Zayats.Core.ITargetFilter";

            for (int i = 0; i < names.Length; i++)
                Assert(names[i] is not null, i.ToString());
            _interfaceNames = names;
        }


        private List<IFieldSymbol>[] _exportedFields;
        
        public Task Collect()
        {
            return Task.Run(() =>
            {
                var master = MasterEnvironment.Instance;
                var fieldsWithExport = _coreProject.Types
                    .SelectMany(t => t
                        .GetMembers()
                        .OfType<IFieldSymbol>()
                        .Where(f => f.HasExportAttribute(master.Compilation)));

                var choices = new INamedTypeSymbol[_interfaceNames.Length];
                for (int i = 0; i < choices.Length; i++)
                {
                    choices[i] = master.Compilation.GetTypeByMetadataName(_interfaceNames[i]);
                    Assert(choices[i] is not null, _interfaceNames[i]);
                }

                _exportedFields = new List<IFieldSymbol>[_interfaceNames.Length];
                for (int i = 0; i < _interfaceNames.Length; i++)
                    _exportedFields[i] = new();

                foreach (var f in fieldsWithExport)
                {
                    for (int i = 0; i < choices.Length; i++)
                    {
                        if (f.Type.AllInterfaces.Contains(choices[i]))
                            _exportedFields[i].Add(f);
                    }
                }
            });
        }
        
        public Task Generate()
        {
            var t = Task.Run(() =>
            {
                var builder = CodeBuilder.Create();
                var master = MasterEnvironment.Instance;

                if (_exportedFields.All(f => f.Count == 0))
                    return;

                builder.AppendLine("namespace ", _serializationProject.Data.GeneratedNamespaceName);
                builder.StartBlock();
                builder.AppendLine("using static Kari.Zayats.Exporter.ExportCategory;");
                builder.AppendLine("using System.Collections.Generic;");
                builder.AppendLine("public static partial class SerializationHelper");
                builder.StartBlock();

                builder.AppendLine("public static (string Name, object Object)[][] CreateMap()");
                builder.StartBlock();
                builder.AppendLine($"var result = new (string Name, object Object)[{_exportedFields.Length}][];");
                for (int category = 0; category < _exportedFields.Length; category++)
                {
                    var fields = _exportedFields[category];

                    var categoryName = ((ExportCategory) category).ToString();
                    builder.AppendLine($"result[(int) {categoryName}] = new (string Name, object Object)[]");
                    builder.StartBlock();

                    for (int i = 0; i < fields.Count; i++)
                    {
                        var f = fields[i];
                        var isInstance = f.Name == "Instance";
                        var name = isInstance ? f.ContainingType.Name : (f.Type.Name + "." + f.Name);
                        var fqn = f.GetFullyQualifiedName();
                        builder.AppendLine($"(Name: \"{name}\", Object: {fqn}),");
                    }

                    builder.DecreaseIndent();
                    builder.AppendLine("};");
                }
                builder.AppendLine("return result;");
                builder.EndBlock();

                builder.AppendLine("public static Dictionary<System.Type, Kari.Zayats.Exporter.ExportCategory> GetInterfaceToCategoryMap()");
                builder.StartBlock();
                builder.AppendLine($"var result = new Dictionary<System.Type, Kari.Zayats.Exporter.ExportCategory>({_interfaceNames.Length});");
                for (int i = 0; i < _interfaceNames.Length; i++)
                {
                    var categoryName = ((ExportCategory) i).ToString();
                    builder.AppendLine($"result.Add(typeof({_interfaceNames[i]}), {categoryName});");
                }
                builder.AppendLine("return result;");
                builder.EndBlock();
                
                builder.EndBlock();
                builder.EndBlock();

                var codeFragment = CodeFragment.CreateFromBuilder("SerializationHelper.cs", "Serialization", builder);
                _serializationProject.Data.AddCodeFragment(codeFragment);
            });

            return Task.WhenAll(t, 
                AdministratorHelpers.AddCodeStringAsync(_coreProject.Data, "ExporterAnnotations.cs", "Exporter", GetAnnotations()));
        }

        public string GetAnnotations() => DummyExporterAnnotations.Text;
    }
}
