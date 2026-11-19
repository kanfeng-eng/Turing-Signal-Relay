using System;

namespace TuringSignalRelay
{
    [Serializable]
    public sealed class RelayGameOutcome
    {
        public int[] teamBySide;
        public RelayResult result;
        public bool forfeit;
        // 0/1 indicates a side, 2 indicates both sides failed.
        public int faultSide = -1;
    }

    [Serializable]
    public sealed class RelaySeriesResult
    {
        public string type = "relay-series-result";
        public string status, decision;
        public int winnerTeam = -1;
        public int[] totalScoreHalfPoints = new int[2], bestGameScoreHalfPoints = new int[2], urgentDeliveredCounts = new int[2];
        public RelayGameOutcome[] games;
    }

    public static class RelaySeries
    {
        public static RelaySeriesResult Aggregate(RelayGameOutcome first, RelayGameOutcome second)
        {
            var games = new[] { first, second };
            foreach (var game in games)
                if (game == null || game.result == null || game.result.config == null || game.result.mapHash == null || game.result.finalSnapshot == null ||
                    game.result.finalSnapshot.scoreHalfPoints == null || game.result.finalSnapshot.scoreHalfPoints.Length != 2 || game.teamBySide == null || game.teamBySide.Length != 2 ||
                    game.teamBySide[0] < 0 || game.teamBySide[0] > 1 || game.teamBySide[1] != 1 - game.teamBySide[0] ||
                    (game.forfeit && (game.faultSide < 0 || game.faultSide > 2)) || (!game.forfeit && !game.result.finalSnapshot.completed))
                    throw new ArgumentException("Invalid paired game result.");
            if (first.teamBySide[0] != second.teamBySide[1] || first.result.config.seed != second.result.config.seed ||
                first.result.mapHash != second.result.mapHash || first.result.config.rulesHash != second.result.config.rulesHash ||
                first.result.config.protocolVersion != second.result.config.protocolVersion || first.result.config.durationTicks != second.result.config.durationTicks ||
                first.result.config.featureSet != second.result.config.featureSet || first.result.config.supplementalRulesHash != second.result.config.supplementalRulesHash)
                throw new ArgumentException("Games must share configuration and swap teams.");
            var series = new RelaySeriesResult { games = games, status = "completed", decision = "draw" };
            bool[] failed = new bool[2];
            foreach (var game in games)
            {
                for (int side = 0; side < 2; side++)
                {
                    int team = game.teamBySide[side], score = game.result.finalSnapshot.scoreHalfPoints[side];
                    series.totalScoreHalfPoints[team] += score;
                    series.bestGameScoreHalfPoints[team] = Math.Max(series.bestGameScoreHalfPoints[team], score);
                    foreach (var entry in game.result.finalSnapshot.ledger)
                        if (entry.side == side && (entry.reason == "urgent-delivery" || entry.reason == "assault-delivery")) series.urgentDeliveredCounts[team]++;
                    if (game.forfeit && (game.faultSide == 2 || game.faultSide == side)) failed[team] = true;
                }
            }
            if (failed[0] || failed[1])
            {
                series.status = failed[0] && failed[1] ? "both-forfeit" : "forfeit";
                series.decision = "participant-fault";
                series.winnerTeam = failed[0] && failed[1] ? -1 : failed[0] ? 1 : 0;
                return series;
            }
            int comparison = series.totalScoreHalfPoints[0].CompareTo(series.totalScoreHalfPoints[1]);
            if (comparison != 0) series.decision = "total-score";
            else
            {
                comparison = series.bestGameScoreHalfPoints[0].CompareTo(series.bestGameScoreHalfPoints[1]);
                if (comparison != 0) series.decision = "best-game-score";
                else
                {
                    comparison = series.urgentDeliveredCounts[0].CompareTo(series.urgentDeliveredCounts[1]);
                    if (comparison != 0) series.decision = "urgent-deliveries";
                }
            }
            series.winnerTeam = comparison == 0 ? -1 : comparison > 0 ? 0 : 1;
            return series;
        }
    }
}
