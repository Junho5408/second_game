using System.Collections.Generic;
using Origami.Core.Room;
using Origami.Runtime.Systems;
using UnityEngine;
using UnityEngine.Rendering;

namespace Origami.Runtime.Gameplay
{
    /// <summary>
    /// The character drawn from code meshes (C-193, CONTENT §6): capsule body, sphere head, apron band, a nose to show the
    /// facing. Height <see cref="RoomLayout.CharacterHeight"/>. Walking bobs the body; sitting lowers it onto the chair.
    /// No Update — <see cref="OrigamiApp"/> calls <see cref="SetPose"/>.
    /// </summary>
    public sealed class CharacterView : MonoBehaviour
    {
        private const float BodyRadius = 0.26f, BodyHeight = 1.25f, HeadRadius = 0.19f;
        private const float BobHeight = 0.04f, SitDrop = 0.42f;

        private static readonly Color BodyColor = new Color(0.36f, 0.52f, 0.78f);
        private static readonly Color HeadColor = new Color(0.98f, 0.84f, 0.7f);
        private static readonly Color ApronColor = new Color(0.93f, 0.62f, 0.32f);
        private static readonly Color NoseColor = new Color(0.9f, 0.55f, 0.45f);

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Material> _materials = new List<Material>();
        private Transform _body;
        private bool _built;

        /// <summary>World (x, z) of the last pose.</summary>
        internal Vector2 Position { get; private set; }

        internal float Yaw { get; private set; }

        internal bool Sitting { get; private set; }

        /// <summary>Places the character: world (x, z), yaw in degrees (0 = north, 90 = east), sitting, walk phase (radians, bob).</summary>
        public void SetPose(Vector2 xz, float yaw, bool sitting, float walkPhase)
        {
            Build();
            Position = xz;
            Yaw = yaw;
            Sitting = sitting;
            transform.SetPositionAndRotation(new Vector3(xz.x, 0f, xz.y), Quaternion.Euler(0f, yaw, 0f));
            float y = sitting ? -SitDrop : Mathf.Abs(Mathf.Sin(walkPhase)) * BobHeight;
            _body.localPosition = new Vector3(0f, y, 0f);
        }

        private void Awake() => Build();

        private void Build()
        {
            if (_built) return;
            _built = true;
            var body = new GameObject("Body");
            body.transform.SetParent(transform, false);
            _body = body.transform;
            Part("Torso", MeshFactory.Capsule(BodyRadius, BodyHeight), BodyColor, new Vector3(0f, BodyHeight * 0.5f, 0f));
            Part("Apron", MeshFactory.Box(new Vector3(BodyRadius * 1.7f, 0.5f, 0.06f)), ApronColor, new Vector3(0f, 0.55f, BodyRadius * 0.92f));
            float headY = (float)RoomLayout.CharacterHeight - HeadRadius;
            Part("Head", MeshFactory.Sphere(HeadRadius), HeadColor, new Vector3(0f, headY, 0f));
            Part("Nose", MeshFactory.Sphere(0.045f), NoseColor, new Vector3(0f, headY, HeadRadius * 0.95f));
        }

        private void Part(string name, Mesh mesh, Color color, Vector3 localPos)
        {
            _meshes.Add(mesh);
            var mat = Materials.Lit(color);
            _materials.Add(mat);
            var go = new GameObject(name);
            go.transform.SetParent(_body, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
        }

        private void OnDestroy()
        {
            foreach (var m in _meshes)
                if (m != null) Destroy(m);
            foreach (var m in _materials)
                if (m != null) Destroy(m);
            _meshes.Clear();
            _materials.Clear();
        }
    }
}
