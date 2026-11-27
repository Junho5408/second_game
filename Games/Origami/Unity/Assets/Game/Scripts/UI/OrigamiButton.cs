using System;

namespace Origami.Runtime.UI
{
    /// <summary>
    /// One on-screen button of the current F-007 screen (see <see cref="OrigamiButtons"/>): a label and the single
    /// <c>OrigamiFlow</c> method it calls. Immutable.
    /// </summary>
    public sealed class OrigamiButton
    {
        private readonly Func<bool> _command;

        /// <param name="label">Text on the button (C-031 labels).</param>
        /// <param name="modelId">Model of a workbench model button (icon drawn next to it); null for other buttons.</param>
        /// <param name="command">The Flow method call; returns the method's result.</param>
        /// <exception cref="ArgumentNullException">label or command is null.</exception>
        public OrigamiButton(string label, string modelId, Func<bool> command)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            ModelId = modelId;
            _command = command ?? throw new ArgumentNullException(nameof(command));
        }

        /// <summary>Text on the button.</summary>
        public string Label { get; }

        /// <summary>Model ID for workbench model buttons, otherwise null.</summary>
        public string ModelId { get; }

        /// <summary>Calls the Flow method (same method as the corresponding key). Returns its result
        /// (false = ignored, e.g. the screen changed since the button list was built).</summary>
        public bool Invoke() => _command();
    }
}
