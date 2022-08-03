namespace Kari.Plugins.Forward
{
    using System;
    using System.Diagnostics;

    public enum ForwardOptions
    {
        // Structs will inherit the readonly modifier.
        Methods = 1 << 0,

        _ForwardFields = 1 << 1,
        _ForwardFieldsAsRefProperties = 1 << 2,
        _ForwardFieldsAsGetters = 1 << 3,
        _ForwardFieldsAsSetters = 1 << 4,

        // Won't work for struct types.
        FieldsAsRefProperties = _ForwardFields | _ForwardFieldsAsRefProperties,
        
        // Structs get the readonly modifier for the getter.
        FieldsAsGetters = _ForwardFields | _ForwardFieldsAsGetters,
        FieldsAsSetters = _ForwardFields | _ForwardFieldsAsSetters,

        _ForwardProperties = 1 << 5,
        _ForwardGetterProperties = 1 << 6,
        _ForwardSetterProperties = 1 << 7,

        // Structs will inherit the readonly modifier of the getter.
        Properties = _ForwardProperties | _ForwardGetterProperties | _ForwardSetterProperties,
        GetterProperties = _ForwardProperties | _ForwardGetterProperties,
        SetterProperties = _ForwardProperties | _ForwardSetterProperties,
    }

    [AttributeUsage(AttributeTargets.Field
        | AttributeTargets.Property
        | AttributeTargets.Class
        | AttributeTargets.Struct)]
    [Conditional("CodeGeneration")]
    public class ForwardAttribute : Attribute
    {
        public ForwardAttribute(ForwardOptions options)
        {
            Options = new ForwardOptions?(options);
        }

        public ForwardAttribute()
        {
        }

        public ForwardOptions? Options { get; set; }
        //  =
            // ForwardOptions.ForwardFieldsAsGetters
            // | ForwardOptions.ForwardFieldsAsSetters
            // | ForwardOptions.ForwardMethods
            // | ForwardOptions.ForwardProperties;

        // Use {Name} to mean the field/property name to which we're forwarding.
        public string MethodPrefix { get; set; }
        public string MethodSuffix { get; set; }

        public string PropertyPrefix { get; set; }
        public string PropertySuffix { get; set; }

        public string RefPropertyPrefix { get; set; }
        public string RefPropertySuffix { get; set; }

        // Regex patterns.
        public string AcceptPattern { get; set; }
        public string RejectPattern { get; set; }
        public bool AcceptOverReject { get; set; } = false;
        public bool RejectOverAccept
        {
            get => !AcceptOverReject;
            set => AcceptOverReject = !value;
        }
    }
}
