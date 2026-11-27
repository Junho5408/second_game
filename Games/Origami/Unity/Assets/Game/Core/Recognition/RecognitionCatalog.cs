using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Folding;

namespace Origami.Core.Recognition
{
    /// <summary>
    /// Injected recognition catalogue (C-110, F-009): model IDs in catalogue order (= tie priority) with their reference
    /// states. The 8 centred D4 outlines of every reference are computed once at construction.
    /// </summary>
    public sealed class RecognitionCatalog
    {
        private readonly string[] _ids;
        private readonly Outline[][] _outlines;
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <exception cref="ArgumentNullException">references is null.</exception>
        /// <exception cref="ArgumentException">null/empty/duplicate ID or null state.</exception>
        public RecognitionCatalog(IEnumerable<KeyValuePair<string, PaperState>> references)
        {
            if (references == null) throw new ArgumentNullException(nameof(references));
            var ids = new List<string>();
            var outlines = new List<Outline[]>();
            foreach (var kv in references)
            {
                if (string.IsNullOrEmpty(kv.Key)) throw new ArgumentException("model ID must not be null or empty", nameof(references));
                if (kv.Value == null) throw new ArgumentException("reference state of " + kv.Key + " is null", nameof(references));
                if (_index.ContainsKey(kv.Key)) throw new ArgumentException("duplicate model ID " + kv.Key, nameof(references));
                _index.Add(kv.Key, ids.Count);
                ids.Add(kv.Key);
                var polys = Outline.WorldPolygons(kv.Value);
                var set = new Outline[D4.Count];
                for (int g = 0; g < D4.Count; g++) set[g] = Outline.FromPolygons(Outline.Centered(polys, g));
                outlines.Add(set);
            }
            _ids = ids.ToArray();
            _outlines = outlines.ToArray();
            ModelIds = new ReadOnlyCollection<string>(_ids);
        }

        /// <summary>Catalogue order.</summary>
        public IReadOnlyList<string> ModelIds { get; }

        /// <summary>Match score s (0..1) of the player state against one model.</summary>
        /// <exception cref="ArgumentNullException">player or modelId is null.</exception>
        /// <exception cref="ArgumentException">unknown model ID.</exception>
        public double Score(PaperState player, string modelId)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (modelId == null) throw new ArgumentNullException(nameof(modelId));
            if (!_index.TryGetValue(modelId, out int index)) throw new ArgumentException("unknown model ID " + modelId, nameof(modelId));
            return Score(CenteredOutline(player), index);
        }

        internal int IndexOf(string id) => id != null && _index.TryGetValue(id, out int i) ? i : -1;

        internal static Outline CenteredOutline(PaperState player) =>
            Outline.FromPolygons(Outline.Centered(Outline.WorldPolygons(player), 0));

        internal double Score(Outline centeredPlayer, int index)
        {
            double best = 0;
            foreach (var o in _outlines[index])
            {
                double v = Outline.IoU(centeredPlayer, o);
                if (v > best) best = v;
            }
            return best;
        }
    }
}
