// Crest 4 URP port. MIT; see LICENSE.Crest.
using System.Collections;
using System.IO;
using System.Reflection;
using Crest;
using Crest.Examples;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering;

[DefaultExecutionOrder(-1000)]
public class LegacySamplingTarget : MonoBehaviour
{
    public int AboveCount;
    public int BelowCount;
    public int Distances;
    public float LastDistance;
    public bool Move;
    public void Above() => AboveCount++;
    public void Below() => BelowCount++;

    public void Distance(float value)
    {
        LastDistance = value;
        Distances++;
    }

    void Update()
    {
        if (Move) transform.position += Vector3.right * (3 * Time.deltaTime);
    }
}

public class LegacySamplingHelpersTests
{
    const float SeaLevel = 1.25f;
    const float WakeStrength = 100;
    Material _wakeMaterial;
    int _captureRate;
    bool _batchGraphics;
    string _report;

    [UnityTest, Timeout(60000)]
    public IEnumerator HeightEventsLegacyInteractionAndVisualisedRaysUseTheOcean()
    {
        _captureRate = Time.captureFramerate;
        _batchGraphics = OceanRenderer.EnableGraphicsInBatchMode;
        OceanRenderer.EnableGraphicsInBatchMode = true;
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Calm.unity");
        yield return new EnterPlayMode();
        Time.captureFramerate = 60;
        foreach (var shape in Object.FindObjectsByType<ShapeWaves>()) shape.enabled = false;
        new GameObject("Collision request fence").AddComponent<BoatProbesQueryFence>();
        var ocean = OceanRenderer.Instance;
        Camera.main.transform.position = new Vector3(0, 4, 0);
        ocean.transform.position = Vector3.up * SeaLevel;
        var oceanSettings = new SerializedObject(ocean);
        oceanSettings.FindProperty("_createDynamicWaveSim").boolValue = true;
        oceanSettings.ApplyModifiedPropertiesWithoutUndo();
        yield return Updates(12);
        Assert.That(ocean.CollisionProvider, Is.TypeOf<QueryDisplacements>());

        var target = new GameObject("Legacy water sampling and kinematic interaction").AddComponent<LegacySamplingTarget>();
        target.transform.position = Vector3.up * (SeaLevel + 2);
        var events = target.gameObject.AddComponent<OceanSampleHeightEvents>();
        UnityEventTools.AddPersistentListener(events.OnAboveOceanSurface, target.Above);
        UnityEventTools.AddPersistentListener(events.OnBelowOceanSurface, target.Below);
        UnityEventTools.AddPersistentListener(events.DistanceFromOceanSurface, target.Distance);
        var controls = new SerializedObject(events);
        controls.FindProperty("_maximumDistance").floatValue = 4;
        controls.ApplyModifiedPropertiesWithoutUndo();

        var wake = GameObject.CreatePrimitive(PrimitiveType.Plane);
        wake.transform.SetParent(target.transform, false);
        wake.transform.localScale = Vector3.one * .2f;
        Object.DestroyImmediate(wake.GetComponent<Collider>());
        _wakeMaterial = new Material(Shader.Find("Crest/Inputs/Dynamic Waves/Object Interaction"));
        _wakeMaterial.SetFloat("_Strength", WakeStrength);
        var renderer = wake.GetComponent<Renderer>();
        renderer.sharedMaterial = _wakeMaterial;
        var input = wake.AddComponent<RegisterDynWavesInput>();
        input.enabled = false;
        wake.AddComponent<ObjectWaterInteraction>();
        var visual = new GameObject("Native ray helper").AddComponent<VisualiseRayTrace>();
        visual.transform.SetPositionAndRotation(Vector3.up * (SeaLevel + 4.5f), Quaternion.LookRotation(Vector3.down, Vector3.forward));
        var ray = (RayTraceHelper)typeof(VisualiseRayTrace)
            .GetField("_rayTrace", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(visual);
        var rayPoints = (Vector3[])typeof(RayTraceHelper)
            .GetField("_queryPos", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ray);

        _report = $"Evidence/P13-LegacySampling-{SystemInfo.graphicsDeviceType}.txt";
        File.WriteAllText(_report, $"SeaLevel={ocean.SeaLevel:R}; Provider={ocean.CollisionProvider.GetType().Name}; WakeStrength={WakeStrength:R}\n");
        yield return Updates(12);
        var interaction = target.GetComponent<ObjectWaterInteractionAdaptor>();
        Assert.That(interaction, Is.Not.Null, "The original wake must install an adaptor on the parent.");
        CheckState("Above", target, interaction, 1, 0, .5f, false);
        Assert.That(rayPoints[0], Is.EqualTo(visual.transform.position));
        Assert.That(ray.Trace(out float hit), Is.True);
        Assert.That(hit, Is.EqualTo(4.5f).Within(.001f));

        visual.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        target.transform.position = Vector3.up * SeaLevel;
        yield return Updates(8);
        CheckState("AtSurface", target, interaction, 1, 1, 1, true);
        Assert.That(rayPoints[1].y, Is.EqualTo(visual.transform.position.y + 2).Within(.001f));
        Assert.That(ray.Trace(out float miss), Is.False);
        Assert.That(miss, Is.EqualTo(-1));
        File.AppendAllText(_report, $"VisualRayHit={hit:R}; VisualRayMiss={miss:R}\n");

        target.transform.position = Vector3.up * (SeaLevel - 1);
        target.Move = true;
        yield return Updates(8);
        CheckState("SubmergedMoving", target, interaction, 1, 1, .75f, true);
        Assert.That(interaction.Velocity.x, Is.EqualTo(3).Within(.001f));
        Assert.That(interaction.Velocity.y, Is.Zero.Within(.001f));
        File.AppendAllText(_report, $"Velocity={interaction.Velocity.x:R}; ObjectWidth={interaction.ObjectWidth:R}\n");
        Assert.That(interaction.ObjectWidth, Is.Zero);

        controls.Update();
        controls.FindProperty("_normaliseDistance").boolValue = false;
        controls.ApplyModifiedPropertiesWithoutUndo();
        target.Move = false;
        yield return Updates(8);
        CheckState("SubmergedRaw", target, interaction, 1, 1, 1, true);
        Assert.That(interaction.Velocity.magnitude, Is.LessThan(.001f));
        target.transform.position = Vector3.up * (SeaLevel + 2);
        yield return Updates(8);
        CheckState("ReturnedAbove", target, interaction, 2, 1, 2, false);
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        Assert.That(block.GetFloat(RegisterLodDataInputBase.sp_Weight), Is.Zero);

        target.transform.position = Vector3.up * (SeaLevel - 1);
        target.Move = true;
        yield return Updates(12);
        renderer.GetPropertyBlock(block);
        float weight = block.GetFloat(RegisterLodDataInputBase.sp_Weight);
        float velocity = block.GetVector(ObjectWaterInteraction.ShaderIDs.s_Velocity).x;
        float offset = wake.transform.position.x - target.transform.position.x;
        var point = wake.transform.position;
        int sourceLod = LodDataMgrAnimWaves.SuggestDataLOD(new Rect(point.x, point.z, 0, 0));
        Assert.That(sourceLod, Is.Zero, "The wake must sit inside the measured LOD0 grid.");
        float sourceOff = WaveVelocityPeak(ocean);
        input.enabled = true;
        yield return Updates(12);
        float sourceOn = WaveVelocityPeak(ocean);
        input.enabled = false;
        ocean._lodDataDynWaves.ClearLodData();
        yield return Updates(4);
        float cleared = WaveVelocityPeak(ocean);
        File.AppendAllText(_report, $"SourceLod={sourceLod}; WetWeight={weight:R}; WakeVelocity={velocity:R}; PositionOffset={offset:R}; "
            + $"WaveVelocityWithSourceOff={sourceOff:R}; WithSourceOn={sourceOn:R}; Cleared={cleared:R}\n");
        Assert.That(weight, Is.GreaterThan(0));
        Assert.That(velocity, Is.EqualTo(3).Within(.005f));
        Assert.That(offset, Is.EqualTo(.6f).Within(.001f));
        Assert.That(sourceOff, Is.LessThan(1e-6f));
        Assert.That(sourceOn, Is.GreaterThan(.001f));
        Assert.That(cleared, Is.LessThan(1e-6f));
    }

    static float WaveVelocityPeak(OceanRenderer ocean)
    {
        var read = AsyncGPUReadback.Request(ocean._lodDataDynWaves.DataTexture, 0, TextureFormat.RGFloat);
        read.WaitForCompletion();
        Assert.That(read.hasError, Is.False);
        float peak = 0;
        foreach (var texel in read.GetData<Vector2>(0))
        {
            Assert.That(float.IsFinite(texel.y), Is.True);
            peak = Mathf.Max(peak, Mathf.Abs(texel.y));
        }

        return peak;
    }

    static IEnumerator Updates(int count)
    {
        for (int i = 0; i < count; i++) yield return null;
    }

    void CheckState(string label, LegacySamplingTarget target, ObjectWaterInteractionAdaptor interaction,
        int above, int below, float distance, bool inWater)
    {
        File.AppendAllText(_report, $"{label}; Above={target.AboveCount}; Below={target.BelowCount}; "
            + $"Distance={target.LastDistance:R}; Packets={target.Distances}; InWater={interaction.InWater}\n");
        Assert.That(target.AboveCount, Is.EqualTo(above), label);
        Assert.That(target.BelowCount, Is.EqualTo(below), label);
        Assert.That(target.Distances, Is.GreaterThan(0), label);
        Assert.That(target.LastDistance, Is.EqualTo(distance).Within(.001f), label);
        Assert.That(interaction.InWater, Is.EqualTo(inWater), label);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.captureFramerate = _captureRate;
        OceanRenderer.EnableGraphicsInBatchMode = _batchGraphics;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (_wakeMaterial != null) Object.DestroyImmediate(_wakeMaterial);
    }
}
