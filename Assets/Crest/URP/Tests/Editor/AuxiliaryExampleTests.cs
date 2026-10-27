// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public partial class MaskedExampleTests
{
    sealed class AuxiliaryLightInput : Crest.URP.ICameraGraphPass
    {
        public Color Colour;public Vector3 Direction;
        public UnityEngine.Rendering.Universal.RenderPassEvent Stage=>UnityEngine.Rendering.Universal.RenderPassEvent.BeforeRenderingOpaques;
        public void Record(UnityEngine.Rendering.RenderGraphModule.RenderGraph graph,ContextContainer frame)
        {
            var data=frame.Get<UnityEngine.Rendering.Universal.UniversalLightData>();
            if(data.mainLightIndex<0){Colour=Color.black;return;}
            var light=data.visibleLights[data.mainLightIndex];Colour=light.finalColor;Direction=-light.localToWorldMatrix.GetColumn(2);
        }
    }
    const string OriginalMaterials="Assets/Crest/Crest-Examples/Examples/Materials/";
    void AuxiliaryReport()
    {
        _report=$"Evidence/AuxiliaryExamples-{TestContext.CurrentContext.Test.Name}-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report,"API="+SystemInfo.graphicsDeviceType+"; Target="+W+"x"+H+"; ReversedZ="+SystemInfo.usesReversedZBuffer+"\n");
    }
    Color32[] Aux(string name)=>Capture(name,"AuxiliaryExamples");
    GameObject AuxiliaryQuad(Material material,float z,Vector3 scale,Vector3 position)
    {
        var obj=Own(GameObject.CreatePrimitive(PrimitiveType.Quad));obj.layer=5;obj.transform.position=position+Vector3.forward*z;obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=material;return obj;
    }
    Light AuxiliaryLight()
    {
        var light=Own(new GameObject("Auxiliary example main light",typeof(Light))).GetComponent<Light>();light.type=LightType.Directional;light.intensity=.6f;light.shadows=LightShadows.None;RenderSettings.sun=light;return light;
    }
    void Agreement(string label,Color32[] actual,Color32[] expected)
    {
        int max=0,changed=0;long sum=0;int count=0;
        for(int y=2;y<H-2;y++)for(int x=2;x<W-2;x++)
        {
            int i=y*W+x;var a=actual[i];var b=expected[i];int r=Math.Abs(a.r-b.r),g=Math.Abs(a.g-b.g),blue=Math.Abs(a.b-b.b);int delta=Math.Max(r,Math.Max(g,blue));max=Math.Max(max,delta);sum+=r+g+blue;count+=3;if(delta>2)changed++;
        }
        File.AppendAllText(_report,$"Reference={label}; RGBMax={max}; RGBMean={(double)sum/count}; PixelsOver2={changed}\n");Assert.That(max,Is.LessThanOrEqualTo(2),label);
    }
    [UnityTest,Timeout(180000)]
    public IEnumerator AuxiliaryDepthOnlyOccludesWithoutWritingColour()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();Setup("AuxiliaryExamples");AuxiliaryReport();Mode("_MASK_NONE");
        _camera.enabled=true;for(int i=0;i<4;i++)yield return null;_camera.enabled=false;
        var baseline=Aux("DepthOnlyOff");Check("Off",baseline,(x,y)=>true);
        _quad.SetActive(false);var clear=Aux("DepthOnlyClear");_quad.SetActive(true);
        var depth=Own(new Material(AssetDatabase.LoadAssetAtPath<Material>(OriginalMaterials+"Examples_Material_DepthOnly.mat")));
        Assert.That(depth.renderQueue,Is.LessThan(_material.renderQueue));
        var occluder=AuxiliaryQuad(depth,5,new Vector3(4,6,1),Vector3.left*2);
        _quad.SetActive(false);Assert.That(Aux("DepthOnlyColourUnchanged"),Is.EqualTo(clear),"DepthOnly must not write camera colour");_quad.SetActive(true);
        Check("DepthOnlyLeft",Aux("DepthOnlyLeft"),(x,y)=>x>=W/2,true);
        occluder.transform.position+=Vector3.forward*10;Assert.That(Aux("DepthOnlyBehind"),Is.EqualTo(baseline));
        occluder.transform.position-=Vector3.forward*10;Check("DepthOnlyReturned",Aux("DepthOnlyReturned"),(x,y)=>x>=W/2,true);
        occluder.SetActive(false);Assert.That(Aux("DepthOnlyRestored"),Is.EqualTo(baseline));
    }
    [UnityTest,Timeout(180000)]
    public IEnumerator AuxiliaryMaskDepthControlsLaterGeometryAndPreservesColour()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();Setup("AuxiliaryExamples");AuxiliaryReport();Mode("_MASK_NONE");
        // Original material is queue2450: exercise its depth mutation before a later opaque draw.
        _material.renderQueue=2490;var depth=Own(new Material(AssetDatabase.LoadAssetAtPath<Material>(OriginalMaterials+"Examples_Material_MaskedDepth.mat")));Assert.That(depth.renderQueue,Is.EqualTo(2450));
        var mask=AuxiliaryQuad(depth,5,new Vector3(8,6,1),Vector3.zero);
        _publication=new Publication();Crest.URP.CameraGraphPassRegistry.Register(_publication);Field("_CrestOceanMaskTexture",(x,y)=>x<W/2?1:0);
        _camera.enabled=true;for(int i=0;i<4;i++)yield return null;_camera.enabled=false;
        _quad.SetActive(false);mask.SetActive(false);var clear=Aux("MaskDepthClear");mask.SetActive(true);Assert.That(Aux("MaskDepthColourUnchanged"),Is.EqualTo(clear),"Zero-alpha depth output must preserve camera colour");_quad.SetActive(true);
        var first=Aux("MaskDepthLeft");Check("MaskDepthLeft",first,(x,y)=>x<W/2,true);
        Field("_CrestOceanMaskTexture",(x,y)=>x>=W/2?1:0);Check("MaskDepthRight",Aux("MaskDepthRight"),(x,y)=>x>=W/2,true);
        Field("_CrestOceanMaskTexture",(x,y)=>0);var zero=Aux("MaskDepthZero");Check("MaskDepthZero",zero,(x,y)=>false);Assert.That(zero,Is.EqualTo(clear));
        Field("_CrestOceanMaskTexture",(x,y)=>1);Check("MaskDepthOne",Aux("MaskDepthOne"),(x,y)=>true);
        Field("_CrestOceanMaskTexture",(x,y)=>x<W/2?1:0);Assert.That(Aux("MaskDepthRestored"),Is.EqualTo(first));
        mask.SetActive(false);Check("MaskDepthDisabled",Aux("MaskDepthDisabled"),(x,y)=>true);
    }
    [UnityTest,Timeout(180000)]
    public IEnumerator AuxiliarySphereGradientMatchesOriginalDirectionAndExponentLaw()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();Setup("AuxiliaryExamples");AuxiliaryReport();_quad.SetActive(false);
        _camera.orthographic=false;_camera.fieldOfView=60;_camera.aspect=(float)W/H;
        var material=Own(new Material(AssetDatabase.LoadAssetAtPath<Material>(OriginalMaterials+"Examples_Material_GradientBackground.mat")));
        var away=new Color(.1f,.2f,.3f,1);var towards=new Color(.7f,.4f,.15f,1);material.SetColor("_ColorAwayFromSun",away);material.SetColor("_ColorTowardsSun",towards);
        var sphere=Own(GameObject.CreatePrimitive(PrimitiveType.Sphere));sphere.layer=5;sphere.transform.localScale=Vector3.one*40;sphere.GetComponent<Renderer>().sharedMaterial=material;
        var light=AuxiliaryLight();_camera.enabled=true;for(int i=0;i<4;i++)yield return null;_camera.enabled=false;
        var input=new AuxiliaryLightInput();Crest.URP.CameraGraphPassRegistry.Register(input);
        Color32[] Reference(float exponent)
        {
            var pixels=new Color32[W*H];var sun=input.Direction.normalized;
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;var a=linear?away.linear:away;var b=linear?towards.linear:towards;
            float strength=Mathf.Clamp01(Mathf.Max(input.Colour.r,Mathf.Max(input.Colour.g,input.Colour.b)));
            File.AppendAllText(_report,$"ReferenceInput: Linear={linear}; TargetSRGB={_target.sRGB}; NativeLight={input.Colour}; Strength={strength}\n");
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                var ray=_camera.ViewportPointToRay(new Vector3((x+.5f)/W,(y+.5f)/H,0));float alpha=Mathf.Pow(Mathf.Clamp01(.5f*Vector3.Dot(ray.direction,sun)+.5f),exponent);
                var colour=Color.Lerp(a,b,alpha)*strength;if(linear&&_target.sRGB)colour=colour.gamma;colour.a=1;pixels[y*W+x]=colour;
            }
            return pixels;
        }
        try
        {
        light.transform.rotation=Quaternion.LookRotation(-Vector3.forward);material.SetFloat("_Exponent",1);var first=Aux("GradientTowards");Agreement("TowardsExponent1",first,Reference(1));
        material.SetFloat("_Exponent",3);var exponent=Aux("GradientExponent3");Agreement("TowardsExponent3",exponent,Reference(3));Assert.That(first.Zip(exponent,(a,b)=>a.Equals(b)?0:1).Sum(),Is.GreaterThan(1000));
        light.transform.rotation=Quaternion.LookRotation(Vector3.right);Agreement("SideExponent3",Aux("GradientSide"),Reference(3));
        light.intensity=0;Agreement("LightOff",Aux("GradientOff"),Reference(3));
        light.intensity=.6f;light.transform.rotation=Quaternion.LookRotation(-Vector3.forward);material.SetFloat("_Exponent",1);Assert.That(Aux("GradientRestored"),Is.EqualTo(first));
        }
        finally {Crest.URP.CameraGraphPassRegistry.Unregister(input);}
    }
    [UnityTest,Timeout(180000)]
    public IEnumerator AuxiliaryMaskedNonEmissiveLightingMatchesInstalledUrpLit()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();Setup("AuxiliaryExamples");AuxiliaryReport();Mode("_MASK_NONE");
        _material.SetFloat("_Emission",0);_material.DisableKeyword("_EMISSION_ON");
        var reference=Own(new Material(Shader.Find("Universal Render Pipeline/Lit")));reference.SetColor("_BaseColor",_material.GetColor("_Color"));reference.SetTexture("_BaseMap",Texture2D.whiteTexture);
        var renderer=_quad.GetComponent<Renderer>();var light=AuxiliaryLight();_camera.enabled=true;for(int i=0;i<4;i++)yield return null;_camera.enabled=false;
        Color32[] Compare(string name,float metallic,float smoothness,Vector3 direction,float intensity)
        {
            light.intensity=intensity;light.transform.rotation=Quaternion.LookRotation(direction);
            foreach(var mat in new[]{_material,reference}){mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",smoothness);}
            renderer.sharedMaterial=_material;var actual=Aux("Pbr"+name);renderer.sharedMaterial=reference;var expected=Aux("Lit"+name);Agreement(name,actual,expected);renderer.sharedMaterial=_material;return actual;
        }
        var off=Compare("Off",0,.2f,Vector3.forward,0);Check("PbrOff",off,(x,y)=>false);
        var lit=Compare("Diffuse",0,.2f,Vector3.forward,.6f);Assert.That(lit.Zip(off,(a,b)=>a.Equals(b)?0:1).Sum(),Is.GreaterThan(1000));
        Compare("ObliqueMetal",.4f,.55f,new Vector3(.4f,.2f,1),.6f);
        Compare("RoughMetal",.8f,.1f,new Vector3(.4f,.2f,1),.6f);
        Assert.That(Compare("Restored",0,.2f,Vector3.forward,.6f),Is.EqualTo(lit));
    }
}
