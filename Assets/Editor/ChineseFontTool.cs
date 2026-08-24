#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// 自动生成中文字体资产：TMP 默认字体（Liberation Sans）不含中文，
// 首次编译/加载时检测 Assets/Fonts/MyFont.asset 不存在 → 用系统微软雅黑（动态模式）生成并设为 TMP 全局默认。
// 也可手动：菜单 Tools/生成中文字体
[InitializeOnLoad]
public static class ChineseFontTool
{
    private const string AssetPath = "Assets/Fonts/MyFont.asset";
    private static readonly string[] FontPaths =
    {
        "C:/Windows/Fonts/msyh.ttc",    // 微软雅黑
        "C:/Windows/Fonts/simhei.ttf",  // 黑体
        "C:/Windows/Fonts/simsun.ttc",  // 宋体
    };

    static ChineseFontTool()
    {
        EditorApplication.delayCall += Ensure;
    }

    [MenuItem("Tools/生成中文字体")]
    public static void Ensure()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath) != null) return; // 已生成过
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Ensure; // 等编译完成再执行
            return;
        }

        string fontPath = null;
        foreach (var p in FontPaths)
        {
            if (File.Exists(p)) { fontPath = p; break; }
        }
        if (fontPath == null)
        {
            Debug.LogWarning("[Font] 未找到系统中文字体文件，请手动下载后 Tools/生成中文字体");
            return;
        }

        // 从字体文件路径直接创建动态 SDF：TMP 自带字体数据读取，字符运行时按需生成，中文免烘焙
        // 签名 (fontFilePath, faceIndex, samplingPointSize, atlasPadding, renderMode, atlasWidth, atlasHeight)
        var tmpFont = TMP_FontAsset.CreateFontAsset(
            fontPath, 0, 90, 5, GlyphRenderMode.SDFAA, 1024, 1024);

        if (!AssetDatabase.IsValidFolder("Assets/Fonts")) AssetDatabase.CreateFolder("Assets", "Fonts");
        AssetDatabase.CreateAsset(tmpFont, AssetPath);

        TMP_Settings.defaultFontAsset = tmpFont; // 静态属性：全局默认，所有 TMP 文本自动支持中文

        AssetDatabase.SaveAssets();
        Debug.Log($"[Font] 已自动生成中文字体 {AssetPath}（源 {fontPath}）并设为 TMP 默认字体");
    }
}
#endif
