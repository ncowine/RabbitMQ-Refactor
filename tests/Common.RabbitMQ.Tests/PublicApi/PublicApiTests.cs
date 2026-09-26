extern alias baseline;

using System.Collections.Generic;
using System.Linq;
using Xunit;
using BaselineService = baseline::Common.RabbitMQ.RabbitMQService;

namespace Common.RabbitMQ.Tests.PublicApi
{
    /// <summary>
    /// The public surface of Common.RabbitMQ must match the baseline build exactly (ADR 0001, section 1): legacy apps
    /// compile and bind against it. An extra public constructor also counts as a break, because DryIoc rejects types
    /// with more than one.
    /// </summary>
    public class PublicApiTests
    {
        [Fact]
        public void MatchesBaseline()
        {
            HashSet<string> expected = PublicApiReader.Read(typeof(BaselineService).Assembly);
            HashSet<string> actual = PublicApiReader.Read(typeof(RabbitMQService).Assembly);

            List<string> removed = expected.Except(actual).OrderBy(s => s).ToList();
            List<string> added = actual.Except(expected).OrderBy(s => s).ToList();

            Assert.True(
                removed.Count == 0 && added.Count == 0,
                "Public API changed.\nRemoved:\n  " + string.Join("\n  ", removed) + "\nAdded:\n  " + string.Join("\n  ", added));
        }

        [Fact]
        public void IsNotEmpty()
        {
            // Guards the reader itself: an empty result would make MatchesBaseline pass for anything.
            HashSet<string> api = PublicApiReader.Read(typeof(RabbitMQService).Assembly);

            Assert.Contains("ctor Common.RabbitMQ.RabbitMQService(Prism.Events.IEventAggregator, Common.RabbitMQ.RemoteEventRegistry)", api);
            Assert.Contains("event Common.RabbitMQ.RabbitMQService.Log : System.EventHandler<System.String>", api);
        }
    }
}
