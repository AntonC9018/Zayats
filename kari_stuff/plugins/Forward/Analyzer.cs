using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Kari.GeneratorCore.Workflow;
using Kari.Plugins.Forward;
using Kari.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kari.Plugins.Forward
{
    using static SyntaxFactory;
    using static SyntaxHelper;

    public class ForwardAnalyzer : ICollectSymbols
    {
        private readonly List<ForwardInfo> _infos = new();
        
        public void CollectSymbols(ProjectEnvironment environment)
        {
            HashSet<int> enumValues = new();
            NamedLogger logger = new("Forward");

            foreach (var type in environment.TypesWithAttributes)
            {
                ForwardInfo result;
                
                result.Config = new();
                
                {
                    ref var config = ref result.Config;
                    config.Symbol = type;

                    if (type.TryGetForwardAttribute(environment.Compilation, logger, out var a))
                    {
                        TryParseRegex(a.AcceptPattern, type, out config.AcceptRegex);
                        TryParseRegex(a.RejectPattern, type, out config.RejectRegex);
                    }
                    else
                    {
                        a = new();
                    }

                    config.Attribute = a;

                    a.Options = ForwardOptions.ForwardFieldsAsGetters
                        | ForwardOptions.ForwardFieldsAsSetters
                        | ForwardOptions.ForwardMethods
                        | ForwardOptions.ForwardProperties;
                    a.MethodPrefix ??= "";
                    a.MethodSuffix ??= "";
                    a.PropertyPrefix ??= "";
                    a.PropertySuffix ??= "";
                    a.RefPropertyPrefix ??= "";
                    a.RefPropertySuffix ??= "Ref";
                }

                bool TryParseRegex(string str, ISymbol member, out Regex regex)
                {
                    if (str is null)
                    {
                        regex = null;
                        return true;
                    }
                    if (str == "")
                    {
                        logger.LogError($"Empty regex detected at {member.GetLocationInfo()}.");
                        regex = null;
                        return false;
                    }

                    // TODO: how to format in a normal way?
                    str = str.Replace("{Name}", member.Name);

                    try
                    {
                        regex = new Regex(str);
                        return true;
                    }
                    catch (ArgumentException parsingError)
                    {
                        logger.LogError($"Bad regex at {member.GetLocationInfo()}: {parsingError}");
                        regex = null;
                        return false;
                    }
                }

                result.DecoratedFieldsOrProperties = new();
                foreach (var m in type.GetMembers().Where(m => m is IPropertySymbol || m is IFieldSymbol))
                {
                    ForwardSingleInfo f;
                    f.Symbol = m;

                    if (!m.TryGetForwardAttribute(environment.Compilation, logger, out f.Attribute))
                        continue;

                    bool accept = TryParseRegex(f.Attribute.AcceptPattern, m, out f.AcceptRegex);
                    bool reject = TryParseRegex(f.Attribute.RejectPattern, m, out f.RejectRegex);
                    
                    if (!accept || !reject)
                        continue;
                        
                    result.DecoratedFieldsOrProperties.Add(f);
                }

                if (result.DecoratedFieldsOrProperties.Count == 0)
                    continue;


                _infos.Add(result);
            }
        }
        public IEnumerable<MemberDeclarationSyntax> GenerateSyntax(ProjectEnvironmentData project)
        {
            if (_infos.Count == 0)
                yield break;

            var logger = project.Logger;
            List<MemberDeclarationSyntax> newMembers = new();

            foreach (var info in _infos)
            {
                newMembers.Clear();

                bool? ShouldRegardName(in ForwardSingleInfo f, string name)
                {
                    if (f.Attribute.AcceptOverReject)
                    {
                        if (f.AcceptRegex is not null && f.AcceptRegex.IsMatch(name))
                            return true;
                        if (f.RejectRegex is not null)
                            return !f.RejectRegex.IsMatch(name);
                    }
                    else
                    {
                        if (f.RejectRegex is not null && f.RejectRegex.IsMatch(name))
                            return false;
                        if (f.AcceptRegex is not null)
                            return f.AcceptRegex.IsMatch(name);
                    }
                    return null;
                }

                foreach (var f in info.DecoratedFieldsOrProperties)
                {
                    ITypeSymbol fieldOrPropertyType;
                    if (f.Symbol is IFieldSymbol decoratedField)
                    {
                        fieldOrPropertyType = decoratedField.Type;
                    }
                    else if (f.Symbol is IPropertySymbol decoratedProperty)
                    {
                        fieldOrPropertyType = decoratedProperty.Type;
                    }
                    else
                    {
                        logger.LogWarning($"Expected either field or property to have been decorated at {f.Symbol.GetLocationInfo()}");
                        continue;
                    }

                    var fieldOrPropertyName = IdentifierName(f.Symbol.Name);

                    bool HasEitherFlag(ForwardOptions opts)
                    {
                        if (f.Attribute.Options.HasValue)
                            return (f.Attribute.Options & opts) != 0;
                        return (info.Config.Attribute.Options & opts) != 0;
                    }

                    bool ShouldRegardName_WithParentCheck(string name)
                    {
                        bool? result = ShouldRegardName(f, name);
                        if (result.HasValue)
                            return result.Value;
                        
                        result = ShouldRegardName(info.Config, name);
                        if (result.HasValue)
                            return result.Value;

                        // Forward any member by default.
                        return true;
                    }

                    string ProcessMethodName(string name)
                    {
                        var prefix = f.Attribute.MethodPrefix;
                        var suffix = f.Attribute.MethodSuffix;
                        
                        prefix ??= info.Config.Attribute.MethodPrefix;
                        suffix ??= info.Config.Attribute.MethodSuffix;

                        name = prefix + name + suffix;

                        return name;
                    }

                    string ProcessPropertyName(string name)
                    {
                        var prefix = f.Attribute.PropertyPrefix;
                        var suffix = f.Attribute.PropertySuffix;
                        
                        prefix ??= info.Config.Attribute.PropertyPrefix;
                        suffix ??= info.Config.Attribute.PropertySuffix;

                        name = prefix + name + suffix;

                        return name;
                    }

                    string ProcessRefPropertyName(string name)
                    {
                        var prefix = f.Attribute.RefPropertyPrefix;
                        var suffix = f.Attribute.RefPropertySuffix;
                        
                        prefix ??= info.Config.Attribute.RefPropertyPrefix;
                        suffix ??= info.Config.Attribute.RefPropertySuffix;
                        
                        name = prefix + name + suffix;

                        return name;
                    }

                    TypeSyntax FullyQualifyTypes(TypeSyntax type, SemanticModel semanticModel)
                    {
                        if (type is PredefinedTypeSyntax predef)
                            return type;

                        if (type is not NameSyntax name)
                        {
                            return type.ReplaceNodes(
                                type.DescendantNodes().OfType<TypeSyntax>(),
                                (t0, t1) => t1 ?? FullyQualifyTypes(t0, semanticModel));
                        }

                        return type.GetFullyQualifiedTypeNameSyntax(semanticModel).Syntax
                            .WithTriviaFrom(type);
                    }

                    foreach (var m in fieldOrPropertyType.GetMembers())
                    {
                        if (m.IsImplicitlyDeclared)
                            continue;
                        
                        (SyntaxReference SyntaxReference, SemanticModel SemanticModel) Basic()
                        {
                            var syntaxReference = m.DeclaringSyntaxReferences[0];
                            var semanticModel = MasterEnvironment.Instance.Compilation.GetSemanticModel(syntaxReference.SyntaxTree);
                            return (syntaxReference, semanticModel);
                        }

                        bool CheckAccessibility(ISymbol symbol, SemanticModel semanticModel)
                        {
                            return semanticModel.IsAccessible(f.Symbol.DeclaringSyntaxReferences[0].Span.Start, symbol);
                        }

                        if (m is IMethodSymbol method)
                        {
                            if (!HasEitherFlag(ForwardOptions.ForwardMethods))
                                continue;
                            var (syntaxReference, semanticModel) = Basic();
                            
                            if (!CheckAccessibility(m, semanticModel))
                                continue;
                            if (!ShouldRegardName_WithParentCheck(m.Name))
                                continue;
                            
                            var methodDeclaration = (MethodDeclarationSyntax) syntaxReference.GetSyntax();
                            
                            var argumentList = SeparatedList(
                                methodDeclaration.ParameterList.Parameters.Select(p => 
                                {
                                    var refKind = p.Modifiers.MaybeFirst(
                                        k => k.Kind() is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword or SyntaxKind.InKeyword);
                                    var expression = IdentifierName(p.Identifier);
                                    if (refKind.HasValue)
                                        return Argument(nameColon: null, refKind.Value, expression);
                                    return Argument(expression);
                                }));
                            
                            // f.m
                            var methodNameExpression = MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                fieldOrPropertyName,
                                IdentifierName(methodDeclaration.Identifier));

                            // f.m(a, ref b);
                            var invocationExpression = InvocationExpression(
                                methodNameExpression,
                                ArgumentList(argumentList));

                            // maybe add ref in front
                            ExpressionSyntax returnExpression = methodDeclaration.ReturnType is RefTypeSyntax
                                ? RefExpression(invocationExpression)
                                : invocationExpression;

                            // => ref f.m(a, ref b)
                            var body = ArrowExpressionClause(returnExpression);

                            // PrefixMPostfix
                            var newFuncName = ProcessMethodName(method.Name);
                            var newFuncIdentifier = Identifier(newFuncName);

                            // Fully qualify the return type.
                            var returnType = FullyQualifyTypes(methodDeclaration.ReturnType, semanticModel);

                            // Fully qualify the parameter types.
                            var newParameters = methodDeclaration.ParameterList.ReplaceNodes(
                                methodDeclaration.ParameterList.Parameters.Select(p => p.Type),
                                (t0, t1) => t1 ?? FullyQualifyTypes(t0, semanticModel));

                            var newFunctionSyntax = MethodDeclaration(
                                attributeLists: default,
                                modifiers: methodDeclaration.Modifiers,
                                returnType: returnType,
                                explicitInterfaceSpecifier: default,
                                identifier: newFuncIdentifier,
                                typeParameterList: methodDeclaration.TypeParameterList,
                                parameterList: newParameters,
                                constraintClauses: methodDeclaration.ConstraintClauses,
                                body: null,
                                expressionBody: body);

                            newMembers.Append(newFunctionSyntax);
                        }
                        else if (m is IPropertySymbol property)
                        {
                            if (!HasEitherFlag(ForwardOptions._ForwardProperties))
                                continue;
                            var (syntaxReference, semanticModel) = Basic();
                            
                            bool hasGet = property.GetMethod is not null;
                            bool hasSet = property.SetMethod is not null;

                            bool isGetAccessile = CheckAccessibility(property.GetMethod, semanticModel);
                            bool isSetAccessile = CheckAccessibility(property.SetMethod, semanticModel);

                            bool shouldForwardGet = hasGet && isGetAccessile && HasEitherFlag(ForwardOptions._ForwardGetterProperties);
                            bool shouldForwardSet = hasSet && isSetAccessile && HasEitherFlag(ForwardOptions._ForwardSetterProperties);

                            bool shouldForwardSomething = shouldForwardGet || shouldForwardSet;

                            if (!shouldForwardSomething)
                                continue;

                            if (!ShouldRegardName_WithParentCheck(property.Name))
                                continue;

                            var propertyDeclaration = (PropertyDeclarationSyntax) syntaxReference.GetSyntax();

                            // Fully qualify the return type.
                            var returnType = FullyQualifyTypes(propertyDeclaration.Type, semanticModel);

                            // f.m
                            var propNameExpression = MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                fieldOrPropertyName,
                                IdentifierName(propertyDeclaration.Identifier));
                            
                            // PrefixMPostfix
                            var newPropName = ProcessPropertyName(property.Name);
                            var newPropIdentifier = Identifier(newPropName);

                            int accessorCount = 0;
                            if (shouldForwardGet)
                                accessorCount++;
                            if (shouldForwardSet)
                                accessorCount++;
                            
                            var accessors = new AccessorDeclarationSyntax[accessorCount];

                            {
                                int i = 0;
                                if (shouldForwardGet)
                                {
                                    var getAccessor = GetAccessorArrow(
                                        propNameExpression,
                                        propertyDeclaration.GetGetModifiers(isClass: info.Symbol.IsReferenceType));
                                    accessors[i++] = getAccessor;
                                }
                                if (shouldForwardSet)
                                {
                                    var setAccessor = SetAccessorArrow(
                                        propNameExpression.Assignment("value"),
                                        propertyDeclaration.GetSetModifiers());
                                    accessors[i++] = setAccessor;
                                }
                            }

                            var accessorList = AccessorList().AddAccessors(accessors);

                            var newPropSyntax = PropertyDeclaration(
                                attributeLists: default,
                                modifiers: propertyDeclaration.GetNonAccessorModifiers(),
                                type: returnType,
                                explicitInterfaceSpecifier: default,
                                identifier: newPropIdentifier,
                                accessorList: accessorList);

                            newMembers.Add(newPropSyntax);
                        }
                        else if (m is IFieldSymbol field)
                        {
                            if (!HasEitherFlag(ForwardOptions._ForwardFields))
                                continue;

                            bool cannotDoRef = fieldOrPropertyType.IsValueType && info.Symbol.IsValueType;

                            var (syntaxReference, semanticModel) = Basic();

                            if (!CheckAccessibility(field, semanticModel))
                                continue;
                            
                            bool shouldForwardGet = HasEitherFlag(ForwardOptions._ForwardFieldsAsGetters);
                            bool shouldForwardSet = !field.IsReadOnly
                                && HasEitherFlag(ForwardOptions._ForwardFieldsAsSetters)
                                && !(info.Symbol.IsValueType && field.IsReadOnly);
                            bool shouldForwardRef = HasEitherFlag(ForwardOptions._ForwardFieldsAsRefProperties);

                            bool shouldForwardSomething = shouldForwardGet || shouldForwardSet || shouldForwardRef;

                            if (!shouldForwardSomething)
                                continue;

                            if (!ShouldRegardName_WithParentCheck(field.Name))
                                continue;

                            string propertyName = null;
                            string refPropertyName = null;

                            if (shouldForwardGet || shouldForwardSet)
                                propertyName = ProcessPropertyName(field.Name);
                            
                            if (shouldForwardRef)
                                refPropertyName = ProcessRefPropertyName(field.Name);

                            if ((shouldForwardGet || shouldForwardSet) && shouldForwardRef)
                            {
                                if (propertyName == refPropertyName)
                                {
                                    logger.LogWarning($"Same property name ({propertyName}) at {field.GetLocationInfo()}, ignoring.");
                                    continue;
                                }
                            }

                            VariableDeclaratorSyntax variableDeclarator;
                            VariableDeclarationSyntax variableDeclaration;
                            {
                                variableDeclaration = syntaxReference.GetSyntax() as VariableDeclarationSyntax;
                                if (variableDeclaration is not null)
                                {
                                    if (variableDeclaration.Variables.Count != 0)
                                    {
                                        logger.LogWarning($"Multiple declarations are not supported {field.GetLocationInfo()}."
                                            + "Only the first one will be considered.");
                                    }
                                    variableDeclarator = variableDeclaration.Variables[0];
                                }
                                else
                                {
                                    variableDeclarator = (VariableDeclaratorSyntax) syntaxReference.GetSyntax();
                                    variableDeclaration = (VariableDeclarationSyntax) variableDeclarator.Parent;
                                }
                            }
                            var fieldDeclaration = (FieldDeclarationSyntax) variableDeclaration.Parent;;

                            // f.m
                            var fieldNameExpression = MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                fieldOrPropertyName,
                                IdentifierName(variableDeclarator.Identifier));
                            
                            // Fully qualify the return type.
                            var returnType = FullyQualifyTypes(variableDeclaration.Type, semanticModel);
        
                            SyntaxTokenList getterModifiers = info.Symbol.IsReferenceType
                                ? default
                                : new(Token(SyntaxKind.ReadOnlyKeyword));

                            var nonAccessorModifiers = fieldDeclaration.Modifiers.GetNonAccessor();
                            
                            if (shouldForwardGet || shouldForwardSet)
                            {
                                int accessorCount = 0;
                                if (shouldForwardGet)
                                    accessorCount++;
                                if (shouldForwardSet)
                                    accessorCount++;
                                
                                var accessors = new AccessorDeclarationSyntax[accessorCount];

                                {
                                    int i = 0;
                                    if (shouldForwardGet)
                                    {
                                        var getAccessor = GetAccessorArrow(fieldNameExpression, getterModifiers);
                                        accessors[i++] = getAccessor;
                                    }
                                    if (shouldForwardSet)
                                    {
                                        var setAccessor = SetAccessorArrow(fieldNameExpression.Assignment("value"));
                                        accessors[i++] = setAccessor;
                                    }
                                }

                                var accessorList = AccessorList(new(accessors));

                                var newPropIdentifier = Identifier(propertyName);
                                var newPropSyntax = PropertyDeclaration(
                                    attributeLists: default,
                                    modifiers: nonAccessorModifiers,
                                    type: returnType,
                                    explicitInterfaceSpecifier: default,
                                    identifier: newPropIdentifier,
                                    accessorList: accessorList);

                                newMembers.Add(newPropSyntax);
                            }

                            if (shouldForwardRef)
                            {
                                var accessor = GetAccessorArrow(RefExpression(fieldNameExpression), getterModifiers);

                                var accessorList = AccessorList(new(accessor));
                                var refReturnType = RefType(returnType);
                                var newPropIdentifier = Identifier(refPropertyName);

                                var newPropSyntax = PropertyDeclaration(
                                    attributeLists: default,
                                    modifiers: nonAccessorModifiers,
                                    type: refReturnType,
                                    explicitInterfaceSpecifier: default,
                                    identifier: newPropIdentifier,
                                    accessorList: accessorList);

                                newMembers.Add(newPropSyntax);
                            }
                        }
                    }
                }

                if (newMembers.Count == 0)
                    continue;

                var outerTypeSyntax = (TypeDeclarationSyntax) info.Symbol.DeclaringSyntaxReferences[0].GetSyntax();
                outerTypeSyntax = outerTypeSyntax.WithMembers(new(newMembers)).WithAttributeLists(default);
                yield return WrapPartialType(info.Symbol, outerTypeSyntax);
            }
        }
    }

    public struct ForwardSingleInfo
    {
        public ForwardAttribute Attribute;
        public ISymbol Symbol;
        public Regex AcceptRegex;
        public Regex RejectRegex;
    }
    public struct ForwardInfo
    {
        public readonly INamedTypeSymbol Symbol => (INamedTypeSymbol) Config.Symbol;
        public ForwardSingleInfo Config;
        public List<ForwardSingleInfo> DecoratedFieldsOrProperties;
    }
}