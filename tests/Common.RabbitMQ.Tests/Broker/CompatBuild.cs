namespace Common.RabbitMQ.Tests.Broker
{
    public enum CompatBuild
    {
        /// <summary>src/Common.RabbitMQ: the build being refactored.</summary>
        Current,

        /// <summary>tests/Fixtures/Common.RabbitMQ.Baseline: the build the legacy apps run today.</summary>
        Baseline,
    }
}
