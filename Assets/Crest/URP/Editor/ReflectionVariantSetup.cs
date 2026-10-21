using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crest.URP.Editor
{
    public static class ReflectionVariantSetup
    {
        const string VariantAsset = "Assets/CrestPortDemos/ReflectionVertexVariants.asset";

        public static void Configure()
        {
            var primary = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (primary == null) throw new InvalidOperationException("An active URP asset is required.");
            var includer = GraphicsSettings.GetRenderPipelineSettings<IncludeAdditionalRPAssets>();
            if (includer == null) throw new InvalidOperationException("URP IncludeAdditionalRPAssets settings are unavailable.");
            var variant = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(VariantAsset);
            if (variant == null)
            {
                variant = UnityEngine.Object.Instantiate(primary);
                AssetDatabase.CreateAsset(variant, VariantAsset);
            }
            else EditorUtility.CopySerialized(primary, variant);
            variant.name = "ReflectionVertexVariants";
            var settings = new SerializedObject(variant);
            settings.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerVertex;
            settings.ApplyModifiedPropertiesWithoutUndo();
            string label = string.IsNullOrEmpty(includer.labelToInclude) ? "CrestReflectionVariants" : includer.labelToInclude;
            includer.labelToInclude = label;
            includer.includeAssetsByLabel = true;
            AssetDatabase.SetLabels(variant, new[] { label });
            EditorUtility.SetDirty(variant);
            var global = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Settings/UniversalRenderPipelineGlobalSettings.asset");
            if (global != null) EditorUtility.SetDirty(global);
            AssetDatabase.SaveAssets();
            Debug.Log("CREST_REFLECTION_VARIANTS: per-vertex and per-pixel variants are included through the official additional-RP-asset build settings.");
        }
    }
}
