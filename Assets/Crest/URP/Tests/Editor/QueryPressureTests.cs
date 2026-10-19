using System.Collections;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public class QueryPressureTests
{
    [UnityTest]
    public IEnumerator QueriesRecoverAfterCpuBurst()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Waves.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        yield return null;
        var sample = new SampleHeightHelper();
        var provider = OceanRenderer.Instance.CollisionProvider as QueryBase;
        Assert.That(provider, Is.Not.Null);
        // Deliberately enqueue work without yielding to the player loop/readback callbacks.
        for (int i = 0; i < 128; i++)
        {
            sample.Init(Vector3.zero, 1, true);
            sample.Sample(out float ignored);
            provider.UpdateQueries();
        }
        Assert.That(provider.RequestCount, Is.LessThanOrEqualTo(7), "Outstanding GPU readbacks must remain bounded.");
        int ready = 0;
        for (int frame = 0; frame < 180; frame++)
        {
            yield return new WaitForSeconds(.02f);
            sample.Init(Vector3.zero, 1, true);
            if (sample.Sample(out float height))
            {
                Assert.That(float.IsNaN(height) || float.IsInfinity(height), Is.False);
                ready++;
            }
        }
        Assert.That(ready, Is.GreaterThan(30), "Queries did not recover after readback pressure.");
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator RestoreState()
    {
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
