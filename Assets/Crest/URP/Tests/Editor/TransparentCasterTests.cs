// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
public partial class MaskedExampleTests
{
    [UnityTest,Timeout(180000)] public IEnumerator TransparentOwnCasterRetainsOriginalOpaqueAlphaSemantics()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();Setup("TransparentOwnCaster");
        _material=Own(new Material(Shader.Find("Crest/Examples/UnderwaterTransparentSurfaceShader")));_material.SetColor("_Color",new Color(.8f,.02f,.01f,0));_material.SetShaderPassEnabled("ShadowCaster",true);
        var receiver=Own(new Material(Shader.Find("Universal Render Pipeline/Lit")));receiver.SetColor("_BaseColor",Color.white);receiver.SetFloat("_Smoothness",0);
        _quad.GetComponent<Renderer>().sharedMaterial=receiver;_quad.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        var caster=Own(GameObject.CreatePrimitive(PrimitiveType.Cube));caster.layer=5;caster.transform.position=new Vector3(-1,0,6);caster.transform.localScale=Vector3.one*1.5f;
        var renderer=caster.GetComponent<Renderer>();renderer.sharedMaterial=_material;renderer.receiveShadows=false;
        var light=Own(new GameObject("Transparent own-caster light",typeof(Light))).GetComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.shadows=LightShadows.Hard;light.shadowStrength=1;light.shadowBias=.01f;light.shadowNormalBias=0;light.transform.rotation=Quaternion.LookRotation(new Vector3(.3f,0,1));RenderSettings.sun=light;
        _camera.enabled=true;for(int warm=0;warm<4;warm++)yield return null;_camera.enabled=false;
        var on=Capture("AlphaZeroShadow");renderer.shadowCastingMode=ShadowCastingMode.Off;var off=Capture("ShadowOff");renderer.shadowCastingMode=ShadowCastingMode.On;var restored=Capture("ShadowRestored");
        int changed=on.Zip(off,(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>20?1:0).Sum();int restore=on.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum();
        File.AppendAllText(_report,$"Alpha0 own caster changed={changed}; Restore={restore}; explicit pass enabled on temporary material; no source material/renderer flag acceptance or bake.\n");Assert.That(changed,Is.GreaterThan(1000));Assert.That(restore,Is.Zero);
        _material.SetShaderPassEnabled("ShadowCaster",false);Assert.That(Capture("PassDisabled"),Is.EqualTo(off));_material.SetShaderPassEnabled("ShadowCaster",true);Assert.That(Capture("PassRestored"),Is.EqualTo(on));
    }
}
