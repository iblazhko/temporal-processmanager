namespace SharedInfrastructure.Configuration;

using System.Text;

#nullable disable

public class TemporalSettings
{
    public string ServerAddress { get; init; }
    public string Namespace { get; init; }
    public bool HealthCheck { get; init; }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.AppendSettingValue(() => ServerAddress);
        builder.AppendSettingValue(() => Namespace);
        builder.AppendSettingValue(() => HealthCheck);

        return builder.ToString();
    }
}
