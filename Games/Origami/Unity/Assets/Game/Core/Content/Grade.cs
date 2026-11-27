namespace Origami.Core.Content
{
    /// <summary>
    /// Quality grade of a finished piece (R-009). The numeric value is the star count.
    /// <c>default(Grade)</c> (0) means "no grade" (e.g. <c>CommandResult.Piece</c> of a non-completion).
    /// </summary>
    public enum Grade
    {
        Star1 = 1,
        Star2 = 2,
        Star3 = 3,
    }
}
