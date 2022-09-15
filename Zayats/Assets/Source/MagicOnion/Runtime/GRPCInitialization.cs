using Grpc.Core;
using MagicOnion.Unity;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using UnityEngine;

namespace Zayats.Net.Unity
{
    public class Resolver : IFormatterResolver
    {
        public static readonly Resolver Instance = new();
        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            // return formatter for type T.
            // basic types / complex types.
            return null;
        }
    }

    public static class Initialization
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeGRPC()
        {
            StaticCompositeResolver.Instance.Register(
                Resolver.Instance,
                StandardResolver.Instance);

            MessagePackSerializer.DefaultOptions = MessagePackSerializer.DefaultOptions
                .WithResolver(StaticCompositeResolver.Instance);

            // Initialize gRPC channel provider when the application is loaded.
            GrpcChannelProviderHost.Initialize(new DefaultGrpcChannelProvider(new []
            {
                // send keepalive ping every 5 second, default is 2 hours
                new ChannelOption("grpc.keepalive_time_ms", 5000),
                // keepalive ping time out after 5 seconds, default is 20 seconds
                new ChannelOption("grpc.keepalive_timeout_ms", 5 * 1000),
            }));
        }
    }
}