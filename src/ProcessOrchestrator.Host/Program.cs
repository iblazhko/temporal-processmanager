using ProcessOrchestrator.Host;
using ProcessOrchestrator.Temporal;
using SharedInfrastructure;
using SharedInfrastructure.Configuration;
using Temporalio.Extensions.Hosting;

var settings = SettingsResolver.GetSettings();
Console.WriteLine(settings.ToString());

if (!InfrastructureWaitPolicy.WaitForInfrastructure(settings))
{
    throw new InvalidOperationException("Infrastructure service(s) not available");
}

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddHostedTemporalWorker(
        clientTargetHost: settings.Temporal.ServerAddress,
        clientNamespace: settings.Temporal.Namespace,
        taskQueue: ProcessOrchestratorTaskQueue.TaskQueue
    )
    .AddScopedActivities<ShipmentProcessOrchestrationActivities>()
    .AddWorkflow<ShipmentProcessWorkflow>()
    .AddWorkflow<DomesticShipmentProcessWorkflow>()
    .AddWorkflow<InternationalShipmentProcessWorkflow>()
    .AddWorkflow<InternationalShipmentWithPaperlessTradeProcessWorkflow>();
builder.Services.AddTemporalClient(
    clientTargetHost: settings.Temporal.ServerAddress,
    clientNamespace: settings.Temporal.Namespace
);

builder.WebHost.UseUrls(settings.ApiBaseUrl);

var app = builder.Build();
app.AddApiEndpoints();

app.Run();
