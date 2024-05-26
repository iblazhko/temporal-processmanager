namespace SharedInfrastructure;

using System;
using System.Linq;
using System.Net.Sockets;
using Polly;
using Polly.Contrib.WaitAndRetry;
using SharedInfrastructure.Configuration;

public static class InfrastructureWaitPolicy
{
    public static bool WaitForInfrastructure(ShipmentProcessSettings settings)
    {
        if (!settings.WaitForInfrastructureOnStartup)
            return true;

        var policy = Policy
            .HandleResult(false)
            .WaitAndRetry(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(3), 10));

        return policy.Execute(() => IsInfrastructureAvailable(settings));
    }

    private static bool IsInfrastructureAvailable(ShipmentProcessSettings settings)
    {
        var temporalUri = new Uri($"tcp://{settings.Temporal.ServerAddress}");

        var infrastructureServicesAvailability = new[]
        {
            GetServiceAvailability(nameof(settings.Temporal), temporalUri.Host, temporalUri.Port)
        };

        var unavailableMessages = infrastructureServicesAvailability
            .Where(x => !x.IsAvailable)
            .Select(x => $"Service {x.ServiceName} is not available at {x.Host}:{x.Port}")
            .ToArray();

        if (unavailableMessages.Length > 0)
        {
            var timestamp = DateTime.UtcNow.ToString("u");
            var errorMessage = string.Join(";", unavailableMessages);
            Console.WriteLine($"[{timestamp}] {errorMessage}");
        }

        return unavailableMessages.Length == 0;
    }

    private static bool IsPortOpen(string host, int port, TimeSpan timeout)
    {
        try
        {
            using var client = new TcpClient();
            var result = client.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(timeout);
            client.EndConnect(result);

            return success;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static ServiceAvailabilityResult GetServiceAvailability(
        string serviceName,
        string host,
        int port
    ) => new(serviceName, host, port, IsPortOpen(host, port, PortCheckTimeout));

    private record ServiceAvailabilityResult(
        string ServiceName,
        string Host,
        int Port,
        bool IsAvailable
    );

    private static readonly TimeSpan PortCheckTimeout = TimeSpan.FromSeconds(3);
}
