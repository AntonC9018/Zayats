
namespace Kari.Plugins.Overloads
{
    using Microsoft.CodeAnalysis;
    using System;
    using System.Diagnostics;

    public class GenerateCodeForAttribute : Attribute
    {
        public ISymbol Type { get; set; }
    }
    // Use this instead:
    // https://docs.microsoft.com/en-us/dotnet/api/system.componentmodel.defaultvalueattribute?view=net-6.0
    // public class DefaultAttribute : System.Attribute{}

    public class OverloadAttribute : Attribute
    {
    }
}
