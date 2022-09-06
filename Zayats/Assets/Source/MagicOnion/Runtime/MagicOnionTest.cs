using Grpc.Core;
using MagicOnion;
using MagicOnion.Client;
using MagicOnion.Unity;
using UnityEngine;
using Zayats.Net.Shared;
using Zayats.Unity.Net.Generated;

namespace Zayats.Unity.Net
{
    public class MagicOnionTest : MonoBehaviour
    {
        async void Start()
        {
            MagicOnionInitializer.Register();
            
            var channel = GrpcChannelx.ForTarget(new GrpcChannelTarget("localhost", 5000, isInsecure: true));
            var serviceClient = MagicOnionClient.Create<ITestService>(channel);
            int result = await serviceClient.SumAsync(1, 2);
            Debug.Log(result);
        }
    }
}