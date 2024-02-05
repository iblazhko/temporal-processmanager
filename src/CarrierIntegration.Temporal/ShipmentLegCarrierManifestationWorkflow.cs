namespace CarrierIntegration.Temporal;

using SharedKernel.DTOs;
using Temporalio.Workflows;

[Workflow]
public class ShipmentLegCarrierManifestationWorkflow
{
    [WorkflowRun]
    public async Task<ShipmentLegManifestationResult> ManifestShipmentLeg(
        ShipmentLegManifestationRequest request
    )
    {
        return await Workflow.ExecuteActivityAsync(
            (CarrierIntegrationActivities ac) => ac.ManifestShipmentLeg(request),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(3) }
        );
    }
}
