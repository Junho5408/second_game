namespace Origami.Core.Screens
{
    /// <summary>Latest message of the flow (C-171). A new message has a larger <see cref="Serial"/> (the Runtime shows it for 2 s).</summary>
    public readonly struct FlowMessage
    {
        private readonly string _text;

        internal FlowMessage(int serial, MessageKind kind, string text)
        {
            Serial = serial;
            Kind = kind;
            _text = text;
        }

        /// <summary>0 before the first message, then +1 per message.</summary>
        public int Serial { get; }

        public MessageKind Kind { get; }

        /// <summary>Message text; "" before the first message (never null).</summary>
        public string Text => _text ?? "";
    }
}
