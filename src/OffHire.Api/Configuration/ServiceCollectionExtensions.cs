using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using OffHire.Application.Abstractions;
using OffHire.Application.Commands;
using OffHire.Domain.Services;
using OffHire.Infrastructure.Cosmos;
using OffHire.Infrastructure.Dynamics;
using OffHire.Infrastructure.Messaging;

namespace OffHire.Api.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOffHireServices(this IServiceCollection services, CosmosClient cosmosClient, string databaseId, string containerId)
    {
        services.AddScoped<Container>(_ => cosmosClient.GetContainer(databaseId, containerId));
        services.AddScoped<IOffHireOrderRepository, CosmosOffHireOrderRepository>();
        services.AddScoped<IDynamicsClient, DynamicsClientStub>();
        services.AddScoped<IDynamicsService, DynamicsService>();
        services.AddScoped<IOffHireOrderPublisher, ServiceBusOffHireOrderPublisher>();
        services.AddScoped<IServiceBusTopicClient, ServiceBusTopicClientStub>();
        services.AddScoped<CoreSubscriptionHandler>();
        services.AddScoped<DynamicsSubscriptionHandler>();
        services.AddScoped<IDynamicsResultQueue, DynamicsResultQueueStub>();
        services.AddScoped<DynamicsResultProcessor>();
        services.AddScoped<IOffHirePlanner, OffHirePlanner>();
        services.AddScoped<UpsertOffHireOrderCommandHandler>();
        services.AddScoped<ProcessReconOffHireCommandHandler>();
        return services;
    }

    private sealed class DynamicsClientStub : IDynamicsClient
    {
        public Task SendAsync(IntegrationCommand command, CancellationToken cancellationToken)
        {
            // TODO: replace with actual Dynamics integration implementation.
            return Task.CompletedTask;
        }
    }
}
