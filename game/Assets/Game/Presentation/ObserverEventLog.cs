using System;
using System.Text;

namespace TuringSignalRelay
{
    // Adapted from the user-owned DebugUIManager.cs. The trimmed result is retained.
    public sealed class ObserverEventLog
    {
        public string Text { get; private set; }
        private readonly int maxLines;
        private int sourceVersion = -1;
        private int lastTick = -1;

        public ObserverEventLog(int lineLimit)
        {
            if (lineLimit < 1) throw new ArgumentOutOfRangeException("lineLimit");
            maxLines = lineLimit;
            Text = "";
        }

        public void Accept(int source, int tick, string message)
        {
            if (source != sourceVersion || tick < lastTick)
            {
                Text = "";
                lastTick = -1;
                sourceVersion = source;
            }
            if (tick == lastTick) return;
            lastTick = tick;
            if (string.IsNullOrEmpty(message)) return;
            // Bound a single event before concatenation, including untrusted remote event text.
            string bounded = message.Length > 2048 ? message.Substring(0, 2048) : message;
            string[] lines = (Text + "[" + tick + "] " + bounded.Replace("\r", "") + "\n").TrimEnd('\n').Split('\n');
            var result = new StringBuilder();
            for (int i = Math.Max(0, lines.Length - maxLines); i < lines.Length; i++)
                result.Append(lines[i]).Append('\n');
            Text = result.ToString();
        }
    }
}
