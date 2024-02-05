namespace ProcessOrchestrator.Temporal;

using CollectionBooking.Temporal;
using DocumentsGeneration.Temporal;
using Manifestation.Temporal;
using SharedKernel.DTOs;
using Temporalio.Workflows;

[Workflow]
public class DomesticShipmentProcessWorkflow
{
    [WorkflowRun]
    public async Task<ShipmentProcessResult> Run(ShipmentProcessRequest request)
    {
        var shipmentManifestationRequest = new ShipmentManifestationRequest
        {
            ShipmentId = request.ShipmentId,
            Legs = request.Legs,
            CollectionDate = request.CollectionDate,
            TimeZone = request.TimeZone
        };

        var shipmentManifestationResult = await Workflow.ExecuteChildWorkflowAsync(
            (ShipmentManifestationWorkflow wf) =>
                wf.ManifestShipment(shipmentManifestationRequest),
            new ChildWorkflowOptions
            {
                Id = $"{request.ShipmentId}_manifestation",
                TaskQueue = ManifestationTaskQueue.TaskQueue
            }
        );

        ManifestedShipment manifestedShipment;

        switch (shipmentManifestationResult._Case)
        {
            case nameof(ShipmentManifestationResult.Success):
                manifestedShipment = shipmentManifestationResult.Success;
                break;
            case nameof(ShipmentManifestationResult.Failure):
                return new ShipmentProcessResult
                {
                    _Case = nameof(ShipmentProcessResult.Failure),
                    Failure = shipmentManifestationResult.Failure
                };
            default:
                return InconsistentInternalState;
        }

        var shipmentDocumentsGenerationRequest = new ShipmentDocumentsGenerationRequest
        {
            ShipmentId = manifestedShipment.ShipmentId,
            Legs = manifestedShipment.Legs,
            CollectionDate = manifestedShipment.CollectionDate,
            TimeZone = manifestedShipment.TimeZone,
            IncludeCustomsInvoice = false
        };
        var shipmentDocumentsGenerationResult = await Workflow.ExecuteChildWorkflowAsync(
            (ShipmentDocumentsGenerationWorkflow wf) =>
                wf.GenerateShipmentDocuments(shipmentDocumentsGenerationRequest),
            new ChildWorkflowOptions
            {
                Id = $"{request.ShipmentId}_documents",
                TaskQueue = DocumentsGenerationTaskQueue.TaskQueue
            }
        );

        ShipmentDocuments shipmentDocuments;
        switch (shipmentDocumentsGenerationResult._Case)
        {
            case nameof(ShipmentDocumentsGenerationResult.Success):
                shipmentDocuments = shipmentDocumentsGenerationResult.Success;
                break;
            case nameof(ShipmentDocumentsGenerationResult.Failure):
                return new ShipmentProcessResult
                {
                    _Case = nameof(ShipmentProcessResult.Failure),
                    Failure = shipmentDocumentsGenerationResult.Failure
                };
            default:
                return InconsistentInternalState;
        }

        var shipmentCollectionBookingRequest = new ShipmentLegCollectionBookingRequest()
        {
            ShipmentId = manifestedShipment.ShipmentId,
            Leg = manifestedShipment.Legs.First(),
            CollectionDate = manifestedShipment.CollectionDate,
            TimeZone = manifestedShipment.TimeZone
        };
        var shipmentCollectionBookingResult = await Workflow.ExecuteChildWorkflowAsync(
            (ShipmentCollectionBookingWorkflow wf) =>
                wf.BookShipmentCollection(shipmentCollectionBookingRequest),
            new ChildWorkflowOptions
            {
                Id = $"{request.ShipmentId}_collection",
                TaskQueue = CollectionBookingTaskQueue.TaskQueue
            }
        );

        ShipmentCollectionBooking shipmentCollection;
        switch (shipmentCollectionBookingResult._Case)
        {
            case nameof(ShipmentLegCollectionBookingResult.Success):
                shipmentCollection = shipmentCollectionBookingResult.Success;
                break;
            case nameof(ShipmentLegCollectionBookingResult.Failure):
                return new ShipmentProcessResult
                {
                    _Case = nameof(ShipmentProcessResult.Failure),
                    Failure = shipmentCollectionBookingResult.Failure
                };
            default:
                return InconsistentInternalState;
        }

        return new ShipmentProcessResult
        {
            _Case = nameof(ShipmentProcessResult.Success),
            Success = new CompletedShipmentProcessOutcome
            {
                ShipmentId = request.ShipmentId,
                ManifestedLegs = manifestedShipment
                    .Legs.Select(
                        x =>
                            new ManifestedShipmentLegOutcome
                            {
                                CarrierId = x.CarrierId,
                                TrackingNumbers = x.TrackingNumbers,
                                LabelsUrl = x.LabelsDocument
                            }
                    )
                    .ToArray(),
                ShipmentDocuments = shipmentDocuments,
                CollectionBooking = shipmentCollection
            }
        };
    }

    private static readonly ShipmentProcessResult InconsistentInternalState =
        new()
        {
            _Case = nameof(ShipmentProcessResult.Failure),
            Failure = new ShipmentProcessFailure
            {
                Faults = [new() { Description = "Inconsistent internal state" }]
            }
        };
}
