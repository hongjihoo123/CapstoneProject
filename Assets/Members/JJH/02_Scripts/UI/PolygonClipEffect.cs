using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Members.JJH._02_Scripts.UI
{
    /// <summary>
    /// UI Graphic(Image 등)의 메시를 다각형 구역 안으로만 잘라낸다.
    /// SlotIconClipper가 런타임에 자동으로 붙여주므로 직접 붙일 필요 없음.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class PolygonClipEffect : BaseMeshEffect
    {
        private RectTransform _region;      // 다각형 좌표의 기준(부모 슬롯)
        private Vector2[] _points;          // 부모 Rect 중앙 기준 로컬 좌표
        private List<int> _triangles;       // 다각형 삼각분할 결과(인덱스)
        private Matrix4x4 _lastMatrix;

        // GC 방지용 재사용 버퍼
        private static readonly List<UIVertex> s_Input = new List<UIVertex>();
        private static readonly List<UIVertex> s_Output = new List<UIVertex>();
        private static readonly List<UIVertex> s_BufA = new List<UIVertex>();
        private static readonly List<UIVertex> s_BufB = new List<UIVertex>();

        public void Setup(RectTransform region, Vector2[] points)
        {
            _region = region;
            _points = points;
            _triangles = (points != null && points.Length >= 3) ? Triangulate(points) : null;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        private void LateUpdate()
        {
            if (_region == null || graphic == null) return;

            // 아이콘과 부모의 상대 변환이 바뀌면 다시 계산
            Matrix4x4 m = transform.worldToLocalMatrix * _region.localToWorldMatrix;
            if (m != _lastMatrix)
            {
                _lastMatrix = m;
                graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || _region == null || _triangles == null || _triangles.Count < 3) return;

            // 다각형 점을 아이콘 로컬 좌표로 변환
            Vector2 center = _region.rect.center;
            var local = new Vector2[_points.Length];
            for (int i = 0; i < _points.Length; i++)
            {
                Vector3 world = _region.TransformPoint(center + _points[i]);
                Vector3 l = transform.InverseTransformPoint(world);
                local[i] = new Vector2(l.x, l.y);
            }

            s_Input.Clear();
            vh.GetUIVertexStream(s_Input);
            s_Output.Clear();

            // 원본의 삼각형마다, 다각형의 삼각형(볼록)마다 잘라서 합친다
            for (int t = 0; t + 2 < s_Input.Count; t += 3)
            {
                for (int c = 0; c + 2 < _triangles.Count; c += 3)
                {
                    Vector2 a = local[_triangles[c]];
                    Vector2 b = local[_triangles[c + 1]];
                    Vector2 d = local[_triangles[c + 2]];

                    // 반전 스케일 등으로 방향이 뒤집혀도 항상 반시계로 맞춤
                    if (Cross(a, b, d) < 0f) { Vector2 tmp = b; b = d; d = tmp; }

                    s_BufA.Clear();
                    s_BufA.Add(s_Input[t]);
                    s_BufA.Add(s_Input[t + 1]);
                    s_BufA.Add(s_Input[t + 2]);

                    var cur = s_BufA;
                    var next = s_BufB;
                    ClipByEdge(cur, next, a, b); Swap(ref cur, ref next);
                    ClipByEdge(cur, next, b, d); Swap(ref cur, ref next);
                    ClipByEdge(cur, next, d, a); Swap(ref cur, ref next);

                    // 결과 다각형을 삼각형 팬으로 출력
                    for (int i = 1; i + 1 < cur.Count; i++)
                    {
                        s_Output.Add(cur[0]);
                        s_Output.Add(cur[i]);
                        s_Output.Add(cur[i + 1]);
                    }
                }
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(s_Output);
        }

        // ---------- Sutherland-Hodgman (볼록 클립 변 1개) ----------
        private static void ClipByEdge(List<UIVertex> src, List<UIVertex> dst, Vector2 a, Vector2 b)
        {
            dst.Clear();
            int n = src.Count;
            if (n == 0) return;

            for (int i = 0; i < n; i++)
            {
                UIVertex cur = src[i];
                UIVertex prev = src[(i + n - 1) % n];

                float dCur = Cross(a, b, cur.position);
                float dPrev = Cross(a, b, prev.position);
                bool curIn = dCur >= 0f;
                bool prevIn = dPrev >= 0f;

                if (curIn)
                {
                    if (!prevIn) dst.Add(LerpVertex(prev, cur, dPrev / (dPrev - dCur)));
                    dst.Add(cur);
                }
                else if (prevIn)
                {
                    dst.Add(LerpVertex(prev, cur, dPrev / (dPrev - dCur)));
                }
            }
        }

        private static void Swap(ref List<UIVertex> x, ref List<UIVertex> y)
        {
            var t = x; x = y; y = t;
        }

        private static UIVertex LerpVertex(UIVertex a, UIVertex b, float t)
        {
            UIVertex v = new UIVertex();
            v.position = Vector3.Lerp(a.position, b.position, t);
            v.normal = Vector3.Lerp(a.normal, b.normal, t);
            v.tangent = Vector4.Lerp(a.tangent, b.tangent, t);
            v.uv0 = Vector4.Lerp(a.uv0, b.uv0, t);
            v.uv1 = Vector4.Lerp(a.uv1, b.uv1, t);
            v.uv2 = Vector4.Lerp(a.uv2, b.uv2, t);
            v.uv3 = Vector4.Lerp(a.uv3, b.uv3, t);
            v.color = Color32.Lerp(a.color, b.color, t);
            return v;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        // ---------- Ear Clipping (오목 다각형 지원) ----------
        private static List<int> Triangulate(Vector2[] p)
        {
            var result = new List<int>();
            var idx = new List<int>();
            for (int i = 0; i < p.Length; i++) idx.Add(i);

            if (SignedArea(p) < 0f) idx.Reverse(); // 반시계로 통일

            while (idx.Count > 3)
            {
                bool earFound = false;
                int n = idx.Count;

                for (int i = 0; i < n; i++)
                {
                    int a = idx[(i + n - 1) % n];
                    int b = idx[i];
                    int c = idx[(i + 1) % n];

                    if (Cross(p[a], p[b], p[c]) <= 0f) continue; // 오목 꼭짓점

                    bool inside = false;
                    for (int j = 0; j < n; j++)
                    {
                        int k = idx[j];
                        if (k == a || k == b || k == c) continue;
                        if (PointInTriangle(p[k], p[a], p[b], p[c])) { inside = true; break; }
                    }
                    if (inside) continue;

                    result.Add(a); result.Add(b); result.Add(c);
                    idx.RemoveAt(i);
                    earFound = true;
                    break;
                }

                if (!earFound) // 꼬인 다각형이면 팬으로 마무리
                {
                    for (int i = 1; i + 1 < idx.Count; i++)
                    {
                        result.Add(idx[0]); result.Add(idx[i]); result.Add(idx[i + 1]);
                    }
                    return result;
                }
            }

            if (idx.Count == 3)
            {
                result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]);
            }
            return result;
        }

        private static float SignedArea(Vector2[] p)
        {
            float area = 0f;
            for (int i = 0; i < p.Length; i++)
            {
                Vector2 a = p[i], b = p[(i + 1) % p.Length];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }

        private static bool PointInTriangle(Vector2 pt, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(a, b, pt);
            float d2 = Cross(b, c, pt);
            float d3 = Cross(c, a, pt);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }
    }
}