using CarrierIntegration.Temporal;
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
        taskQueue: CarrierIntegrationTaskQueue.TaskQueue
    )
    .AddScopedActivities<CarrierIntegrationActivities>()
    .AddWorkflow<ShipmentLegCarrierManifestationWorkflow>()
    .AddWorkflow<ShipmentLegCarrierCollectionBookingWorkflow>();

builder.WebHost.UseUrls(settings.ApiBaseUrl);

var app = builder.Build();
app.MapGet("/", () => "Hello from carrier integration!");
app.Run();
