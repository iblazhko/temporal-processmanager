namespace SharedKernel.Domain.Models;

using System;
using OneOf;

// ReSharper disable NotAccessedPositionalProperty.Global

public readonly record struct ShipmentId(string Id)
{
    public static explicit operator string(ShipmentId id) => id.Id;

    public static explicit operator ShipmentId(string id) => new(id);

    public override string ToString() => Id;
}

public readonly record struct ShipmentProcessCategory(string Id)
{
    public static explicit operator string(ShipmentProcessCategory category) => category.Id;

    public static explicit operator ShipmentProcessCategory(string id) => new(id);

    public override string ToString() => Id;
}

public record ShipmentLeg(CarrierId CarrierId, string Sender, string Receiver, string Collection);

public record ManifestedShipmentLeg(
    CarrierId CarrierId,
    string Sender,
    string Receiver,
    string Collection,
    string TrackingNumber,
    DocumentLocation LabelsDocument
);

public record ShipmentDocuments(
    DocumentLocation Labels,
    DocumentLocation CustomsInvoice,
    DocumentLocation CombinedDocument,
    DocumentLocation Receipt
);

public record struct DocumentLocation(Uri Location)
{
    public static explicit operator Uri(DocumentLocation location) => location.Location;

    public static explicit operator DocumentLocation(Uri location) => new(location);

    public static explicit operator DocumentLocation(string location) => new(new Uri(location));

    public override string ToString() => Location.ToString();
}

public record DocumentContent(byte[] Content)
{
    public static explicit operator byte[](DocumentContent content) => content.Content;

    public static explicit operator DocumentContent(byte[] content) => new(content);

    public override string ToString() => Convert.ToBase64String(Content);
}

public readonly record struct TimeZoneId(string TimeZone)
{
    public static explicit operator string(TimeZoneId id) => id.TimeZone;

    public static explicit operator TimeZoneId(string timeZone) => new(timeZone);

    public override string ToString() => TimeZone;
}

public readonly record struct CarrierId(Guid Id)
{
    public static explicit operator Guid(CarrierId id) => id.Id;

    public static explicit operator CarrierId(Guid id) => new(id);

    public override string ToString() => Id.ToString();
}

public enum ShipmentDocumentType
{
    CustomsInvoice,
    Labels,
    Receipt,
    CombinedDocument
}

public enum ShipmentLegDocumentType
{
    Labels
}

public record ShipmentCollectionBooking(
    Guid CarrierId,
    string BookingReference,
    string LocationReference
);

public record ShipmentProcessOutcome(
    string ShipmentId,
    ManifestedShipmentLeg[] ManifestedLegs,
    ShipmentDocuments ShipmentDocuments,
    ShipmentCollectionBooking CollectionBooking
);

public record Failure(string Description, Fault[] Faults);

public record Fault(string Description, string[] Errors);

public sealed class ShipmentProcessResult
    : OneOfBase<
        ShipmentProcessResult.PendingResult,
        ShipmentProcessResult.OutcomeResult,
        ShipmentProcessResult.FailureResult
    >
{
    public record PendingResult();

    public record OutcomeResult(ShipmentProcessOutcome Outcome);

    public record FailureResult(Failure Failure);

    public static ShipmentProcessResult Pending() => new(new PendingResult());

    public static ShipmentProcessResult Success(ShipmentProcessOutcome outcome) =>
        new(new OutcomeResult(outcome));

    public static ShipmentProcessResult Failure(Failure failure) => new(new FailureResult(failure));

    private ShipmentProcessResult(OneOf<PendingResult, OutcomeResult, FailureResult> input)
        : base(input) { }
}
