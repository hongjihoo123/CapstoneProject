using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Members.HJH._02.Scripts.Editor
{
    // 빌드에서 URP/Lit 의 "투명 + 데칼(DBuffer)" 조합을 뺌
    // 이 조합에서 d3d11 컴파일러가 "can't unroll loops marked with loop attribute" 를 내서 빌드가 실패함
    // 투명 표면은 원래 DBuffer 데칼을 안 받음 (URP DBuffer.hlsl 이 투명이면 끔)
    public class StripTransparentDecalVariants : IPreprocessShaders
    {
        private const string ShaderName = "Universal Render Pipeline/Lit";
        private const string PassName = "ForwardLit";

        public int callbackOrder => 0;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader.name != ShaderName || snippet.passName != PassName)
                return;

            int removed = 0;
            for (int i = data.Count - 1; i >= 0; i--)
            {
                if (!IsTransparentDecal(data[i].shaderKeywordSet))
                    continue;

                data.RemoveAt(i);
                removed++;
            }

            if (removed > 0)
                Debug.Log($"[StripTransparentDecalVariants] {ShaderName} {PassName}: {removed}개 조합 제외");
        }

        private static bool IsTransparentDecal(ShaderKeywordSet keywords)
        {
            bool transparent = false;
            bool decal = false;
            foreach (ShaderKeyword keyword in keywords.GetShaderKeywords())
            {
                string name = keyword.name;
                if (name == "_SURFACE_TYPE_TRANSPARENT")
                    transparent = true;
                else if (name.StartsWith("_DBUFFER_MRT"))
                    decal = true;
            }

            return transparent && decal;
        }
    }
}
