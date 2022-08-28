using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Kari.GeneratorCore.Workflow;
using Kari.Plugins.Overloads;
using Kari.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kari.Plugins.Overloads
{
    using static SyntaxFactory;
    using static SyntaxHelper;
    using DefaultValue = System.ComponentModel.DefaultValueAttribute;

    public class OverloadsAnalyzer : ICollectSymbols, IGenerateSyntax
    {
        private readonly List<OverloadsInfo> _infos = new();
        private static readonly ConcurrentDictionary<INamedTypeSymbol, OverloadType> _OverloadTypesMap;
        private INamedTypeSymbol _defaultValueAttributeSymbol;

        public void CollectSymbols(ProjectEnvironment environment)
        {
            var compilation = MasterEnvironment.Instance.Compilation;
            var overloadSymbol = compilation.GetOverloadAttributeSymbol();
            var logger = new NamedLogger("Overloads");
            List<FieldOverload> overloads = new();
            List<IParameterSymbol> overloadParameters = new();

            // TODO: lift to the admin and pass it down in the constructor.
            // TODO: same with the types map, should be shared, but not static.
            if (compilation.GetTypeByMetadataName(typeof(DefaultValue).FullName) is not INamedTypeSymbol _defaultValueAttributeSymbol)
            {
                logger.LogError("The right assembly is likely not loaded for code analysis. Please add the assembly "
                    + typeof(DefaultValue).Assembly.FullName);
                return;
            }

            foreach (var m in environment.MethodsWithAttributes
                    .Where(m => m.HasAttribute(overloadSymbol)))
            {
                // Initial error checking
                {
                    bool isError = false;
                    // for now
                    if (!m.IsStatic)
                    {
                        logger.LogError($"The method must be static in order to generate overloads: {m.GetLocationInfo()}");
                        isError = true;
                    }
                    if (isError)
                        continue;
                }

                OverloadsInfo info;
                info.Method = m;

                foreach (var p in m.Parameters)
                {
                    var pType = p.Type;

                    // We only overload on struct types, because classes should be reused.
                    if (!pType.IsValueType)
                        continue;

                    if (pType.SpecialType != SpecialType.None)
                        continue;

                    if (pType is not INamedTypeSymbol namedType)
                        continue; 
                    
                    var value = _OverloadTypesMap.GetOrAdd(namedType, (type) =>
                    {
                        overloads.Clear();

                        // Let's just ignore properties for now.
                        foreach (var field in type.GetFields())
                        {
                            if (field.IsCompilerGenerated())
                                continue;

                            FieldOverload ov = new();
                            ov.Field = field;

                            var fieldType = field.Type;

                            if (field.TryGetAttributeData(_defaultValueAttributeSymbol, out ov.DefaultAttributeData))
                            {
                            }
                            // No DefaultValue attribute.
                            // Will only trigger for classes.
                            else
                            {
                                var syntax = field.DeclaringSyntaxReferences[0].GetSyntax();
                                var (fieldDeclaration, variableDeclaration, variableDeclarator) = syntax.AsFieldSyntaxes();
                                ov.DefaultValueSyntax = variableDeclarator.Initializer?.Value;
                            }

                            overloads.Add(ov);
                        }

                        return new OverloadType
                        {
                            Type = type,
                            OverloadFields = overloads.ToArray(),
                        };
                    });

                    // Error
                    if (value is null)
                        continue;

                    overloadParameters.Add(p);
                }

                if (overloadParameters.Count > 1)
                {
                    logger.LogError("For now, only supporting a single overload parameter");
                    continue;
                }

                if (overloadParameters.Count == 0)
                    continue;

                if (logger.AnyHasErrors)
                    continue;

                info.OverloadParameters = overloadParameters.ToArray();
                overloadParameters.Clear();

                _infos.Add(info);
            }
        }

        public IEnumerable<MemberDeclarationSyntax> GenerateSyntax(ProjectEnvironmentData project)
        {
            // TODO:
            /*
                - a partial class that would hold the overloads, perhaps the same one they were defined in.
                
                assume there's a method like this

                [Overload]
                public static void Method(Struct s)
                {
                }

                and the struct

                public struct Struct
                {
                    [DefaultValue(5)] public int b;
                    [DefaultValue(nameof(b))] public int a;
                }

                we'd generate the overloads

                public static void Method(int a,
                    // The expression in DefaultValue of b.
                    int b = 5)
                {
                    Struct s;
                    s.a = a;
                    s.b = b;
                    Method(s);
                }

                public static void Method(int b = 5)
                {
                    Struct s;
                    s.b = b;
                    
                    // (the expression in DefaultValue of a)
                    // implied to mean b because of nameof.
                    s.a = s.b;

                    Method(s);
                }

                we'd also need type checks for the value stored in DefaultValue.
                if it's a string, we should interpret it as an expression string.

                why do this? because it would save me from writing boilerplate.
            */
            return null;
        }
    }

    public struct FieldOverload
    {
        // public DefaultValue DefaultValue;
        public AttributeData DefaultAttributeData;
        public ExpressionSyntax DefaultValueSyntax;

        // One of these will be set.
        public IFieldSymbol Field;
        public IPropertySymbol Property;
    }

    public class OverloadType
    {
        public ITypeSymbol Type;
        public FieldOverload[] OverloadFields;
    }

    public struct OverloadsInfo
    {
        public IMethodSymbol Method;
        public IParameterSymbol[] OverloadParameters;
    }
}