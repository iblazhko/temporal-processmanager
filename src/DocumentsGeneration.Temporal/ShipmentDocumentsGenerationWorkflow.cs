namespace DocumentsGeneration.Temporal;

using SharedKernel.DTOs;
using Temporalio.Workflows;

[Workflow]
public class ShipmentDocumentsGenerationWorkflow
{
    [WorkflowRun]
    public async Task<ShipmentDocumentsGenerationResult> GenerateShipmentDocuments(
        ShipmentDocumentsGenerationRequest request
    )
    {
        var labelsUrl = await Workflow.ExecuteActivityAsync(
            (ShipmentDocumentsGenerationActivities ac) => ac.GenerateShipmentLabels(request),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(5) }
        );

        var invoiceUrl = request.IncludeCustomsInvoice
            ? await Workflow.ExecuteActivityAsync(
                (ShipmentDocumentsGenerationActivities ac) => ac.GenerateCustomsInvoice(request),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(5) }
            )
            : default;

        var receiptUrl = await Workflow.ExecuteActivityAsync(
            (ShipmentDocumentsGenerationActivities ac) => ac.GenerateShipmentReceipt(request),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(5) }
        );

        var combinedDocumentUrl = await Workflow.ExecuteActivityAsync(
            (ShipmentDocumentsGenerationActivities ac) =>
                ac.GenerateShipmentCombinedDocument(request),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(5) }
        );

        return ShipmentDocumentsGenerationResult.CreateSuccess(
            new ShipmentDocuments
            {
                LabelsUrl = labelsUrl,
                InvoiceUrl = invoiceUrl,
                ReceiptUrl = receiptUrl,
                CombinedDocumentUrl = combinedDocumentUrl
            }
        );
    }
}
