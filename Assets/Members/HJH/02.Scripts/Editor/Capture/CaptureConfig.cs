using System;
using UnityEngine;

namespace HJH.CaptureTool
{
    [Serializable]
    public class CaptureConfig
    {
        public string scene;
        public string outDir;
        public int width;
        public int height;
        public bool play;
        public float maxSeconds;
        public CaptureShot[] shots;
    }

    [Serializable]
    public class CaptureShot
    {
        public string name;
        public string mode;

        public string target;
        public string space;
        public Vector3 offset;
        public Vector3 lookAt;
        public Vector3 position;
        public Vector3 euler;
        public float fov;
        public float orthoSize;

        public float delay;
        public string waitState;
        public string forceState;
        public float timeout;
    }
}
