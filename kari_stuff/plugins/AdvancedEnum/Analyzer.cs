using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Kari.GeneratorCore.Workflow;
using Kari.Utils;
using Microsoft.CodeAnalysis;

namespace Kari.Plugins.AdvancedEnum
{
    public class AdvancedEnumAnalyzer : ICollectSymbols, IGenerateCode
    {
        private readonly List<AdvancedEnumInfo> _infos = new();
        
        public void CollectSymbols(ProjectEnvironment environment)
        {
            HashSet<int> enumValues = new();
            var logger = environment.Logger;

            foreach (var type in environment.TypesWithAttributes)
            {
                if (type.TryGetGenerateArrayWrapperAttribute(environment.Compilation, logger, out var attribute))
                {
                    var enumMembers = type.GetFields().Where(m => m.Name != "Count").ToArray();
                    if (enumMembers.Length == 0)
                        continue;

                    enumValues.Clear();
                    foreach (var enumMember in enumMembers)
                        enumValues.Add((int) enumMember.ConstantValue);
                    int numUniqueEnumValues = enumValues.Count;

                    bool IsSimple()
                    {
                        BitArray valueMet = new(numUniqueEnumValues);
                        foreach (var enumMember in enumMembers)
                        {
                            int value = (int) enumMember.ConstantValue;
                            if (value < 0 || value >= valueMet.Length)
                                return false;
                            valueMet[value] = true;
                        }
                        for (int i = 0; i < valueMet.Length; i++)
                        {
                            if (!valueMet[i])
                                return false; 
                        }
                        return true;
                    }
                    bool useSimpleVariant = IsSimple();


                    Dictionary<int, int> uniqueValueToIndexMap;
                    
                    if (useSimpleVariant)
                    {
                        uniqueValueToIndexMap = null;
                    }
                    else
                    {
                        uniqueValueToIndexMap = new();
                        int index = 0;
                        foreach (var value in enumValues)
                            uniqueValueToIndexMap.Add(value, index);
                    }
                    
                    _infos.Add(new AdvancedEnumInfo(
                        generatedTypeName: attribute.TypeName ?? type.Name + "Array",
                        type, useSimpleVariant, numUniqueEnumValues, enumMembers, uniqueValueToIndexMap));
                }
            }
        }

        public void AddUnityPropertyDrawers(MasterEnvironment master, ProjectEnvironmentData project)
        {
            if (_infos.Count == 0) 
                return;

            var editorProjectName = project.Name + ".Editor";
            var targetProject = master.AllProjectDatas.FirstOrDefault(p => p.Name == editorProjectName, master.RootPseudoProject);
            foreach (var info in _infos)
            {
                var b = CodeBuilder.Create();
                b.AppendLine("namespace ", targetProject.GeneratedNamespaceName);
                b.StartBlock();
                b.AppendLine("using UnityEditor;");
                b.AppendLine("[CustomPropertyDrawer(typeof(", project.GeneratedNamespaceName.Join(info.GeneratedTypeName), "<>))]");

                string propertyDrawerName = info.Symbol.Name + "PropertyDrawer";
                b.AppendLine("public class ", propertyDrawerName, " : Kari.Plugins.AdvancedEnum.Editor.EnumArrayDrawer");
                b.StartBlock();

                // This info might also be useful elsewhere.
                string[] names = new string[info.NumUniqueEnumValues];
                foreach (var member in info.EnumMembers)
                {
                    int index = info.UseSimpleVariant
                        ? (int) member.ConstantValue
                        : info.UniqueValueToIndexMap[(int) member.ConstantValue];
                    names[index] ??= member.Name;
                }
                string namesList = string.Join(", ", names.Select(a => '"' + a + '"'));

                b.AppendLine("private static readonly string[] _Names = new[] {", namesList, "};");
                b.AppendLine("protected override string[] Names => _Names;");
                b.EndBlock();
                b.EndBlock();

                var fragment = CodeFragment.CreateFromBuilder(propertyDrawerName + ".cs", project.Name, b);
                targetProject.AddCodeFragment(fragment);
            }
        }
        
        public void GenerateCode(ProjectEnvironmentData project, ref CodeBuilder b)
        {
            // Returing early implies no output should be generated for the given template.
            if (_infos.Count == 0) 
                return;

            b.AppendLine($"namespace {project.GeneratedNamespaceName}");
            b.StartBlock();
            b.AppendLine("using System;");
            

            foreach (var info in _infos)
            {
                var generatedTypeName = info.GeneratedTypeName;

                b.AppendLine($"[Serializable]");
                b.AppendLine($"public partial struct {generatedTypeName}<T>");
                b.StartBlock();

                int numUniqueEnumValues = info.NumUniqueEnumValues;

                b.AppendLine("public /*readonly*/ T[] Values;");

                b.AppendLine($"private {generatedTypeName}(T[] values) => Values = values;");
                // b.AppendLine($"public static {generatedTypeName}<T> Create(T[] values) => new {generatedTypeName}<T>(values);");
                b.AppendLine($"public static {generatedTypeName}<T> Create() => new {generatedTypeName}<T>(new T[{numUniqueEnumValues}]);");

                void AppendSwitchBegin(ref CodeBuilder b)
                {
                    b.AppendLine("switch (key)");
                    b.StartBlock();
                    b.AppendLine("default: throw new Kari.Plugins.AdvancedEnum.InvalidEnumValueException((int) key);");
                }

                void AppendSwitchEnd(ref CodeBuilder b)
                {
                    b.EndBlock();
                }
                
                var fullyQualifiedEnumName = info.Symbol.GetFullyQualifiedName();
                
                // Getters and setters by key.
                {
                    string FormatSwitchCase(string memberName) => $"case {fullyQualifiedEnumName}.{memberName}: ";

                    {
                        b.AppendLine($"public readonly ref T GetRef({fullyQualifiedEnumName} key)");
                        b.StartBlock();

                        if (info.UseSimpleVariant)
                        {
                            b.AppendLine("return ref Values[(int) key];");
                        }
                        else
                        {
                            AppendSwitchBegin(ref b);
                            foreach (var enumMember in info.EnumMembers)
                            {
                                int index = info.UniqueValueToIndexMap[(int) enumMember.ConstantValue];
                                b.AppendLine(FormatSwitchCase(enumMember.Name), $"return ref Values[{index}];");
                            }
                            AppendSwitchEnd(ref b);
                        }

                        b.EndBlock();
                    }

                    {
                        b.AppendLine($"public readonly T Get({fullyQualifiedEnumName} key)");
                        b.StartBlock();

                        if (info.UseSimpleVariant)
                        {
                            b.AppendLine("return Values[(int) key];");
                        }
                        else
                        {
                            AppendSwitchBegin(ref b);
                            foreach (var enumMember in info.EnumMembers)
                            {
                                int index = info.UniqueValueToIndexMap[(int) enumMember.ConstantValue];
                                b.AppendLine(FormatSwitchCase(enumMember.Name), $"return Values[{index}];");
                            }
                            AppendSwitchEnd(ref b);
                        }

                        b.EndBlock();
                    }

                    {
                        b.AppendLine($"public readonly void Set({fullyQualifiedEnumName} key, T value)");
                        b.StartBlock();

                        if (info.UseSimpleVariant)
                        {
                            b.AppendLine("Values[(int) key] = value;");
                        }
                        else
                        {
                            AppendSwitchBegin(ref b);
                            foreach (var enumMember in info.EnumMembers)
                            {
                                int index = info.UniqueValueToIndexMap[(int) enumMember.ConstantValue];
                                b.AppendLine(FormatSwitchCase(enumMember.Name), $"Values[{index}] = value; break;");
                            }
                            AppendSwitchEnd(ref b);
                        }

                        b.EndBlock();
                    }
                }

                // Getters and setters for values by name.
                {
                    foreach (var enumMember in info.EnumMembers)
                    {
                        string access;
                        if (info.UseSimpleVariant)
                        {
                            access = $"Values[(int) {fullyQualifiedEnumName}.{enumMember.Name}]";
                        }
                        else
                        {
                            int index = info.UniqueValueToIndexMap[(int) enumMember.ConstantValue];
                            access = $"Values[{index}]";
                        }

                        b.AppendLine($"public readonly ref T {enumMember.Name}Ref => ref {access};");
                        
                        b.AppendLine($"public T {enumMember.Name}");
                        b.StartBlock();
                        b.AppendLine($"readonly get => {access};");
                        b.AppendLine($"set => {access} = value;");
                        b.EndBlock();
                    }
                }

                {
                    b.AppendLine($"public static implicit operator T[]({generatedTypeName}<T> a) => a.Values;");
                    b.AppendLine($"public readonly T[] Array => Values;");
                    b.AppendLine($"public readonly ref T this[{fullyQualifiedEnumName} key] => ref GetRef(key);");
                    b.AppendLine($"public readonly ref T this[int index] => ref Values[index];");
                    b.AppendLine($"public readonly int Length => {numUniqueEnumValues};");
                }

                b.EndBlock();
            }
                
            b.EndBlock();
        }
    }

    public readonly struct AdvancedEnumInfo
    {
        public readonly string GeneratedTypeName;
        public readonly INamedTypeSymbol Symbol;
        public readonly bool UseSimpleVariant;
        public readonly int NumUniqueEnumValues;
        public readonly IFieldSymbol[] EnumMembers;
        public readonly Dictionary<int, int> UniqueValueToIndexMap;

        public AdvancedEnumInfo(string generatedTypeName, INamedTypeSymbol symbol, bool useSimpleVariant, int numUniqueEnumValues, IFieldSymbol[] enumMembers, Dictionary<int, int> uniqueValueToIndexMap)
        {
            GeneratedTypeName = generatedTypeName;
            Symbol = symbol;
            UseSimpleVariant = useSimpleVariant;
            NumUniqueEnumValues = numUniqueEnumValues;
            EnumMembers = enumMembers;
            UniqueValueToIndexMap = uniqueValueToIndexMap;
        }
    }
}
