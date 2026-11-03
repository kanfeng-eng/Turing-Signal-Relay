using System;
public static class BootstrapCheckMain
{
    public static int Main()
    {
        try
        {
            TuringSignalRelay.Tests.BootstrapChecks.Run();
            Console.WriteLine("PASS: tick timing, pause/step, completion, snapshots, validation, 9000 callbacks.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
