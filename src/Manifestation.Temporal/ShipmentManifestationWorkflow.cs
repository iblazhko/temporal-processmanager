namespace Manifestation.Temporal;

using CarrierIntegration.Temporal;
using SharedKernel.DTOs;
using Temporalio.Workflows;

[Workflow]
public class ShipmentManifestationWorkflow
{
    [WorkflowRun]
    public async Task<ShipmentManifestationResult> ManifestShipment(
        ShipmentManifestationRequest request
    )
    {
        var manifestedLegs = new List<ManifestedShipmentLeg>();

        foreach (var leg in request.Legs)
        {
            var legManifestationRequest = new ShipmentLegManifestationRequest
            {
                ShipmentId = request.ShipmentId,
                Leg = leg,
                CollectionDate = request.CollectionDate,
                TimeZone = request.TimeZone
            };

            var legManifestationResult = await Workflow.ExecuteChildWorkflowAsync(
                (ShipmentLegCarrierManifestationWorkflow wf) =>
                    wf.ManifestShipmentLeg(legManifestationRequest),
                new ChildWorkflowOptions
                {
                    Id = $"{request.ShipmentId}_{leg.CarrierId}_manifestation",
                    TaskQueue = CarrierIntegrationTaskQueue.TaskQueue
                }
            );

            switch (legManifestationResult._Case)
            {
                case nameof(ShipmentLegManifestationResult.Success):
                    manifestedLegs.Add(legManifestationResult.Success);
                    break;
                case nameof(ShipmentLegManifestationResult.Failure):
                    return new ShipmentManifestationResult
                    {
                        _Case = nameof(ShipmentManifestationResult.Failure),
                        Failure = legManifestationResult.Failure
                    };
                default:
                    return InconsistentInternalState;
            }
        }

        return new ShipmentManifestationResult
        {
            _Case = nameof(ShipmentManifestationResult.Success),
            Success = new ManifestedShipment
            {
                ShipmentId = request.ShipmentId,
                Legs = manifestedLegs.ToArray(),
                CollectionDate = request.CollectionDate,
                TimeZone = request.TimeZone
            }
        };
    }

    private static readonly ShipmentManifestationResult InconsistentInternalState =
        new()
        {
            _Case = nameof(ShipmentManifestationResult.Failure),
            Failure = new ShipmentProcessFailure { Faults = [ new() { Description = "Inconsistent internal state" }] }
        };
}
