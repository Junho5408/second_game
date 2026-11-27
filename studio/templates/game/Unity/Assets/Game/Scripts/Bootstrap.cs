using UnityEngine;

namespace {{GAME}}.Runtime
{
    /// <summary>
    /// Code-first entry point. Runs after the first scene loads (the build has a single empty scene),
    /// creates the camera and the persistent root object that game systems attach to.
    /// </summary>
    public static class Bootstrap
    {
        public const string RootName = "[Bootstrap]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (GameObject.Find(RootName) != null) return;

            if (Camera.main == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = {{ORTHO}};
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
                camGo.transform.position = new Vector3(0f, 0f, -10f);
            }

            var root = new GameObject(RootName);
            Object.DontDestroyOnLoad(root);
        }
    }
}
