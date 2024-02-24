namespace CarrierIntegration.Temporal;

using SharedKernel.DTOs;
using Temporalio.Activities;

internal class CarrierIntegrationActivities
{
    [Activity]
    public Task<ShipmentLegManifestationResult> ManifestShipmentLeg(
        ShipmentLegManifestationRequest request
    )
    {
        return Task.FromResult(
            request.ShipmentId.EndsWith('1')
                ? new ShipmentLegManifestationResult
                {
                    _Case = nameof(ShipmentLegManifestationResult.Failure),
                    Failure = new ShipmentProcessFailure
                    {
                        Faults =
                        [
                            new Fault
                            {
                                Description = "Carrier manifestation failed",
                                Errors =
                                [
                                    "1.2.3: Address line 1 missing",
                                    "1.2.5: Postal code missing"
                                ]
                            }
                        ]
                    }
                }
                : new ShipmentLegManifestationResult
                {
                    _Case = nameof(ShipmentLegManifestationResult.Success),
                    Success = new ManifestedShipmentLeg
                    {
                        CarrierId = request.Leg.CarrierId,
                        Sender = request.Leg.Sender,
                        Receiver = request.Leg.Receiver,
                        Collection = request.Leg.Collection,
                        TrackingNumbers =
                        [
                            Guid.NewGuid().ToString("N"),
                            Guid.NewGuid().ToString("N")
                        ],
                        LabelsDocument = new Uri(
                            $"http://somewhere.net/shipment-documents/{request.ShipmentId}_{request.Leg.CarrierId}"
                        ).ToString()
                    }
                }
        );
    }

    [Activity]
    public Task<ShipmentLegCollectionBookingResult> BookShipmentLegCollection(
        ShipmentLegCollectionBookingRequest request
    )
    {
        return Task.FromResult(
            request.ShipmentId.EndsWith('2')
                ? new ShipmentLegCollectionBookingResult
                {
                    _Case = nameof(ShipmentLegCollectionBookingResult.Failure),
                    Failure = new ShipmentProcessFailure
                    {
                        Faults =
                        [
                            new Fault
                            {
                                Description = "Carrier collection booking failed",
                                Errors = ["2.3.4: Collection not possible on the requested day"]
                            }
                        ]
                    }
                }
                : new ShipmentLegCollectionBookingResult
                {
                    _Case = nameof(ShipmentLegCollectionBookingResult.Success),
                    Success = new ShipmentCollectionBooking
                    {
                        CarrierId = request.Leg.CarrierId,
                        BookingReference = Guid.NewGuid().ToString("N"),
                        LocationReference = Guid.NewGuid().ToString("N")
                    }
                }
        );
    }
}
