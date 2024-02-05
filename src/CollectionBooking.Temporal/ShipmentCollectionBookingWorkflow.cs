namespace CollectionBooking.Temporal;

using CarrierIntegration.Temporal;
using SharedKernel.DTOs;
using Temporalio.Workflows;

[Workflow]
public class ShipmentCollectionBookingWorkflow
{
    [WorkflowRun]
    public async Task<ShipmentLegCollectionBookingResult> BookShipmentCollection(
        ShipmentLegCollectionBookingRequest request
    )
    {
        var now = Workflow.UtcNow;
        var schedulingRequest = request with
        {
            UtcNow = now.ToString("O")
        };

        // TODO: This is arguably should be a part of ShipmentProcessWorkflow
        // there is no point in doing manifestation if collection cannot be booked
        var canBeScheduledForCollectionBooking = await Workflow.ExecuteActivityAsync(
            (ShipmentCollectionBookingActivities ac) =>
                ac.CanShipmentBeScheduledForCollectionBooking(schedulingRequest),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(1) }
        );

        switch (canBeScheduledForCollectionBooking._Case)
        {
            case nameof(ShipmentLegCollectionBookingSchedulingCheckResult.Success):
                break;
            case nameof(ShipmentLegCollectionBookingSchedulingCheckResult.Failure):
                return new ShipmentLegCollectionBookingResult
                {
                    _Case = nameof(ShipmentLegCollectionBookingResult.Failure),
                    Failure = canBeScheduledForCollectionBooking.Failure
                };
            default:
                return InconsistentInternalState;
        }

        var schedulingResult = await Workflow.ExecuteActivityAsync(
            (ShipmentCollectionBookingActivities ac) =>
                ac.ScheduleShipmentCollectionBooking(schedulingRequest),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(1) }
        );

        ShipmentCollectionSchedule collectionSchedule;
        switch (schedulingResult._Case)
        {
            case nameof(ShipmentLegCollectionBookingSchedulingResult.Success):
                collectionSchedule = schedulingResult.Success;
                break;
            case nameof(ShipmentLegCollectionBookingSchedulingResult.Failure):
                return new ShipmentLegCollectionBookingResult
                {
                    _Case = nameof(ShipmentLegCollectionBookingResult.Failure),
                    Failure = schedulingResult.Failure
                };
            default:
                return InconsistentInternalState;
        }

        var bookAt = DateTime.Parse(collectionSchedule.BookAt);
        if (bookAt > now) await Workflow.DelayAsync(bookAt - now);
        // else
        //   TODO: Consider failing if `bookAt` is too far behind `now`

        // TODO: Consider checking current Workflow.UtcNow and checking if we are not late
        // (due to e.g. Temporal delaying workflow for longer than we have asked for)
        // if (Workflow.UtcNow - bookAt > threshold) ...
        return await Workflow.ExecuteChildWorkflowAsync(
            (ShipmentLegCarrierCollectionBookingWorkflow wf) =>
                wf.BookShipmentLegCollection(request),
            new ChildWorkflowOptions
            {
                Id = $"{request.ShipmentId}_{request.Leg.CarrierId}_collection_booking",
                TaskQueue = CarrierIntegrationTaskQueue.TaskQueue
            }
        );
    }

    private static readonly ShipmentLegCollectionBookingResult InconsistentInternalState =
        new()
        {
            _Case = nameof(ShipmentLegCollectionBookingResult.Failure),
            Failure = new ShipmentProcessFailure { Faults = [ new() { Description = "Inconsistent internal state" }] }
        };
}
