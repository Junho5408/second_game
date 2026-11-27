namespace Origami.Core.Game
{
    /// <summary>Workshop mode (C-151, R-003). Seated additionally has a "book open" flag (<see cref="GameSnapshot.BookOpen"/>).</summary>
    public enum WorkshopMode
    {
        Walking = 1,
        Seated = 2,
        Bookshelf = 3,
        Counter = 4,
        SleepConfirm = 5,
        /// <summary>Run over; only <see cref="OrigamiGame.NewRun"/> is accepted.</summary>
        Ended = 6,
    }
}
