namespace SharedInfrastructure.Configuration;

using System.Text;

#nullable disable

public class ShipmentProcessSettings
{
    public string ApiBaseUrl { get; init; }
    public TemporalSettings Temporal { get; init; }
    public bool WaitForInfrastructureOnStartup { get; init; }

    public override string ToString() =>
        new StringBuilder()
            .AppendSettingTitle("Shipment Process")
            .AppendSettingValue(() => ApiBaseUrl)
            .AppendLine()
            .AppendSubSection(() => Temporal)
            .AppendLine()
            .AppendSettingValue(() => WaitForInfrastructureOnStartup)
            .ToString();
}
