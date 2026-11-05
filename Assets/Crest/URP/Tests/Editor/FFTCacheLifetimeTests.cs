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
    Texture[] _evictedTextures;
    RenderTexture _queueCopy;
    OceanRenderer _editorOcean;
    static void Cleanup()=>typeof(FFTCompute).GetMethod("CleanUpAll",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
    static RenderTexture SharedOutput()
    {
        var cache=(System.Collections.IDictionary)typeof(FFTCompute).GetField("_generators",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        return cache.Values.Cast<object>().Select(g=>(RenderTexture)typeof(FFTCompute).GetField("_waveBuffers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(g)).First(t=>t!=null&&t.IsCreated());
    }
    static RenderTexture Generate(OceanWaveSpectrum spectrum,float time,float wind=10)
    {
        using(var cmd=new CommandBuffer())
        {
            var result=FFTCompute.GenerateDisplacements(cmd,32,60,.145f,0,wind,time,spectrum,false);
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
    static Texture[] GeneratorTextures(float wind)
    {
        var cache=(System.Collections.IDictionary)typeof(FFTCompute).GetField("_generators",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        var generator=cache.Values.Cast<object>().Single(g=>(float)typeof(FFTCompute).GetField("_windSpeed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(g)==wind);
        return typeof(FFTCompute).GetFields(BindingFlags.Instance|BindingFlags.NonPublic)
            .Where(f=>typeof(Texture).IsAssignableFrom(f.FieldType)).Select(f=>(Texture)f.GetValue(generator)).Where(t=>t!=null).ToArray();
    }
    static void Rekey(OceanWaveSpectrum spectrum,float from,float to)=>FFTCompute.OnGenerationDataUpdated(32,60,.145f,0,from,spectrum,.145f,0,to,spectrum);
    [UnityTest]
    public IEnumerator RekeyConvergenceDisposesOldResourcesAndUpdatesFrozenConditions()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Interactions.unity");
        yield return new EnterPlayMode();OceanRenderer.EnableGraphicsInBatchMode=true;
        for(int i=0;i<16;i++)yield return null;
        foreach(var wave in Object.FindObjectsByType<ShapeWaves>())Object.Destroy(wave.gameObject);
        for(int i=0;i<8;i++)yield return null;Assert.That(FFTCompute.GeneratorCount,Is.Zero);
        _spectrum=ScriptableObject.CreateInstance<OceanWaveSpectrum>();
        var a=Generate(_spectrum,123.5f,10);var b=Generate(_spectrum,123.5f,20);
        Assert.That(FFTCompute.GeneratorCount,Is.EqualTo(2));_evictedTextures=GeneratorTextures(10);
        Assert.That(_evictedTextures.Length,Is.EqualTo(10));byte[] originalA=null,originalB=null;
        yield return Read(a,data=>originalA=data);yield return Read(b,data=>originalB=data);
        Rekey(_spectrum,10,20);for(int i=0;i<8;i++)yield return null;
        int aliveAfterEviction=_evictedTextures.Count(t=>t!=null);int cacheAfterMerge=FFTCompute.GeneratorCount;
        var target=Generate(_spectrum,123.5f,20);bool targetRetained=ReferenceEquals(target,b);byte[] targetData=null;
        yield return Read(target,data=>targetData=data);
        var oldConsumer=Generate(_spectrum,123.5f,10);byte[] oldData=null;yield return Read(oldConsumer,data=>oldData=data);
        bool oldRecovered=oldData.SequenceEqual(originalA),targetUnchanged=targetData.SequenceEqual(originalB);
        Cleanup();for(int i=0;i<4;i++)yield return null;
        int orphanedAfterCleanup=_evictedTextures.Count(t=>t!=null);
        var moving=Generate(_spectrum,456.5f,30);byte[] beforeMove=null;yield return Read(moving,data=>beforeMove=data);
        Rekey(_spectrum,30,40);var adapted=Generate(_spectrum,456.5f,40);bool reused=ReferenceEquals(adapted,moving);
        byte[] adaptedData=null;yield return Read(adapted,data=>adaptedData=data);
        Cleanup();var reference=Generate(_spectrum,456.5f,40);byte[] freshData=null;yield return Read(reference,data=>freshData=data);
        bool correctFrozenUpdate=adaptedData.SequenceEqual(freshData),changed=!adaptedData.SequenceEqual(beforeMove);
        File.WriteAllText("Evidence/P10-Rekey-"+SystemInfo.graphicsDeviceType+".txt",
            $"Unity={Application.unityVersion}; API={SystemInfo.graphicsDeviceType}\nOldTextureCount=10\nAliveAfterEviction={aliveAfterEviction}\nCacheAfterMerge={cacheAfterMerge}\nTargetRetained={targetRetained}\nTargetBytesUnchanged={targetUnchanged}\nOldConsumerBytesRecovered={oldRecovered}\nOrphanedAfterCleanup={orphanedAfterCleanup}\nAdaptedTextureReused={reused}\nFrozenUpdateMatchesFresh={correctFrozenUpdate}\nChangedWindChangesBytes={changed}\n");
        Assert.That(targetRetained&&targetUnchanged&&oldRecovered&&reused,Is.True);
        Assert.That(aliveAfterEviction,Is.Zero,"Removed generator must release its ten owned native textures");
        Assert.That(orphanedAfterCleanup,Is.Zero);Assert.That(cacheAfterMerge,Is.EqualTo(1));
        Assert.That(correctFrozenUpdate&&changed,Is.True,"Changed conditions must regenerate at unchanged time");
    }
    [UnityTest]
    public IEnumerator RekeyKeepsAlreadyQueuedFFTCommandsValidInEditMode()
    {
        OceanRenderer.EnableGraphicsInBatchMode=true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Interactions.unity");
        foreach(var wave in Object.FindObjectsByType<ShapeWaves>())wave.enabled=false;
        if(OceanRenderer.Instance==null)
        {
            _editorOcean=Object.FindObjectsByType<OceanRenderer>().Single();
            typeof(OceanRenderer).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(_editorOcean,null);
        }
        Assert.That(OceanRenderer.Instance,Is.Not.Null);
        Cleanup();_spectrum=ScriptableObject.CreateInstance<OceanWaveSpectrum>();
        RenderTexture recovered;
        using(var cmd=new CommandBuffer())
        {
            var a=FFTCompute.GenerateDisplacements(cmd,32,60,.145f,0,10,123.5f,_spectrum,false);
            _evictedTextures=GeneratorTextures(10);
            _queueCopy=new RenderTexture(a.descriptor);_queueCopy.Create();cmd.CopyTexture(a,_queueCopy);
            FFTCompute.GenerateDisplacements(cmd,32,60,.145f,0,20,123.5f,_spectrum,false);
            Rekey(_spectrum,10,20);
            recovered=FFTCompute.GenerateDisplacements(cmd,32,60,.145f,0,10,123.5f,_spectrum,false);
            Graphics.ExecuteCommandBuffer(cmd);
        }
        byte[] queued=null,fresh=null;yield return Read(_queueCopy,b=>queued=b);yield return Read(recovered,b=>fresh=b);
        bool exact=queued.SequenceEqual(fresh);Cleanup();yield return null;
        File.WriteAllText("Evidence/P10-Rekey-Queued-"+SystemInfo.graphicsDeviceType+".txt",$"PlayMode={Application.isPlaying}\nAll16QueuedLayersEqualRecreated={exact}\n");
        Assert.That(exact,Is.True,"Eviction must not invalidate commands recorded earlier in the same buffer");
    }
    static void DestroyOwned(Object value)
    {
        if(value==null)return;if(value is RenderTexture texture)texture.Release();
        if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);
    }
    [UnityTearDown]
    public IEnumerator Restore()
    {
        Cleanup();foreach(var texture in _evictedTextures??Array.Empty<Texture>())DestroyOwned(texture);
        _evictedTextures=null;DestroyOwned(_queueCopy);_queueCopy=null;DestroyOwned(_spectrum);
        if(_editorOcean!=null&&!Application.isPlaying)typeof(OceanRenderer).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(_editorOcean,null);
        _editorOcean=null;OceanRenderer.EnableGraphicsInBatchMode=false;
        if(Application.isPlaying)yield return new ExitPlayMode();
    }
}
