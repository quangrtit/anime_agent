using System;
using AnimeAssistant.Domain;

internal static class Program
{
    private static int Main()
    {
        if (!Enum.IsDefined(typeof(SummonState), SummonState.Faulted))
        {
            Console.Error.WriteLine("SummonState.Faulted is missing.");
            return 1;
        }

        if (new ReturnHomeCommand().Priority != 100)
        {
            Console.Error.WriteLine("ReturnHomeCommand priority contract changed.");
            return 1;
        }

        Console.WriteLine("Domain smoke verification passed.");
        return 0;
    }
}

