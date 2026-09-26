using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Common.RabbitMQ.Tests.Broker
{
    /// <summary>A raw consumer bound to every routing key on the test exchange: sees exactly what a build puts on the wire.</summary>
    public sealed class WireCapture
    {
        private readonly ConcurrentQueue<CapturedMessage> messages = new ConcurrentQueue<CapturedMessage>();
        private readonly SemaphoreSlim available = new SemaphoreSlim(0);

        private WireCapture()
        {
        }

        public static async Task<WireCapture> Start(IChannel channel, string exchangeName)
        {
            WireCapture capture = new WireCapture();

            QueueDeclareOk queue = await channel.QueueDeclareAsync(queue: "", durable: false, exclusive: true, autoDelete: true).ConfigureAwait(false);
            await channel.QueueBindAsync(queue.QueueName, exchangeName, "#").ConfigureAwait(false);

            AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += capture.OnReceived;
            await channel.BasicConsumeAsync(queue.QueueName, autoAck: true, consumer: consumer).ConfigureAwait(false);

            return capture;
        }

        /// <returns>The next message, or null when none arrives within <paramref name="timeout"/>.</returns>
        public async Task<CapturedMessage> Next(TimeSpan timeout)
        {
            if (!await available.WaitAsync(timeout).ConfigureAwait(false))
            {
                return null;
            }

            messages.TryDequeue(out CapturedMessage message);
            return message;
        }

        private Task OnReceived(object sender, BasicDeliverEventArgs args)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.Ordinal);
            if (args.BasicProperties.Headers != null)
            {
                foreach (KeyValuePair<string, object> header in args.BasicProperties.Headers)
                {
                    headers[header.Key] = header.Value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : header.Value?.ToString();
                }
            }

            messages.Enqueue(new CapturedMessage
            {
                Exchange = args.Exchange,
                RoutingKey = args.RoutingKey,
                ContentType = args.BasicProperties.ContentType,
                MessageId = args.BasicProperties.MessageId,
                AppId = args.BasicProperties.AppId,
                Headers = headers,

                // The body buffer is only valid during this callback.
                Body = args.Body.ToArray(),
            });
            available.Release();

            return Task.CompletedTask;
        }
    }
}
