namespace ProcessOrchestrator.Temporal;

using SharedKernel;
using SharedKernel.DTOs;
using Temporalio.Workflows;

[Workflow]
public class ShipmentProcessWorkflow
{
    // Note that even if the shipmentProcessResult is Failure, the Temporal workflow
    // will be marked as Completed. This means that the process has finished and there were no internal faults.
    //
    // We may consider marking Temporal workflow as Failed, to make it easier to find failed shipments
    // in Temporal dashboard, but we need to make sure Temporal will not do unnecessary retries
    // (e.g. by using RetryPolicy with appropriate NonRetryableErrorTypes, but this may not be enough
    // to terminate the failed workflow completely).
    //
    // Disadvantage of this approach is that it will be harder to see failure details -
    // result will not appear in the "Input and Results" dashboard section.
    [WorkflowRun]
    public async Task<ShipmentProcessResult> Run(ShipmentProcessRequest request)
    {
        var validationResult = await Workflow.ExecuteActivityAsync(
            (ShipmentProcessOrchestrationActivities ac) => ac.ValidateShipment(request),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(5) }
        );

        ShipmentProcessRequest validRequest;

        switch (validationResult._Case)
        {
            case nameof(ShipmentValidationResult.Success):
                validRequest = validationResult.Success;
                break;
            case nameof(ShipmentValidationResult.Failure):
                return new ShipmentProcessResult
                {
                    _Case = nameof(ShipmentProcessResult.Failure),
                    Failure = new ShipmentProcessFailure { Faults = [validationResult.Failure] }
                };
            default:
                return InconsistentInternalState();
        }

        var processCategory = await Workflow.ExecuteActivityAsync(
            (ShipmentProcessOrchestrationActivities ac) => ac.ClassifyShipment(validRequest),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(5) }
        );

        var shipmentProcessWorkflowOptions = new ChildWorkflowOptions
        {
            Id = validRequest.ShipmentId,
            TaskQueue = ProcessOrchestratorTaskQueue.TaskQueue
        };

        var shipmentProcessResult = await (
            processCategory switch
            {
                ShipmentProcessCategory.Domestic
                    => Workflow.ExecuteChildWorkflowAsync(
                        (DomesticShipmentProcessWorkflow wf) => wf.Run(validRequest),
                        shipmentProcessWorkflowOptions
                    ),
                ShipmentProcessCategory.International
                    => Workflow.ExecuteChildWorkflowAsync(
                        (InternationalShipmentProcessWorkflow wf) => wf.Run(validRequest),
                        shipmentProcessWorkflowOptions
                    ),
                ShipmentProcessCategory.InternationalWithPaperlessTrade
                    => Workflow.ExecuteChildWorkflowAsync(
                        (InternationalShipmentWithPaperlessTradeProcessWorkflow wf) =>
                            wf.Run(validRequest),
                        shipmentProcessWorkflowOptions
                    ),
                _
                    => Task.FromResult(
                        InconsistentInternalState(
                            $"No implementation found for process category '{processCategory}'"
                        )
                    )
            }
        );

        return shipmentProcessResult;
    }

    private static ShipmentProcessResult InconsistentInternalState(
        string description = "Inconsistent internal state"
    ) =>
        new()
        {
            _Case = nameof(ShipmentProcessResult.Failure),
            Failure = new ShipmentProcessFailure { Faults = [new() { Description = description }] }
        };
}
