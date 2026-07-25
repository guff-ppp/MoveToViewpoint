using Reiria_001.runtime;
using nadena.dev.ndmf;
using nadena.dev.ndmf.vrchat;

[assembly: ExportsPlugin(typeof(Reiria_001.editor.MoveToViewpointPlugin))]

namespace Reiria_001.editor
{
    public sealed class MoveToViewpointPlugin : Plugin<MoveToViewpointPlugin>
    {
        public override string QualifiedName => "com.reiria_001.move-to-viewpoint";
        public override string DisplayName => "Move To Viewpoint";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).Run(DisplayName, ctx =>
            {
                var descriptor = ctx.VRChatAvatarDescriptor();
                if (descriptor == null) return;

                var root = ctx.AvatarRootTransform;

                // VRChatの ViewPosition は「アバターRoot基準のローカル座標」なので、
                // ワールド座標へは Root位置 + Root回転 * ローカル で変換する。
                var viewWorld = root.position + root.rotation * descriptor.ViewPosition;

                var markers = ctx.AvatarRootObject.GetComponentsInChildren<MoveToViewpoint>(true);
                foreach (var m in markers)
                {
                    if (m == null) continue;

                    // オフセットもRootローカル基準→ワールドへ
                    var worldOffset = root.rotation * m.localOffset;
                    m.transform.position = viewWorld + worldOffset;

                    // 回転：デフォルトON（Root基準）、オフで維持
                    if (m.applyRootRotation)
                    {
                        m.transform.rotation = root.rotation * m.rotationOffset;
                    }
                }
            });
        }
    }
}