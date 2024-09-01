namespace SharedInfrastructure.Configuration;

using Microsoft.Extensions.Configuration;

public static class SettingsResolver
{
    public static ShipmentProcessSettings GetSettings() =>
        new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables() // looking for env. vars with prefix "ShipmentProcess__" derived from top section name
            .Build()
            .GetSection("ShipmentProcess")
            .Get<ShipmentProcessSettings>()
        ?? throw new InvalidOperationException("Could not get application settings");
}
