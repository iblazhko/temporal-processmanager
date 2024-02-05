namespace CarrierIntegration.Temporal;

using SharedKernel.DTOs;
using Temporalio.Workflows;

[Workflow]
public class ShipmentLegCarrierCollectionBookingWorkflow
{
    [WorkflowRun]
    public async Task<ShipmentLegCollectionBookingResult> BookShipmentLegCollection(
        ShipmentLegCollectionBookingRequest request
    )
    {
        return await Workflow.ExecuteActivityAsync(
            (CarrierIntegrationActivities ac) => ac.BookShipmentLegCollection(request),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(3) }
        );
    }
}
