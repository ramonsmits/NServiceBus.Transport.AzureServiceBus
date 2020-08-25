namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;

    static class MessageReceiverExtensions
    {
        public static Task SafeCompleteAsync(this ServiceBusReceiver messageReceiver, TransportTransactionMode transportTransactionMode, string lockToken)
        {
            if (transportTransactionMode != TransportTransactionMode.None)
            {
                return messageReceiver.CompleteMessageAsync(lockToken);
            }

            return Task.CompletedTask;
        }

        public static Task SafeAbandonAsync(this ServiceBusReceiver messageReceiver, TransportTransactionMode transportTransactionMode, string lockToken)
        {
            if (transportTransactionMode != TransportTransactionMode.None)
            {
                return messageReceiver.AbandonMessageAsync(lockToken);
            }

            return Task.CompletedTask;
        }

        public static Task SafeDeadLetterAsync(this ServiceBusReceiver messageReceiver, TransportTransactionMode transportTransactionMode, string lockToken, string deadLetterReason, string deadLetterErrorDescription)
        {
            if (transportTransactionMode != TransportTransactionMode.None)
            {
                return messageReceiver.DeadLetterMessageAsync(lockToken, deadLetterReason, deadLetterErrorDescription);
            }

            return Task.CompletedTask;
        }
    }
}