using System;
using System.IO;
using UnityEngine;

namespace TuringSignalRelay
{
    public static class HeadlessEntry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--bootstrap-replay");
            if (index < 0) return;
            try
            {
                if (index + 1 >= args.Length) throw new ArgumentException("Expected replay output path.");
                string path = Path.GetFullPath(args[index + 1]);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var config = new MatchConfig();
                var engine = new MatchEngine(config);
                using (var writer = new BootstrapReplayWriter(path, config))
                {
                    writer.Write(engine.GetSnapshot());
                    while (!engine.Clock.Completed) engine.Step(writer.Write);
                }
                BootstrapReplayReader.ReadFinal(path);
                Debug.Log("BOOTSTRAP_COMPLETED ticks=" + engine.State.tick + " replay=" + path);
                Application.Quit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Application.Quit(1);
            }
        }
    }
}
