using UnityEngine;
using VRC.SDKBase;

namespace Reiria_001.runtime
{
    /// <summary>
    /// NDMFビルド時に、このコンポーネントが付いたオブジェクトを
    /// アバターの ViewPosition へ移動します。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Reiria_001/Avatar/Move To Viewpoint")]
    public sealed class MoveToViewpoint : MonoBehaviour, IEditorOnly
    {
        [Header("Position")]
        [Tooltip("ViewPosition からの追加オフセット（アバターRootローカル基準）")]
        public Vector3 localOffset = Vector3.zero;

        [Header("Rotation")]
        [Tooltip("ON: アバターRoot回転に揃える / OFF: 元の回転を維持する")]
        public bool applyRootRotation = true;

        [Tooltip("applyRootRotation=ON のとき、Root回転にさらに足すローカル回転オフセット")]
        public Quaternion rotationOffset = Quaternion.identity;
    }
}