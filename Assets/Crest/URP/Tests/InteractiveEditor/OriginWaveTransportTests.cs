using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[DefaultExecutionOrder(10000)]
public class OriginWaveShiftObservation : MonoBehaviour
{
    public bool Armed, Done;
    public Vector3 Origin;
    public int Frame;
    void FixedUpdate()
    {
        if(Armed && ShiftingOrigin.HasTeleportedThisFrame)
        {
            Origin=ShiftingOrigin.TeleportOriginThisFrame; Frame=Time.frameCount; Done=true; Armed=false;
        }
    }
}

public class OriginWaveTransportTests
{
    class Snapshot
    {
        public Color[][] Waves;
        public float[][] Foam;
        public Vector3[] Centers;
        public float FoamSum, WavePeak, FoamPeak;
        public int FoamNonzero;
    }
    readonly List<Object> _assets=new List<Object>();
    Camera _camera;
    Transform _marker;
    WindZone _wind;
    ShapeGerstner _waves;
    TimeProviderCustom _clock;
    RegisterFoamInput _foamSource;
    ShiftingOrigin _shift;
    OriginWaveShiftObservation _observer;
    Vector3[] _measurement;
    float _scale;
    int _capture, _cases, _packets;
    string _report;
    static readonly FieldInfo ResultTime=typeof(QueryBase).GetField("_queryResultsTime",BindingFlags.Instance|BindingFlags.NonPublic);

    [UnityTest,Timeout(60000)]
    public IEnumerator FrozenWaveCompatibilityAndUnforcedFoamHistorySurviveActualShift()
    {
        _scale=Time.timeScale; _capture=Time.captureFramerate;
        Assert.That(Application.isBatchMode,Is.False,"Requires -Interactive and actual EOF.");
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        var game=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")); game.Show(); game.Focus();
        yield return new EnterPlayMode(); Time.timeScale=1; Time.captureFramerate=60;
        _report=$"Evidence/OriginWaveTransport-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report,"NON-BATCH, original ShiftingOrigin and frozen original Gerstner, actual2048m X/Z shifts, default periodic/control incompatible/compatibility enabled at headings0/30/90/-45. Fixed scale8 isolates origin from LOD filtering. Full seven native animated-wave RGB and localized source-OFF persistent foam layers compared at corresponding relative texels, plus fresh public queries with distinct owners before/after. Tolerances wave .005m, query .025m, foam .005, foam sum1%, centres .002m. Original EOF observed, no private shifts/flag resets. WindZone transform/parameters checked, not a dynamic wind-force test.\n");
        for(int mode=0;mode<7;mode++)
        {
            if(mode>0) { yield return SceneManager.LoadSceneAsync("Assets/CrestPortDemos/Ocean_Calm.unity"); ReleaseAssets(); }
            bool compatibility=mode>=2, preserve=mode!=1; int octave=mode==0?9:13; float heading=mode==3||mode==6?30:mode==4?90:mode==5?-45:0;
            Vector3 expectedShift=mode==6?Vector3.forward*2048:Vector3.right*2048;
            Setup(octave,compatibility,heading,expectedShift);
            yield return Updates(8);
            var ocean=OceanRenderer.Instance;
            ocean.PushTimeProvider(_clock);
            yield return Updates(8);
            _clock._deltaTime=1f/30;
            yield return Updates(4);
            _foamSource.enabled=false; _clock._deltaTime=0;
            yield return Updates(6);
            Assert.That(_foamSource.GetComponent<Renderer>().enabled,Is.False);
            Assert.That(ocean.TimeProvider,Is.SameAs(_clock)); Assert.That(ocean.Scale,Is.EqualTo(8));
            // The original input shader explicitly uses sampler_Crest_linear_repeat; the texture asset itself is Clamp.
            double phase=TexturePeriodRemainder(expectedShift);
            if(mode==0) Assert.That(phase,Is.LessThan(.001)); else Assert.That(phase,Is.GreaterThan(.1),"Long-period control must actually be incompatible with the shift.");
            yield return Measure(-1948561000+mode*10);
            var beforeQueries=(Vector3[])_measurement.Clone(); var before=Capture();
            Assert.That(before.WavePeak,Is.GreaterThan(.01f)); Assert.That(before.FoamPeak,Is.EqualTo(.65f).Within(.002f));
            Assert.That(before.FoamNonzero,Is.GreaterThan(8)); Assert.That(before.FoamNonzero,Is.LessThan(7*384*384/2),"Localized history, not a constant false positive.");
            var beforeMarker=_marker.position; var beforeWind=_wind.transform.position; var windDirection=_wind.transform.forward;
            _observer.Armed=true; _shift.enabled=true;
            float deadline=Time.realtimeSinceStartup+8;
            while(!_observer.Done && Time.realtimeSinceStartup<deadline) yield return new WaitForFixedUpdate();
            Assert.That(_observer.Done,Is.True); Assert.That(_observer.Origin,Is.EqualTo(expectedShift));
            yield return new WaitForEndOfFrame();
            Assert.That(ShiftingOrigin.HasTeleportedThisFrame,Is.False);
            Assert.That(Vector3.Distance(_marker.position,beforeMarker-expectedShift),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(_wind.transform.position,beforeWind-expectedShift),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(_wind.transform.forward,windDirection),Is.LessThan(.000001f));
            Assert.That(_wind.windMain,Is.EqualTo(2.3f)); Assert.That(_wind.windTurbulence,Is.EqualTo(.4f)); Assert.That(_wind.windPulseMagnitude,Is.EqualTo(.2f)); Assert.That(_wind.windPulseFrequency,Is.EqualTo(.6f));
            Assert.That(Shader.GetGlobalInteger(ShiftingOrigin.sp_CrestFloatingOriginWaveCompatibilityMode),Is.EqualTo(compatibility?1:0));
            yield return Updates(8);
            yield return Measure(-1948560999+mode*10);
            var afterQueries=(Vector3[])_measurement.Clone(); var after=Capture();
            float waveError=0,foamError=0,queryError=0,centerError=0;
            for(int layer=0;layer<7;layer++)
            {
                centerError=Mathf.Max(centerError,Vector3.Distance(after.Centers[layer],before.Centers[layer]-expectedShift));
                for(int pixel=0;pixel<before.Waves[layer].Length;pixel++)
                {
                    var a=before.Waves[layer][pixel]; var b=after.Waves[layer][pixel];
                    Assert.That(float.IsFinite(b.r)&&float.IsFinite(b.g)&&float.IsFinite(b.b)&&float.IsFinite(after.Foam[layer][pixel]),Is.True);
                    waveError=Mathf.Max(waveError,Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b));
                    foamError=Mathf.Max(foamError,Mathf.Abs(before.Foam[layer][pixel]-after.Foam[layer][pixel]));
                }
            }
            for(int i=0;i<beforeQueries.Length;i++) queryError=Mathf.Max(queryError,Vector3.Distance(beforeQueries[i],afterQueries[i]));
            float sumError=Mathf.Abs(after.FoamSum-before.FoamSum)/before.FoamSum;
            File.AppendAllText(_report,$"Case={mode}; Octave={octave}; Heading={heading:R}; Origin={expectedShift:F0}; Compatibility={compatibility}; ExpectedPreserved={preserve}; NonIntegerTextureShiftTurns={phase:R}; WavePeak={before.WavePeak:R}; MaxWaveRGBDifference={waveError:R}; MaxQueryDifference={queryError:R}; MaxFoamDifference={foamError:R}; FoamSumRelativeError={sumError:R}; FoamNonzeroBefore={before.FoamNonzero}; FoamNonzeroAfter={after.FoamNonzero}; CentreError={centerError:R}; ShiftFrame={_observer.Frame}; SourceEnabled={_foamSource.enabled}\n");
            Assert.That(centerError,Is.LessThan(.002f)); Assert.That(foamError,Is.LessThan(.005f)); Assert.That(sumError,Is.LessThan(.01f)); Assert.That(_foamSource.enabled,Is.False);
            if(preserve) { Assert.That(waveError,Is.LessThan(.005f)); Assert.That(queryError,Is.LessThan(.025f)); }
            else Assert.That(waveError,Is.GreaterThan(.01f),"Negative control must detect a real phase change without compatibility compensation.");
            _cases++;
        }
        File.AppendAllText(_report,$"Summary; Cases={_cases}; FreshPackets={_packets}; Batch={Application.isBatchMode}\n");
        Assert.That(_cases,Is.EqualTo(7));
    }

    T Own<T>(T asset) where T:Object { _assets.Add(asset); return asset; }
    void Setup(int octave,bool compatibility,float heading,Vector3 expectedShift)
    {
        var ocean=OceanRenderer.Instance; _camera=Camera.main; _camera.transform.position=expectedShift.x!=0?new Vector3(2048.25f,4,.125f):new Vector3(.125f,4,2048.25f);
        foreach(var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled=false;
        var settings=new SerializedObject(ocean); settings.FindProperty("_minScale").floatValue=8; settings.FindProperty("_maxScale").floatValue=8; settings.ApplyModifiedPropertiesWithoutUndo();
        var depth=Own(ScriptableObject.CreateInstance<SimSettingsSeaFloorDepth>()); depth._allowVaryingWaterLevel=false; ocean._simSettingsSeaFloorDepth=depth;
        var foam=Own(ScriptableObject.CreateInstance<SimSettingsFoam>()); foam._prewarm=false; foam._waveFoamStrength=foam._shorelineFoamStrength=foam._foamFadeRate=0; ocean._simSettingsFoam=foam;
        new GameObject("Origin transport numerical fence").AddComponent<BoatProbesQueryFence>();
        var spectrum=Own(ScriptableObject.CreateInstance<OceanWaveSpectrum>()); spectrum._chop=1.6f; spectrum._waveDirectionVariance=0;
        var powers=new SerializedObject(spectrum);
        for(int i=0;i<OceanWaveSpectrum.NUM_OCTAVES;i++) { powers.FindProperty("_powerDisabled").GetArrayElementAtIndex(i).boolValue=i!=octave; powers.FindProperty("_powerLog").GetArrayElementAtIndex(i).floatValue=-2; }
        powers.ApplyModifiedPropertiesWithoutUndo();
        var waveOwner=new GameObject("Frozen original origin wave field"); waveOwner.SetActive(false); _waves=waveOwner.AddComponent<ShapeGerstner>();
        _waves._spectrum=spectrum; _waves._randomSeed=27; _waves._componentsPerOctave=2; _waves._resolution=128;
        _waves._overrideGlobalWindSpeed=true; _waves._windSpeed=120; _waves._overrideGlobalWindDirection=true; _waves._waveDirectionHeadingAngle=heading;
        var waveFields=new SerializedObject(_waves); waveFields.FindProperty("_respectShallowWaterAttenuation").floatValue=0; waveFields.ApplyModifiedPropertiesWithoutUndo(); waveOwner.SetActive(true);
        _marker=new GameObject("Relative wave query points and wind root").transform; _marker.position=_camera.transform.position;
        _wind=new GameObject("Wind state across origin shift").AddComponent<WindZone>(); _wind.transform.SetParent(_marker,false); _wind.transform.localPosition=new Vector3(3,0,5); _wind.transform.localRotation=Quaternion.Euler(0,17,0);
        _wind.windMain=2.3f; _wind.windTurbulence=.4f; _wind.windPulseMagnitude=.2f; _wind.windPulseFrequency=.6f;
        var source=GameObject.CreatePrimitive(PrimitiveType.Plane); Object.DestroyImmediate(source.GetComponent<Collider>());
        source.transform.SetParent(_marker,false); source.transform.localPosition=Vector3.down*4; source.transform.localScale=Vector3.one*.8f;
        var material=Own(new Material(Shader.Find("Crest/Inputs/Foam/Override Foam"))); material.SetFloat("_FoamValue",.65f); source.GetComponent<Renderer>().sharedMaterial=material; _foamSource=source.AddComponent<RegisterFoamInput>();
        _clock=ocean.gameObject.AddComponent<TimeProviderCustom>(); _clock._overrideTime=_clock._overrideDeltaTime=true; _clock._time=1; _clock._deltaTime=0;
        _shift=_camera.gameObject.AddComponent<ShiftingOrigin>(); _shift.enabled=false; _shift._threshold=2048;
        var fields=new SerializedObject(_shift); fields.FindProperty("_waveCompatibiltyMode").boolValue=compatibility; fields.FindProperty("_physicsThreshold").floatValue=0; fields.ApplyModifiedPropertiesWithoutUndo();
        _observer=_camera.gameObject.AddComponent<OriginWaveShiftObservation>();
    }
    IEnumerator Updates(int count)
    {
        int start=OceanRenderer.FrameCount; float deadline=Time.realtimeSinceStartup+8;
        while(OceanRenderer.FrameCount<start+count && Time.realtimeSinceStartup<deadline) yield return null;
        Assert.That(OceanRenderer.FrameCount-start,Is.GreaterThanOrEqualTo(count));
    }
    IEnumerator Measure(int owner)
    {
        var ocean=OceanRenderer.Instance; var points=new[]{_marker.position+Vector3.left*2,_marker.position,_marker.position+Vector3.right*2}; var results=new Vector3[3];
        int fresh=0; float last=-1,deadline=Time.realtimeSinceStartup+8; Vector3[] reference=null;
        while(fresh<8 && Time.realtimeSinceStartup<deadline)
        {
            yield return null;
            int status=ocean.CollisionProvider.Query(owner,0,points,results,null,null);
            Assert.That(status&(int)QueryBase.QueryStatus.PostFailed,Is.Zero);
            if(status!=0) continue;
            if(reference==null) reference=(Vector3[])results.Clone();
            for(int i=0;i<results.Length;i++) { float error=Vector3.Distance(reference[i],results[i]); Assert.That(float.IsFinite(error)&&error<.005f,Is.True,"Frozen field must remain stable within the same stage."); }
            float time=(float)ResultTime.GetValue(ocean.CollisionProvider); if(time>last) { fresh++; last=time; _packets++; }
        }
        Assert.That(fresh,Is.EqualTo(8)); _measurement=(Vector3[])results.Clone();
    }
    Snapshot Capture()
    {
        var ocean=OceanRenderer.Instance;
        Assert.That(ocean.CurrentLodCount,Is.EqualTo(7)); Assert.That(ocean.Scale,Is.EqualTo(8)); Assert.That(ocean.CurrentTime,Is.EqualTo(1));
        var w=AsyncGPUReadback.Request(ocean._lodDataAnimWaves.DataTexture,0,TextureFormat.RGBAFloat); var f=AsyncGPUReadback.Request(ocean._lodDataFoam.DataTexture,0,TextureFormat.RFloat);
        w.WaitForCompletion(); f.WaitForCompletion(); Assert.That(w.hasError||f.hasError,Is.False);
        var snapshot=new Snapshot {Waves=new Color[7][],Foam=new float[7][],Centers=new Vector3[7]};
        for(int layer=0;layer<7;layer++)
        {
            snapshot.Waves[layer]=w.GetData<Color>(layer).ToArray(); snapshot.Foam[layer]=f.GetData<float>(layer).ToArray(); snapshot.Centers[layer]=ocean._lodTransform._renderData[layer].Current._posSnapped;
            for(int i=0;i<snapshot.Waves[layer].Length;i++)
            {
                var p=snapshot.Waves[layer][i]; float value=snapshot.Foam[layer][i];
                Assert.That(float.IsFinite(p.r)&&float.IsFinite(p.g)&&float.IsFinite(p.b)&&float.IsFinite(value),Is.True);
                snapshot.WavePeak=Mathf.Max(snapshot.WavePeak,Mathf.Abs(p.r),Mathf.Abs(p.g),Mathf.Abs(p.b)); snapshot.FoamPeak=Mathf.Max(snapshot.FoamPeak,value); snapshot.FoamSum+=value; if(value>.001f) snapshot.FoamNonzero++;
            }
        }
        return snapshot;
    }
    double TexturePeriodRemainder(Vector3 shift)
    {
        var packets=(Array)typeof(ShapeGerstner).GetField("_waveData",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_waves);
        var cascades=(Array)typeof(ShapeGerstner).GetField("_cascadeParams",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_waves);
        int Start(int index)=>(int)cascades.GetValue(index).GetType().GetField("_startIndex").GetValue(cascades.GetValue(index));
        double max=0; int active=0;
        for(int cascade=0;cascade<cascades.Length-1;cascade++)
        {
            bool nonzero=false;
            for(int index=Start(cascade);index<Start(cascade+1);index++)
            {
                var packet=packets.GetValue(index); var type=packet.GetType();
                var amp=(Vector4)type.GetField("_amp").GetValue(packet); var amp2=(Vector4)type.GetField("_amp2").GetValue(packet);
                for(int lane=0;lane<4;lane++) nonzero|=Mathf.Abs(amp[lane])+Mathf.Abs(amp2[lane])>.000001f;
            }
            if(!nonzero) continue;
            var axis=_waves.PrimaryWaveDirection;
            double period=.5*(1<<cascade); double remainder=Math.Max(Math.Abs(Math.IEEERemainder((shift.x*axis.x+shift.z*axis.y)/period,1)),Math.Abs(Math.IEEERemainder((-shift.x*axis.y+shift.z*axis.x)/period,1)));
            max=Math.Max(max,remainder); active++;
            File.AppendAllText(_report,$"ActiveWaveTexture; Cascade={cascade}; RepeatPeriod={period:R}; ShiftRemainderTurns={remainder:R}\n");
        }
        Assert.That(active,Is.GreaterThan(0)); return max;
    }
    void ReleaseAssets() { foreach(var asset in _assets) if(asset!=null) Object.DestroyImmediate(asset); _assets.Clear(); }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if(_observer!=null) _observer.Armed=false;
        Time.timeScale=_scale; Time.captureFramerate=_capture;
        if(Application.isPlaying) yield return new ExitPlayMode(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); ReleaseAssets();
    }
}
