using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EventStore.Client;

namespace EventStoreSubscriptionDrop
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var connectionString = "esdb://localhost:2113?Tls=false";

            var settings = EventStoreClientSettings
                .Create(connectionString);
            
            // Write some events to a random stream
            var writeClient = new EventStoreClient(settings);

            var eventStoreStreamName = $"Test-{Uuid.NewUuid()}";
            const string eventStoreGroupName = "EventStoreSubscriptionDrop";

            for (var i = 0; i < 1000; i++)
                await writeClient.AppendToStreamAsync(
                    eventStoreStreamName,
                    StreamState.Any,
                    new []
                    {
                        new EventData(Uuid.NewUuid(), "DummyEvent", new byte[] {})
                    });

            // create a subscription to the stream
            var subscriptionSettings = new PersistentSubscriptionSettings(
                    messageTimeout: TimeSpan.FromSeconds(30),
                    namedConsumerStrategy: SystemConsumerStrategies.RoundRobin,
                    resolveLinkTos: true,
                    startFrom: StreamPosition.Start);

            var subscriptionsClient = new EventStorePersistentSubscriptionsClient(settings);

            try
            {
                if (!subscriptionsClient.CreateAsync(
                    eventStoreStreamName,
                    eventStoreGroupName,
                    subscriptionSettings).Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new Exception(
                        "Event Store subscription creation timed out.");
                }
            }
            catch (AggregateException e)
            {
                if (!(e.InnerException is InvalidOperationException oe))
                    throw;

                if (!Regex.IsMatch(oe.Message, "AlreadyExists"))
                    throw;
            }

            await subscriptionsClient.SubscribeAsync(
                eventStoreStreamName,
                eventStoreGroupName,
                (sub, @event, _, token) =>
                {
                    sub.Ack(@event);
                    return Task.CompletedTask;
                },
                (sub, reason, e) =>
                {
                    Console.WriteLine($"Sub dropped with {reason}. {e}");
                },
                autoAck: false);

            while (true)
            {
                await Task.Delay(1000);
            }
        }
    }
}
