using System.Configuration;

namespace Common.RabbitMQ.Configuration
{
    public class BusElement : ConfigurationElement
    {
        [ConfigurationProperty("name", IsRequired = true, IsKey = true)]
        public string Name => (string)this["name"];

        [ConfigurationProperty("hostName", DefaultValue = "localhost")]
        public string HostName => (string)this["hostName"];

        [ConfigurationProperty("port", DefaultValue = 5672)]
        public int Port => (int)this["port"];

        [ConfigurationProperty("virtualHost", DefaultValue = "/")]
        public string VirtualHost => (string)this["virtualHost"];

        [ConfigurationProperty("userName", DefaultValue = "guest")]
        public string UserName => (string)this["userName"];

        [ConfigurationProperty("password", DefaultValue = "guest")]
        public string Password => (string)this["password"];

        [ConfigurationProperty("exchangeName", IsRequired = true)]
        public string ExchangeName => (string)this["exchangeName"];

        [ConfigurationProperty("clientName", DefaultValue = "")]
        public string ClientName => (string)this["clientName"];

        [ConfigurationProperty("reconnectDelaySeconds", DefaultValue = 5)]
        public int ReconnectDelaySeconds => (int)this["reconnectDelaySeconds"];

        [ConfigurationProperty("outstandingPollIntervalMilliseconds", DefaultValue = 100)]
        public int OutstandingPollIntervalMilliseconds => (int)this["outstandingPollIntervalMilliseconds"];

        [ConfigurationProperty("prefetchCount", DefaultValue = 50)]
        public int PrefetchCount => (int)this["prefetchCount"];

        [ConfigurationProperty("heartbeatSeconds", DefaultValue = 30)]
        public int HeartbeatSeconds => (int)this["heartbeatSeconds"];

        public RabbitMQConfig ToRabbitMQConfig()
        {
            return new RabbitMQConfig
            {
                BusName = Name,
                HostName = HostName,
                Port = Port,
                VirtualHost = VirtualHost,
                UserName = UserName,
                Password = Password,
                ExchangeName = ExchangeName,
                ClientName = ClientName,
                ReconnectDelaySeconds = ReconnectDelaySeconds,
                OutstandingPollIntervalMilliseconds = OutstandingPollIntervalMilliseconds,
                PrefetchCount = PrefetchCount,
                HeartbeatSeconds = HeartbeatSeconds,
            };
        }
    }
}
