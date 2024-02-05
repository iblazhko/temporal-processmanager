namespace SharedInfrastructure.Configuration;

using System.Text;

#nullable disable

public class TemporalSettings
{
    public string ServerAddress { get; set; }
    public string Namespace { get; set; }
    public bool HealthCheck { get; set; }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.AppendSettingValue(() => ServerAddress);
        builder.AppendSettingValue(() => Namespace);
        builder.AppendSettingValue(() => HealthCheck);

        return builder.ToString();
    }
}
