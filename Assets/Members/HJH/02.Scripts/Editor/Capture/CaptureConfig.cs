using System;
using UnityEngine;

namespace HJH.CaptureTool
{
    // 캡처 설정 파일(JSON) 형식. JsonUtility 로 읽는다. 0 이나 빈 값은 "기본값 사용" 으로 본다.
    [Serializable]
    public class CaptureConfig
    {
        public string scene;            // 열 씬 경로 (Assets/...). 비우면 지금 열린 씬 그대로
        public string outDir;           // 저장 폴더 (프로젝트 루트 기준). 기본 Captures
        public int width;               // 기본 1280
        public int height;              // 기본 720
        public bool play;               // true 면 플레이 모드로 들어가서 찍는다
        public float maxSeconds;        // 전체 제한 시간(초). 기본 120. 넘으면 중단
        public CaptureShot[] shots;
    }

    [Serializable]
    public class CaptureShot
    {
        public string name;             // 파일 이름 (확장자 없이)
        public string mode;             // free(기본, 임시 카메라) | game(Game 뷰/메인 카메라 그대로) | scene(Scene 뷰)

        // 시점: target 이 있으면 target 기준, 없으면 position/euler 절대값. 둘 다 없으면 메인 카메라 시점 그대로
        public string target;           // 오브젝트 이름 또는 경로(PlayerRig/Player), "tag:Player" 도 가능
        public string space;            // target(기본, 대상의 Y 회전 기준: +Z 가 대상 정면) | world
        public Vector3 offset;          // 대상 위치에서 카메라까지
        public Vector3 lookAt;          // 대상 위치에서 바라볼 점까지 (예: y 1 = 가슴 높이)
        public Vector3 position;
        public Vector3 euler;
        public float fov;               // 0 이면 메인 카메라 값(없으면 40)
        public float orthoSize;         // 0 보다 크면 직교 투영

        // 플레이 모드 전용
        public float delay;             // 플레이 시작 뒤 이 시간(초, Time.time)이 지나야 찍는다
        public string waitState;        // 대상 Animator 가 이 상태일 때까지 기다린다 (예: Run). 레이어 아무 곳이나
        public string forceState;       // 찍기 전에 대상 Animator 를 이 상태로 강제 재생 (입력 없이 자세 확인용)
        public float timeout;           // waitState 를 기다리는 최대 시간. 기본 10초. 넘으면 이름에 _timeout 붙여 찍는다
    }
}
