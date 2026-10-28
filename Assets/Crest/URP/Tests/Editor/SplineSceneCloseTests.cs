// Crest 4 URP port. MIT; see LICENSE.Crest.
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Crest.Spline;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;
public class SplineChangeObserver : MonoBehaviour, IReceiveSplineChangeMessages
{
    public int Changes;
    public void OnSplineChange(){Changes++;}
}
public class SplineSceneCloseTests
{
    [UnityTest,Timeout(120000)] public IEnumerator OriginalRiverSceneClosesWithoutRecreatingMeshes()
    {
        EditorSceneManager.OpenScene("Assets/Crest/Crest-Examples/LakesAndRivers/Scenes/LakesAndRivers.unity");
        foreach(var shape in UnityEngine.Object.FindObjectsByType<Crest.ShapeWaves>())shape.enabled=false;
        yield return null;yield return null;
        yield return new EnterPlayMode();for(int i=0;i<8;i++)yield return null;foreach(var h in UnityEngine.Object.FindObjectsByType<Crest.RegisterHeightInput>())h.enabled=false;foreach(var f in UnityEngine.Object.FindObjectsByType<Crest.RegisterFlowInput>())f.enabled=false;yield return null;foreach(var h in UnityEngine.Object.FindObjectsByType<Crest.RegisterHeightInput>())h.enabled=true;foreach(var f in UnityEngine.Object.FindObjectsByType<Crest.RegisterFlowInput>())f.enabled=true;yield return null;yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return null;yield return null;
        Assert.That(UnityEngine.Object.FindObjectsByType<UnityEngine.MeshFilter>().Length,Is.Zero);
    }
    [UnityTest] public IEnumerator PointMoveDisableDeleteAndUndoStillNotify()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("Spline edit observer");var observer=root.AddComponent<SplineChangeObserver>();
        var point=new GameObject("Point");point.transform.parent=root.transform;var script=point.AddComponent<SplinePoint>();yield return null;yield return null;
        int before=observer.Changes;point.transform.position=Vector3.right;yield return null;yield return null;Assert.That(observer.Changes,Is.GreaterThan(before));
        before=observer.Changes;script.enabled=false;Assert.That(observer.Changes,Is.GreaterThan(before));script.enabled=true;
        before=observer.Changes;Undo.DestroyObjectImmediate(point);Assert.That(observer.Changes,Is.GreaterThan(before));Undo.PerformUndo();yield return null;yield return null;Assert.That(root.transform.childCount,Is.EqualTo(1));
        before=observer.Changes;root.transform.GetChild(0).position=Vector3.up;yield return null;yield return null;Assert.That(observer.Changes,Is.GreaterThan(before));
        Undo.ClearAll();Object.DestroyImmediate(root);
    }
    [UnityTearDown] public IEnumerator Cleanup(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return null;}
}
