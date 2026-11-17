namespace TuringSignalRelay
{
    public static class VisibilitySystem
    {
        public static bool Visible(RelayMap map, RelayState state, int side, int x, int y)
        {
            var home = map.Headquarters(side);
            var target = new GridPosition(x, y);
            if (GridMap.InRadius(new GridPosition(home.x, home.y), target, state.ruleParameters.headquartersVision)) return true;
            foreach (var unit in state.units)
                if (unit.side == side && GridMap.InRadius(new GridPosition(unit.x, unit.y), target, Radius(unit.role, state.ruleParameters))) return true;
            return false;
        }

        public static int Radius(UnitRole role, RelayRuleParameters parameters = null)
        {
            parameters = parameters ?? new RelayRuleParameters();
            return role == UnitRole.Communicator ? parameters.communicatorVision : role == UnitRole.Engineer ? parameters.engineerVision : parameters.transporterVision;
        }

        public static int[] Masks(RelayMap map, RelayState state)
        {
            var masks = new int[96 * 96];
            for (int side = 0; side < 2; side++)
            {
                var home = map.Headquarters(side);
                Paint(masks, side, home.x, home.y, state.ruleParameters.headquartersVision);
            }
            foreach (var unit in state.units) Paint(masks, unit.side, unit.x, unit.y, Radius(unit.role, state.ruleParameters));
            return masks;
        }

        private static void Paint(int[] masks, int side, int x, int y, int radius)
        {
            for (int cy = System.Math.Max(0, y - radius); cy <= System.Math.Min(95, y + radius); cy++)
                for (int cx = System.Math.Max(0, x - radius); cx <= System.Math.Min(95, x + radius); cx++)
                    if ((cx - x) * (cx - x) + (cy - y) * (cy - y) <= radius * radius) masks[cy * 96 + cx] |= 1 << side;
        }
    }
}
