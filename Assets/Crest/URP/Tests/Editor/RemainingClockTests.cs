using System.Collections;
using System.IO;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;

// A real manually advanced PlayableDirector graph, with no Timeline/package dependency.
public class ClockContractPlayableAsset : PlayableAsset
{
    public override double duration => 60;
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = Playable.Create(graph);
        playable.SetDuration(60);
        return playable;
    }
}

[DefaultExecutionOrder(10000)]
public class ClockContractObserver : MonoBehaviour
{
    public ITimeProvider Expected;
    public bool Record;
    public int Samples;
    public float MaxShaderError;
    void LateUpdate()
    {
        if (!Record) return;
        var ocean = OceanRenderer.Instance;
        Assert.That(ocean.TimeProvider, Is.SameAs(Expected));
        float error = Mathf.Abs(Shader.GetGlobalFloat(OceanRenderer.sp_crestTime) - Expected.CurrentTime);
        Assert.That(float.IsFinite(error) && error < .00001f, Is.True, "Actual ocean shader clock follows the active original provider.");
        MaxShaderError = Mathf.Max(MaxShaderError, error);
        Samples++;
    }
}

public class RemainingClockTests
{
    float _scale;
    int _capture;
    ClockContractPlayableAsset _asset;
    string _report;
    int _checks;

    [UnityTest]
    public IEnumerator NetworkOffsetAndDirectorFallbackRestoreOriginalClockStack()
    {
        _scale = Time.timeScale;
        _capture = Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Time.timeScale = 1;
        Time.captureFramerate = 60;
        var ocean = OceanRenderer.Instance;
        for (int i = 0; i < 8; i++) yield return null;
        var baseline = ocean.TimeProvider;
        Assert.That(baseline, Is.TypeOf<TimeProviderDefault>());
        var observer = ocean.gameObject.AddComponent<ClockContractObserver>();
        var network = new GameObject("Original network offset clock").AddComponent<TimeProviderNetworked>();
        ocean.PushTimeProvider(network);
        ocean.PushTimeProvider(network);
        _report = $"Evidence/RemainingClock-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Original TimeProviderNetworked and TimeProviderCutscene; real PlayableDirector with a memory-only PlayableAsset/manual graph. No networking/FishNet/transport or Timeline UI claim. Public stack push/pop, network offsets, director seek/pause/stop/disable and component restore. Actual LateUpdate shader _CrestTime observed without GPU fence. Tolerance1e-5s. Cutscene fallback is default Unity time, not the previous network clock; delta remains Unity delta even while manual director time is frozen. Re-enable auto-push is Editor-only coverage (original retry Update is UNITY_EDITOR).\n");
        observer.Expected = network; observer.Record = true;
        foreach (float offset in new[] { 0f, 1.25f, -3.5f, 30f })
        {
            network.TimeOffsetToServer = offset;
            for (int frame = 0; frame < 8; frame++)
            {
                yield return null;
                Check("Network", ocean, network, Time.time + offset);
            }
            File.AppendAllText(_report, $"NetworkOffset={offset:R}; Current={network.CurrentTime:R}; UnityTime={Time.time:R}\n");
        }
        ocean.PopTimeProvider(network);
        Assert.That(ocean.TimeProvider, Is.SameAs(baseline), "Push duplicate must not leave another network instance after one pop.");
        ocean.PushTimeProvider(network);
        network.TimeOffsetToServer = 7;
        observer.Record = false;
        var owner = new GameObject("Original cutscene clock");
        var director = owner.AddComponent<PlayableDirector>();
        director.playOnAwake = false;
        _asset = ScriptableObject.CreateInstance<ClockContractPlayableAsset>();
        director.playableAsset = _asset;
        director.timeUpdateMode = DirectorUpdateMode.Manual;
        director.extrapolationMode = DirectorWrapMode.Hold;
        owner.SetActive(false);
        var cutscene = owner.AddComponent<TimeProviderCutscene>();
        var fields = new SerializedObject(cutscene);
        fields.FindProperty("_playableDirector").objectReferenceValue = director;
        fields.FindProperty("_timeOffset").floatValue = .375f;
        fields.ApplyModifiedPropertiesWithoutUndo();
        owner.SetActive(true);
        yield return null;
        yield return null;
        Assert.That(ocean.TimeProvider, Is.SameAs(cutscene), "Original Start auto-pushes the configured cutscene provider.");
        observer.Expected = cutscene; observer.Record = true;
        for (int i = 0; i < 8; i++)
        {
            yield return null;
            Assert.That(director.state, Is.EqualTo(PlayState.Paused), "Fallback phase explicitly disables director auto-play.");
            Check("DirectorNotPlayingFallback", ocean, cutscene, Time.time);
            Assert.That(Mathf.Abs(cutscene.CurrentTime - network.CurrentTime), Is.EqualTo(7).Within(.00001f));
        }
        director.Play();
        Assert.That(director.state, Is.EqualTo(PlayState.Playing));
        foreach (double seek in new[] { 1.25, 6.5, 2.0 })
        {
            director.time = seek;
            director.Evaluate();
            for (int i = 0; i < 8; i++)
            {
                yield return null;
                Assert.That(director.state, Is.EqualTo(PlayState.Playing));
                Assert.That(director.time, Is.EqualTo(seek).Within(.000001));
                Check("DirectorSeek", ocean, cutscene, (float)seek + .375f);
            }
            File.AppendAllText(_report, $"DirectorSeek={seek:R}; Current={cutscene.CurrentTime:R}; Delta={cutscene.DeltaTime:R}\n");
        }
        director.Pause();
        for (int i = 0; i < 8; i++)
        {
            yield return null;
            Assert.That(director.state, Is.EqualTo(PlayState.Paused));
            Check("PausedFallback", ocean, cutscene, Time.time);
        }
        director.Play(); director.time = 4; director.Evaluate();
        director.enabled = false;
        for (int i = 0; i < 8; i++)
        {
            yield return null;
            Check("DisabledDirectorFallback", ocean, cutscene, Time.time);
        }
        director.enabled = true;
        director.Stop();
        for (int i = 0; i < 8; i++)
        {
            yield return null;
            Check("StoppedFallback", ocean, cutscene, Time.time);
        }
        cutscene.enabled = false;
        Assert.That(ocean.TimeProvider, Is.SameAs(network), "OnDisable restores the exact previous provider.");
        observer.Expected = network;
        for (int i = 0; i < 8; i++) { yield return null; Check("RestoredNetwork", ocean, network, Time.time + 7); }
        // Original Editor-only Update retries Initialise; this is not proof of Player re-enable behaviour.
        observer.Record = false;
        cutscene.enabled = true;
        yield return null; yield return null;
        Assert.That(ocean.TimeProvider, Is.SameAs(cutscene));
        observer.Expected = cutscene; observer.Record = true;
        for (int i = 0; i < 8; i++) { yield return null; Check("EditorReenable", ocean, cutscene, Time.time); }
        cutscene.enabled = false;
        observer.Expected = network;
        Assert.That(ocean.TimeProvider, Is.SameAs(network));
        ocean.PopTimeProvider(network);
        observer.Expected = baseline;
        Assert.That(ocean.TimeProvider, Is.SameAs(baseline));
        for (int i = 0; i < 8; i++) { yield return null; Check("FinalDefault", ocean, baseline, Time.time); }
        File.AppendAllText(_report, $"Summary; ClockChecks={_checks}; ShaderFrames={observer.Samples}; MaxShaderClockError={observer.MaxShaderError:R}; FinalProvider={ocean.TimeProvider.GetType().Name}\n");
        Assert.That(observer.Samples, Is.GreaterThanOrEqualTo(100));
        observer.Record = false;
    }

    void Check(string phase, OceanRenderer ocean, ITimeProvider provider, float expected)
    {
        Assert.That(ocean.TimeProvider, Is.SameAs(provider));
        Assert.That(provider.CurrentTime, Is.EqualTo(expected).Within(.00001f), phase);
        Assert.That(ocean.CurrentTime, Is.EqualTo(expected).Within(.00001f));
        Assert.That(provider.DeltaTime, Is.EqualTo(Time.deltaTime).Within(.00001f));
        Assert.That(provider.DeltaTimeDynamics, Is.EqualTo(Time.deltaTime).Within(.00001f));
        Assert.That(Time.deltaTime, Is.GreaterThan(0), "Frozen manual director time does not freeze original provider delta.");
        File.AppendAllText(_report, $"Check={phase}; Current={provider.CurrentTime:R}; Expected={expected:R}; Delta={provider.DeltaTime:R}; UnityTime={Time.time:R}\n");
        _checks++;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying)
        {
            foreach (var observer in Object.FindObjectsByType<ClockContractObserver>()) observer.Record = false;
        }
        Time.timeScale = _scale;
        Time.captureFramerate = _capture;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_asset != null) Object.DestroyImmediate(_asset);
    }
}
