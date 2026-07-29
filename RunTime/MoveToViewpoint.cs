using UnityEngine;
using VRC.SDK3.Avatars.Components;
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


        /// <summary>
        /// 座標を ViewPosition に移動します。
        /// </summary>
        public void SetViewpoint(VRCAvatarDescriptor vRCAvatarDescriptor, Transform rootTransform)
        {
            if (vRCAvatarDescriptor == null || rootTransform == null)
            {
                Debug.LogWarning("VRCAvatarDescriptor or rootTransform is null.");
                return;
            }

            // VRChatの ViewPosition は「アバターRoot基準のローカル座標」なので、
            // ワールド座標へは Root位置 + Root回転 * ローカル で変換する。
            Vector3 WorldViewpointPos = rootTransform.position + rootTransform.rotation * vRCAvatarDescriptor.ViewPosition;

            SetViewpoint(vRCAvatarDescriptor, rootTransform.rotation, WorldViewpointPos);
        }

        /// <summary>
        /// 座標を ViewPosition に移動します。
        /// </summary>
        public void SetViewpoint(VRCAvatarDescriptor vRCAvatarDescriptor, Quaternion rootRotation, Vector3 WorldViewpointPos)
        {
            if (vRCAvatarDescriptor == null)
            {
                Debug.LogWarning("VRCAvatarDescriptor is null.");
                return;
            }

            // オフセットもRootローカル基準→ワールドへ
            Vector3 worldOffset = rootRotation * localOffset;
            transform.position = WorldViewpointPos + worldOffset;

            if (applyRootRotation)
            {
                transform.rotation = rootRotation * rotationOffset;
            }
        }
    }
}