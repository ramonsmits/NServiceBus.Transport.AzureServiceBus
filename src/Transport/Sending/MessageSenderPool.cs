namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;

    class MessageSenderPool
    {
        public MessageSenderPool(ServiceBusClient client)
        {
            this.client = client;

            senders = new ConcurrentDictionary<(string, (ServiceBusConnection, string)), ConcurrentQueue<ServiceBusSender>>();
        }

        public ServiceBusSender GetMessageSender(string destination, (ServiceBusConnection connection, string path) receiverConnectionAndPath)
        {
            var sendersForDestination = senders.GetOrAdd((destination, receiverConnectionAndPath), _ => new ConcurrentQueue<ServiceBusSender>());

            if (!sendersForDestination.TryDequeue(out var sender) || sender.IsClosedOrClosing)
            {
                // Send-Via case
                if (receiverConnectionAndPath != (null, null))
                {
                    sender = client.CreateSender(destination, new ServiceBusSenderOptions { ViaQueueOrTopicName = receiverConnectionAndPath.path });
                }
                else
                {
                    // if (tokenProvider == null)
                    // {
                    sender = client.CreateSender(destination);
                    // }
                    // else
                    // {
                    //     sender = new MessageSender(connectionStringBuilder.Endpoint, destination, tokenProvider, connectionStringBuilder.TransportType, retryPolicy);
                    // }
                }
            }

            return sender;
        }

        public void ReturnMessageSender(ServiceBusSender sender)
        {
            if (sender.IsDisposed/*.IsClosedOrClosing*/)
            {
                return;
            }

            var connectionToUse = sender.OwnsConnection ? null : sender.ServiceBusConnection;
            var path = sender.OwnsConnection ? sender.Path : sender.TransferDestinationPath;

            if (senders.TryGetValue((path, (connectionToUse, sender.ViaEntityPath)), out var sendersForDestination))
            {
                sendersForDestination.Enqueue(sender);
            }
        }

        public Task Close()
        {
            var tasks = new List<Task>();

            foreach (var key in senders.Keys)
            {
                var queue = senders[key];

                foreach (var sender in queue)
                {
                    tasks.Add(sender.DisposeAsync().AsTask());
                }
            }

            return Task.WhenAll(tasks);
        }


        readonly ServiceBusClient client;

        ConcurrentDictionary<(string destination, (ServiceBusConnection connnection, string incomingQueue)), ConcurrentQueue<ServiceBusSender>> senders;
    }
}