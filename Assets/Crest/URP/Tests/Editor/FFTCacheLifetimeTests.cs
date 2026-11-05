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
    [UnityTearDown]
    public IEnumerator Restore()
    {
        Cleanup();if(_spectrum!=null)Object.Destroy(_spectrum);
        OceanRenderer.EnableGraphicsInBatchMode=false;
        if(Application.isPlaying)yield return new ExitPlayMode();
    }
}
