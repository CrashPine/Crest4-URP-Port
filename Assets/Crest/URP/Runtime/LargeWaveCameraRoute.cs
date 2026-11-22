// MIT. Camera tour for the large-waves validation scene.
using UnityEngine;

namespace Crest.URP
{
    [DisallowMultipleComponent]
    public sealed class LargeWaveCameraRoute : MonoBehaviour
    {
        [SerializeField, Min(1f)] float _secondsPerLeg = 7f;
        [SerializeField] Vector3[] _positions =
        {
            new Vector3(0, 4, -20),
            new Vector3(0, -5, -15),
            new Vector3(10, 52, -45),
        };
        [SerializeField] Vector3 _lookAt = new Vector3(1, 0, 0);
        float _started;

        void OnEnable() => _started = Time.time;

        void Update()
        {
            if (_positions.Length < 2) return;
            float segment = Mathf.Repeat((Time.time - _started) / _secondsPerLeg, _positions.Length);
            int from = Mathf.FloorToInt(segment);
            int to = (from + 1) % _positions.Length;
            var position = Vector3.Lerp(_positions[from], _positions[to], Mathf.SmoothStep(0f, 1f, segment - from));
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(_lookAt - position));
        }
    }
}
