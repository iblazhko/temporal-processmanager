namespace SharedInfrastructure;

using System;
using Polly;
using Polly.Contrib.WaitAndRetry;
using SharedInfrastructure.Configuration;
using Temporalio.Client;

public static class InfrastructureWaitPolicy
{
    public static bool WaitForInfrastructure(ShipmentProcessSettings settings)
    {
        if (!settings.WaitForInfrastructureOnStartup)
            return true;

        var policy = Policy
            .HandleResult(false)
            .WaitAndRetry(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(5), 10));

        return policy.Execute(() => IsInfrastructureAvailable(settings));
    }

    private static bool IsInfrastructureAvailable(ShipmentProcessSettings settings)
    {
        try
        {
            var _ = TemporalClient.ConnectAsync(new(settings.Temporal.ServerAddress)).Result;
            return true;
        }
        catch (Exception)
        {
            var timestamp = DateTime.UtcNow.ToString("u");
            Console.WriteLine(
                $"[{timestamp}] Temporal server is not available at {settings.Temporal.ServerAddress}"
            );
            return false;
        }
    }
}
