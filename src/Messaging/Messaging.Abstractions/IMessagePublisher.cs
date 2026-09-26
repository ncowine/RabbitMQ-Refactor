using System.Threading;
using System.Threading.Tasks;

namespace Messaging
{
    public interface IMessagePublisher
    {
        /// <summary>
        /// Sends <paramref name="message"/> to every bus its type is routed to. Completes once the message is
        /// serialized and buffered on each bus; delivery to the broker happens in the background.
        /// </summary>
        Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default);
    }
}
