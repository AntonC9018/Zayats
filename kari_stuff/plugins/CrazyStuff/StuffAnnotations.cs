namespace Kari.Plugins.Stuff
{
    using System;
    using Microsoft.CodeAnalysis;

    public enum SomeEnum
    {
        A, B, C,
    }

    public class CrazyAttribute : Attribute
    {
        public CrazyAttribute(params string[] values)
        {
            Strings = values;
        }
        public CrazyAttribute(int a, params string[] values)
        {
            Property = a;
            Strings = values;
        }
        public CrazyAttribute(int a, int b)
        {
            Property = a + b;
        }
        public CrazyAttribute(INamedTypeSymbol a, ITypeSymbol b, Microsoft.CodeAnalysis.ITypeSymbol c)
        {
            NamedTypes = new[] {a};
            Type = b ?? c;
        }
        public CrazyAttribute(SomeEnum s)
        {
            Property = (int) s;
        }

        public int Property { get; set; }
        public string[] Strings;
        public INamedTypeSymbol[] NamedTypes;
        public ITypeSymbol Type;
        public int OtherProperty { get; set; }
    }

    public class NoConstructorAttribute : Attribute
    {
        public INamedTypeSymbol Type;
    }
}
