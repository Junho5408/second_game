namespace Origami.Core.Room
{
    /// <summary>Axis-aligned floor rectangle in world (x, z) metres (C-180, D-130). Min ≤ Max is the caller's responsibility.</summary>
    public readonly struct Box
    {
        public Box(double minX, double minZ, double maxX, double maxZ)
        {
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
        }

        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }
    }
}
