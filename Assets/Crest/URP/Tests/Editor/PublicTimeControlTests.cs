using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Crest;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public class PublicTimeControlTests
{
    OceanWaveSpectrum _spectrum;
    float _scale;
    int _capture;
    string _report;
    readonly Vector3[] _points = { new Vector3(-1, 0, -.5f), Vector3.zero, new Vector3(1, 0, 1) };
    readonly float[] _heights = new float[3];
    readonly Vector3[] _velocities = new Vector3[3];
    static readonly FieldInfo ResultTime = typeof(QueryBase).GetField("_queryResultsTime", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo PreviousTime = typeof(QueryBase).GetField("_queryResultsTimeLast", BindingFlags.Instance | BindingFlags.NonPublic);

    [UnityTest]
    public IEnumerator PublicClockAndUnityPauseResumePreserveWaveTimeAndQueryStatuses()
    {
        _scale = Time.timeScale;
        _capture = Time.captureFramerate;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Assert.That(Application.isPlaying, Is.True);
        Time.timeScale = 1;
        Time.captureFramerate = 60;
        var ocean = OceanRenderer.Instance;
        Camera.main.transform.position = new Vector3(0, 4, 0);
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        _spectrum = ScriptableObject.CreateInstance<OceanWaveSpectrum>();
        _spectrum._chop = 0;
        _spectrum._waveDirectionVariance = 0;
        var powers = new SerializedObject(_spectrum);
        for (int i = 0; i < OceanWaveSpectrum.NUM_OCTAVES; i++)
        {
            powers.FindProperty("_powerDisabled").GetArrayElementAtIndex(i).boolValue = i != 9;
            powers.FindProperty("_powerLog").GetArrayElementAtIndex(i).floatValue = -2;
        }
        powers.ApplyModifiedPropertiesWithoutUndo();
        var owner = new GameObject("Time-controlled original Gerstner");
        owner.SetActive(false);
        var waves = owner.AddComponent<ShapeGerstner>();
        waves._spectrum = _spectrum;
        waves._randomSeed = 27;
        waves._componentsPerOctave = 2;
        waves._resolution = 128;
        waves._overrideGlobalWindSpeed = true;
        waves._windSpeed = 60;
        waves._overrideGlobalWindDirection = true;
        waves._waveDirectionHeadingAngle = 0;
        var fields = new SerializedObject(waves);
        fields.FindProperty("_respectShallowWaterAttenuation").floatValue = 0;
        fields.ApplyModifiedPropertiesWithoutUndo();
        owner.SetActive(true);
        float deadline = Time.realtimeSinceStartup + 15;
        while ((waves._waveBuffers == null || !waves._waveBuffers.IsCreated()) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(waves._waveBuffers, Is.Not.Null);
        var clock = ocean.gameObject.AddComponent<TimeProviderCustom>();
        yield return null; // Let the original Start initialise its internal clock.
        ocean.PushTimeProvider(clock);
        var capture = ocean.gameObject.AddComponent<GerstnerQueryCapture>();
        capture.Waves = waves;
        capture.Capture = true;
        _report = $"Evidence/PublicTime-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, "Original TimeProviderCustom via public Push/Pop; real Gerstner chop0 waves and public GPU queries at three points. Independent scalar combined-RT snapshot sampler reused from Gerstner accuracy fixture. Test-only readback fence, not latency/performance. Tolerances: clock .0001s, height .025m, velocity .05m/s; frozen span/velocity .001. No fixedDeltaTime assignment.\n");
        yield return Observe(ocean, clock, capture, "Running", 160, false, false, false);
        clock._paused = true;
        yield return Observe(ocean, clock, capture, "OceanPausedDefaultDelta", 24, true, false, false);
        clock._overrideDeltaTime = true;
        clock._deltaTime = 0;
        yield return Observe(ocean, clock, capture, "OceanPausedZeroDelta", 24, true, false, true);
        clock._paused = false;
        clock._overrideDeltaTime = false;
        yield return Observe(ocean, clock, capture, "OceanResumed", 160, false, false, false);
        Time.timeScale = 0;
        yield return Observe(ocean, clock, capture, "UnityPaused", 24, true, true, true);
        Time.timeScale = 1;
        yield return Observe(ocean, clock, capture, "UnityResumed", 160, false, false, false);
        ocean.PopTimeProvider(clock);
        Assert.That(ocean.TimeProvider, Is.TypeOf<TimeProviderDefault>(), "Public stack restores default provider");
        Assert.That(ocean.CurrentTime, Is.EqualTo(Time.time).Within(.0001f));
    }

    IEnumerator Observe(OceanRenderer ocean, TimeProviderCustom clock, GerstnerQueryCapture capture, string phase,
        int steps, bool frozenOcean, bool frozenUnity, bool zeroDelta)
    {
        float beforeOcean = ocean.CurrentTime, beforeUnity = Time.time;
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        float maxHeightError = 0, maxVelocityError = 0, maxVelocity = 0, maxClockError = 0;
        int compared = 0, invalidVelocity = 0;
        for (int frame = 0; frame < steps; frame++)
        {
            yield return null;
            Assert.That(ocean.TimeProvider, Is.SameAs(clock));
            float expectedAdvance = frozenOcean ? 0 : Time.time - beforeUnity;
            float clockError = Mathf.Abs(ocean.CurrentTime - beforeOcean - expectedAdvance);
            maxClockError = Mathf.Max(maxClockError, clockError);
            Assert.That(clockError, Is.LessThan(.0001f), phase + " internal time must hold/resume without a jump");
            Assert.That(ocean.DeltaTime, Is.EqualTo(zeroDelta ? 0 : Time.deltaTime).Within(1e-6f), phase + " public delta contract");
            Assert.That(ocean.DeltaTimeDynamics, Is.EqualTo(ocean.DeltaTime));
            if (frozenUnity) Assert.That(Time.time, Is.EqualTo(beforeUnity));
            beforeOcean = ocean.CurrentTime;
            beforeUnity = Time.time;
            int status = ocean.CollisionProvider.Query(GetHashCode(), 0, _points, _heights, null, _velocities);
            float time = (float)ResultTime.GetValue(ocean.CollisionProvider);
            float previous = (float)PreviousTime.GetValue(ocean.CollisionProvider);
            File.AppendAllText(_report, $"{phase}; Frame={frame}; Unity={Time.time:R}; Ocean={ocean.CurrentTime:R}; Delta={ocean.DeltaTime:R}; Status={status}; ResultTime={time:R}; Previous={previous:R}\n");
            if (frame >= 3)
            {
                Assert.That(ocean.CollisionProvider.RetrieveSucceeded(status), Is.True, phase + " height remains ready");
                Assert.That(capture.Frames.TryGetValue(time, out var snapshot), Is.True, "Reference field at dispatch timestamp");
                if (frozenUnity)
                {
                    Assert.That(status, Is.EqualTo((int)QueryBase.QueryStatus.InvalidDtForVelocity));
                    Assert.That(time, Is.EqualTo(previous), "Paused Unity timestamps invalidate velocity despite ready heights");
                    invalidVelocity++;
                }
                else Assert.That(status, Is.Zero, phase + " full velocity readiness");
                for (int i = 0; i < _points.Length; i++)
                {
                    float expectedHeight = snapshot.Height(_points[i]);
                    float heightError = Mathf.Abs(_heights[i] - expectedHeight);
                    maxHeightError = Mathf.Max(maxHeightError, heightError);
                    Assert.That(heightError, Is.LessThan(.025f));
                    if (!frozenUnity)
                    {
                        Assert.That(time - previous, Is.GreaterThan(.0001f));
                        Assert.That(capture.Frames.TryGetValue(previous, out var oldSnapshot), Is.True);
                        float expectedVelocity = (expectedHeight - oldSnapshot.Height(_points[i])) / (time - previous);
                        float velocityError = Vector3.Distance(_velocities[i], Vector3.up * expectedVelocity);
                        maxVelocityError = Mathf.Max(maxVelocityError, velocityError);
                        maxVelocity = Mathf.Max(maxVelocity, _velocities[i].magnitude);
                        Assert.That(velocityError, Is.LessThan(.05f));
                        if (frozenOcean) Assert.That(_velocities[i].magnitude, Is.LessThan(.001f));
                    }
                }
                min = Mathf.Min(min, _heights[0]);
                max = Mathf.Max(max, _heights[0]);
                compared++;
            }
            // Keep the reference captures bounded. Preserve both query timestamps.
            foreach (float key in new List<float>(capture.Frames.Keys))
                if (key < Time.time - .15f && key != time && key != previous) capture.Frames.Remove(key);
        }
        File.AppendAllText(_report, $"Summary={phase}; Compared={compared}; InvalidVelocity={invalidVelocity}; HeightSpanPoint0={max - min:R}; MaxClockError={maxClockError:R}; MaxHeightError={maxHeightError:R}; MaxVelocityError={maxVelocityError:R}; MaxVelocity={maxVelocity:R}\n");
        Assert.That(compared, Is.EqualTo(steps - 3));
        if (frozenOcean) Assert.That(max - min, Is.LessThan(.001f));
        else
        {
            Assert.That(max - min, Is.GreaterThan(.05f), phase + " real wave motion returns");
            Assert.That(maxVelocity, Is.GreaterThan(.02f));
        }
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = _scale;
        Time.captureFramerate = _capture;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_spectrum != null) Object.DestroyImmediate(_spectrum);
    }
}
