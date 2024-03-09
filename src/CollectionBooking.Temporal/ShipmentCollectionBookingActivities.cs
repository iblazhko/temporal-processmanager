namespace CollectionBooking.Temporal;

using SharedKernel.DTOs;
using Temporalio.Activities;

internal class ShipmentCollectionBookingActivities
{
    [Activity]
    public Task<ShipmentLegCollectionBookingSchedulingResult> ScheduleShipmentCollectionBooking(
        ShipmentLegCollectionBookingRequest request
    )
    {
        var now = DateTime.Parse(request.UtcNow);
        // Real application would use a domain service that calculates collection booking time
        // For testing purposes, pretending that the booking is due few seconds from now
        var bookAt = now.AddSeconds(5);

        return Task.FromResult(
            new ShipmentLegCollectionBookingSchedulingResult
            {
                _Case = nameof(ShipmentLegCollectionBookingSchedulingResult.Success),
                Success = new ShipmentCollectionSchedule
                {
                    ShipmentId = request.ShipmentId,
                    CarrierId = request.Leg.CarrierId,
                    BookAt = bookAt.ToString("O"),
                    BookingIsDue = false
                }
            }
        );
    }

    [Activity]
    public Task<ShipmentLegCollectionBookingSchedulingCheckResult> CanShipmentBeScheduledForCollectionBooking(
        ShipmentLegCollectionBookingRequest request
    )
    {
        // Real application would use a domain service that do some collection-specific validations
        var canBeScheduledForCollectionBooking = true;
        if (!canBeScheduledForCollectionBooking)
        {
            return Task.FromResult(
                new ShipmentLegCollectionBookingSchedulingCheckResult
                {
                    _Case = nameof(ShipmentLegCollectionBookingSchedulingCheckResult.Failure),
                    Failure = new ShipmentProcessFailure
                    {
                        Faults =
                        [
                            new ValidationFault
                            {
                                Description = "Shipment cannot be scheduled for collection booking",
                                Errors = ["3.4.5 Missed booking cut-off time"]
                            }
                        ]
                    }
                }
            );
        }

        return Task.FromResult(
            new ShipmentLegCollectionBookingSchedulingCheckResult
            {
                _Case = nameof(ShipmentLegCollectionBookingSchedulingCheckResult.Success),
                Success = new Unit()
            }
        );
    }
}
