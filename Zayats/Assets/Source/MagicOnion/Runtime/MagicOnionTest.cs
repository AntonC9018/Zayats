using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using MagicOnion;
using MagicOnion.Client;
using MagicOnion.Unity;
using MagicOnion.Utils;
using MessagePack;
using UnityEngine;
using Zayats.Net.Shared;
using Zayats.Unity.Net.Generated;

namespace Zayats.Unity.Net
{
    public class MagicOnionTest : MonoBehaviour
    {
        async void Start()
        {
            var channel = GrpcChannelx.ForTarget(new GrpcChannelTarget("localhost", 5000, isInsecure: true));
            var hub = new GameClient();
            await hub.ConnectAsync(channel, roomName: "a", playerName: "Anton");
            await hub.LeaveAsync();
        }
    }

    public class SubscriptionContext
    {
    }

    public enum Result
    {
        // Should discontinue the current operation
        CannotContinue,
        //
        CanContinue,
    }
    

    public static class Helper2
    {
        public interface ISubscribeErrorHandler
        {
            // Returns true if the exc
            Result HandleConsumeException(Exception e);
            Result HandleStreamException(Exception e);
            // ErrorHandlingResult HandleConsumeError(string error); 
        }

        public interface IConsumeData
        {
            Result ConsumeData(byte[] data);
        }
        
        public static async Task StartSubscription<TConsumeErrorHandler>(
            IAsyncStreamReader<byte[]> reader,
            ISubscribeErrorHandler errorHandler,
            IConsumeData consumer,
            CancellationToken cancellationToken)
        {
            var firstMoveNextTask = reader.MoveNext(cancellationToken);
            if (firstMoveNextTask.IsFaulted)
            {
                // NOTE: Grpc.Net:
                //           If an error is returned from `StreamingHub.Connect` method server-side,
                //           ResponseStream.MoveNext synchronously returns a task that is `IsFaulted = true`.
                //       C-core:
                //           `firstMoveNextTask` is incomplete task (`IsFaulted = false`) whether ResponseHeadersAsync is failed or not.
                //           If the channel is disconnected or the server returns an error (StatusCode != OK), awaiting the Task will throw an exception.
                try
                {
                    await firstMoveNextTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (errorHandler.HandleStreamException(ex) == Result.CannotContinue)
                        return;
                }
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            try
            {
                var t = firstMoveNextTask;
                while (await t.ConfigureAwait(false)) // avoid Post to SyncContext(it loses one-frame per operation)
                {
                    try
                    {
                        if (consumer.ConsumeData(reader.Current) == Result.CannotContinue)
                            return;
                    }
                    catch (Exception ex)
                    {
                        if (errorHandler.HandleConsumeException(ex) == Result.CannotContinue)
                            return;
                    }

                    if (cancellationToken.IsCancellationRequested)
                        return;

                    t = reader.MoveNext(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                    return;

                errorHandler.HandleStreamException(ex);
            }
            finally
            {
            }
        }

        public struct HandleMessageResult
        {
            public bool WasHandled;
            public Result Result;
        }

        public interface IResponseHandler
        {
            Result? HandleResponse(int messageId, int methodId, ArraySegment<byte> data);
            Result? HandleServerError(int messageId, Status status, string errorMessage); 
            Result HandleBroadcast(int methodId, ArraySegment<byte> data);
        }

        public interface IConsumeErrorHandler
        {
            Result HandleResponseException(Exception e);
            Result HandleIgnoredResponse(int messageId, int methodId, ArraySegment<byte> data);
            Result HandleIgnoredServerError(int messageId, Status status, string errorMessage);
            Result HandleInvalidFormatException(MessagePackSerializationException e);
        }

        public static Result ConsumeStandardMessage(
            byte[] data,
            SynchronizationContext syncContext,
            IResponseHandler responseHandler,
            IConsumeErrorHandler errorHandler)
        {
            try
            {
                var messagePackReader = new MessagePackReader(data);
                var arrayLength = messagePackReader.ReadArrayHeader();
                
                // response: [messageId, methodId, response]
                if (arrayLength == 3)
                {
                    var messageId = messagePackReader.ReadInt32();
                    var methodId = messagePackReader.ReadInt32();
                    var offset = (int) messagePackReader.Consumed;
                    var rest = new ArraySegment<byte>(data, offset, data.Length - offset);

                    var r = responseHandler.HandleResponse(messageId, methodId, rest);
                    if (r.HasValue)
                        return r.Value;

                    return errorHandler.HandleIgnoredResponse(messageId, methodId, rest);
                }
                else if (arrayLength == 4)
                {
                    var messageId = messagePackReader.ReadInt32();
                    var statusCode = messagePackReader.ReadInt32();
                    var detail = messagePackReader.ReadString();
                    var error = messagePackReader.ReadString();
                    var status = new Status((StatusCode) statusCode, detail);

                    var r = responseHandler.HandleServerError(messageId, status, error);
                    if (r.HasValue)
                        return r.Value;

                    return errorHandler.HandleIgnoredServerError(messageId, status, error);
                }

                // broadcast: [methodId, [argument]]
                else
                {
                    var methodId = messagePackReader.ReadInt32();
                    var offset = (int) messagePackReader.Consumed;
                    var rest = new ArraySegment<byte>(data, offset, data.Length - offset);
                    return responseHandler.HandleBroadcast(methodId, rest);
                }
            }
            catch (MessagePackSerializationException e)
            {
                return errorHandler.HandleInvalidFormatException(e);
            }
        }

        public static async Task WriteMessageAsync<T>(
            int methodId,
            T message,
            AsyncLock lock_,
            IClientStreamWriter<byte[]> writer,
            MessagePackSerializerOptions serializerOptions)
        {
            byte[] BuildMessage()
            {
                using (var buffer = ArrayPoolBufferWriter.RentThreadStaticWriter())
                {
                    var writer = new MessagePackWriter(buffer);
                    writer.WriteArrayHeader(2);
                    writer.Write(methodId);
                    MessagePackSerializer.Serialize(ref writer, message, serializerOptions);
                    writer.Flush();
                    return buffer.WrittenSpan.ToArray();
                }
            }

            var messageBytes = BuildMessage();
            using (await lock_.LockAsync().ConfigureAwait(false))
                await writer.WriteAsync(messageBytes).ConfigureAwait(false);
        }

        protected async Task<TResponse> WriteMessageWithResponseAsync<TRequest, TResponse>(int methodId, TRequest message)
        {
            ThrowIfDisposed();

            var mid = Interlocked.Increment(ref _currentMessageId);
            var tcs = new TaskCompletionSourceEx<TResponse>(); // use Ex
            _responseFutures[mid] = (object)tcs;

            byte[] BuildMessage()
            {
                using (var buffer = ArrayPoolBufferWriter.RentThreadStaticWriter())
                {
                    var writer = new MessagePackWriter(buffer);
                    writer.WriteArrayHeader(3);
                    writer.Write(mid);
                    writer.Write(methodId);
                    MessagePackSerializer.Serialize(ref writer, message, _serializerOptions);
                    writer.Flush();
                    return buffer.WrittenSpan.ToArray();
                }
            }

            var v = BuildMessage();
            using (await _asyncLock.LockAsync().ConfigureAwait(false))
            {
                await _connection.RawStreamingCall.RequestStream.WriteAsync(v).ConfigureAwait(false);
            }

            return await tcs.Task.ConfigureAwait(false); // wait until server return response(or error). if connection was closed, throws cancellation from DisposeAsyncCore.
        }
    }


    public abstract class HubClientBase
    {
        public const string ZayatsHubVersionHeaderKey = "zayats-hub";
        public const string ZayatsHubVersionHeaderValue = "1";

        private readonly string _host;
        private readonly CallOptions _option;
        private readonly CallInvoker _callInvoker;
        private readonly IMagicOnionClientLogger _logger;

        protected readonly MessagePackSerializerOptions _serializerOptions;
        readonly AsyncLock _asyncLock = new AsyncLock();

        private DuplexStreamingResult<byte[], byte[]> _connection;
        private Task _subscription;
        private TaskCompletionSource<object> _waitForDisconnect = new TaskCompletionSource<object>();

        // {messageId, TaskCompletionSource}
        private ConcurrentDictionary<int, object> _responseFutures = new ConcurrentDictionary<int, object>();
        protected CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private int _currentMessageId = 0;
        private bool _disposed;

        protected HubClientBase(Method<byte[], byte[]> method, CallInvoker callInvoker, string host, CallOptions option, MessagePackSerializerOptions serializerOptions, IMagicOnionClientLogger logger)
        {
            DuplexStreamingAsyncMethod = method;
            _callInvoker = callInvoker;
            _host = host;
            _option = option;
            _serializerOptions = serializerOptions;
            _logger = logger ?? NullMagicOnionClientLogger.Instance;
        }

        protected Method<byte[], byte[]> DuplexStreamingAsyncMethod { get; }


        public enum ConnectionResult
        {
            Success,
            CancellationRequested,
            MissingVersionHeader,
            WrongVersionHeader,
            ConnectionFailure,
            ConnectionSuccessful_NegotiationFailure,
        } 

        // call immediately after create.
        public async Task<ConnectionResult> ConnectAndSubscribeAsync(CancellationToken cancellationToken)
        {
            var syncContext = SynchronizationContext.Current; // capture SynchronizationContext.
            var callResult = _callInvoker.AsyncDuplexStreamingCall<byte[], byte[]>(DuplexStreamingAsyncMethod, _host, _option);
            var streamingResult = new DuplexStreamingResult<byte[], byte[]>(
                callResult,
                new MarshallingClientStreamWriter<byte[]>(callResult.RequestStream, _serializerOptions),
                new MarshallingAsyncStreamReader<byte[]>(callResult.ResponseStream, _serializerOptions),
                _serializerOptions);

            _connection = streamingResult;

            // Establish ZayatsHub connection between the client and the server.
            Metadata.Entry messageVersion = default;
            try
            {
                // The client can read the response headers before any StreamingHub's message.
                // MagicOnion.Server v4.0.x or before doesn't send any response headers. The client is incompatible with that versions.
                // NOTE: Grpc.Net:
                //           If the channel can not be connected, ResponseHeadersAsync will throw an exception.
                //       C-core:
                //           If the channel can not be connected, ResponseHeadersAsync will **return** an empty metadata.
                var headers = await streamingResult.ResponseHeadersAsync.ConfigureAwait(false);
                messageVersion = headers.FirstOrDefault(x => x.Key == ZayatsHubVersionHeaderKey);

                cancellationToken.ThrowIfCancellationRequested();

                if (messageVersion is null)
                {
                    _logger.Debug("Message version string not found in the metadata of the connection.");
                    return ConnectionResult.MissingVersionHeader;
                }

                // Check message version of StreamingHub.
                if (messageVersion.Value != ZayatsHubVersionHeaderValue)
                {
                    _logger.Debug($"Mismatch of the message version between the client and the server. (ServerVersion={messageVersion?.Value}; Expected={ZayatsHubVersionHeaderValue})");
                    return ConnectionResult.WrongVersionHeader;
                }
            }
            catch (RpcException e)
            {
                _logger.Error(e, $"Failed to connect to the hub '{DuplexStreamingAsyncMethod.ServiceName}'. ({e.Status})");
                return ConnectionResult.ConnectionFailure;
            }

            // What the heck does this do?
            var linkedToken = _cancellationTokenSource.Token; //CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token).Token;
            var firstMoveNextTask = _connection.RawStreamingCall.ResponseStream.MoveNext(linkedToken);
            if (firstMoveNextTask.IsFaulted)
            {
                // NOTE: Grpc.Net:
                //           If an error is returned from `StreamingHub.Connect` method server-side,
                //           ResponseStream.MoveNext synchronously returns a task that is `IsFaulted = true`.
                //       C-core:
                //           `firstMoveNextTask` is incomplete task (`IsFaulted = false`) whether ResponseHeadersAsync is failed or not.
                //           If the channel is disconnected or the server returns an error (StatusCode != OK), awaiting the Task will throw an exception.
                try
                {
                    await firstMoveNextTask.ConfigureAwait(false);
                }
                catch
                {
                    _logger.Debug($"The request started successfully (StatusCode = OK), but the StreamingHub client has failed to negotiate with the server.");
                    return ConnectionResult.ConnectionSuccessful_NegotiationFailure;
                }
            }

            this._subscription = StartSubscribe(syncContext, firstMoveNextTask);
        }

        protected abstract void OnResponseEvent(int methodId, object taskCompletionSource, ArraySegment<byte> data);
        protected abstract void OnBroadcastEvent(int methodId, ArraySegment<byte> data);

        private async Task StartSubscribe(SynchronizationContext syncContext, Task<bool> firstMoveNext)
        {
            var reader = _connection.RawStreamingCall.ResponseStream;
            try
            {
                var moveNext = firstMoveNext;
                while (await moveNext.ConfigureAwait(false)) // avoid Post to SyncContext(it loses one-frame per operation)
                {
                    try
                    {
                        ConsumeData(syncContext, reader.Current);
                    }
                    catch (Exception ex)
                    {
                        const string msg = "An error occurred when consuming a received message, but the subscription is still alive.";
                        // log post on main thread.
                        if (syncContext != null)
                        {
                            syncContext.Post(state => _logger.Error((Exception)state, msg), ex);
                        }
                        else
                        {
                            _logger.Error(ex, msg);
                        }
                    }

                    moveNext = reader.MoveNext(_cancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                {
                    return;
                }
                const string msg = "An error occurred while subscribing to messages.";
                // log post on main thread.
                if (syncContext != null)
                {
                    syncContext.Post(state => _logger.Error((Exception)state, msg), ex);
                }
                else
                {
                    _logger.Error(ex, msg);
                }
            }
            finally
            {
                try
                {
#if !UNITY_WEBGL
                    // set syncContext before await
                    // NOTE: If restore SynchronizationContext in WebGL environment, a continuation will not be executed inline and will be stuck.
                    if (syncContext != null && SynchronizationContext.Current == null)
                    {
                        SynchronizationContext.SetSynchronizationContext(syncContext);
                    }
#endif

                    await DisposeAsyncCore(false).ConfigureAwait(false);
                }
                finally
                {
                    _waitForDisconnect.TrySetResult(null);
                }
            }
        }

        // MessageFormat:
        // error-response: [messageId, statusCode, detail, StringMessage]
        private void ConsumeData(SynchronizationContext syncContext, byte[] data)
        {
            var messagePackReader = new MessagePackReader(data);
            var arrayLength = messagePackReader.ReadArrayHeader();
            
            // response: [messageId, methodId, response]
            if (arrayLength == 3)
            {
                var messageId = messagePackReader.ReadInt32();
                object future;
                if (_responseFutures.TryRemove(messageId, out future))
                {
                    var methodId = messagePackReader.ReadInt32();
                    try
                    {
                        var offset = (int) messagePackReader.Consumed;
                        var rest = new ArraySegment<byte>(data, offset, data.Length - offset);
                        OnResponseEvent(methodId, future, rest);
                    }
                    catch
                    {
                        throw;
                    }
                }
            }
            else if (arrayLength == 4)
            {
                var messageId = messagePackReader.ReadInt32();
                object future;
                if (_responseFutures.TryRemove(messageId, out future))
                {
                    var statusCode = messagePackReader.ReadInt32();
                    var detail = messagePackReader.ReadString();
                    var offset = (int) messagePackReader.Consumed;
                    var rest = new ArraySegment<byte>(data, offset, data.Length - offset);
                    var error = MessagePackSerializer.Deserialize<string>(rest, _serializerOptions);

                    {
                        RpcException ex;
                        var status = new Status((StatusCode) statusCode, detail);
                        if (string.IsNullOrWhiteSpace(error))
                        {
                            ex = new (status);
                        }
                        else
                        {
                            ex = new (status, detail + Environment.NewLine + error);
                        }
                        _logger.Error(ex, "Server error");
                    }
                }
            }

            // broadcast: [methodId, [argument]]
            else
            {
                var methodId = messagePackReader.ReadInt32();
                var offset = (int) messagePackReader.Consumed;
                if (syncContext != null)
                {
                    var tuple = Tuple.Create(methodId, data, offset, data.Length - offset);
                    syncContext.Post(state =>
                    {
                        var t = (Tuple<int, byte[], int, int>) state;
                        OnBroadcastEvent(t.Item1, new ArraySegment<byte>(t.Item2, t.Item3, t.Item4));
                    }, tuple);
                }
                else
                {
                    OnBroadcastEvent(methodId, new ArraySegment<byte>(data, offset, data.Length - offset));
                }
            }
        }

        protected async Task WriteMessageAsync<T>(int methodId, T message)
        {
            ThrowIfDisposed();

            byte[] BuildMessage()
            {
                using (var buffer = ArrayPoolBufferWriter.RentThreadStaticWriter())
                {
                    var writer = new MessagePackWriter(buffer);
                    writer.WriteArrayHeader(2);
                    writer.Write(methodId);
                    MessagePackSerializer.Serialize(ref writer, message, _serializerOptions);
                    writer.Flush();
                    return buffer.WrittenSpan.ToArray();
                }
            }

            var v = BuildMessage();
            using (await _asyncLock.LockAsync().ConfigureAwait(false))
            {
                await _connection.RawStreamingCall.RequestStream.WriteAsync(v).ConfigureAwait(false);
            }
        }

        protected async Task<TResponse> WriteMessageAsyncFireAndForget<TRequest, TResponse>(int methodId, TRequest message)
        {
            await WriteMessageAsync(methodId, message).ConfigureAwait(false);
#pragma warning disable CS8603 // Possible null reference return.
            return default;
#pragma warning restore CS8603 // Possible null reference return.
        }

        protected async Task<TResponse> WriteMessageWithResponseAsync<TRequest, TResponse>(int methodId, TRequest message)
        {
            ThrowIfDisposed();

            var mid = Interlocked.Increment(ref _currentMessageId);
            var tcs = new TaskCompletionSourceEx<TResponse>(); // use Ex
            _responseFutures[mid] = (object)tcs;

            byte[] BuildMessage()
            {
                using (var buffer = ArrayPoolBufferWriter.RentThreadStaticWriter())
                {
                    var writer = new MessagePackWriter(buffer);
                    writer.WriteArrayHeader(3);
                    writer.Write(mid);
                    writer.Write(methodId);
                    MessagePackSerializer.Serialize(ref writer, message, _serializerOptions);
                    writer.Flush();
                    return buffer.WrittenSpan.ToArray();
                }
            }

            var v = BuildMessage();
            using (await _asyncLock.LockAsync().ConfigureAwait(false))
            {
                await _connection.RawStreamingCall.RequestStream.WriteAsync(v).ConfigureAwait(false);
            }

            return await tcs.Task.ConfigureAwait(false); // wait until server return response(or error). if connection was closed, throws cancellation from DisposeAsyncCore.
        }

        void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("StreamingHubClient", $"The StreamingHub has already been disconnected from the server.");
            }
        }

        public Task WaitForDisconnect()
        {
            return _waitForDisconnect.Task;
        }

        public Task DisposeAsync()
        {
            return DisposeAsyncCore(true);
        }

        private async Task DisposeAsyncCore(bool waitSubscription)
        {
            if (_disposed)
                return;
            if (_connection.RawStreamingCall is null)
                return;

            _disposed = true;

            try
            {
                await _connection.RequestStream.CompleteAsync().ConfigureAwait(false);
            }
            catch { } // ignore error?
            finally
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                try
                {
                    if (waitSubscription)
                    {
                        if (_subscription != null)
                        {
                            await _subscription.ConfigureAwait(false);
                        }
                    }

                    // cleanup completion
                    List<Exception> aggregateException = null;
                    foreach (var item in _responseFutures)
                    {
                        try
                        {
                            (item.Value as ITaskCompletion).TrySetCanceled();
                        }
                        catch (Exception ex)
                        {
                            if (!(ex is OperationCanceledException))
                            {
                                if (aggregateException != null)
                                {
                                    aggregateException = new List<Exception>();
                                    aggregateException.Add(ex);
                                }
                            }
                        }
                    }
                    if (aggregateException != null)
                    {
                        throw new AggregateException(aggregateException);
                    }
                }
                catch (Exception ex)
                {
                    if (!(ex is OperationCanceledException))
                    {
                        throw;
                    }
                }
            }
        }
    }
}