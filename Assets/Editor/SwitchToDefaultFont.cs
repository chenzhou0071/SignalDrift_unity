#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 一键切换全部 TMP 字体为 Unity 自带（LiberationSans SDF）：
// 设置全局默认 + 遍历全部场景统一组件引用并保存。配合英文 UI 使用，稳定无缺字。
// 用法：菜单 Tools/一键切换自带字体（跑完直接 Play 验证）
public static class SwitchToDefaultFont
{
    private const string DefaultFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Tools/一键切换自带字体")]
    public static void SwitchAll()
    {
        var def = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontPath);
        if (def == null)
        {
            Debug.LogError($"[Font] 未找到自带字体 {DefaultFontPath}");
            return;
        }

        TMP_Settings.defaultFontAsset = def;
        Debug.Log("[Font] TMP 默认字体已切回自带（LiberationSans SDF）");

        int total = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Scene"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue; // 跳过 Packages 只读包
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            int n = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.font != def)
                    {
                        t.font = def;
                        n++;
                    }
                }
            }

            EditorSceneManager.SaveScene(scene);
            total += n;
            Debug.Log($"[Font] {path}: {n} 个 TMP 组件 → 自带字体");
        }

        Debug.Log($"[Font] 一键切换完成，共 {total} 个 TMP 组件。请打开 Login 场景 Play 验证");
    }
}
#endif
