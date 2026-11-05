using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class FFTCacheLifetimeTests
{
    OceanWaveSpectrum _spectrum;
    static void Cleanup()=>typeof(FFTCompute).GetMethod("CleanUpAll",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
    static RenderTexture SharedOutput()
    {
        var cache=(System.Collections.IDictionary)typeof(FFTCompute).GetField("_generators",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        return cache.Values.Cast<object>().Select(g=>(RenderTexture)typeof(FFTCompute).GetField("_waveBuffers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(g)).First(t=>t!=null&&t.IsCreated());
    }
    static RenderTexture Generate(OceanWaveSpectrum spectrum,float time)
    {
        using(var cmd=new CommandBuffer())
        {
            var result=FFTCompute.GenerateDisplacements(cmd,32,60,.145f,0,10,time,spectrum,false);
            Graphics.ExecuteCommandBuffer(cmd);return result;
        }
    }
    static IEnumerator Read(RenderTexture texture,Action<byte[]> done)
    {
        var request=AsyncGPUReadback.Request(texture,0);while(!request.done)yield return null;
        Assert.That(request.hasError,Is.False);
        var bytes=Enumerable.Range(0,texture.volumeDepth).SelectMany(layer=>request.GetData<byte>(layer).ToArray()).ToArray();
        Assert.That(bytes.Length,Is.EqualTo(32*32*8*16));Assert.That(bytes.Any(b=>b!=0),Is.True);
        done(bytes);
    }
    [UnityTest]
    public IEnumerator SharedGeneratorSurvivesOneOwnerAndSameTimeRegeneratesAfterLastCleanup()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Interactions.unity");
        yield return new EnterPlayMode();OceanRenderer.EnableGraphicsInBatchMode=true;
        for(int i=0;i<16;i++)yield return null;
        var source=Object.FindObjectsByType<ShapeFFT>().Single();
        Assert.That(Object.FindObjectsByType<ShapeWaves>().Length,Is.EqualTo(1));
        var first=SharedOutput();Assert.That(FFTCompute.GeneratorCount,Is.EqualTo(1));
        var copy=Object.Instantiate(source.gameObject);for(int i=0;i<8;i++)yield return null;
        Assert.That(FFTCompute.GeneratorCount,Is.EqualTo(1));Assert.That(SharedOutput(),Is.SameAs(first));
        copy.GetComponent<ShapeFFT>().enabled=false;for(int i=0;i<4;i++)yield return null;
        Assert.That(first.IsCreated(),Is.True);Assert.That(SharedOutput(),Is.SameAs(first));
        copy.GetComponent<ShapeFFT>().enabled=true;for(int i=0;i<4;i++)yield return null;
        Object.Destroy(source.gameObject);for(int i=0;i<4;i++)yield return null;
        Assert.That(first!=null&&first.IsCreated(),Is.True,"A remaining shared owner must retain its output");
        Assert.That(SharedOutput(),Is.SameAs(first));
        Object.Destroy(copy);for(int i=0;i<4;i++)yield return null;
        Assert.That(first==null||!first.IsCreated(),Is.True);
        int entriesAfterLastOwner=FFTCompute.GeneratorCount;
        _spectrum=ScriptableObject.CreateInstance<OceanWaveSpectrum>();
        var before=Generate(_spectrum,123.5f);var shared=Generate(_spectrum,123.5f);
        Assert.That(shared,Is.SameAs(before));byte[] initial=null;
        yield return Read(before,b=>initial=b);
        Cleanup();int entriesAfterCleanup=FFTCompute.GeneratorCount;
        var recovered=Generate(_spectrum,123.5f);bool recoveredSameTime=recovered!=null&&recovered.IsCreated();
        byte[] restored=null;if(recoveredSameTime)yield return Read(recovered,b=>restored=b);
        var advanced=Generate(_spectrum,124f);bool advancedValid=advanced!=null&&advanced.IsCreated();
        var report=new StringBuilder($"Unity={Application.unityVersion}; API={SystemInfo.graphicsDeviceType}\nSharedOutputRetainedAfterOneOwner=True\nEntriesAfterLastOwner={entriesAfterLastOwner}\nEntriesAfterExplicitCleanup={entriesAfterCleanup}\nSameTimeRecovered={recoveredSameTime}\nAdvancedTimeValid={advancedValid}\n");
        using(var sha=SHA256.Create())
        {
            report.AppendLine("InitialAll16LayersSHA256="+BitConverter.ToString(sha.ComputeHash(initial)).Replace("-",""));
            if(restored!=null)report.AppendLine("RecoveredAll16LayersSHA256="+BitConverter.ToString(sha.ComputeHash(restored)).Replace("-",""));
        }
        File.WriteAllText("Evidence/P10-FFTCache-"+SystemInfo.graphicsDeviceType+".txt",report.ToString());
        Assert.That(recoveredSameTime,Is.True,"Cleanup must allow regeneration at unchanged simulation time");
        Assert.That(restored,Is.EqualTo(initial),"All16 native FFT layers must regenerate exactly");
        Assert.That(entriesAfterLastOwner,Is.Zero);Assert.That(entriesAfterCleanup,Is.Zero);Assert.That(advancedValid,Is.True);
    }
    [UnityTest]
    public IEnumerator MixedFFTFirstRetainsGerstnerAndCleansEachFamily()=>Mixed(true);
    [UnityTest]
    public IEnumerator MixedGerstnerFirstRetainsFFTAndCleansSwell()=>Mixed(false);
    IEnumerator Mixed(bool fftFirst)
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Interactions.unity");
        yield return new EnterPlayMode();OceanRenderer.EnableGraphicsInBatchMode=true;
        for(int i=0;i<16;i++)yield return null;
        var fft=Object.FindObjectsByType<ShapeFFT>().Single();var fftTexture=SharedOutput();
        var go=new GameObject("Mixed lifetime Gerstner");var gerstner=go.AddComponent<ShapeGerstner>();
        gerstner.enabled=false;gerstner._swell=!fftFirst;gerstner.enabled=true;
        for(int i=0;i<16;i++)yield return null;
        var gerstnerTexture=gerstner._waveBuffers;
        var field=typeof(ShapeWaves).GetField("_activeSpectrum",BindingFlags.Instance|BindingFlags.NonPublic);
        var fftSpectrum=(OceanWaveSpectrum)field.GetValue(fft);var gerstnerSpectrum=(OceanWaveSpectrum)field.GetValue(gerstner);
        Assert.That(fftTexture!=null&&fftTexture.IsCreated()&&gerstnerTexture!=null&&gerstnerTexture.IsCreated(),Is.True);
        Assert.That(fftFirst?fftSpectrum==gerstnerSpectrum:fftSpectrum!=gerstnerSpectrum,Is.True);
        Object.Destroy(fftFirst?fft.gameObject:go);for(int i=0;i<8;i++)yield return null;
        int cacheAfterFirst=FFTCompute.GeneratorCount;
        bool fftAlive=fftTexture!=null&&fftTexture.IsCreated(),gerstnerAlive=gerstnerTexture!=null&&gerstnerTexture.IsCreated();
        bool survivorSpectrum=fftFirst?gerstnerSpectrum!=null:fftSpectrum!=null;
        bool swellReleased=fftFirst||gerstnerSpectrum==null;
        var height=new SampleHeightHelper();int ready=0;double start=Time.realtimeSinceStartupAsDouble;
        while(Time.realtimeSinceStartupAsDouble-start<5&&ready<8)
        {
            height.Init(Vector3.zero,1,true);bool valid=height.Sample(out float h)&&!float.IsNaN(h)&&!float.IsInfinity(h);
            ready=valid?ready+1:0;yield return null;
        }
        Object.Destroy(fftFirst?go:fft.gameObject);for(int i=0;i<8;i++)yield return null;
        bool fftReleased=fftTexture==null||!fftTexture.IsCreated(),gerstnerReleased=gerstnerTexture==null||!gerstnerTexture.IsCreated();
        bool spectraReleased=fftSpectrum==null&&gerstnerSpectrum==null;
        int finalCache=FFTCompute.GeneratorCount;
        File.WriteAllText("Evidence/P10-MixedWaves-"+(fftFirst?"FFTFirst":"GerstnerFirst")+"-"+SystemInfo.graphicsDeviceType+".txt",
            $"Unity={Application.unityVersion}; API={SystemInfo.graphicsDeviceType}\nCacheAfterFirst={cacheAfterFirst}\nFFTAliveAfterFirst={fftAlive}\nGerstnerAliveAfterFirst={gerstnerAlive}\nSurvivorSpectrumAlive={survivorSpectrum}\nSwellReleasedAfterFirst={swellReleased}\nReady={ready}\nFFTReleasedAfterAll={fftReleased}\nGerstnerReleasedAfterAll={gerstnerReleased}\nSpectraReleasedAfterAll={spectraReleased}\nFinalCache={finalCache}\n");
        Assert.That(ready,Is.EqualTo(8));Assert.That(survivorSpectrum,Is.True);
        Assert.That(fftFirst?!fftAlive&&gerstnerAlive:fftAlive&&!gerstnerAlive,Is.True,"Only the destroyed family's native output is released");
        Assert.That(cacheAfterFirst,Is.EqualTo(fftFirst?0:1));Assert.That(swellReleased,Is.True);
        Assert.That(fftReleased&&gerstnerReleased&&spectraReleased,Is.True);Assert.That(finalCache,Is.Zero);
    }
    [UnityTearDown]
    public IEnumerator Restore()
    {
        Cleanup();if(_spectrum!=null)Object.Destroy(_spectrum);
        OceanRenderer.EnableGraphicsInBatchMode=false;
        if(Application.isPlaying)yield return new ExitPlayMode();
    }
}
