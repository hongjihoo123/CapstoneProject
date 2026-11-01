#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;

namespace Assets.Members.HJH._02.Scripts.Char.Visual
{
    // 플레이 중인 게임 화면을 UI 까지 그대로 mp4 로 녹화 (에디터에서만, 보여주기용. 빌드엔 안 들어감)
    //  - Time.captureFramerate 로 게임 시간을 1/30초씩 진행 → 녹화가 느려도 영상은 정상 속도
    //  - 켜고 끄기: 에디터 브리지 "rec start <경로>" / "rec stop" (EditorTools.GameplayRecorder)
    //  - 게임 오브젝트에 붙는 컴포넌트라 Editor 폴더 밖에 있어야 함
    public class GameplayCapture : MonoBehaviour
    {
        private const int Fps = 30;

        private MediaEncoder _encoder;
        private Texture2D _frame;
        private string _path;

        public int Frames { get; private set; }
        public string FilePath => _path;
        public static float FramesPerSecond => Fps;

        public static GameplayCapture Begin(string path)
        {
            var go = new GameObject("[GameplayCapture]") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            var capture = go.AddComponent<GameplayCapture>();
            capture._path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(capture._path));
            Time.captureFramerate = Fps;
            capture.StartCoroutine(capture.Capture());
            return capture;
        }

        private IEnumerator Capture()
        {
            var wait = new WaitForEndOfFrame();
            while (true)
            {
                yield return wait;
                Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                if (_encoder == null)
                {
                    int w = shot.width & ~1, h = shot.height & ~1;   // 영상은 짝수 크기여야 함
                    _frame = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    _encoder = new MediaEncoder(_path, new VideoTrackAttributes
                    {
                        frameRate = new MediaRational(Fps),
                        width = (uint)w,
                        height = (uint)h,
                        includeAlpha = false,
                        bitRateMode = VideoBitrateMode.High,
                    });
                }
                if (shot.width >= _frame.width && shot.height >= _frame.height)
                {
                    _frame.SetPixels(shot.GetPixels(0, 0, _frame.width, _frame.height));
                    _frame.Apply();
                    _encoder.AddFrame(_frame);
                    Frames++;
                    if (Frames % 90 == 45)   // 확인용: 3초마다 한 장 (Temp/FxBridge/cap_XX.jpg)
                    {
                        Directory.CreateDirectory("Temp/FxBridge");
                        File.WriteAllBytes($"Temp/FxBridge/cap_{Frames / 90:00}.jpg", _frame.EncodeToJPG(70));
                    }
                }
                Destroy(shot);
            }
        }

        private void OnDestroy()
        {
            Time.captureFramerate = 0;
            _encoder?.Dispose();
            _encoder = null;
            if (_frame != null)
                Destroy(_frame);
        }
    }
}
#endif
