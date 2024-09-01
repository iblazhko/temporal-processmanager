namespace DocumentsGeneration.Temporal;

using SharedKernel.DTOs;
using Temporalio.Activities;

internal class ShipmentDocumentsGenerationActivities
{
    [Activity]
    public Task<string> GenerateCustomsInvoiceForPaperlessTrade(
        PaperlessTradeDocumentsGenerationRequest request
    ) =>
        Task.FromResult(
            new Uri(
                $"http://somewhere.net/shipment-documents/{request.ShipmentId}_pt_invoice"
            ).ToString()
        );

    [Activity]
    public Task<string> GenerateShipmentLabels(ShipmentDocumentsGenerationRequest request) =>
        Task.FromResult(
            new Uri(
                $"http://somewhere.net/shipment-documents/{request.ShipmentId}_labels"
            ).ToString()
        );

    [Activity]
    public Task<string> GenerateCustomsInvoice(ShipmentDocumentsGenerationRequest request) =>
        Task.FromResult(
            new Uri(
                $"http://somewhere.net/shipment-documents/{request.ShipmentId}_invoice"
            ).ToString()
        );

    [Activity]
    public Task<string> GenerateShipmentReceipt(ShipmentDocumentsGenerationRequest request) =>
        Task.FromResult(
            new Uri(
                $"http://somewhere.net/shipment-documents/{request.ShipmentId}_receipt"
            ).ToString()
        );

    [Activity]
    public Task<string> GenerateShipmentCombinedDocument(
        ShipmentDocumentsGenerationRequest request
    ) =>
        Task.FromResult(
            new Uri(
                $"http://somewhere.net/shipment-documents/{request.ShipmentId}_combined_document"
            ).ToString()
        );
}
