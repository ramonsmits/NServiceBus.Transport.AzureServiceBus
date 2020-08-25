namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Management;

    class NamespacePermissions
    {
        readonly ServiceBusConnectionStringBuilder connectionStringBuilder;
        readonly TokenCredential tokenCredential;

        public NamespacePermissions(ServiceBusConnectionStringBuilder connectionStringBuilder, TokenCredential tokenCredential)
        {
            this.connectionStringBuilder = connectionStringBuilder;
            this.tokenCredential = tokenCredential;
        }

        public async Task<StartupCheckResult> CanManage()
        {
            var client = new ServiceBusManagementClient(/*connectionStringBuilder*/"connectionstring or FQDN", tokenCredential);

            try
            {
                await client.QueueExistsAsync("$nservicebus-verification-queue").ConfigureAwait(false);
            }
            catch (ServiceBusException ex) when(ex.Reason == ServiceBusFailureReason.Unauthorized)
            {
                return StartupCheckResult.Failed("Management rights are required to run this endpoint. Verify that the SAS policy has the Manage claim.");
            }
            catch (Exception exception)
            {
                return StartupCheckResult.Failed(exception.Message);
            }

            return StartupCheckResult.Success;
        }
        public Task<StartupCheckResult> CanSend()
        {
            // TODO: blocked by https://github.com/Azure/azure-service-bus-dotnet/issues/520
            return Task.FromResult(StartupCheckResult.Success);
        }

        public Task<StartupCheckResult> CanReceive()
        {
            // TODO: blocked by https://github.com/Azure/azure-service-bus-dotnet/issues/520
            return Task.FromResult(StartupCheckResult.Success);
        }
    }
}