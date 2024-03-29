namespace Zayats.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Kari.Plugins.Forward;
    using Kari.Zayats.Exporter;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Bson;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;
    using Zayats.Core;
    using Zayats.Core.Generated;
    using Zayats.Serialization.Generated;
    using static Zayats.Core.Assert;
    
    public struct CommonSerializationContext
    {
        public Dictionary<System.Type, ExportCategory> InterfaceToCategory;
    }
    
    public partial class DeserializationContext
    {
        [Forward] public CommonSerializationContext Common;
        public Dictionary<string, object>[] NameToObject;
        public object[][] NumberToObject;
    }

    public partial class SerializationContext
    {
        [Forward] public CommonSerializationContext Common;
        public Dictionary<object, string>[] ObjectToName;
        public Dictionary<object, int>[] ObjectToNumber; 
    }

    public enum BehaviorSerialization
    {
        String,
        Id,
    }

    public class MapInterfacesSerializeConverter : JsonConverter
    {
        private SerializationContext _context;

        public MapInterfacesSerializeConverter(SerializationContext context)
        {
            _context = context;
        }

        public override bool CanWrite => true;
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JToken token;
            if (value is null)
            {
                token = JValue.CreateNull();
            }
            else
            {
                var type = value.GetType();
                var category = _context.InterfaceToCategory[type];

                // if (writer is BsonDataWriter)
                {
                    int id = _context.ObjectToNumber[(int) category][value];
                    token = (JToken) id;
                }
                // else
                {
                    string name = _context.ObjectToName[(int) category][value];
                    token = (JToken) name;
                }
            }
            token.WriteTo(writer);
        }

        public override bool CanRead => false;
        public override bool CanConvert(System.Type objectType) => _context.InterfaceToCategory.ContainsKey(objectType);
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }
    }

    public class MapInterfacesDeserializeConverter : JsonConverter
    {
        private DeserializationContext _context;

        public MapInterfacesDeserializeConverter(DeserializationContext context)
        {
            _context = context;
        }

        public override bool CanWrite => false;
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead => true;
        public override bool CanConvert(System.Type objectType) => _context.InterfaceToCategory.ContainsKey(objectType);
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            var value = reader.Value;
            if (value is null)
                return null;
            var category = _context.InterfaceToCategory[objectType];
            if (value is int intValue)
                return _context.NumberToObject[(int) category][intValue];
            else if (value is string strValue)
                return _context.NameToObject[(int) category][strValue];
            else
                throw new NotSupportedException("The only id types supported are int and string.");
        }
    }

    public class OnlyFields_IgnoreTypes_Resolver : DefaultContractResolver
    {
        private readonly System.Type[] _ignoredTypes;

        public OnlyFields_IgnoreTypes_Resolver(params System.Type[] ignoredTypes)
        {
            this.IgnoreSerializableAttribute = true;
            this.IgnoreSerializableInterface = true;
            this.IgnoreShouldSerializeMembers = true;
            this.SerializeCompilerGeneratedMembers = true;
            _ignoredTypes = ignoredTypes;
        }

        protected override JsonContract CreateContract(Type objectType)
        {
            return base.CreateContract(objectType);
        }

        protected override List<MemberInfo> GetSerializableMembers(Type objectType)
        {
            return objectType
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !_ignoredTypes.Contains(f.FieldType))
                .Cast<MemberInfo>()
                .ToList();
        }
    }

    public class TraceWriter : ITraceWriter
    {
        public TraceLevel LevelFilter => TraceLevel.Error;

        public void Trace(TraceLevel level, string message, Exception ex)
        {
            System.Console.WriteLine("level " + level);
            System.Console.WriteLine("message " + message);
            System.Console.WriteLine("ex " + ex);
        }
    }

    
    public class PopulateComponents : JsonConverter
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanConvert(System.Type objectType) => objectType == typeof(Components.Storage);
        
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            var storages = ((Components.Storage) existingValue);
            if (storages.Storages is null)
                storages = Components.CreateEmptyStorages();
            
            // { Storages: [{},{}...] }
            var jobj = JObject.Load(reader);
            var storagesProp = jobj.Property("Storages", StringComparison.OrdinalIgnoreCase);
            var arrayValues = storagesProp.Values().GetEnumerator();

            for (int i = 0; i < storages.Storages.Length; i++)
            {
                if (!arrayValues.MoveNext())
                    throw new NotSupportedException("Wrong count in components array.");

                if (arrayValues.Current is not JObject storageValueObj)
                    throw new NotSupportedException("Component storages must be objects in json.");

                var reader1 = storageValueObj.CreateReader();
                serializer.Populate(reader1, storages.Storages[i]); 
            }

            return storages;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }
    }
    

    public static class Serialization
    {
        private static (string Name, object Object)[][] _Map;
        private static Dictionary<System.Type, ExportCategory> _CategoryMap;
        private static readonly OnlyFields_IgnoreTypes_Resolver _ResolverInstance = new(typeof(Events.Storage));
        private static readonly PopulateComponents _PopulateComponents = new();
        private static MapInterfacesSerializeConverter _MapInterfacesSerializeConverter;
        private static MapInterfacesDeserializeConverter _MapInterfacesDeserializeConverter;
        private static JsonSerializer _Serializer;
        private static JsonSerializer _Deserializer;

        private static JsonSerializer CreateDefaultJsonSerializer()
        {
            JsonSerializer serializer = new()
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Include,
                ObjectCreationHandling = ObjectCreationHandling.Auto,
                TypeNameHandling = TypeNameHandling.None,
                ConstructorHandling = ConstructorHandling.Default,
                ContractResolver = _ResolverInstance,
                CheckAdditionalContent = false,
                Formatting = Formatting.Indented,
            };
            return serializer;
        }

        public static void SerializeJson(in Data.Game game, JsonWriter writer)
        {
            var serializer = _Serializer;
            if (serializer is null)
            {
                _Map ??= SerializationHelper.CreateMap();
                _CategoryMap ??= SerializationHelper.GetInterfaceToCategoryMap();

                serializer = CreateDefaultJsonSerializer();
                var context = new SerializationContext
                {
                    InterfaceToCategory = _CategoryMap,
                    ObjectToName = _Map.Select(a => a.ToDictionary(t => t.Object, t => t.Name)).ToArray(),
                    ObjectToNumber = _Map.Select(a => 
                    {
                        var dict = new Dictionary<object, int>();
                        for (int i = 0; i < a.Length; i++)
                            dict.Add(a[i], i);
                        return dict;
                    }).ToArray(),
                };
                _MapInterfacesSerializeConverter = new(context);
                serializer.Converters.Add(_MapInterfacesSerializeConverter);
                _Serializer = serializer;
            }
            serializer.Serialize(writer, game);
        }

        public static Data.Game Deserialize(JsonReader reader)
        {
            var deserializer = _Deserializer;
            if (deserializer is null)
            {
                _Map ??= SerializationHelper.CreateMap();
                _CategoryMap ??= SerializationHelper.GetInterfaceToCategoryMap();

                deserializer = CreateDefaultJsonSerializer();
                var context = new DeserializationContext
                {
                    InterfaceToCategory = _CategoryMap,
                    NameToObject = _Map.Select(a => a.ToDictionary(t => t.Name, t => t.Object)).ToArray(),
                    NumberToObject = _Map.Select(a => a.Select(t => t.Object).ToArray()).ToArray(),
                };
                _MapInterfacesDeserializeConverter = new(context);
                var converters = deserializer.Converters;
                converters.Add(_MapInterfacesDeserializeConverter);
                converters.Add(_PopulateComponents);
                _Deserializer = deserializer;
            }
            return deserializer.Deserialize<Data.Game>(reader); 
        }
    }
}