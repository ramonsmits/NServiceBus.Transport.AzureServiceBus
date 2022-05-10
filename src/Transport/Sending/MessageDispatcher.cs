namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Transactions;
    using Azure.Messaging.ServiceBus;
    using Extensibility;

    class MessageDispatcher : IDispatchMessages
    {
        readonly MessageSenderPool messageSenderPool;
        readonly string topicName;

        public MessageDispatcher(MessageSenderPool messageSenderPool, string topicName)
        {
            this.messageSenderPool = messageSenderPool;
            this.topicName = topicName;
        }

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            // Assumption: we're not implementing batching as it will be done by ASB client
            transaction.TryGet<ServiceBusClient>(out var serviceBusClient);
            transaction.TryGet<string>("IncomingQueue.PartitionKey", out var partitionKey);
            transaction.TryGet<Lazy<CommittableTransaction>>(out var committableTransaction);

            var unicastTransportOperations = outgoingMessages.UnicastTransportOperations;
            var multicastTransportOperations = outgoingMessages.MulticastTransportOperations;

            var tasks = new List<Task>(unicastTransportOperations.Count + multicastTransportOperations.Count);

            async Task Send(IOutgoingTransportOperation oto, string destination)
            {
                var message = oto.Message.ToAzureServiceBusMessage(oto.DeliveryConstraints, partitionKey);
                var sender = messageSenderPool.GetMessageSender(destination, serviceBusClient);

                try
                {
                    ApplyCustomizationToOutgoingNativeMessage(context, oto, message);
                    var transactionToUse = oto.RequiredDispatchConsistency == DispatchConsistency.Isolated ? null : committableTransaction.Value;
                    using (var scope = transactionToUse.ToScope())
                    {
                        // Invoke sender and immediately return it back to the pool w/o awaiting for completion
                        await sender.SendMessageAsync(message)
                            .ConfigureAwait(false);
                        //committable tx will not be committed because this scope is not the owner
                        scope.Complete();
                    }
                }
                finally
                {
                    messageSenderPool.ReturnMessageSender(sender, serviceBusClient);
                }
            }

            foreach (var transportOperation in unicastTransportOperations)
            {
                var destination = transportOperation.Destination;

                // Workaround for reply-to address set by ASB transport
                var index = transportOperation.Destination.IndexOf('@');

                if (index > 0)
                {
                    destination = destination.Substring(0, index);
                }

                tasks.Add(Send(transportOperation, destination));
            }

            foreach (var transportOperation in multicastTransportOperations)
            {
                tasks.Add(Send(transportOperation, topicName));
            }

            return tasks.Count == 1 ? tasks[0] : Task.WhenAll(tasks);
        }

        static void ApplyCustomizationToOutgoingNativeMessage(ReadOnlyContextBag context, IOutgoingTransportOperation transportOperation, ServiceBusMessage message)
        {
            if (transportOperation.Message.Headers.TryGetValue(CustomizeNativeMessageExtensions.CustomizationHeader, out var customizationId))
            {
                if (context.TryGet<NativeMessageCustomizer>(out var nmc) && nmc.Customizations.Keys.Contains(customizationId))
                {
                    nmc.Customizations[customizationId].Invoke(message);
                }

                transportOperation.Message.Headers.Remove(CustomizeNativeMessageExtensions.CustomizationHeader);
            }
        }
    }
}
