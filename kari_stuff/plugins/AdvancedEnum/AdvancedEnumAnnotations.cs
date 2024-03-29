namespace Kari.Plugins.AdvancedEnum
{
    using System;
    using System.Diagnostics;

    [AttributeUsage(AttributeTargets.Enum)]
    [Conditional("CodeGeneration")]
    public class GenerateArrayWrapperAttribute : Attribute
    {
        public bool IsStruct { get; } = true;
        public string TypeName { get; set; }

        public GenerateArrayWrapperAttribute(string className = null)
        {
            TypeName = className;
        }
    }

    public class InvalidEnumValueException : Exception
    {
        public int RecordedValue { get; }

        public InvalidEnumValueException(int recordedValue)
        {
            RecordedValue = recordedValue;
        }
    }
}
