using System;

namespace TuringSignalRelay
{
    public enum UnitRole { Communicator, Engineer, Transporter }
    public enum TerrainKind { Grass, Road, River, Mountain }
    public enum CrossingKind { RopeBridge, Ferry, BuiltBridge }

    public struct GridPosition : IEquatable<GridPosition>
    {
        public readonly int X, Y;
        public GridPosition(int x, int y) { X = x; Y = y; }
        public bool Equals(GridPosition other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object other) { return other is GridPosition && Equals((GridPosition)other); }
        public override int GetHashCode() { return X * 101 + Y; }
    }

    [Serializable]
    public sealed class CrossingDefinition
    {
        public int id, fromX, fromY, toX, toY;
        public CrossingKind kind;
        public bool enabled;
        public CrossingDefinition Copy() { return (CrossingDefinition)MemberwiseClone(); }
    }
}
