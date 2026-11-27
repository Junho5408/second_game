using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Folding;

namespace Origami.Core.Catalog
{
    /// <summary>One model of the catalogue (C-120, D-110/D-111). Immutable.</summary>
    public sealed class ModelDefinition
    {
        internal ModelDefinition(int order, string id, string displayName, int basePrice, int pagePrice, bool initiallyOwned,
            int difficulty, string techniques, double coverRotationDeg, FoldStep[] steps, string[] stepTexts)
        {
            if (steps.Length != stepTexts.Length) throw new ArgumentException("steps and texts differ in length for " + id);
            Order = order;
            Id = id;
            DisplayName = displayName;
            BasePrice = basePrice;
            PagePrice = pagePrice;
            InitiallyOwned = initiallyOwned;
            Difficulty = difficulty;
            Techniques = techniques;
            CoverRotationDeg = coverRotationDeg;
            Steps = new ReadOnlyCollection<FoldStep>((FoldStep[])steps.Clone());
            StepTexts = new ReadOnlyCollection<string>((string[])stepTexts.Clone());
            int folds = 0;
            foreach (var s in steps) if (s.Kind == FoldStepKind.Fold) folds++;
            FoldCount = folds;
        }

        /// <summary>1..5 (catalogue order = recognition tie order).</summary>
        public int Order { get; }
        public string Id { get; }
        public string DisplayName { get; }
        public int BasePrice { get; }
        public int PagePrice { get; }
        public bool InitiallyOwned { get; }

        /// <summary>Number of ★ shown in the book.</summary>
        public int Difficulty { get; }

        /// <summary>Cover "쓰는 기법" text.</summary>
        public string Techniques { get; }

        /// <summary>CONTENT §2 reference sequence.</summary>
        public IReadOnlyList<FoldStep> Steps { get; }

        /// <summary>CONTENT §2 "책 설명" (same length as <see cref="Steps"/>).</summary>
        public IReadOnlyList<string> StepTexts { get; }

        /// <summary>= Steps.Count (book actions).</summary>
        public int ActionCount => Steps.Count;

        /// <summary>Number of Fold steps.</summary>
        public int FoldCount { get; }

        /// <summary>Cover / complete picture rotation (tulip, cicada 45; others 0).</summary>
        public double CoverRotationDeg { get; }
    }
}
