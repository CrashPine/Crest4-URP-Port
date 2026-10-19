using System.Collections;
using System.IO;
using System.Text;
using Crest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public class BuoyancyGraphicsTests
{
    int _previousCaptureRate;

    [UnityTest]
    public IEnumerator DifferentBodiesRemainBuoyant()
    {
        EditorSceneManager.OpenScene("Assets/CrestPortDemos/Ocean_Buoyancy.unity");
        yield return new EnterPlayMode();
        OceanRenderer.EnableGraphicsInBatchMode = true;
        _previousCaptureRate = Time.captureFramerate;
        Time.captureFramerate = 60;
        var bodies = Object.FindObjectsByType<SimpleFloatingObject>(FindObjectsSortMode.None);
        Assert.That(bodies.Length, Is.EqualTo(3));
        var wetFrames = new int[bodies.Length];
        var start = Time.time;
        for (int frame = 0; frame < 600; frame++)
        {
            yield return new WaitForSeconds(.016f);
            for (int i = 0; i < bodies.Length; i++)
            {
                var y = bodies[i].transform.position.y;
                Assert.That(float.IsNaN(y) || float.IsInfinity(y), Is.False);
                Assert.That(y, Is.InRange(-6f, 8f), "Body escaped the test water column.");
                if (bodies[i].InWater) wetFrames[i]++;
            }
        }
        var report = new StringBuilder($"Unity={Application.unityVersion}\nAPI={SystemInfo.graphicsDeviceType}\nSimulatedSeconds={Time.time - start}\n");
        Assert.That(Time.time - start, Is.GreaterThan(9));
        for (int i = 0; i < bodies.Length; i++)
        {
            var body = bodies[i];
            Assert.That(wetFrames[i], Is.GreaterThan(300));
            Assert.That(body.transform.position.y, Is.GreaterThan(-4));
            Assert.That(body.Velocity.magnitude, Is.LessThan(20));
            report.AppendLine($"Mass={body.GetComponent<Rigidbody>().mass}; Width={body.ObjectWidth}; Height={body.transform.position.y}; Velocity={body.Velocity}; WetFrames={wetFrames[i]}");
        }
        report.AppendLine("Original SimpleFloatingObject uses ForceMode.Acceleration; this is stability coverage, not a mass-dependent displacement model.");
        Directory.CreateDirectory("Evidence");
        File.WriteAllText("Evidence/Buoyancy-" + SystemInfo.graphicsDeviceType + ".txt", report.ToString());
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator RestoreState()
    {
        Time.captureFramerate = _previousCaptureRate;
        OceanRenderer.EnableGraphicsInBatchMode = false;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
