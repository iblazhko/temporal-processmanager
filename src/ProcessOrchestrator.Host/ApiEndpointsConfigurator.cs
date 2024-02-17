namespace ProcessOrchestrator.Host;

using System;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using ProcessOrchestrator.Temporal;
using SharedKernel.DTOs;
using Temporalio.Client;

public static class ApiEndpointsConfigurator
{
    private const string Carrier1 = "c62bee763e7a4ce387dda5eb11678815";
    private const string Carrier2 = "9d6d28f2fbee4e53aeac2c3b3ba98d28";

    private static string GetStarterWorkflowId(string id) => $"{id}_starter";

    public static void AddApiEndpoints(this WebApplication app)
    {
        var temporalClient = app.Services.GetRequiredService<ITemporalClient>();

        app.MapGet("/", () => "Hello from shipment process orchestrator!");

        app.MapPost(
            "/{id}",
            async ([FromRoute] string id) =>
            {
                var request = id.StartsWith('1')
                    ? new ShipmentProcessRequest
                    {
                        ShipmentId = id,
                        Legs =
                        [
                            new ShipmentLeg
                            {
                                CarrierId = Carrier1,
                                Sender = "GB-sender1",
                                Receiver = "GB-receiver1",
                                Collection = "GB-collection1"
                            }
                        ],
                        CollectionDate = DateTime.UtcNow.Date.AddDays(1).ToIsoDate(),
                        TimeZone = "Europe/London"
                    }
                    : new ShipmentProcessRequest
                    {
                        ShipmentId = id,
                        Legs =
                        [
                            new ShipmentLeg
                            {
                                CarrierId = Carrier1,
                                Sender = "DE-sender1",
                                Receiver = "DE-receiver1",
                                Collection = "DE-collection1"
                            },
                            new ShipmentLeg
                            {
                                CarrierId = Carrier2,
                                Sender = "DE-sender2",
                                Receiver = "GB-receiver2",
                                Collection = "DE-collection2"
                            }
                        ],
                        CollectionDate = DateTime.UtcNow.Date.AddDays(1).ToIsoDate(),
                        TimeZone = "Europe/Berlin"
                    };

                var workflowHandle = await temporalClient.StartWorkflowAsync(
                    (ShipmentProcessWorkflow wf) => wf.Run(request),
                    new(
                        id: GetStarterWorkflowId(id),
                        taskQueue: ProcessOrchestratorTaskQueue.TaskQueue
                    )
                );

                return new { ShipmentId = id, WorkflowId = workflowHandle.Id };
            }
        );

        app.MapGet(
            "/{id}",
            async ([FromRoute] string id) =>
                await GetShipmentProcessOutcome(id) is { } apiResult
                    ? TypedResults.Ok(apiResult)
                    : TypedResults.NotFound() as IResult
        );

        async Task<ShipmentProcessApiResult?> GetShipmentProcessOutcome(string id)
        {
            try
            {
                var workflowHandle = temporalClient.GetWorkflowHandle(GetStarterWorkflowId(id));
                var result = await workflowHandle.QueryAsync<ShipmentProcessResult>(
                    ShipmentProcessWorkflow.OutcomeQueryName,
                    []
                );

                return new ShipmentProcessApiResult
                {
                    ShipmentId = id,
                    Status = result._Case,
                    Outcome = result.Success,
                    Failure = result.Failure
                };
            }
            catch (Temporalio.Exceptions.RpcException ex)
                when (ex.Message.Contains(
                        "sql: no rows in result set",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            {
                return default;
            }
        }
    }

    public record ShipmentProcessApiResult
    {
        public string? ShipmentId { get; init; }
        public string? Status { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CompletedShipmentProcessOutcome? Outcome { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ShipmentProcessFailure? Failure { get; init; }
    }
}
