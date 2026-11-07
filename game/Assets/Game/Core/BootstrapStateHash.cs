using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TuringSignalRelay
{
    // Only the bootstrap fields below are covered; this is not the formal world-state hash.
    public static class BootstrapStateHash
    {
        public static string Compute(MatchConfig config, Snapshot snapshot)
        {
            if (config == null) throw new ArgumentNullException("config");
            config.Validate();
            if (snapshot == null || snapshot.protocolVersion != config.protocolVersion ||
                snapshot.rulesVersion != config.rulesVersion || snapshot.tick < 0 ||
                snapshot.tick > config.durationTicks || snapshot.completed != (snapshot.tick == config.durationTicks) ||
                snapshot.scoreHalfPoints == null || snapshot.scoreHalfPoints.Length != 2)
                throw new ArgumentException("Invalid bootstrap snapshot.");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            using (var sha = SHA256.Create())
            {
                writer.Write(Encoding.ASCII.GetBytes("TSR_BOOTSTRAP_STATE_V1"));
                writer.Write(config.protocolVersion);
                writer.Write(config.ticksPerSecond);
                writer.Write(config.durationTicks);
                writer.Write(config.seed);
                byte[] rules = Encoding.UTF8.GetBytes(config.rulesVersion);
                writer.Write(rules.Length);
                writer.Write(rules);
                writer.Write(snapshot.tick);
                writer.Write(snapshot.completed);
                writer.Write(snapshot.scoreHalfPoints[0]);
                writer.Write(snapshot.scoreHalfPoints[1]);
                writer.Flush();
                return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
