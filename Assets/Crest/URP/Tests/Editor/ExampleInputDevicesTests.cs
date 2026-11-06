// Crest 4 URP port. MIT; see LICENSE.Crest.
using System;
using System.Collections;
using System.Reflection;
using Crest.Examples;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public class ExampleInputDevicesTests
{
    [UnityTest, Timeout(90000)]
    public IEnumerator MissingDevicesPreserveMovementAndMouseReconnection()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        yield return new EnterPlayMode();
        var previousKeyboard = Keyboard.current; var previousMouse = Mouse.current;
        var settings = InputSystem.settings;
        var previousEditorBehavior = settings.editorInputBehaviorInPlayMode;
        var previousBackgroundBehavior = settings.backgroundBehavior;
        var obj = new GameObject("Device regression", typeof(Camera));
        var controller = obj.AddComponent<CamController>(); controller.enabled = false;
        obj.GetComponent<Camera>().pixelRect = new Rect(0, 0, 640, 360);
        Keyboard keyboard = null; Mouse mouse = null; Action afterInput = null;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var move = typeof(CamController).GetMethod("UpdateMovement", flags, null,
            new[] { typeof(float), typeof(Keyboard), typeof(Mouse) }, null);
        Action<Keyboard, Mouse> moveWith = (k, m) => move.Invoke(controller, new object[] { 1f, k, m });
        try
        {
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            moveWith(null, null); Assert.That(obj.transform.position, Is.EqualTo(Vector3.zero));
            controller.simForwardInput = true; moveWith(null, null);
            Assert.That(obj.transform.position.z, Is.EqualTo(20f)); // Original two movement contributions.
            controller._requireLMBToMove = true; var before = obj.transform.position; moveWith(null, null);
            Assert.That(obj.transform.position, Is.EqualTo(before));
            controller.simForwardInput = false; controller._requireLMBToMove = false;
            obj.transform.position = Vector3.zero;
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D, Key.LeftShift)); InputSystem.Update();
            moveWith(keyboard, null); Assert.That(obj.transform.position, Is.EqualTo(new Vector3(40, 0, 40)));
            controller._requireLMBToMove = true; obj.transform.position = Vector3.zero;
            moveWith(keyboard, null); Assert.That(obj.transform.position, Is.EqualTo(Vector3.zero));
            mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); InputSystem.Update();
            moveWith(keyboard, mouse); Assert.That(obj.transform.position, Is.EqualTo(new Vector3(40, 0, 40)));
            InputSystem.RemoveDevice(mouse); mouse = null;
            var dragging = typeof(CamController).GetField("_dragging", flags);
            var lastPosition = typeof(CamController).GetField("_lastMousePos", flags);
            var drag = typeof(CamController).GetMethod("UpdateDragging", flags, null, new[] { typeof(float), typeof(Mouse) }, null);
            dragging.SetValue(controller, true); lastPosition.SetValue(controller, new Vector2(100, 100));
            drag.Invoke(controller, new object[] { 1f, null });
            Assert.That(dragging.GetValue(controller), Is.False);
            Assert.That(lastPosition.GetValue(controller), Is.EqualTo(-Vector2.one));
            mouse = InputSystem.AddDevice<Mouse>();
            bool delivered = false; Exception failure = null;
            afterInput = () =>
            {
                if (!mouse.leftButton.wasPressedThisFrame) return;
                delivered = true;
                try
                {
                    Assert.That(Crest.OceanDebugGUI.OverGUI(new Vector2(320, 180)), Is.False);
                    drag.Invoke(controller, new object[] { 1f, mouse });
                    Assert.That(dragging.GetValue(controller), Is.True);
                    Assert.That(obj.transform.eulerAngles, Is.EqualTo(Vector3.zero));
                }
                catch (Exception error) { failure = error; }
            };
            InputSystem.onAfterUpdate += afterInput;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(320, 180) }.WithButton(MouseButton.Left));
            for (int frame = 0; frame < 20 && !delivered; frame++) yield return null;
            Assert.That(delivered, Is.True, "Actual Player input update must deliver queued press");
            if (failure != null) throw failure;
        }
        finally
        {
            if (afterInput != null) InputSystem.onAfterUpdate -= afterInput;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
            settings.backgroundBehavior = previousBackgroundBehavior;
            Object.DestroyImmediate(obj);
        }
        yield return new ExitPlayMode();
    }
}
