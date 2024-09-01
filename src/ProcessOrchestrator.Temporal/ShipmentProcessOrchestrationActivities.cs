namespace ProcessOrchestrator.Temporal;

using SharedKernel;
using SharedKernel.DTOs;
using Temporalio.Activities;

// Activities input / output are serialization - friendly DTOs
internal class ShipmentProcessOrchestrationActivities
{
    [Activity]
    public Task<ShipmentValidationResult> ValidateShipment(ShipmentProcessRequest command)
    {
        var validationErrors = new List<string>();

        if (string.IsNullOrWhiteSpace(command?.ShipmentId))
            validationErrors.Add("ShipmentId must be specified");

        if (command?.Legs == null || command.Legs?.Length == 0 || command.Legs?.Length > 2)
            validationErrors.Add("Shipment must have 1 or 2 legs");

        var result = validationErrors.Any()
            ? ShipmentValidationResult.CreateFailure(
                new ValidationFault
                {
                    Description = "Invalid request",
                    Errors = validationErrors.ToArray()
                }
            )
            : ShipmentValidationResult.CreateSuccess(
                new ShipmentProcessRequest
                {
                    ShipmentId = command!.ShipmentId,
                    Legs = command.Legs,
                    CollectionDate = command.CollectionDate,
                    TimeZone = command.TimeZone
                }
            );

        return Task.FromResult(result);
    }

    [Activity]
    public string ClassifyShipment(ShipmentProcessRequest request) =>
        request.ShipmentId[0] switch
        {
            '1' => ShipmentProcessCategory.Domestic,
            '2' => ShipmentProcessCategory.InternationalWithPaperlessTrade,
            _ => ShipmentProcessCategory.International
        };
}
