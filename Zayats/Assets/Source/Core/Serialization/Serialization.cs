namespace Zayats.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using Kari.Plugins.Forward;
    using Kari.Zayats.Exporter;
    using Newtonsoft.Json;
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

    public partial struct JsonSerializationContext
    {
        [Forward] public CommonSerializationContext Common;
        public Dictionary<object, string>[] ObjectToJsonName;
    }
    
    public partial struct JsonDeserializationContext
    {
        [Forward] public CommonSerializationContext Common;
        public Dictionary<string, object>[] JsonNameToObject;
    }

    public partial struct BinarySerializationContext
    {
        [Forward] public CommonSerializationContext Common;
        public Dictionary<object, int>[] ObjectToNumber; 
    }

    public partial struct BinaryDeserializationContext
    {
        [Forward] public CommonSerializationContext Common;
        public object[][] NumberToObject;
    }

    public class MapInterfacesConverter : JsonConverter
    {
        private JsonSerializationContext _context;

        public MapInterfacesConverter(JsonSerializationContext context)
        {
            _context = context;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is null)
            {
                JToken token = null;
                token.WriteTo(writer);
            }
            else
            {
                var type = value.GetType();
                var category = _context.InterfaceToCategory[type];
                string name = _context.ObjectToJsonName[(int) category][value];
                var token = (JToken) name;
                token.WriteTo(writer);
            }
        }

        public override bool CanRead => false;
        public override bool CanConvert(System.Type objectType) => _context.InterfaceToCategory.ContainsKey(objectType);
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new System.NotImplementedException();
        }
    }

    public class GameStateCreator : JsonConverter
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanConvert(System.Type objectType) => objectType == typeof(Data.Game);
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            var t = new Data.Game();
            Components.InitializeComponentStorages(ref t);
            object obj = t;
            serializer.Populate(reader, obj);
            return obj;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new System.NotImplementedException();
        }
    }

    public class DoNothingConverter : JsonConverter
    {
        public System.Type[] _types;

        public DoNothingConverter(params System.Type[] types)
        {
            _types = types;
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanConvert(System.Type objectType) => Array.IndexOf(_types, objectType) != -1;
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            return existingValue;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
        }
    }
    
    public class OnlyFieldsResolver : DefaultContractResolver
    {
        public OnlyFieldsResolver()
        {
            this.IgnoreSerializableAttribute = true;
            this.IgnoreSerializableInterface = true;
            this.IgnoreShouldSerializeMembers = true;
            this.SerializeCompilerGeneratedMembers = true;
        }

        protected override JsonContract CreateContract(Type objectType)
        {
            return base.CreateContract(objectType);
        }

        protected override List<MemberInfo> GetSerializableMembers(Type objectType)
        {
            return objectType
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
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

    public static class Serialization
    {
        public static string SerializeJson(in Data.Game game)
        {
            var map = SerializationHelper.CreateMap();
            JsonSerializationContext context = new()
            {
                ObjectToJsonName = map.Select(a => a.ToDictionary(t => t.Object, t => t.Name)).ToArray(),
                InterfaceToCategory = SerializationHelper.GetInterfaceToCategoryMap(),
            };

            JsonSerializerSettings settings = new()
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Include,
                ObjectCreationHandling = ObjectCreationHandling.Reuse,
                TypeNameHandling = TypeNameHandling.None,
                ConstructorHandling = ConstructorHandling.Default,
                ContractResolver = new OnlyFieldsResolver(),
                Converters = new JsonConverter[]
                {
                    new MapInterfacesConverter(context),
                    // new GameStateCreator(),
                    new DoNothingConverter(typeof(Events.Storage)),
                },
                CheckAdditionalContent = false,
            };

            var result = JsonConvert.SerializeObject(game, settings: settings, formatting: Formatting.Indented);

            return null;
        }
        public static Data.Game Deserialize(string json)
        {
            return default;
        }
    }
}