using System;

namespace TuringSignalRelay
{
    [Serializable]
    public sealed class RelayRuleParameters
    {
        public int communicatorCount = 3;
        public int engineerCount = 2;
        public int transporterCount = 2;
        public int plainFirstTick = 200;
        public int plainPeriodTicks = 300;
        public int plainDeadlineTicks = 600;
        public int lateWindowTicks = 300;
        public int plainBaseHalfPoints = 20;
        public int urgentFirstTick = 1800;
        public int urgentPeriodTicks = 1200;
        public int urgentCount = 6;
        public int urgentDeadlineTicks = 300;
        public int urgentFirstHalfPoints = 90;
        public int urgentLaterHalfPoints = 40;
        public int secretFirstTick = 2400;
        public int secretPeriodTicks = 1800;
        public int secretCount = 4;
        public int secretDeadlineTicks = 900;
        public int secretBaseHalfPoints = 50;
        public int broadcastFirstTick = 3600;
        public int broadcastPeriodTicks = 1800;
        public int broadcastCount = 3;
        public int broadcastDeadlineTicks = 600;
        public int broadcastHoldTicks = 30;
        public int broadcastHalfPoints = 40;
        public int assaultTick = 7200;
        public int assaultFirstHalfPoints = 120;
        public int decodeTicks = 50;
        public int depotPeriodTicks = 250;
        public int depotRefreshAmount = 2;
        public int depotCapacity = 6;
        public int centralDepotTick = 3000;
        public int airdropTick = 1200;
        public int airdropAmount = 4;
        public int stormTick = 3600;
        public int stormDurationTicks = 450;
        public int stormReduction = 3;
        public int interferenceTick = 4800;
        public int interferenceDurationTicks = 300;
        public int interferenceReduction = 5;
        public int stationSetupTicks = 20;
        public int communicatorRadius = 9;
        public int relayRadius = 12;
        public int suppressionRadius = 7;
        public int suppressionTicks = 40;
        public int suppressionCooldownTicks = 250;
        public int bridgeCost = 3;
        public int relayCost = 2;
        public int bridgeBuildTicks = 60;
        public int relayBuildTicks = 50;
        public int bridgeScoreLimit = 3;
        public int relayScoreLimit = 4;
        public int bridgeHalfPoints = 40;
        public int relayHalfPoints = 30;
        public int hubOpenTick = 5400;
        public int hubUnits = 3;
        public int hubMaterials = 6;
        public int hubConditionHalfPoints = 80;
        public int remainderLimit = 10;
        public int remainderHalfPoints = 2;
        public int compensationPercent = 15;
        public int compensationLimit = 2;
        public int communicatorSpeed = 120;
        public int engineerSpeed = 90;
        public int transporterSpeed = 70;
        public int roadMultiplier = 10;
        public int grassMultiplier = 14;
        public int mountainMultiplier = 22;
        public int communicatorVision = 5;
        public int engineerVision = 4;
        public int transporterVision = 3;
        public int headquartersVision = 3;
        public int communicatorReports = 1;
        public int transporterReports = 3;
        public int engineerMaterials = 3;
        public int transporterMaterials = 6;
        public int replanTicks = 10;
        public int ropeBridgeTicks = 20;
        public int ferryTicks = 30;
        public int builtBridgeTicks = 20;
        public int transporterCrossingMultiplier = 2;
        public int ferryFirstTick = 2400;
        public int ferryPeriodTicks = 450;
        public int ferryOpenTicks = 150;
        public RelayRuleParameters Copy() { return (RelayRuleParameters)MemberwiseClone(); }
        public void Validate()
        {
            if (communicatorCount + engineerCount + transporterCount > 100)
                throw new ArgumentException("Unit counts exceed the side identifier range.");
            foreach (var field in typeof(RelayRuleParameters).GetFields())
            {
                int value = (int)field.GetValue(this);
                if (value < 1 || value > 9000) throw new ArgumentException("Invalid rule parameter " + field.Name);
            }
            if (ferryOpenTicks > ferryPeriodTicks || roadMultiplier > 100 || grassMultiplier > 100 || mountainMultiplier > 100)
                throw new ArgumentException("Invalid movement parameters.");
            if (compensationPercent > 100 || stormTick + stormDurationTicks > 9000 || interferenceTick + interferenceDurationTicks > 9000)
                throw new ArgumentException("Invalid rule parameter range.");
            if ((8999 - plainFirstTick) / plainPeriodTicks + 1 > 50 || urgentCount > 50 || secretCount > 50 || broadcastCount > 50 ||
                plainBaseHalfPoints % 2 != 0 || secretBaseHalfPoints % 2 != 0)
                throw new ArgumentException("Rule parameters exceed task identifiers or half-point precision.");
        }
    }

}
