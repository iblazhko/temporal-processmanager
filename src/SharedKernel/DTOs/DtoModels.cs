namespace SharedKernel.DTOs;

/*
In this example it is expected that any DTO can be serialized to / deserialized from JSON
without any customizations in JSON serializer.

In practice, this means that we can use primitive types (bool/int/double/decimal/string), arrays, dictionaries
with primitive key type, and classes/records, but cannot use anything more advanced. In particular, we cannot use
.net enums or DateTime / DateOnly / TimeOnly / DateTimeOffset.

For enums we use string representation of .net enum member (name, not the underlying value).
For date and time we use string representation in ISO 8601 format.

[Product types](https://en.wikipedia.org/wiki/Product_type) are represented as a .NET class or record, shape of
DTO can match shape of the domain model, but types of the DTO properties may need to be adjusted to satisfy
restrictions described above.

[Sum types](https://en.wikipedia.org/wiki/Tagged_union) a.k.a. discriminated unions can be represented using
following structure (note that discrimination is not enforced):

    public class UnionCaseResult
    {
        public string _Case { get; init; }
        public A Case1 { get; init; }
        public B Case2 { get; init; }
    }

Instances of that class will look like this:
    {
        "_Case": "Case1",
        "Case1": { ... }
        "Case2": null
    }
    {
        "_Case": "Case2",
        "Case1": null,
        "Case2": { ... }
    }

Once DTO is received as an input to workflow / activity / message consumer, it can be converted to a strongly-typed
domain model, if needed. Same applies to the workflow / activity output or message publishing -
if we use strongly-typed domain models internally, we need to convert domain model to a DTO, and use the DTO
as the output.

DTOs are simple data containers and should not have any business logic. Related to that, DTOs should allow
initialization from incomplete / semantically invalid JSON, DTOs should only define *shape* of the data,
but should not have any restrictions about the data *content* or *format*. Any validations belong to domain model and
should be done separately, in a separate DTO -> domain model mapper, or in a separate validator.
*/

#nullable disable

#region Generic Result

public abstract record ActivityResult<TSuccess, TFailure>
{
    // ReSharper disable once InconsistentNaming
    public string _Case { get; init; }

    public TSuccess Success { get; init; }

    public TFailure Failure { get; init; }
}

// Represents an empty input / output
public record Unit;

#endregion

public record ShipmentLeg
{
    public string CarrierId { get; init; }
    public string Sender { get; init; }
    public string Receiver { get; init; }
    public string Collection { get; init; }
}

public record ManifestedShipmentLeg : ShipmentLeg
{
    public string[] TrackingNumbers { get; init; }
    public string LabelsDocument { get; init; }
}

public record PaperlessTradeDocuments
{
    public string InvoiceUrl { get; init; }
}

public record ShipmentDocuments
{
    public string LabelsUrl { get; init; }
    public string InvoiceUrl { get; init; }
    public string ReceiptUrl { get; init; }
    public string CombinedDocumentUrl { get; init; }
}

public record ShipmentCollectionSchedule
{
    public string ShipmentId { get; init; }
    public string CarrierId { get; init; }
    public string BookAt { get; set; }
    public bool BookingIsDue { get; set; }
}

public record ShipmentCollectionBooking
{
    public string CarrierId { get; init; }
    public string BookingReference { get; init; }
    public string LocationReference { get; init; }
}

// Anywhere apart from the top level orchestrator it is safe to assume the request is valid in general.
// 1st step in the top level orchestrator workflow is to validate it and fail immediately if the request
// is not valid in general (ShipmentId is specified, and we have 1 or 2 shipment legs).
// Activities / child workflows may implement additional (more restrictive) validations if needed.
public record ShipmentProcessRequest
{
    public string ShipmentId { get; init; }
    public ShipmentLeg[] Legs { get; init; }
    public string CollectionDate { get; init; }
    public string TimeZone { get; init; }
}

public record ManifestedShipmentLegOutcome
{
    public string CarrierId { get; init; }
    public string[] TrackingNumbers { get; set; }
    public string LabelsUrl { get; init; }
}

public record CompletedShipmentProcessOutcome
{
    public string ShipmentId { get; init; }
    public ManifestedShipmentLegOutcome[] ManifestedLegs { get; set; }
    public ShipmentDocuments ShipmentDocuments { get; init; }
    public ShipmentCollectionBooking CollectionBooking { get; init; }
}

/*
 When it comes to terminology for representing issues with process execution, there are multiple
 (sometimes overlapping / conflicting) definitions. This example assumes the following definitions:

 * Failure: System was not able to perform what it was expected from it. This is the problem we observe.
 * Fault: The cause of the failure.
 * Error: The condition which caused the fault to occur. e.g, missing or incorrectly formatted properties.

 Saying "failure" means we know something is wrong but we may not know the cause.
 Saying "fault" means we know the cause category, but may not know exactly why the fault occurred.
 Saying "error" means we know why the fault occurred.

 https://stackoverflow.com/a/47963772

 Given that the process is composed from nested workflows, the line between failure and fault may be blurry:
 shipment process failure is caused by failures in nested workflows or activities, and each of those failures
 can be considered a fault from the point of view of the top level process.
*/

public record ShipmentProcessFailure
{
    public Fault[] Faults { get; init; }
}

public record Fault
{
    public string Description { get; init; }
    public string[] Errors { get; init; }
}

public record ValidationFault : Fault;

public record ShipmentValidationResult : ActivityResult<ShipmentProcessRequest, ValidationFault> { }

public record ShipmentProcessResult
    : ActivityResult<CompletedShipmentProcessOutcome, ShipmentProcessFailure> { }

public record ShipmentManifestationRequest : ShipmentProcessRequest
{
    public string PaperlessTradeDocumentUrl { get; init; }
}

public record ManifestedShipment
{
    public string ShipmentId { get; init; }
    public ManifestedShipmentLeg[] Legs { get; init; }
    public string CollectionDate { get; init; }
    public string TimeZone { get; init; }
}

public record ShipmentManifestationResult
    : ActivityResult<ManifestedShipment, ShipmentProcessFailure> { }

public record ShipmentLegManifestationRequest
{
    public string ShipmentId { get; init; }
    public ShipmentLeg Leg { get; init; }
    public string CollectionDate { get; init; }
    public string TimeZone { get; init; }
}

public record ShipmentLegManifestationResult
    : ActivityResult<ManifestedShipmentLeg, ShipmentProcessFailure> { }

public record PaperlessTradeDocumentsGenerationRequest : ShipmentProcessRequest { }

public record PaperlessTradeDocumentsGenerationResult
    : ActivityResult<PaperlessTradeDocuments, ShipmentProcessFailure> { }

public record ShipmentDocumentsGenerationRequest : ManifestedShipment
{
    public bool IncludeCustomsInvoice { get; init; }
}

public record ShipmentDocumentsGenerationResult
    : ActivityResult<ShipmentDocuments, ShipmentProcessFailure> { }

public record ShipmentLegCollectionBookingRequest : ShipmentLegManifestationRequest
{
    // To avoid dependency on system time in activity, passing current time explicitly
    public string UtcNow { get; set; }
}

public record ShipmentLegCollectionBookingSchedulingCheckResult
    : ActivityResult<Unit, ShipmentProcessFailure> { }

public record ShipmentLegCollectionBookingSchedulingResult
    : ActivityResult<ShipmentCollectionSchedule, ShipmentProcessFailure> { }

public record ShipmentLegCollectionBookingResult
    : ActivityResult<ShipmentCollectionBooking, ShipmentProcessFailure> { }
