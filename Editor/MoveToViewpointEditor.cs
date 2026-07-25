#if UNITY_EDITOR
using Reiria_001.runtime;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Reiria_001.editor
{
    [CustomEditor(typeof(MoveToViewpoint))]
    public sealed class MoveToViewpointEditor : Editor
    {
        private struct Backup
        {
            public Vector3 localPos;
            public Quaternion localRot;
        }

        // 選択中のGameObjectでのみプレビューON
        private static int _activeGoId = 0;         // GameObject instanceID
        private static int _activeTransformId = 0;  // Transform instanceID（復帰に使う）
        private static int _activeCompId = 0;       // MoveToViewpoint instanceID（適用に使う）

        private static Backup _backup;// 変更前の位置と回転のバックアップ
        private static bool _hasBackup = false;// バックアップが有効かどうか

        private static bool _queuedApply = false;// 適用が遅延して積まれているかどうか
        private static bool _queuedRevert = false;// 復帰が遅延して積まれているかどうか

        private static bool _selectionHooked = false;//Hook検知
        private static bool _playModeHooked = false;//Hook検知

        /// <summary>
        /// エディタ有効化時に、SelectionやEditorApplicationのフックを仕込む。
        /// </summary>
        private void OnEnable()
        {
            EnsureHooks();
        }

        /// <summary>
        /// SelectionやEditorApplicationのフックを仕込む。複数のMoveToViewpointがあってもフックは一度だけ。
        /// </summary>
        private void EnsureHooks()
        {
            if (!_selectionHooked)
            {
                _selectionHooked = true;
                Selection.selectionChanged += OnSelectionChanged;
                EditorApplication.update += OnEditorUpdate;
            }

            if (!_playModeHooked)
            {
                _playModeHooked = true;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            }
        }

        /// <summary>
        /// Selectionが変わったときの処理。選択が外れたのにON状態が残ってる可能性を潰す。
        /// </summary>
        private static void OnSelectionChanged()
        {
            ChangeCheckAndRevertIfNeeded();
        }

        /// <summary>
        /// EditorApplication.update に登録する処理。選択中＆ONのときだけ追従する。
        /// </summary>
        private static void OnEditorUpdate()
        {
            if (ChangeCheckAndRevertIfNeeded())
            {
                return;
            }

            // 選択中＆ONのときだけ追従
            QueueApply();
        }

        /// <summary>
        /// 選択が外れているのにON状態が残ってる可能性を潰す。選択が外れているなら元に戻す。
        /// </summary>
        private static bool ChangeCheckAndRevertIfNeeded()
        {
            if (_activeGoId == 0) return true;

            var go = Selection.activeGameObject;

            // 選択が外れているのにON状態が残ってる可能性を潰す
            if (go == null || go.GetInstanceID() != _activeGoId)
            {
                QueueRevert();
                return true;
            }

            return false;
        }

        /// <summary>
        /// PlayModeの状態が変わったときの処理。Edit -> Play 直前に必ず元に戻す。Play -> Edit 後も保険で元に戻す。
        /// </summary>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Edit -> Play 直前：必ず元に戻す（遅延しない）
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                RevertImmediate();
            }
        }

        /// <summary>
        /// インスペクターのGUI。プレビューON/OFFボタンと、値の編集画面を表示する。
        /// </summary>
        public override void OnInspectorGUI()
        {
            var m = target as MoveToViewpoint;
            if (m == null)
            {
                EditorGUILayout.HelpBox("エラー: MoveToViewpoint コンポーネントが見つかりません。", MessageType.Error);
                return;
            }

            var descriptor = m.GetComponentInParent<VRCAvatarDescriptor>();
            if (descriptor == null)
            {
                EditorGUILayout.HelpBox(
                    "警告: 親階層に VRCAvatarDescriptor が見つかりません。\n" +
                    "アバター直下（Descriptor配下）へ MoveToViewpoint を配置してください。",
                    MessageType.Warning
                );
            }

            EditorGUILayout.HelpBox(
                "Headに追従させるためには、MA Bone Proxy等との併用を推奨します。\n" +
                "これ単体では追従しません。",
                MessageType.Info
            );

            var hostGo = m.gameObject;

            bool isSelected = (Selection.activeGameObject == hostGo);
            bool PreviewActive = (_activeGoId == hostGo.GetInstanceID());//値が同じならプレビューがONの状態とみなす
            bool isActiveAndPreview = isSelected && PreviewActive;

            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                Color defColor = GUI.color;
                GUI.color = (isActiveAndPreview) ? new Color(0.7f, 1.0f, 0.7f) : new Color(1.0f, 0.8f, 0.8f);

                string label = (isActiveAndPreview) ? "プレビュー: ON（クリックでOFF）" : "プレビュー: OFF（クリックでON）";
                if (GUILayout.Button(label, GUILayout.Height(24)))
                {
                    if (!isActiveAndPreview)
                    {
                        EnablePreview(m);
                    }
                    else
                    {
                        QueueRevert();
                    }
                }

                GUI.color = defColor;
            }

            #region インスペクター値編集画面
            serializedObject.Update();

            var propLocalOffset = serializedObject.FindProperty("localOffset");
            var propApplyRootRotation = serializedObject.FindProperty("applyRootRotation");
            var propRotationOffset = serializedObject.FindProperty("rotationOffset");

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(propLocalOffset);
            EditorGUILayout.PropertyField(propApplyRootRotation);

            if (propApplyRootRotation.boolValue)
            {
                EditorGUILayout.PropertyField(propRotationOffset);
            }

            serializedObject.ApplyModifiedProperties();
            #endregion

            bool valuesChanged = EditorGUI.EndChangeCheck();

            if (isActiveAndPreview && valuesChanged)// 値が変わったときだけ適用する
            {
                QueueApply();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("デバッグ", EditorStyles.boldLabel);
            DrawDebugBox(m, descriptor, isActiveAndPreview);
        }

        /// <summary>
        /// プレビューをONにしたときの処理
        /// </summary>
        private void EnablePreview(MoveToViewpoint m)
        {
            var go = m.gameObject;

            // 「選択中＆ON」要件：選択されていないならONにしない
            if (Selection.activeGameObject != go) return;

            // 既に別対象がONなら先にOFF
            if (_activeGoId != 0 && _activeGoId != go.GetInstanceID())
            {
                QueueRevert();
            }

            //変更前の状態をバックアップ
            _backup = new Backup
            {
                localPos = m.transform.localPosition,
                localRot = m.transform.localRotation
            };

            _hasBackup = true;

            _activeGoId = go.GetInstanceID();
            _activeTransformId = m.transform.GetInstanceID();
            _activeCompId = m.GetInstanceID();

            QueueApply();// 最初はここで遅延させて適用
        }

        /// <summary>
        /// プレビューがONのとき、毎フレーム（OnEditorUpdate）か、インスペクターの値が変わったときに呼ばれる。
        /// 選択中＆ONのときだけ追従する。
        /// </summary>
        private static void QueueApply()
        {
            if (_activeGoId == 0) return;// ONの状態じゃなければ適用しない
            if (_queuedApply) return;// 二重に積まないように

            _queuedApply = true;// 適用が遅延して積まれている状態にする

            EditorApplication.delayCall += () =>
            {
                _queuedApply = false;

                if (_activeGoId == 0) return;//遅延が入っているのでONの状態かどうか再度確認する

                var go = Selection.activeGameObject;
                if (go == null || go.GetInstanceID() != _activeGoId) return;

                go.TryGetComponent<MoveToViewpoint>(out var m);
                if (m == null || m.GetInstanceID() != _activeCompId) return;

                ApplyPreviewImpl(m);

            };
        }

        /// <summary>
        /// プレビューがONのとき、毎フレーム（EditorApplication.delayCall）で呼ばれる。
        /// 移動させる処理の本体
        /// </summary>
        private static void ApplyPreviewImpl(MoveToViewpoint m)
        {
            if (!_hasBackup) return;
            if (Selection.activeGameObject != m.gameObject) return;//選択中でなければ適用しない

            var descriptor = m.GetComponentInParent<VRCAvatarDescriptor>();
            if (descriptor == null) return;//VRCAvatarDescriptorが見つからなければ適用しない

            var root = descriptor.transform;//アバターのルートTransform

            Vector3 ViewPositionWorld = root.position + root.rotation * descriptor.ViewPosition;
            Vector3 worldOffset = root.rotation * m.localOffset;

            Vector3 SetPosWorld = ViewPositionWorld + worldOffset;
            Quaternion SetRotWorld = m.applyRootRotation ? (root.rotation * m.rotationOffset) :
                (m.transform.parent != null) ? m.transform.parent.rotation * _backup.localRot : _backup.localRot;

            m.transform.SetPositionAndRotation(SetPosWorld, SetRotWorld);
            EditorUtility.SetDirty(m.transform);
        }

        /// <summary>
        /// プレビューをOFFにするときの処理。遅延させて呼ぶ。
        /// </summary>
        private static void QueueRevert()
        {
            if (_activeGoId == 0) return;// ONの状態じゃなければOFFにしない
            if (_queuedRevert) return;

            _queuedRevert = true;
            EditorApplication.delayCall += () =>
            {
                _queuedRevert = false;
                RevertPreviewImpl();
            };
        }

        /// <summary>
        /// 強制的に即座に元に戻す。PlayModeに入る直前など、遅延させたくないときに呼ぶ。
        /// </summary>
        private static void RevertImmediate()
        {
            if (_activeGoId == 0) return;

            RevertPreviewImpl();

            _queuedApply = false;
            _queuedRevert = false;
        }

        /// <summary>
        /// 元に戻す処理の共通処理本体。Transformが有効なら位置と回転をバックアップから復元する。
        /// </summary>
        private static void RevertPreviewImpl()
        {
            var t = EditorUtility.InstanceIDToObject(_activeTransformId) as Transform;
            var m = EditorUtility.InstanceIDToObject(_activeCompId) as MoveToViewpoint;
            var tr = (t != null) ? t : ((m != null) ? m.transform : null);

            _activeGoId = 0;
            _activeTransformId = 0;
            _activeCompId = 0;

            if (tr == null)
            {
                _hasBackup = false;
            }
            if (!_hasBackup) return;

            tr.SetLocalPositionAndRotation(_backup.localPos, _backup.localRot);

            EditorUtility.SetDirty(tr);

            _hasBackup = false;
        }

        /// <summary>
        /// デバッグ表示。プレビューONかつ選択中のときだけ、計算されたワールド座標やオフセットなどを表示する。
        /// </summary>
        private void DrawDebugBox(MoveToViewpoint m, VRCAvatarDescriptor descriptor, bool activeNow)
        {
            if (!activeNow)
            {
                EditorGUILayout.HelpBox("（プレビューOFF または 非選択）", MessageType.None);
                return;
            }

            if (descriptor == null)
            {
                EditorGUILayout.HelpBox("VRCAvatarDescriptor が見つからないため、デバッグ情報を表示できません。", MessageType.Error);
                return;
            }

            var root = descriptor.transform;

            Vector3 ViewPositionWorld = root.position + root.rotation * descriptor.ViewPosition;
            Vector3 worldOffset = root.rotation * m.localOffset;

            Vector3 SetPosWorld = ViewPositionWorld + worldOffset;
            Quaternion SetRotWorld = m.applyRootRotation ? (root.rotation * m.rotationOffset) :
                  (m.transform.parent != null) ? m.transform.parent.rotation * _backup.localRot : _backup.localRot;

            string text =
                "プレビュー中\n" +
                $"SetPos(World): {SetPosWorld}\n" +
                $"SetRotWorld(Euler): {SetRotWorld.eulerAngles}\n\n" +
                $"ViewPosition(World): {ViewPositionWorld}\n" +
                $"Offset(Local): {m.localOffset}\n" +
                $"applyRootRotation: {m.applyRootRotation}\n";

            if (m.applyRootRotation)
            {
                text +=
                    $"RotOffset(Euler): {m.rotationOffset.eulerAngles}\n" +
                    $"RotOffset(xyzw): {m.rotationOffset.ToString("F2")}";
            }

            EditorGUILayout.HelpBox(text, MessageType.None);
        }
    }
}
#endif