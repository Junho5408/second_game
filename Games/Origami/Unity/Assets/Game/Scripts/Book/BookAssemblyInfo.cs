using System.Runtime.CompilerServices;

// The book PlayMode tests (Tests/PlayMode/Book, ORIGAMI-013) read the panel's internal drawing model (page views, buttons,
// fixed texts, palette) instead of screen pixels. This adds no public surface (C-192 stays as specified).
[assembly: InternalsVisibleTo("Origami.Tests.PlayMode")]
