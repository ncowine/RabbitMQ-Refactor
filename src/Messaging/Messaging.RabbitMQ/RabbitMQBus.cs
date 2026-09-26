using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Messaging.RabbitMQ
{
    /// <summary>
    /// One connection to one bus (virtual host + exchange).
    /// <para>
    /// <see cref="Start"/> starts three fire-and-forget loops:
    /// <list type="bullet">
    /// <item><see cref="ConnectConsumer"/> - connects, creates this instance's queue, binds the subscribed wire names and
    /// then keeps checking for a dropped connection, reconnecting when needed.</item>
    /// <item><see cref="ConnectPublisher"/> - same, for the publishing connection.</item>
    /// <item><see cref="OutstandingQueue"/> - sends buffered messages whenever the publisher is connected.</item>
    /// </list>
    /// </para>
    /// Every instance has its own exclusive queue, so every instance receives every message; its own messages are
    /// dropped using the <see cref="WireHeaders.SourceId"/> header. Received messages go to the <see cref="IInboundDispatcher"/>.
    /// <para>
    /// The optional <see cref="IMessagingObserver"/> sees every stage and can add headers to outgoing messages.
    /// Without one, nothing beyond the frozen wire format is sent.
    /// </para>
    /// </summary>
    public sealed class RabbitMQBus : IDisposable
    {
        private readonly RabbitMQBusOptions options;
        private readonly IMessageSerializer serializer;
        private readonly IInboundDispatcher dispatcher;
        private readonly IMessagingObserver observer;
        private readonly string instanceId;
        private readonly ConcurrentQueue<PendingMessage> outstandingMessages = new ConcurrentQueue<PendingMessage>();
        private readonly ConcurrentDictionary<string, MessageRegistration> subscriptions =
            new ConcurrentDictionary<string, MessageRegistration>(StringComparer.Ordinal);
        private readonly SemaphoreSlim consumerLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        private IConnection consumerConnection;
        private IChannel consumerChannel;
        private string consumerQueueName;
        private IConnection publisherConnection;
        private IChannel publisherChannel;
        private bool started;
        private bool disposed;

        /// <param name="observer">Optional. Null means no observer and no extra headers.</param>
        public RabbitMQBus(RabbitMQBusOptions options, IMessageSerializer serializer, IInboundDispatcher dispatcher, IMessagingObserver observer = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.BusName) || string.IsNullOrWhiteSpace(options.ExchangeName) || string.IsNullOrWhiteSpace(options.ClientName))
            {
                throw new ArgumentException("BusName, ExchangeName and ClientName are required.", nameof(options));
            }

            this.options = options;
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.observer = observer ?? NullMessagingObserver.Instance;
            instanceId = string.IsNullOrWhiteSpace(options.InstanceId) ? Guid.NewGuid().ToString("N") : options.InstanceId;
        }

        /// <summary>Connection and error messages. The sender is this bus.</summary>
        public event EventHandler<string> Log;

        public string BusName => options.BusName;

        public string InstanceId => instanceId;

        public bool IsConsumerConnected => IsOpen(consumerConnection, consumerChannel);

        public bool IsPublisherConnected => IsOpen(publisherConnection, publisherChannel);

        public int OutstandingCount => outstandingMessages.Count;

        /// <summary>Subscribes to <paramref name="registrations"/> and starts connecting in the background.</summary>
        public void Start(IEnumerable<MessageRegistration> registrations)
        {
            EnsureNotDisposed();

            if (started)
            {
                throw new InvalidOperationException($"Bus '{options.BusName}' is already started.");
            }

            started = true;

            foreach (MessageRegistration registration in registrations ?? Array.Empty<MessageRegistration>())
            {
                subscriptions[registration.WireName] = registration;
            }

            CancellationToken token = cancellation.Token;

            Task.Run(() => ConnectConsumer(token));
            Task.Run(() => ConnectPublisher(token));
            Task.Run(() => OutstandingQueue(token));
        }

        /// <summary>
        /// Serializes <paramref name="message"/> now and buffers it for publishing as <paramref name="wireName"/>.
        /// Returns immediately; the message is sent once a publisher connection is available.
        /// <see cref="IMessagingObserver.OnPublishing"/> runs here, on the caller's thread.
        /// </summary>
        public void Enqueue(string wireName, object message)
        {
            EnsureNotDisposed();

            if (string.IsNullOrWhiteSpace(wireName))
            {
                throw new ArgumentException("A wire name is required.", nameof(wireName));
            }

            byte[] body = serializer.Serialize(message);

            PublishContext context = new PublishContext(wireName, options.BusName, Guid.NewGuid().ToString("N"), message?.GetType());
            Observe(o => o.OnPublishing(context), nameof(IMessagingObserver.OnPublishing));

            outstandingMessages.Enqueue(new PendingMessage(context, body));
        }

        /// <summary>Starts receiving <paramref name="registration"/>. Does nothing when already subscribed.</summary>
        public async Task Subscribe(MessageRegistration registration)
        {
            EnsureNotDisposed();

            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            if (!subscriptions.TryAdd(registration.WireName, registration))
            {
                return;
            }

            // If not connected the binding is created on the next connect.
            await BindOrUnbind(registration.WireName, bind: true, cancellation.Token).ConfigureAwait(false);
        }

        /// <summary>Stops receiving <paramref name="wireName"/>. Publishing is not affected.</summary>
        public async Task Unsubscribe(string wireName)
        {
            EnsureNotDisposed();

            if (wireName == null || !subscriptions.TryRemove(wireName, out MessageRegistration removed))
            {
                return;
            }

            await BindOrUnbind(removed.WireName, bind: false, cancellation.Token).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            if (!started)
            {
                cancellation.Dispose();
                return;
            }

            cancellation.Cancel();

            if (!outstandingMessages.IsEmpty)
            {
                WriteLog($"Shutting down with {outstandingMessages.Count} unsent message(s).");
            }

            try
            {
                // Run on the thread pool so a UI thread calling Dispose can't deadlock.
                Task.Run(async () =>
                {
                    await CloseConsumer().ConfigureAwait(false);
                    await ClosePublisher().ConfigureAwait(false);
                }).Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                WriteLog($"Error while closing connections: {ex.Message}");
            }

            cancellation.Dispose();
        }

        #region Consumer

        private async Task ConnectConsumer(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!IsOpen(consumerConnection, consumerChannel))
                    {
                        await CloseConsumer().ConfigureAwait(false);
                        await OpenConsumer(token).ConfigureAwait(false);
                        WriteLog($"Consumer connected, queue '{consumerQueueName}'.");
                        ObserveConnection(ConnectionRole.Consumer, ConnectionStatus.Connected, null, null);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    WriteLog($"Consumer connection failed: {ex.Message}");
                    ObserveConnection(ConnectionRole.Consumer, ConnectionStatus.Failed, null, ex);
                }

                if (!await Delay(options.ReconnectDelay, token).ConfigureAwait(false))
                {
                    break;
                }
            }
        }

        private async Task OpenConsumer(CancellationToken token)
        {
            await consumerLock.WaitAsync(token).ConfigureAwait(false);

            IConnection connection = null;
            IChannel channel = null;
            try
            {
                connection = await CreateConnectionFactory()
                    .CreateConnectionAsync($"{options.ClientName} [{options.BusName}] consumer", token)
                    .ConfigureAwait(false);
                connection.ConnectionShutdownAsync += OnConsumerConnectionShutdown;

                channel = await connection.CreateChannelAsync(cancellationToken: token).ConfigureAwait(false);
                await DeclareExchange(channel, token).ConfigureAwait(false);
                await channel.BasicQosAsync(0, options.PrefetchCount, false, token).ConfigureAwait(false);

                // One queue per running instance: every instance gets every message, and the queue goes away with it.
                QueueDeclareOk queue = await channel.QueueDeclareAsync(
                    queue: $"{options.ClientName}.{options.BusName}.{instanceId}".ToLowerInvariant(),
                    durable: false,
                    exclusive: true,
                    autoDelete: true,
                    cancellationToken: token).ConfigureAwait(false);

                foreach (MessageRegistration registration in subscriptions.Values)
                {
                    await channel.QueueBindAsync(
                        queue: queue.QueueName,
                        exchange: options.ExchangeName,
                        routingKey: registration.WireName,
                        cancellationToken: token).ConfigureAwait(false);
                }

                AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += OnMessageReceived;
                await channel.BasicConsumeAsync(
                    queue: queue.QueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: token).ConfigureAwait(false);

                consumerConnection = connection;
                consumerChannel = channel;
                consumerQueueName = queue.QueueName;
            }
            catch
            {
                await CloseQuietly(channel, connection).ConfigureAwait(false);
                throw;
            }
            finally
            {
                consumerLock.Release();
            }
        }

        private async Task CloseConsumer()
        {
            IConnection connection;
            IChannel channel;

            await consumerLock.WaitAsync().ConfigureAwait(false);
            try
            {
                connection = consumerConnection;
                channel = consumerChannel;
                consumerConnection = null;
                consumerChannel = null;
                consumerQueueName = null;
            }
            finally
            {
                consumerLock.Release();
            }

            if (connection != null)
            {
                connection.ConnectionShutdownAsync -= OnConsumerConnectionShutdown;
            }

            await CloseQuietly(channel, connection).ConfigureAwait(false);
        }

        private async Task BindOrUnbind(string wireName, bool bind, CancellationToken token)
        {
            await consumerLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                IChannel channel = consumerChannel;
                string queueName = consumerQueueName;
                if (channel == null || !channel.IsOpen || queueName == null)
                {
                    return;
                }

                if (bind)
                {
                    await channel.QueueBindAsync(queueName, options.ExchangeName, wireName, cancellationToken: token).ConfigureAwait(false);
                }
                else
                {
                    await channel.QueueUnbindAsync(queueName, options.ExchangeName, wireName, cancellationToken: token).ConfigureAwait(false);
                }
            }
            finally
            {
                consumerLock.Release();
            }
        }

        private async Task OnMessageReceived(object sender, BasicDeliverEventArgs args)
        {
            IChannel channel = ((AsyncEventingBasicConsumer)sender).Channel;
            MessageContext context = null;
            Stopwatch stopwatch = null;

            try
            {
                Dictionary<string, string> headers = ReadHeaders(args.BasicProperties?.Headers);

                // Our own message coming back - the sender's local side has already seen it.
                if (headers.TryGetValue(WireHeaders.SourceId, out string sourceId) && sourceId == instanceId)
                {
                    return;
                }

                string wireName = headers.TryGetValue(WireHeaders.MessageType, out string messageType) && messageType != null
                    ? messageType
                    : args.RoutingKey;
                if (!subscriptions.TryGetValue(wireName, out MessageRegistration registration))
                {
                    WriteLog($"Ignored unknown event '{wireName}'.");
                    return;
                }

                headers.TryGetValue(WireHeaders.CorrelationId, out string correlationId);
                context = new MessageContext(wireName, options.BusName, args.BasicProperties?.MessageId, correlationId, args.Redelivered, headers);
                Observe(o => o.OnReceived(context), nameof(IMessagingObserver.OnReceived));
                stopwatch = Stopwatch.StartNew();

                object message = serializer.Deserialize(args.Body.ToArray(), registration.MessageType);
                await dispatcher.Dispatch(registration, message, context, args.CancellationToken).ConfigureAwait(false);

                Observe(o => o.OnHandled(context, stopwatch.Elapsed), nameof(IMessagingObserver.OnHandled));
            }
            catch (Exception ex)
            {
                // Acked anyway so a bad message can't be redelivered forever.
                WriteLog($"Failed to handle message '{args.RoutingKey}': {ex.Message}");

                if (context != null)
                {
                    TimeSpan duration = stopwatch?.Elapsed ?? TimeSpan.Zero;
                    Observe(o => o.OnHandlingFailed(context, duration, ex), nameof(IMessagingObserver.OnHandlingFailed));
                }
            }
            finally
            {
                try
                {
                    if (channel.IsOpen)
                    {
                        await channel.BasicAckAsync(args.DeliveryTag, false).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"Failed to ack message: {ex.Message}");
                }
            }
        }

        private Task OnConsumerConnectionShutdown(object sender, ShutdownEventArgs args)
        {
            WriteLog($"Consumer connection lost: {args.ReplyText}");
            ObserveConnection(ConnectionRole.Consumer, ConnectionStatus.Lost, args.ReplyText, null);
            return Task.CompletedTask;
        }

        #endregion

        #region Publisher

        private async Task ConnectPublisher(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!IsOpen(publisherConnection, publisherChannel))
                    {
                        await ClosePublisher().ConfigureAwait(false);
                        await OpenPublisher(token).ConfigureAwait(false);
                        WriteLog("Publisher connected.");
                        ObserveConnection(ConnectionRole.Publisher, ConnectionStatus.Connected, null, null);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    WriteLog($"Publisher connection failed: {ex.Message}");
                    ObserveConnection(ConnectionRole.Publisher, ConnectionStatus.Failed, null, ex);
                }

                if (!await Delay(options.ReconnectDelay, token).ConfigureAwait(false))
                {
                    break;
                }
            }
        }

        private async Task OpenPublisher(CancellationToken token)
        {
            IConnection connection = null;
            IChannel channel = null;
            try
            {
                connection = await CreateConnectionFactory()
                    .CreateConnectionAsync($"{options.ClientName} [{options.BusName}] publisher", token)
                    .ConfigureAwait(false);
                connection.ConnectionShutdownAsync += OnPublisherConnectionShutdown;

                // With confirmation tracking BasicPublishAsync only completes once the broker has the message.
                CreateChannelOptions channelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);
                channel = await connection.CreateChannelAsync(channelOptions, token).ConfigureAwait(false);
                await DeclareExchange(channel, token).ConfigureAwait(false);

                publisherConnection = connection;
                publisherChannel = channel;
            }
            catch
            {
                await CloseQuietly(channel, connection).ConfigureAwait(false);
                throw;
            }
        }

        private async Task ClosePublisher()
        {
            IConnection connection = publisherConnection;
            IChannel channel = publisherChannel;
            publisherConnection = null;
            publisherChannel = null;

            if (connection != null)
            {
                connection.ConnectionShutdownAsync -= OnPublisherConnectionShutdown;
            }

            await CloseQuietly(channel, connection).ConfigureAwait(false);
        }

        private Task OnPublisherConnectionShutdown(object sender, ShutdownEventArgs args)
        {
            WriteLog($"Publisher connection lost: {args.ReplyText}");
            ObserveConnection(ConnectionRole.Publisher, ConnectionStatus.Lost, args.ReplyText, null);
            return Task.CompletedTask;
        }

        #endregion

        #region Outstanding queue

        private async Task OutstandingQueue(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                PendingMessage message = null;
                try
                {
                    IChannel channel = publisherChannel;
                    if (channel != null && channel.IsOpen && outstandingMessages.TryPeek(out message))
                    {
                        await PublishMessage(channel, message, token).ConfigureAwait(false);

                        // Only removed once the broker confirmed it; on failure it is retried.
                        outstandingMessages.TryDequeue(out PendingMessage published);
                        Observe(o => o.OnPublished(published.Context), nameof(IMessagingObserver.OnPublished));
                        continue;
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    WriteLog($"Publish failed, message kept in outstanding queue: {ex.Message}");

                    if (message != null)
                    {
                        Observe(o => o.OnPublishFailed(message.Context, ex), nameof(IMessagingObserver.OnPublishFailed));
                    }
                }

                if (!await Delay(options.OutstandingPollInterval, token).ConfigureAwait(false))
                {
                    break;
                }
            }
        }

        private async Task PublishMessage(IChannel channel, PendingMessage message, CancellationToken token)
        {
            PublishContext context = message.Context;

            // Frozen headers first and always ours; the observer's headers are additive only.
            Dictionary<string, object> headers = new Dictionary<string, object>
            {
                [WireHeaders.MessageType] = context.WireName,
                [WireHeaders.SourceId] = instanceId,
            };

            foreach (KeyValuePair<string, string> header in context.Headers)
            {
                if (!WireHeaders.IsFrozen(header.Key) && header.Value != null)
                {
                    headers[header.Key] = header.Value;
                }
            }

            BasicProperties properties = new BasicProperties
            {
                ContentType = serializer.ContentType,
                MessageId = context.MessageId,
                AppId = options.ClientName,
                Timestamp = new AmqpTimestamp(message.CreatedUtc.ToUnixTimeSeconds()),
                Headers = headers,
            };

            await channel.BasicPublishAsync(
                exchange: options.ExchangeName,
                routingKey: context.WireName,
                mandatory: false,
                basicProperties: properties,
                body: message.Body,
                cancellationToken: token).ConfigureAwait(false);
        }

        #endregion

        #region Helpers

        private ConnectionFactory CreateConnectionFactory()
        {
            return new ConnectionFactory
            {
                HostName = options.HostName,
                Port = options.Port,
                VirtualHost = options.VirtualHost,
                UserName = options.UserName,
                Password = options.Password,
                RequestedHeartbeat = options.Heartbeat,

                // Reconnecting is done by ConnectConsumer / ConnectPublisher.
                AutomaticRecoveryEnabled = false,
                TopologyRecoveryEnabled = false,
            };
        }

        private Task DeclareExchange(IChannel channel, CancellationToken token)
        {
            return channel.ExchangeDeclareAsync(
                exchange: options.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: token);
        }

        private void EnsureNotDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(RabbitMQBus));
            }
        }

        private void WriteLog(string message)
        {
            Log?.Invoke(this, message);
        }

        /// <summary>Calls the observer. Its exceptions are logged and never reach the transport.</summary>
        private void Observe(Action<IMessagingObserver> callback, string callbackName)
        {
            try
            {
                callback(observer);
            }
            catch (Exception ex)
            {
                WriteLog($"Observer failed in {callbackName}: {ex.Message}");
            }
        }

        private void ObserveConnection(ConnectionRole role, ConnectionStatus status, string reason, Exception error)
        {
            ConnectionStateChange change = new ConnectionStateChange(options.BusName, role, status, reason, error);
            Observe(o => o.OnConnectionChanged(change), nameof(IMessagingObserver.OnConnectionChanged));
        }

        private static bool IsOpen(IConnection connection, IChannel channel)
        {
            return connection != null && connection.IsOpen && channel != null && channel.IsOpen;
        }

        private static Dictionary<string, string> ReadHeaders(IDictionary<string, object> headers)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (headers == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, object> header in headers)
            {
                // String headers arrive as byte[] from the broker.
                result[header.Key] = header.Value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : header.Value?.ToString();
            }

            return result;
        }

        /// <returns>False when cancelled.</returns>
        private static async Task<bool> Delay(TimeSpan delay, CancellationToken token)
        {
            try
            {
                await Task.Delay(delay, token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private static async Task CloseQuietly(IChannel channel, IConnection connection)
        {
            try
            {
                if (channel != null)
                {
                    if (channel.IsOpen)
                    {
                        await channel.CloseAsync().ConfigureAwait(false);
                    }

                    channel.Dispose();
                }
            }
            catch
            {
                // Already broken; nothing to do.
            }

            try
            {
                if (connection != null)
                {
                    if (connection.IsOpen)
                    {
                        await connection.CloseAsync().ConfigureAwait(false);
                    }

                    connection.Dispose();
                }
            }
            catch
            {
                // Already broken; nothing to do.
            }
        }

        #endregion
    }
}
