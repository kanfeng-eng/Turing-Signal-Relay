using System;
using System.Collections.Generic;

namespace TuringSignalRelay
{
    [Serializable]
    public sealed class RelayConfig
    {
        public const string ApprovedRulesHash = "d40030db80eeda806c921adcce8f4f963481df53ec93fcc734f8993aa7966318";
        public int protocolVersion = 2;
        public string rulesVersion = "signal-relay-1.0";
        public string featureSet = "movement-plain-report-v1";
        public string rulesHash;
        public string supplementalRulesHash = "";
        public int seed;
        public int durationTicks = 9000;
        public void Validate()
        {
            if (protocolVersion != 2 || rulesVersion != "signal-relay-1.0" || (featureSet != "movement-plain-report-v1" && featureSet != "relay-full-v1") ||
                durationTicks < 1 || durationTicks > 9000 || rulesHash != ApprovedRulesHash)
                throw new ArgumentException("Invalid relay configuration.");
            if (FullRules() && supplementalRulesHash != "dd49f601362b03585fb677833f72382ee8ce69bcccf71ca0c89472bab583b2ed")
                throw new ArgumentException("Approved material ownership supplement is required.");
            if (FullRules() && durationTicks != 9000) throw new ArgumentException("Full-rule games require 9000 ticks.");
            foreach (char digit in rulesHash)
                if (!(digit >= '0' && digit <= '9') && !(digit >= 'a' && digit <= 'f'))
                    throw new ArgumentException("Invalid approved rules digest.");
        }
        public RelayConfig Copy() { return (RelayConfig)MemberwiseClone(); }
        public bool FullRules() { return featureSet == "relay-full-v1"; }
    }

    [Serializable]
    public sealed class RelaySite
    {
        public int id, side, x, y;
        // headquarters, report, depot, hub, central-depot
        public string kind;
        public RelaySite Copy() { return (RelaySite)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class RelayMapData
    {
        public string mapVersion = "map-v1";
        public int width = 96, height = 96;
        // Rows are ordered from y=0 upward, digits are TerrainKind values.
        public string[] rows;
        public RelaySite[] sites;
        public CrossingDefinition[] crossings;
    }

    [Serializable]
    public sealed class RelayCommand
    {
        public int unitId;
        public string action;
        public int x, y, messageId, targetUnitId, ticks = 1;
        public RelayCommand Copy() { return (RelayCommand)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class RelayCommandBatch
    {
        public int protocolVersion;
        public string type;
        public int side, tick;
        public RelayCommand[] actions = new RelayCommand[0];
    }

    [Serializable]
    public sealed class RelayCell
    {
        public int x, y;
        public RelayCell() { }
        public RelayCell(int xValue, int yValue) { x = xValue; y = yValue; }
        public RelayCell Copy() { return (RelayCell)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class RelayUnit
    {
        public int id, side, x, y;
        public UnitRole role;
        public string task = "idle";
        public int targetX, targetY, taskUntilTick, waitingSinceTick = -1, blockedTicks;
        public int edgeX, edgeY, edgeCompleteTick, crossingId = -1;
        public RelayCell[] path = new RelayCell[0];
        public int pathIndex;
        public int actionMessageId = -1, actionTargetUnitId = -1;
        public int taskDuration;
        public int materials, reservedMaterials, suppressedUntilTick, suppressReadyTick;
        public bool stationActive;
        public int decodeMessageId = -1, decodeStartedTick = -1;
        public List<int> messageIds = new List<int>();
        public RelayUnit Copy()
        {
            var result = (RelayUnit)MemberwiseClone();
            result.path = Array.ConvertAll(path, value => value.Copy());
            result.messageIds = new List<int>(messageIds);
            return result;
        }
    }

    [Serializable]
    public sealed class RelayMessage
    {
        public int id, side, x, y, createdTick;
        public int carrierId = -1, deliveredTick = -1;
        public string kind = "plain";
        public int deadlineTicks = 600, sharedTaskId = -1, stationId = -1;
        public bool decoded, expired;
        public int secondX, secondY, progressStartedTick = -1;
        public RelayMessage Copy() { return (RelayMessage)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class RelayScoreEntry
    {
        public int tick, side, messageId, ageTicks, baseHalfPoints = 20, awardedHalfPoints;
        public string reason = "plain-delivery";
        public RelayScoreEntry Copy() { return (RelayScoreEntry)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class RelayResource
    {
        public int id, side, x, y, amount;
        public string kind = "depot";
        public int availableTick;
        public RelayResource Copy() { return (RelayResource)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class RelayFixedStation
    {
        public int id, side, x, y, suppressedUntilTick;
        public RelayFixedStation Copy() { return (RelayFixedStation)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class RelayBuiltBridge
    {
        public int crossingId, side, completedTick;
        public RelayBuiltBridge Copy() { return (RelayBuiltBridge)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class RelayRadioNode
    {
        public int id, side, x, y, radius;
        public string kind;
        public bool active, connectedToHome;
        public RelayRadioNode Copy() { return (RelayRadioNode)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class RelayRadioLink
    {
        public int from, to;
        public RelayRadioLink Copy() { return (RelayRadioLink)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class RelayCommandResult
    {
        public int side, unitId;
        public string action, reason;
        public bool accepted;
    }

    [Serializable]
    public sealed class RelayState
    {
        public int protocolVersion = 2;
        public string type = "relay-snapshot";
        public int tick;
        public bool completed;
        public int[] scoreHalfPoints = new int[2];
        public List<RelayUnit> units = new List<RelayUnit>();
        public List<RelayMessage> messages = new List<RelayMessage>();
        public List<RelayScoreEntry> ledger = new List<RelayScoreEntry>();
        public List<RelayResource> resources = new List<RelayResource>();
        public List<RelayFixedStation> stations = new List<RelayFixedStation>();
        public List<RelayBuiltBridge> bridges = new List<RelayBuiltBridge>();
        public List<RelayRadioNode> nodes = new List<RelayRadioNode>();
        public List<RelayRadioLink> links = new List<RelayRadioLink>();
        public int[] hubMaterials = new int[2], compensationCount = new int[2];
        public int weatherReduction;
        public string weather = "clear";
        public int eventFocusUnitId = -1, eventFocusMessageId = -1;
        public RelayCommandResult[] commandResults = new RelayCommandResult[0];
        public string[] events = new string[0];
        public RelayState Copy()
        {
            var result = (RelayState)MemberwiseClone();
            result.scoreHalfPoints = (int[])scoreHalfPoints.Clone();
            result.units = units.ConvertAll(value => value.Copy());
            result.messages = messages.ConvertAll(value => value.Copy());
            result.ledger = ledger.ConvertAll(value => value.Copy());
            result.resources = resources.ConvertAll(value => value.Copy());
            result.stations = stations.ConvertAll(value => value.Copy());
            result.bridges = bridges.ConvertAll(value => value.Copy());
            result.nodes = nodes.ConvertAll(value => value.Copy());
            result.links = links.ConvertAll(value => value.Copy());
            result.hubMaterials = (int[])hubMaterials.Clone(); result.compensationCount = (int[])compensationCount.Clone();
            result.commandResults = Array.ConvertAll(commandResults, value => new RelayCommandResult {
                side = value.side, unitId = value.unitId, action = value.action, reason = value.reason, accepted = value.accepted });
            result.events = (string[])events.Clone();
            return result;
        }
    }

    [Serializable]
    public sealed class RelayObservation
    {
        public int protocolVersion = 2;
        public string type = "relay-frame";
        public int tick, side;
        public bool completed;
        public string rulesVersion, featureSet, mapHash;
        public int[] scoreHalfPoints;
        public RelayUnit[] units;
        public RelayUnit[] visibleEnemies;
        public RelayMessage[] messages;
        public RelayResource[] resources;
        public RelayRadioNode[] nodes = new RelayRadioNode[0];
        public RelayRadioLink[] links = new RelayRadioLink[0];
        public RelayFixedStation[] stations = new RelayFixedStation[0];
        public RelayBuiltBridge[] bridges = new RelayBuiltBridge[0];
        public int[] hubMaterials = new int[2];
        public string weather = "clear";
    }
}
