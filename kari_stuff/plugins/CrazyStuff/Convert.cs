namespace Kari.Plugins.Stuff;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;
using System;
using System.Linq;

public static class Program
{
    public static void Main(string[] args)
    {
        T DoStuff<T>(string attributeName, string attributeConstructor, Func<ITypeSymbol, Compilation, Action<string>, T> getAttribute)
        {
            const string attr = DummyStuffAnnotations.Text;
            string text = attr + @"
                namespace Stuff
                { 
                    using Kari.Plugins.Stuff;
                    [" + attributeName + "(" + attributeConstructor + @")]
                    class Test {} 
                }";

            var syntaxTree = CSharpSyntaxTree.ParseText(text);

            var standardMetadataType = new[]
            {
                typeof(object),
                typeof(Attribute),
            };
            var metadata = standardMetadataType
                .Select(t => t.Assembly.Location)
                .Distinct()
                .Select(t => MetadataReference.CreateFromFile(t));
            var compilation = CSharpCompilation.Create("Test", new[]{syntaxTree}, metadata);
            foreach (var d in compilation.GetDiagnostics())
                Console.WriteLine(d.GetMessage());
            var symbol = compilation.GetTypeByMetadataName("Stuff.Test");
            T result = getAttribute(symbol, compilation, s => System.Console.WriteLine(s));
            Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
            return result;
        }

        NoConstructorAttribute NoConstructor(string s) => DoStuff("NoConstructor", s, StuffSymbols.GetNoConstructorAttribute);
        var t = NoConstructor("Type = typeof(System.Type)");
        Console.WriteLine(t.Type.Name);

        CrazyAttribute Crazy(string s) => DoStuff("Crazy", s, StuffSymbols.GetCrazyAttribute);
        Crazy("SomeEnum.B");
        Crazy(@"a: 1, ""a"",
    Property = 5,
    NamedTypes = new System.Type[]{ typeof(Test) }");
        Crazy(@"values: new[]{""a""}, a: 1");
        Crazy(@"""a""");
        Crazy(@"1, ""a""");
        Crazy("");
        Crazy(@"1, 2");
        Crazy(@"typeof(Test), typeof(Test), typeof(Test)");
    }
}