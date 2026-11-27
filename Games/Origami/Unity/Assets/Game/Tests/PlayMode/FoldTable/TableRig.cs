using System.Linq;
using Origami.Core.Catalog;
using Origami.Runtime.Folding;
using UnityEngine;

namespace Origami.Tests.PlayMode.FoldTableTests
{
    /// <summary>
    /// A <see cref="FoldTable"/> bound to a <see cref="SessionFoldHost"/>, driven only by synchronous
    /// <see cref="FoldTable.ProcessFrame"/> calls with fixed dt (no frame waits, CODING_CONVENTION PlayMode rules).
    /// </summary>
    internal sealed class TableRig
    {
        public const float Dt = 0.02f;

        public readonly GameObject Root;
        public readonly FoldTable Table;
        public readonly SessionFoldHost Host;

        public TableRig()
        {
            Root = new GameObject(FoldTable.ObjectName);
            Table = Root.AddComponent<FoldTable>();
            Host = new SessionFoldHost();
            Table.Bind(Host);
        }

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root);
        }

        /// <summary>tulip steps 1–3 = the diamond base (6 facets, F-005 AC-12).</summary>
        public void DiamondBase()
        {
            Host.ApplySteps(ModelCatalog.Find(ModelIds.Tulip).Steps.Take(3));
            Frame(new PointerFrame());
        }

        public void Frame(PointerFrame p, float dt = Dt) => Table.ProcessFrame(p, dt);

        public static PointerFrame At(float x, float y) =>
            new PointerFrame { HasPaperPoint = true, PaperPoint = new Vector2(x, y) };

        public void Press(float x, float y)
        {
            var p = At(x, y);
            p.LeftDown = true;
            p.LeftHeld = true;
            Frame(p);
        }

        public void Move(float x, float y, bool shift = false, bool ctrl = false, float wheel = 0f)
        {
            var p = At(x, y);
            p.LeftHeld = true;
            p.Shift = shift;
            p.Ctrl = ctrl;
            p.Wheel = wheel;
            Frame(p);
        }

        public void Release(float x, float y, bool shift = false, bool ctrl = false)
        {
            var p = At(x, y);
            p.LeftUp = true;
            p.Shift = shift;
            p.Ctrl = ctrl;
            Frame(p);
        }

        public Transform Child(string path) => Root.transform.Find(path);

        public bool ChildActive(string path)
        {
            var t = Child(path);
            return t != null && t.gameObject.activeSelf;
        }
    }
}
