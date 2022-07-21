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
            foreach (var type in environment.TypesWithAttributes)
            {
                if (type.TryGetAttribute(AdvancedEnumSymbols.GenerateArrayWrapperAttribute, new(type.Name), out var attribute))
                {
                    _infos.Add(new AdvancedEnumInfo(attribute, type));
                }
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
            
            HashSet<int> enumValues = new();

            foreach (var info in _infos)
            {
                var enumMembers = info.Symbol.GetFields().Where(m => m.Name != "Count").ToArray();
                if (enumMembers.Length == 0)
                    continue;

                var generatedTypeName = info.Attribute.TypeName ?? info.Symbol.Name + "Array";

                b.AppendLine($"[Serializable]");
                b.AppendLine($"public partial struct {generatedTypeName}<T>");
                b.StartBlock();

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

                        if (useSimpleVariant)
                        {
                            b.AppendLine("return ref Values[(int) key];");
                        }
                        else
                        {
                            AppendSwitchBegin(ref b);
                            foreach (var enumMember in enumMembers)
                            {
                                int index = uniqueValueToIndexMap[(int) enumMember.ConstantValue];
                                b.AppendLine(FormatSwitchCase(enumMember.Name), $"return ref Values[{index}];");
                            }
                            AppendSwitchEnd(ref b);
                        }

                        b.EndBlock();
                    }

                    {
                        b.AppendLine($"public readonly T Get({fullyQualifiedEnumName} key)");
                        b.StartBlock();

                        if (useSimpleVariant)
                        {
                            b.AppendLine("return Values[(int) key];");
                        }
                        else
                        {
                            AppendSwitchBegin(ref b);
                            foreach (var enumMember in enumMembers)
                            {
                                int index = uniqueValueToIndexMap[(int) enumMember.ConstantValue];
                                b.AppendLine(FormatSwitchCase(enumMember.Name), $"return Values[{index}];");
                            }
                            AppendSwitchEnd(ref b);
                        }

                        b.EndBlock();
                    }

                    {
                        b.AppendLine($"public readonly void Set({fullyQualifiedEnumName} key, T value)");
                        b.StartBlock();

                        if (useSimpleVariant)
                        {
                            b.AppendLine("Values[(int) key] = value;");
                        }
                        else
                        {
                            AppendSwitchBegin(ref b);
                            foreach (var enumMember in enumMembers)
                            {
                                int index = uniqueValueToIndexMap[(int) enumMember.ConstantValue];
                                b.AppendLine(FormatSwitchCase(enumMember.Name), $"Values[{index}] = value; break;");
                            }
                            AppendSwitchEnd(ref b);
                        }

                        b.EndBlock();
                    }
                }

                // Getters and setters for values by name.
                {
                    foreach (var enumMember in enumMembers)
                    {
                        string access;
                        if (useSimpleVariant)
                        {
                            access = $"Values[(int) {fullyQualifiedEnumName}.{enumMember.Name}]";
                        }
                        else
                        {
                            int index = uniqueValueToIndexMap[(int) enumMember.ConstantValue];
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
        public readonly GenerateArrayWrapperAttribute Attribute;
        public readonly INamedTypeSymbol Symbol;

        public AdvancedEnumInfo(GenerateArrayWrapperAttribute attribute, INamedTypeSymbol symbol)
        {
            Attribute = attribute;
            Symbol = symbol;
        }
    }
}
