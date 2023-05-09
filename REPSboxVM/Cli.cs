using System;
using System.Timers;

namespace REPSboxVM;

class Cli
{
    public static string User;
    private static Runtime Runtime;
    private static Timer timer;
    static void Run(string[] args)
    {
        User = args[0];
        timer = new Timer
        {
            AutoReset = false,
            Enabled = false,
            Interval = 5000,
        };
        timer.Elapsed += YieldTimeout;
        try
        {
            Runtime = new Runtime("User");

            Runtime.Run("--");

            Console.CancelKeyPress += new ConsoleCancelEventHandler(IntHandler);

            while (Runtime.IsRunning)
            {
                var script = Console.ReadLine();
                timer.Start();
                Runtime.Run(script);
                if (timer.Enabled)
                {
                    timer.Stop();
                }
            }
        } catch(LuaException e)
        {
            Console.Error.WriteLine(e);
        }
    }

    private static void YieldTimeout(Object source, ElapsedEventArgs e)
    {
        Runtime.KillScript = true;
    }

    static void IntHandler(object sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        Runtime.Dispose();
        Environment.Exit(0);
    }
}
