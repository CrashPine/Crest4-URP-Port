// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.IO;
using System.Collections;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
public class ExampleMetaTests
{
    [UnityTest] public IEnumerator OriginalExampleMetaMatchesInstalledLitAlbedoAndEmission()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
        var camera=new GameObject("Meta pipeline camera",typeof(Camera));yield return null;yield return null;
        string report=$"Evidence/ExampleMeta-{SystemInfo.graphicsDeviceType}.txt";File.WriteAllText(report,"Actual Meta pass float output vs installed URP/Lit. Explicit meta extraction, not a lightmapper/baked-scene acceptance.\n");
        var names=new[]{"unity_MetaVertexControl","unity_MetaFragmentControl","unity_LightmapST","unity_DynamicLightmapST"};var old=names.Select(Shader.GetGlobalVector).ToArray();var floats=new[]{"unity_OneOverOutputBoost","unity_MaxOutputValue","unity_UseLinearSpace"};var oldFloats=floats.Select(Shader.GetGlobalFloat).ToArray();
        var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-.9f,-.9f,.5f),new Vector3(.9f,-.9f,.5f),new Vector3(.9f,.9f,.5f),new Vector3(-.9f,.9f,.5f)};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.normals=Enumerable.Repeat(Vector3.back,4).ToArray();mesh.triangles=new[]{0,2,1,0,3,2};
        var rt=new RenderTexture(64,64,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();RenderPipeline.SubmitRenderRequest(camera.GetComponent<Camera>(),new UniversalRenderPipeline.SingleCameraRequest{destination=rt});var cpu=new Texture2D(64,64,TextureFormat.RGBAFloat,false,true);
        Color[] Draw(Material material,bool emission)
        {
            int pass=material.FindPass("Meta");Assert.That(pass,Is.GreaterThanOrEqualTo(0),material.shader.name+" pipeline="+RenderPipelineManager.currentPipeline);
            using(var cmd=new CommandBuffer())
            {
                cmd.SetRenderTarget(rt);cmd.ClearRenderTarget(false,true,Color.clear);cmd.SetViewport(new Rect(0,0,64,64));cmd.SetViewProjectionMatrices(Matrix4x4.identity,Matrix4x4.identity);
                cmd.SetGlobalVector("unity_MetaVertexControl",Vector4.zero);cmd.SetGlobalVector("unity_MetaFragmentControl",emission?new Vector4(0,1,0,0):new Vector4(1,0,0,0));cmd.SetGlobalFloat("unity_OneOverOutputBoost",1);cmd.SetGlobalFloat("unity_MaxOutputValue",8);cmd.SetGlobalFloat("unity_UseLinearSpace",1);
                cmd.DrawMesh(mesh,Matrix4x4.identity,material,0,pass);Graphics.ExecuteCommandBuffer(cmd);
            }
            var prev=RenderTexture.active;try{RenderTexture.active=rt;cpu.ReadPixels(new Rect(0,0,64,64),0,0);cpu.Apply();return cpu.GetPixels();}finally{RenderTexture.active=prev;}
        }
        try
        {
            foreach(var shader in new[]{"Crest/Examples/Masked","Crest/Examples/UnderwaterTransparentSurfaceShader"})
            {
                var candidate=new Material(Shader.Find(shader));var reference=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                try
                {
                    foreach(float metallic in new[]{0f,.8f})foreach(float smooth in new[]{.2f,.9f})
                    {
                        var tint=new Color(.6f,.3f,.1f,1);candidate.SetColor("_Color",tint);reference.SetColor("_BaseColor",tint);candidate.SetFloat("_Metallic",metallic);reference.SetFloat("_Metallic",metallic);candidate.SetFloat(shader.EndsWith("Masked")?"_Smoothness":"_Glossiness",smooth);reference.SetFloat("_Smoothness",smooth);
                        foreach(bool emission in new[]{false,true})
                        {
                            candidate.SetColor("_Color",emission?Color.white:tint);reference.SetColor("_BaseColor",emission?Color.white:tint);
                            if(shader.EndsWith("Masked")&&emission){candidate.EnableKeyword("_EMISSION_ON");reference.EnableKeyword("_EMISSION");candidate.SetColor("_EmissionColor",new Color(.8f,.4f,.2f));reference.SetColor("_EmissionColor",new Color(.8f,.4f,.2f));}
                            else{candidate.DisableKeyword("_EMISSION_ON");reference.DisableKeyword("_EMISSION");}
                            var actual=Draw(candidate,emission);var expected=Draw(reference,emission);float max=actual.Zip(expected,(a,b)=>Mathf.Max(Math.Abs(a.r-b.r),Math.Abs(a.g-b.g),Math.Abs(a.b-b.b),Math.Abs(a.a-b.a))).Max();int covered=actual.Count(c=>c.a>.5f);
                            File.AppendAllText(report,$"Shader={shader}; Metal={metallic}; Smooth={smooth}; Emission={emission}; Covered={covered}; Max={max:R}\n");Assert.That(covered,Is.GreaterThan(2000));Assert.That(max,Is.LessThan(.002));
                            if(!emission||(emission&&shader.EndsWith("Masked")))Assert.That(actual.Max(c=>c.r),Is.GreaterThan(.01f));else Assert.That(actual.Max(c=>Mathf.Max(c.r,c.g,c.b)),Is.Zero);
                        }
                    }
                
                    var baseline=Draw(candidate,false);
                    if(shader.EndsWith("Masked"))
                    {
                        foreach(var mode in new[]{"_MASK_NONE","_MASK_MASKED","_MASK_FILL"})
                        {
                            foreach(var keyword in new[]{"_MASK_NONE","_MASK_MASKED","_MASK_FILL"})candidate.DisableKeyword(keyword);candidate.EnableKeyword(mode);
                            Assert.That(Draw(candidate,false),Is.EqualTo(baseline),"Meta ignores camera view mask "+mode);
                        }
                        candidate.SetColor("_Color",new Color(1,1,1,.25f));Assert.That(Draw(candidate,false).Count(c=>c.a>.5f),Is.Zero,"Original alpha cutoff applies to Meta");candidate.SetColor("_Color",Color.white);Assert.That(Draw(candidate,false),Is.EqualTo(baseline));
                        File.AppendAllText(report,"Masked Meta mask modes3 exact; alpha cutoff0 pixels/restoration exact.\n");
                    }
                    else
                    {
                        candidate.SetColor("_Color",new Color(1,1,1,0));Assert.That(Draw(candidate,false),Is.EqualTo(baseline),"Transparent opaque Meta retains original albedo despite alpha0");
                        File.AppendAllText(report,"Transparent Meta alpha0 equals alpha1 exactly.\n");
                    }
                }
                finally{Object.DestroyImmediate(candidate);Object.DestroyImmediate(reference);}
            }
        }
        finally{for(int i=0;i<names.Length;i++)Shader.SetGlobalVector(names[i],old[i]);for(int i=0;i<floats.Length;i++)Shader.SetGlobalFloat(floats[i],oldFloats[i]);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(cpu);Object.DestroyImmediate(mesh);Object.DestroyImmediate(camera);}
    }
    [UnityTearDown] public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
}
