using System;
using System.Collections.Generic;
using System.Linq;
using Messaging.RabbitMQ;

namespace Messaging.Hosting
{
    /// <summary>
    /// What <see cref="MessagingBuilder"/> configured: the buses, where each message type is published (routes) and
    /// which handlers receive from which buses. Routing belongs to the host, not the contract (ADR 0001, section 1).
    /// </summary>
    internal sealed class MessagingRegistry
    {
        private readonly Dictionary<string, BusRegistration> buses = new Dictionary<string, BusRegistration>(StringComparer.Ordinal);
        private readonly Dictionary<Type, string[]> routes = new Dictionary<Type, string[]>();
        private readonly List<HandlerRegistration> handlers = new List<HandlerRegistration>();

        public IReadOnlyCollection<BusRegistration> Buses => buses.Values;

        public void AddBus(BusRegistration bus)
        {
            if (buses.ContainsKey(bus.Name))
            {
                throw new InvalidOperationException($"Bus '{bus.Name}' is already added.");
            }

            buses.Add(bus.Name, bus);
        }

        public BusRegistration GetBus(string name)
        {
            return buses[name];
        }

        public void AddRoute(Type messageType, string[] busNames)
        {
            string[] existing = routes.TryGetValue(messageType, out string[] current) ? current : Array.Empty<string>();
            routes[messageType] = existing.Concat(busNames).Distinct(StringComparer.Ordinal).ToArray();
        }

        /// <returns>The buses <paramref name="messageType"/> is published to, or null when it has no route.</returns>
        public IReadOnlyList<string> GetRoute(Type messageType)
        {
            return routes.TryGetValue(messageType, out string[] busNames) ? busNames : null;
        }

        public void AddHandler(HandlerRegistration handler)
        {
            handlers.Add(handler);
        }

        public IEnumerable<HandlerRegistration> GetHandlers(Type messageType, string busName)
        {
            return handlers.Where(h => h.MessageType == messageType && h.Buses.Contains(busName, StringComparer.Ordinal));
        }

        /// <summary>The wire names a bus binds: every message type with a handler on that bus.</summary>
        public IEnumerable<MessageRegistration> GetSubscriptions(string busName)
        {
            return handlers
                .Where(h => h.Buses.Contains(busName, StringComparer.Ordinal))
                .Select(h => h.MessageType)
                .Distinct()
                .Select(t => new MessageRegistration(WireNames.Get(t), t));
        }

        /// <summary>Fails fast at startup instead of on the first message.</summary>
        public void Validate()
        {
            List<string> errors = new List<string>();

            foreach (KeyValuePair<Type, string[]> route in routes)
            {
                errors.AddRange(UnknownBuses(route.Value).Select(b => $"{route.Key.Name} is routed to unknown bus '{b}'."));
                errors.AddRange(MissingWireName(route.Key));
            }

            foreach (HandlerRegistration handler in handlers)
            {
                errors.AddRange(UnknownBuses(handler.Buses).Select(b => $"{handler.HandlerType.Name} receives from unknown bus '{b}'."));
                errors.AddRange(MissingWireName(handler.MessageType));
            }

            foreach (BusRegistration bus in buses.Values)
            {
                // Two types with one wire name on the same bus would make incoming messages ambiguous.
                IEnumerable<string> duplicates = GetSubscriptions(bus.Name)
                    .GroupBy(r => r.WireName)
                    .Where(g => g.Count() > 1)
                    .Select(g => $"Bus '{bus.Name}' has several message types named '{g.Key}': {string.Join(", ", g.Select(r => r.MessageType.Name))}.");
                errors.AddRange(duplicates);
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException("Messaging is misconfigured:\n" + string.Join("\n", errors.Distinct()));
            }
        }

        private IEnumerable<string> UnknownBuses(IEnumerable<string> busNames)
        {
            return busNames.Where(b => !buses.ContainsKey(b));
        }

        private static IEnumerable<string> MissingWireName(Type messageType)
        {
            try
            {
                WireNames.Get(messageType);
                return Enumerable.Empty<string>();
            }
            catch (InvalidOperationException ex)
            {
                return new[] { ex.Message };
            }
        }
    }
}
