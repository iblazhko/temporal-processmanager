namespace DocumentsGeneration.Temporal;

using SharedKernel.DTOs;
using Temporalio.Workflows;

[Workflow]
public class PaperlessTradeDocumentsGenerationWorkflow
{
    [WorkflowRun]
    public async Task<PaperlessTradeDocumentsGenerationResult> GeneratePaperlessTradeDocuments(
        PaperlessTradeDocumentsGenerationRequest request
    )
    {
        var invoiceUrl = await Workflow.ExecuteActivityAsync(
            (ShipmentDocumentsGenerationActivities ac) =>
                ac.GenerateCustomsInvoiceForPaperlessTrade(request),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(5) }
        );

        return new PaperlessTradeDocumentsGenerationResult
        {
            _Case = nameof(ShipmentDocumentsGenerationResult.Success),
            Success = new PaperlessTradeDocuments { InvoiceUrl = invoiceUrl, }
        };
    }
}
