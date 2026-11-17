using System.Text;
using System.Text.RegularExpressions;

namespace TuringSignalRelay.Evaluation;

public sealed record SourceFinding(int Line, string Category, string Symbol);
public sealed record SourceScan(string Version, string SourceHash, SourceFinding[] Findings);

public static class SourceScanner
{
    public static SourceScan Scan(byte[] source)
    {
        string text = new UTF8Encoding(false, true).GetString(source);
        var findings = new List<SourceFinding>();
        var patterns = new[] {
            ("file", @"\b(fopen|freopen|fread|fwrite|ifstream|ofstream|fstream|open|openat)\b"),
            ("network", @"\b(socket|connect|sendto|recvfrom|getaddrinfo|curl_easy_init)\b"),
            ("process", @"\b(fork|vfork|execve|execl|system|popen|posix_spawn)\b"),
            ("thread", @"\b(thread|jthread|pthread_create|clone)\b"),
            ("hardcoded-identity", @"\b(seed|opponentId|opponent_id)\s*==\s*\d+"),
            ("fixed-coordinates", @"\bmoveTo\s*\(\s*[^,\r\n]+,\s*\d+\s*,\s*\d+\s*\)")
        };
        var lines = text.Split('\n');
        for (int index = 0; index < lines.Length && findings.Count < 200; index++)
            foreach (var (category, pattern) in patterns)
            {
                var match = Regex.Match(lines[index], pattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromSeconds(1));
                if (match.Success) findings.Add(new SourceFinding(index + 1, category, match.Value));
            }
        return new SourceScan("source-scan-v1", ContestJson.Hash(source), findings.Take(200).ToArray());
    }
}
