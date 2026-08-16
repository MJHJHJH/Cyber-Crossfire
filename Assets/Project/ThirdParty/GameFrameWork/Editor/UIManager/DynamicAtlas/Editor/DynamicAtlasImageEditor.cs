using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

namespace DynamicAtlas.Editor
{
    [CustomEditor(typeof(DynamicAtlasImage), true)]
    [CanEditMultipleObjects]
    public class DynamicAtlasImageEditor : ImageEditor
    {
        private SerializedProperty _location;
        private SerializedProperty _atlasGroup;
        private SerializedProperty _loadOnEnable;

        protected override void OnEnable()
        {
            base.OnEnable();
            _location = serializedObject.FindProperty("_location");
            _atlasGroup = serializedObject.FindProperty("_atlasGroup");
            _loadOnEnable = serializedObject.FindProperty("_loadOnEnable");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Dynamic Atlas", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_location, new GUIContent("Location", "YooAsset 资源地址"));
            EditorGUILayout.PropertyField(_atlasGroup, new GUIContent("Atlas Group", "图集分组尺寸 1024/2048/4096"));
            EditorGUILayout.PropertyField(_loadOnEnable, new GUIContent("Load On Enable"));

            if (GUILayout.Button("Reload"))
            {
                foreach (Object t in targets)
                {
                    if (t is DynamicAtlasImage image)
                        image.Reload();
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Image", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Play 模式下修改 Source Image 会自动合入动态图集。也可填 Location 走 YooAsset。\n" +
                "用于动态图集的 Sprite 请保持 Mesh Type = FullRect。",
                MessageType.Info);
            base.OnInspectorGUI();

            if (Application.isPlaying && GUI.changed)
            {
                foreach (Object t in targets)
                {
                    if (t is DynamicAtlasImage image && image.sprite != null)
                        image.ApplySourceSprite(image.sprite);
                }
            }
        }
    }
}
