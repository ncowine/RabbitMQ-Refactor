using System.Configuration;

namespace Common.RabbitMQ.Configuration
{
    /// <summary>
    /// App.config section:
    /// <code>
    /// &lt;rabbitMQ&gt;
    ///   &lt;bus name="Legacy" hostName="..." virtualHost="legacy" exchangeName="legacy.events" ... /&gt;
    /// &lt;/rabbitMQ&gt;
    /// </code>
    /// </summary>
    public class RabbitMQConfigSection : ConfigurationSection
    {
        [ConfigurationProperty("", IsDefaultCollection = true)]
        public BusElementCollection Buses => (BusElementCollection)base[""];
    }
}
