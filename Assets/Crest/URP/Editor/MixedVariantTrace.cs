// Read-only compiler trace for the observed original Main failure. MIT.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Rendering;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crest.URP.Editor
{
    static class MixedVariantTrace
    {
        static bool started;
        public static void Record(string phase, Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader.name == "Crest/Ocean" && snippet.shaderType == ShaderType.Fragment &&
                Environment.GetCommandLineArgs().Contains("-crestTraceLocalProbes"))
            {
                var retained = data.Select(variant => variant.shaderKeywordSet.GetShaderKeywords()
                        .Select(keyword => keyword.name).OrderBy(name => name).ToArray())
                    .Where(keys => keys.Contains("_REFLECTION_PROBE_BOX_PROJECTION") || keys.Contains("_REFLECTION_PROBE_ATLAS"))
                    .Select(keys => string.Join(" ", keys)).Distinct().OrderBy(keys => keys).ToArray();
                Debug.Log($"CREST_LOCAL_PROBE_VARIANTS {phase} {snippet.passName} total={data.Count} optional={retained.Length}\n" +
                    string.Join("\n", retained));
                return;
            }
            if (shader.name != "Universal Render Pipeline/Lit" || snippet.passName != "ForwardLit" ||
                !Environment.GetCommandLineArgs().Contains("-crestTraceMixedVariants")) return;
            const string path = "Evidence/P11-Scenes-variant-trace.jsonl";
            if (!started) { File.WriteAllText(path, ""); started = true; }
            var keys = data.Select(d => d.shaderKeywordSet.GetShaderKeywords().Select(k => k.name).OrderBy(k => k).ToArray()).ToArray();
            var expected = new[] { "FOG_EXP", "LIGHTMAP_SHADOW_MIXING", "_ADDITIONAL_LIGHTS", "_MAIN_LIGHT_SHADOWS" };
            if (snippet.shaderType == ShaderType.Fragment) expected = expected.Concat(new[] { "REFLECTION_PROBE_ROTATION" }).ToArray();
            var mixing = keys.Where(k => k.Contains("LIGHTMAP_SHADOW_MIXING") && !k.Contains("LIGHTMAP_ON")).ToArray();
            File.AppendAllText(path, JsonUtility.ToJson(new Trace { phase=phase, stage=snippet.shaderType.ToString(), count=data.Count,
                mixingNoLightmap=mixing.Length, exact=keys.Count(k => k.Length == expected.Length && expected.All(k.Contains)),
                examples=mixing.Take(6).Select(k => string.Join(" ",k)).ToArray() }) + "\n");
        }
        [Serializable] class Trace { public string phase,stage; public int count,mixingNoLightmap,exact; public string[] examples; }
    }
    sealed class MixedVariantTraceBefore : IPreprocessShaders
    {
        public int callbackOrder => int.MinValue;
        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data) => MixedVariantTrace.Record("before-scriptable",shader,snippet,data);
    }
    sealed class MixedVariantTraceAfter : IPreprocessShaders
    {
        public int callbackOrder => int.MaxValue;
        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data) => MixedVariantTrace.Record("after-scriptable",shader,snippet,data);
    }
}
