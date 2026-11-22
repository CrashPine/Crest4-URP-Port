// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
public class OriginalLakesRiversTests
{
    RenderTexture _target; string _stem; Vector3 _position; Quaternion _rotation; int _capture;float _scale;
    Vector4[] Read(RenderTexture texture,bool depth=false)
    {
        var read=AsyncGPUReadback.Request(texture,0,TextureFormat.RGBAFloat);read.WaitForCompletion();Assert.That(read.hasError,Is.False);Assert.That(read.layerCount,Is.EqualTo(7));
        var values=Enumerable.Range(0,read.layerCount).SelectMany(i=>read.GetData<Vector4>(i).ToArray()).ToArray();Assert.That(values.All(v=>(float.IsFinite(v.x)||(depth&&float.IsNegativeInfinity(v.x)))&&float.IsFinite(v.y)&&float.IsFinite(v.z)&&float.IsFinite(v.w)),Is.True);return values;
    }
    Color32[] Capture(Camera camera,string state)
    {
        camera.transform.SetPositionAndRotation(_position,_rotation);
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=_target});var previous=RenderTexture.active;var cpu=new Texture2D(640,360,TextureFormat.RGBA32,false);
        try{RenderTexture.active=_target;cpu.ReadPixels(new Rect(0,0,640,360),0,0);cpu.Apply();var pixels=cpu.GetPixels32();File.WriteAllBytes(_stem+"-"+state+".png",cpu.EncodeToPNG());Assert.That(pixels.Count(c=>c.r>245&&c.g<10&&c.b>245),Is.Zero);return pixels;}
        finally{RenderTexture.active=previous;Object.DestroyImmediate(cpu);}
    }
    [UnityTest,Timeout(240000)] public IEnumerator OriginalRiverHeightAndFlowInputsChangeAndRestore()
    {
        _capture=Time.captureFramerate;_scale=Time.timeScale;OceanRenderer.EnableGraphicsInBatchMode=true;EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/LakesAndRivers/Scenes/LakesAndRivers.unity");
        foreach(var shape in Object.FindObjectsByType<ShapeWaves>())shape.enabled=false;
        yield return new EnterPlayMode();Time.captureFramerate=60;Time.timeScale=1;_stem=$"Evidence/OriginalLakesRivers-{SystemInfo.graphicsDeviceType}";
        var ocean=OceanRenderer.Instance;var camera=Camera.main;Assert.That(ocean,Is.Not.Null);Assert.That(camera,Is.Not.Null);ocean.ViewCamera=camera;
        new GameObject("River query pacing").AddComponent<BoatProbesQueryFence>();for(int i=0;i<60;i++)yield return null;
        var heights=Object.FindObjectsByType<RegisterHeightInput>();
        var discoveredFlows=Object.FindObjectsByType<RegisterFlowInput>();
        // Replay the original draw order on re-enable. Equal sort keys can preserve the
        // registration order, which need not match FindObjectsByType enumeration.
        var flowRegistrar=RegisterLodDataInputBase.GetRegistrar(typeof(LodDataMgrFlow));
        var flows=flowRegistrar.Select(entry=>entry.Value).OfType<RegisterFlowInput>().ToArray();
        Assert.That(heights.Length,Is.GreaterThan(0));
        Assert.That(flows,Is.Not.Empty);
        Assert.That(flows,Is.EquivalentTo(discoveredFlows));
        var clock=new GameObject("River fixed ocean clock").AddComponent<TimeProviderCustom>();clock._overrideTime=clock._overrideDeltaTime=clock._paused=true;clock._time=1;clock._deltaTime=0;ocean.PushTimeProvider(clock);Time.timeScale=0;
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.allowMSAA=false;camera.aspect=16f/9;
        for(int i=0;i<16;i++)yield return null;_position=camera.transform.position;_rotation=camera.transform.rotation;
        File.WriteAllText(_stem+".txt",$"Original LakesAndRivers source camera={_position}; Heights={heights.Length}; Flow={flows.Length}; Terrain={Object.FindObjectsByType<Terrain>().Length}; Bodies={Object.FindObjectsByType<WaterBody>().Length}; Clip={ocean.CreateClipSurfaceData}. Original input names="+string.Join(";",heights.Select(h=>h.name))+" / "+string.Join(";",flows.Select(f=>f.name))+". Other ShapeWaves off only in fixture; original terrain/depth retained. Ocean clock1/delta0 +Unity timeScale0, capture60Hz/query fence, depth on/postprocess/MSAA off.\n");
        _target=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);_target.Create();
        var onW=Read(ocean._lodDataSeaDepths.DataTexture,true);var onF=Read(ocean._lodDataFlow.DataTexture);var on=Capture(camera,"on");
        foreach(var h in heights)h.enabled=false;foreach(var f in flows)f.enabled=false;for(int i=0;i<16;i++)yield return null;
        var offW=Read(ocean._lodDataSeaDepths.DataTexture,true);var offF=Read(ocean._lodDataFlow.DataTexture);var off=Capture(camera,"off");
        foreach(var h in heights)h.enabled=true;foreach(var f in flows)f.enabled=true;for(int i=0;i<16;i++)yield return null;
        Assert.That(flowRegistrar.Select(entry=>entry.Value).OfType<RegisterFlowInput>(),Is.EqualTo(flows),
            "Flow restoration must compare matching draw order.");
        var reW=Read(ocean._lodDataSeaDepths.DataTexture,true);var reF=Read(ocean._lodDataFlow.DataTexture);var restored=Capture(camera,"restored");
        double heightDelta=onW.Zip(offW,(a,b)=>(double)Math.Abs(a.y-b.y)).Sum();double flowDelta=onF.Zip(offF,(a,b)=>(double)(Math.Abs(a.x-b.x)+Math.Abs(a.y-b.y))).Sum();float heightRestore=onW.Zip(reW,(a,b)=>Math.Abs(a.y-b.y)).Max();float flowRestore=onF.Zip(reF,(a,b)=>(a-b).magnitude).Max();int changed=on.Zip(off,(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)>6?1:0).Sum();int restore=on.Zip(restored,(a,b)=>a.Equals(b)?0:1).Sum();
        File.AppendAllText(_stem+".txt",$"Samples={onW.Length}; HeightDelta={heightDelta:R}; FlowDelta={flowDelta:R}; HeightRestoreMax={heightRestore:R}; FlowRestoreMax={flowRestore:R}; ChangedPixels={changed}; RestorePixels={restore}\n");
        Assert.That(heightDelta,Is.GreaterThan(1));Assert.That(flowDelta,Is.GreaterThan(1));Assert.That(heightRestore,Is.LessThan(.002));Assert.That(flowRestore,Is.LessThan(.002));Assert.That(changed,Is.GreaterThan(200));Assert.That(restore,Is.Zero);ocean.PopTimeProvider(clock);
    }
    [UnityTest, Timeout(240000)]
    public IEnumerator RiverHeightFlowAndWavesComposeAtFixedPhases()
    {
        _capture = Time.captureFramerate;
        _scale = Time.timeScale;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/LakesAndRivers/Scenes/LakesAndRivers.unity");
        yield return new EnterPlayMode();

        Time.captureFramerate = 60;
        Time.timeScale = 1;
        _stem = $"Evidence/P13-RiverComposition-{SystemInfo.graphicsDeviceType}";
        var ocean = OceanRenderer.Instance;
        var camera = Camera.main;
        Assert.That(ocean, Is.Not.Null);
        Assert.That(camera, Is.Not.Null);
        ocean.ViewCamera = camera;
        new GameObject("River query pacing").AddComponent<BoatProbesQueryFence>();
        yield return Settle(60);

        var depthInputs = RegisterLodDataInputBase.GetRegistrar(typeof(LodDataMgrSeaFloorDepth));
        var flowInputs = RegisterLodDataInputBase.GetRegistrar(typeof(LodDataMgrFlow));
        var heights = depthInputs.Select(entry => entry.Value).OfType<RegisterHeightInput>().ToArray();
        var flows = flowInputs.Select(entry => entry.Value).OfType<RegisterFlowInput>().ToArray();
        Assert.That(heights, Is.Not.Empty);
        Assert.That(flows, Is.Not.Empty);

        var waves = Object.FindObjectsByType<ShapeWaves>().Where(shape => shape.isActiveAndEnabled).ToArray();
        Assert.That(waves, Is.Not.Empty, "The original scene must provide its authored waves.");

        var clock = new GameObject("River fixed phase").AddComponent<TimeProviderCustom>();
        clock._overrideTime = clock._overrideDeltaTime = clock._paused = true;
        clock._time = 1;
        ocean.PushTimeProvider(clock);
        Time.timeScale = 0;
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        camera.allowMSAA = false;
        camera.aspect = 16f / 9;
        yield return Settle();
        var overview = camera.transform.position;
        var riverView = FindRiverView(ocean);
        camera.transform.SetPositionAndRotation(riverView, Quaternion.Euler(90f, 0f, 0f));
        yield return Settle();
        _position = camera.transform.position;
        _rotation = camera.transform.rotation;
        _target = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32);
        Assert.That(_target.Create(), Is.True);
        File.WriteAllText(_stem + ".txt",
            $"HeightInputs={heights.Length}; FlowInputs={flows.Length}; Waves={string.Join(", ", waves.Select(wave => wave.GetType().Name))}; Overview={overview}; RiverCamera={riverView}; LODScale={ocean.Scale}; WindsKph={string.Join(", ", waves.Select(wave => wave.WindSpeed * 3.6f))}; fixed phases 1s and 2s.\n");

        var (depth, flow, wave) = ReadRiverFields(ocean);
        var frame = Capture(camera, "Phase1");
        var tiles = ocean.Root.GetComponentsInChildren<OceanChunkRenderer>()
            .Select(tile => tile.GetComponent<Renderer>()).Where(renderer => renderer != null && !renderer.forceRenderingOff).ToArray();
        Color32[] withoutSurface;
        try
        {
            foreach (var tile in tiles) tile.forceRenderingOff = true;
            withoutSurface = Capture(camera, "SurfaceHidden");
        }
        finally
        {
            foreach (var tile in tiles) tile.forceRenderingOff = false;
        }
        int visibleWater = ChangedPixels(frame, withoutSurface);
        File.AppendAllText(_stem + ".txt", $"SurfacePixels={visibleWater}\n");
        Assert.That(visibleWater, Is.GreaterThan(200), "The selected river view must contain visible water.");
        int overlap = 0, heightAndFlow = 0;
        float concurrentWavePeak = 0;
        for (int i = 0; i < depth.Length; i++)
        {
            if (Mathf.Abs(depth[i].y) <= .02f || new Vector2(flow[i].x, flow[i].y).sqrMagnitude <= .0004f) continue;
            heightAndFlow++;
            concurrentWavePeak = Mathf.Max(concurrentWavePeak, Mathf.Abs(wave[i].y));
            if (Mathf.Abs(wave[i].y) > .02f) overlap++;
        }
        File.AppendAllText(_stem + ".txt", $"OverlappingTexels={overlap}; HeightAndFlow={heightAndFlow}; ConcurrentWavePeak={concurrentWavePeak:R}; HeightPeak={depth.Max(v => Mathf.Abs(v.y)):R}; FlowPeak={flow.Max(v => new Vector2(v.x, v.y).magnitude):R}; WavePeak={wave.Max(v => Mathf.Abs(v.y)):R}; TotalTexels={depth.Length}\n");
        Assert.That(overlap, Is.GreaterThan(0), "Flow, local level and wave height must coexist in the same cascade texels.");

        clock._time = 2;
        yield return Settle();
        var (_, _, phase2Wave) = ReadRiverFields(ocean);
        double temporalWaveDelta = Difference(wave, phase2Wave, WaveHeight);
        int phasePixels = ChangedPixels(frame, Capture(camera, "Phase2"));
        File.AppendAllText(_stem + ".txt", $"Phase2WaveDelta={temporalWaveDelta:R}; Phase2ChangedPixels={phasePixels}\n");
        Assert.That(temporalWaveDelta, Is.GreaterThan(1));
        Assert.That(phasePixels, Is.GreaterThan(200));

        clock._time = 1;
        foreach (var input in heights) input.enabled = false;
        yield return Settle();
        var (noDepth, flowWithoutHeight, waveWithoutHeight) = ReadRiverFields(ocean);
        CheckContribution("HeightOff", Difference(depth, noDepth, WaveHeight), flowWithoutHeight, waveWithoutHeight);
        foreach (var input in heights) input.enabled = true;
        foreach (var input in flows) input.enabled = false;
        yield return Settle();
        var (depthWithoutFlow, noFlow, waveWithoutFlow) = ReadRiverFields(ocean);
        CheckContribution("FlowOff", Difference(flow, noFlow, FlowVector), depthWithoutFlow, waveWithoutFlow);
        foreach (var input in flows) input.enabled = true;
        yield return Settle();
        Assert.That(depthInputs.Select(entry => entry.Value).OfType<RegisterHeightInput>(), Is.EqualTo(heights));
        Assert.That(flowInputs.Select(entry => entry.Value).OfType<RegisterFlowInput>(), Is.EqualTo(flows));

        var (restoredDepth, restoredFlow, restoredWave) = ReadRiverFields(ocean);
        float depthRestore = MaximumDifference(depth, restoredDepth, WaveHeight);
        float flowRestore = MaximumDifference(flow, restoredFlow, FlowVector);
        float waveRestore = MaximumDifference(wave, restoredWave, (a, b) => (a - b).magnitude);
        int restoredPixels = ChangedPixels(frame, Capture(camera, "RestoredPhase1"));
        File.AppendAllText(_stem + ".txt",
            $"RestoreDepthMax={depthRestore:R}; RestoreFlowMax={flowRestore:R}; RestoreWaveMax={waveRestore:R}; RestoreChangedPixels={restoredPixels}\n");
        Assert.That(depthRestore, Is.LessThan(.002f));
        Assert.That(flowRestore, Is.LessThan(.002f));
        Assert.That(waveRestore, Is.LessThan(.002f));
        Assert.That(restoredPixels, Is.Zero);

        void CheckContribution(string phase, double delta, Vector4[] remainingInput, Vector4[] remainingWaves)
        {
            float inputPeak = phase == "HeightOff"
                ? remainingInput.Max(value => new Vector2(value.x, value.y).magnitude)
                : remainingInput.Max(value => Mathf.Abs(value.y));
            float wavePeak = remainingWaves.Max(value => Mathf.Abs(value.y));
            File.AppendAllText(_stem + ".txt", $"{phase}; InputDelta={delta:R}; OtherInputPeak={inputPeak:R}; WavePeak={wavePeak:R}\n");
            Assert.That(delta, Is.GreaterThan(1), phase);
            Assert.That(inputPeak, Is.GreaterThan(.02f), phase);
            Assert.That(wavePeak, Is.GreaterThan(.02f), phase);
        }
    }

    static Vector3 FindRiverView(OceanRenderer ocean)
    {
        var depth = ocean._lodDataSeaDepths.DataTexture;
        var flow = ocean._lodDataFlow.DataTexture;
        Assert.That(flow.width, Is.EqualTo(depth.width));
        var levels = AsyncGPUReadback.Request(depth, 0, TextureFormat.RGBAFloat);
        var currents = AsyncGPUReadback.Request(flow, 0, TextureFormat.RGBAFloat);
        levels.WaitForCompletion();
        currents.WaitForCompletion();
        Assert.That(levels.hasError || currents.hasError, Is.False);
        for (int layer = 0; layer < levels.layerCount; layer++)
        {
            var offsets = levels.GetData<Vector4>(layer);
            var velocities = currents.GetData<Vector4>(layer);
            int sample = -1;
            float strongest = .25f;
            for (int i = 0; i < offsets.Length; i++)
            {
                var depthSample = offsets[i];
                float waterDepth = ocean.SeaLevel + depthSample.y - depthSample.x;
                if (!float.IsFinite(waterDepth) || waterDepth < .25f || Mathf.Abs(depthSample.y) < .1f) continue;
                float speed = new Vector2(velocities[i].x, velocities[i].y).sqrMagnitude;
                if (speed <= strongest) continue;
                strongest = speed;
                sample = i;
            }
            if (sample < 0) continue;

            var lod = ocean._lodTransform._renderData[layer].Current;
            var x = lod._posSnapped.x + (sample % depth.width + .5f - depth.width * .5f) * lod._texelWidth;
            var z = lod._posSnapped.z + (sample / depth.width + .5f - depth.height * .5f) * lod._texelWidth;
            return new Vector3(x, ocean.SeaLevel + offsets[sample].y + 8f, z);
        }
        Assert.Fail("The overview must contain a shared footprint for the authored height and flow.");
        return default;
    }

    (Vector4[] depth, Vector4[] flow, Vector4[] wave) ReadRiverFields(OceanRenderer ocean)
    {
        var depth = ocean._lodDataSeaDepths.DataTexture;
        var flow = ocean._lodDataFlow.DataTexture;
        var wave = ocean._lodDataAnimWaves.DataTexture;
        Assert.That(flow.width, Is.EqualTo(depth.width));
        Assert.That(wave.width, Is.EqualTo(depth.width));
        Assert.That(flow.height, Is.EqualTo(depth.height));
        Assert.That(wave.height, Is.EqualTo(depth.height));
        return (Read(depth, true), Read(flow), Read(wave));
    }

    static IEnumerator Settle(int frames = 16)
    {
        for (int frame = 0; frame < frames; frame++) yield return null;
    }

    static float WaveHeight(Vector4 a, Vector4 b) => Mathf.Abs(a.y - b.y);
    static float FlowVector(Vector4 a, Vector4 b) => new Vector2(a.x - b.x, a.y - b.y).magnitude;
    static double Difference(Vector4[] a, Vector4[] b, Func<Vector4, Vector4, float> measure)
        => a.Zip(b, (left, right) => (double)measure(left, right)).Sum();
    static float MaximumDifference(Vector4[] a, Vector4[] b, Func<Vector4, Vector4, float> measure)
        => a.Zip(b, measure).Max();
    static int ChangedPixels(Color32[] a, Color32[] b)
        => a.Zip(b, (left, right) => Mathf.Abs(left.r - right.r) + Mathf.Abs(left.g - right.g) + Mathf.Abs(left.b - right.b) > 6)
            .Count(changed => changed);

    [UnityTearDown] public IEnumerator Cleanup()
    {
        OceanRenderer.EnableGraphicsInBatchMode=false;if(Application.isPlaying)yield return new ExitPlayMode();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Time.captureFramerate=_capture;Time.timeScale=_scale;if(_target!=null){_target.Release();Object.DestroyImmediate(_target);}
    }
}
