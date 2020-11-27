using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EventStore.Client;

namespace AckTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var connectionString = "esdb://localhost:2113?Tls=false";

            var settings = EventStoreClientSettings
                .Create(connectionString);

            var subscriptionSettings = new PersistentSubscriptionSettings(
                    messageTimeout: TimeSpan.FromSeconds(30),
                    namedConsumerStrategy: SystemConsumerStrategies.Pinned,
                    resolveLinkTos: true,
                    startFrom: StreamPosition.Start);

            var eventStoreStreamName = "all";
            var eventStoreGroupName = "EventStoreSubscriptionDrop";

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

            subscriptionsClient.SubscribeAsync(
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
                }).Wait();

            Task.Run(() =>
            {
                while (true)
                {
                    Task.Delay(1000).Wait();
                }
            }).Wait();
        }
    }
}
