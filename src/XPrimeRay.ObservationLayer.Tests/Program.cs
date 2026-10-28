namespace XPrimeRay.ObservationLayer.Tests;

internal static class Program
{
    private static int Main()
    {
        (string Name, Action Run)[] suites =
        {
            (nameof(ObservationLayerContractTests), ObservationLayerContractTests.Run),
            (nameof(TransportLedgerV1Tests), TransportLedgerV1Tests.Run)
        };

        int failures = 0;
        foreach ((string name, Action run) in suites)
        {
            try
            {
                run();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
            }
        }

        Console.WriteLine($"{(failures == 0 ? "PASS" : "FAIL")}: {suites.Length} suites, {failures} failures");
        return failures == 0 ? 0 : 1;
    }
}
