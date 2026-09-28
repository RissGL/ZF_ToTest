using System;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ZGameFramework.Utility;
using Object = UnityEngine.Object;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 一键搭建气泡测试场景。
    /// 菜单：Tools → 气泡系统 → 搭建测试场景
    ///
    /// 会生成（全部放在 Assets/.../Bubble/Generated 下，不污染别的地方）：
    ///   ① 气泡九宫格底图（程序画出来的，替换成美术的图就行）
    ///   ② Animation Set 资产：入场=缩放弹出+整体淡入，退场=缩回淡出（同 key 顶掉 = 先收再弹，共用这一对）
    ///   ③ Style 资产
    ///   ④ 场景里：Canvas + BubbleStage（三个槽：阿岚 / 老张 / 旁白，每个槽里一个气泡）
    ///      + 选项面板（ChoiceAnchor + ChoicePanel + 按钮母版）+ 两个 Demo
    ///   ⑤ rig 根上挂 DialogueLayoutRefs —— 这个 rig 就是一个「布局模板」的形状
    ///      （正式模板用 Tools/对话系统/生成默认模板 Prefab 生成，那边的槽还带立绘框）。
    /// </summary>
    public static class BubbleTestSceneBuilder
    {
        private const string GeneratedFolder = "Assets/Scripts/ZFrameworkTest/DialogueSystem/View/Bubble/Generated";
        private const string SpritePath = GeneratedFolder + "/BubbleFrame.png";
        private const string AnimSetPath = GeneratedFolder + "/BubbleAnim_Default.asset";
        private const string StylePath = GeneratedFolder + "/BubbleStyle_Default.asset";
        private const string FontFolder = "Assets/Fonts/Demo";

        /// <summary>雅黑生成的 TMP 字体资产统一叫这个名（就是测试 rig 的默认字体）</summary>
        internal const string YaheiSdfName = "微软雅黑 SDF";
        private const string RigName = "BubbleTestRig";

        [MenuItem("Tools/气泡系统/搭建测试场景")]
        public static void Build()
        {
            EnsureFolder(GeneratedFolder);

            TMP_FontAsset font = EnsureChineseFont(out string fontNote);
            Sprite sprite = EnsureBubbleSprite();
            BubbleAnimSet animSet = EnsureAnimSet();
            BubbleStyleSO style = EnsureStyle(sprite, animSet);

            BuildRig(sprite, style, font);

            Debug.Log($"[气泡测试] 搭好了。{fontNote}\n" +
                      "按 Play，然后：1=阿岚说话 2=老张说话 3=旁白 4=换样式 空格=快进 0=全收\n" +
                      "选项面板：C=弹假选项 X=收起 ↑↓/WS=选 回车=确认 1-9=直选");
        }

        [MenuItem("Tools/气泡系统/清掉测试场景")]
        public static void Clear()
        {
            var rig = GameObject.Find(RigName);
            if (rig == null)
            {
                Debug.Log("[气泡测试] 场景里没有测试用的舞台");
                return;
            }

            Undo.DestroyObjectImmediate(rig);
            Debug.Log("[气泡测试] 已清掉测试舞台");
        }

        // ===================== 资产 =====================

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>
        /// 找一份带中文的字体，生成 / 复用 TMP 字体资产（测试 rig 的默认字体来源）。
        /// 优先顺序：
        ///   ① 工程里已有的中文 TMP 字体资产（★ 名字像雅黑的优先）
        ///   ② 工程里的中文 ttf/otf（★ 优先 Assets/微软雅黑.ttf）
        ///   ③ 从系统字体目录复制（雅黑 → 黑体 → 等线…；.ttc 是字体集合，Unity/TMP 读不了，跳过）
        /// </summary>
        internal static TMP_FontAsset EnsureChineseFont(out string note)
        {
            // ① 工程里已经有「带中文字形」的 TMP 字体资产就用它（雅黑优先）
            TMP_FontAsset fallbackAsset = null;

            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));

                if (asset == null || asset.name == "LiberationSans SDF" || !HasCjkGlyphs(asset.sourceFontFile))
                {
                    continue;
                }

                if (LooksLikeYahei(asset.name) ||
                    (asset.sourceFontFile != null && LooksLikeYahei(asset.sourceFontFile.name)))
                {
                    note = $"用的是工程里已有的 TMP 字体「{asset.name}」（雅黑）。";
                    return asset;
                }

                if (fallbackAsset == null)
                {
                    fallbackAsset = asset;
                }
            }

            if (fallbackAsset != null)
            {
                note = $"用的是工程里已有的 TMP 字体「{fallbackAsset.name}」。";
                return fallbackAsset;
            }

            // ② 工程里找一份真正带中文的 ttf/otf（体积 + 字形双重判断，免得拿到拉丁版）
            Font source = FindProjectChineseFont();
            if (source != null)
            {
                note = $"用工程里的中文字体「{source.name}」生成/复用了 TMP 字体资产。";
            }

            // ③ 都没有就从 Windows 字体目录复制一份中文黑体进来（本地测试用）
            if (source == null)
            {
                foreach (var systemFont in SystemFontCandidates)
                {
                    if (!File.Exists(systemFont))
                    {
                        continue;
                    }

                    // .ttc 是字体集合（Win8+ 的微软雅黑就是 msyh.ttc），Unity 导入不了、TMP 也读不了：
                    // 想要雅黑，把单体的 msyh.ttf（或任意中文 ttf）拷进工程，走 ② 那条路
                    if (systemFont.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string fileName = Path.GetFileName(systemFont);
                    EnsureFolder(FontFolder);
                    string target = $"{FontFolder}/{fileName}";

                    if (!File.Exists(target))
                    {
                        File.Copy(systemFont, target);
                        AssetDatabase.ImportAsset(target);
                    }

                    var imported = AssetDatabase.LoadAssetAtPath<Font>(target);
                    if (HasCjkGlyphs(imported))
                    {
                        source = imported;
                        note = $"⚠️ 工程里没有中文字体，临时从系统复制了「{fileName}」当测试字体。\n" +
                               "   正式发布要换成可商用字体（HarmonyOS Sans SC / 思源黑体 / Noto Sans SC）。";
                        break;
                    }
                }
            }

            if (source == null)
            {
                note = "⚠️ 没找到带中文的字体，气泡里会是空字。\n" +
                       "   注意：HarmonyOS_Sans_Bold.ttf 是拉丁版，里面一个中文字都没有，要用 HarmonyOS_Sans_SC_* 那个版本。\n" +
                       "   解决：把一份中文 ttf/otf 丢进 Assets 里，再点一次这个菜单。";
                return null;
            }

            // 动态图集：中文按需加字形，不用预先烘几千个字
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                source,
                90,
                9,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);
            if (fontAsset == null)
            {
                note = $"⚠️ 生成 TMP 字体失败（{source.name}），气泡里可能没字。";
                return null;
            }

            // 雅黑生成的资产统一叫「微软雅黑 SDF」（测试 rig 的默认字体），其他字体就源名 + SDF
            fontAsset.name = LooksLikeYahei(source.name) ? YaheiSdfName : source.name + " SDF";
            string assetPath = $"{GeneratedFolder}/{fontAsset.name}.asset";
            AssetDatabase.CreateAsset(fontAsset, assetPath);

            // 图集和材质要作为子资产存进去，不然重启 Unity 后字体就丢了
            if (fontAsset.atlasTextures != null)
            {
                foreach (var atlas in fontAsset.atlasTextures)
                {
                    if (atlas != null && !AssetDatabase.IsSubAsset(atlas))
                    {
                        atlas.hideFlags = HideFlags.HideInHierarchy;
                        AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                    }
                }
            }

            if (fontAsset.material != null && !AssetDatabase.IsSubAsset(fontAsset.material))
            {
                fontAsset.material.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();

            note = $"生成了 TMP 字体资产「{fontAsset.name}」（动态图集，中文按需加字形）。";
            return fontAsset;
        }

        /// <summary>系统里能拿来当测试字体的中文 .ttf（.ttc 是字体集合，Unity/TMP 用不了）</summary>
        private static readonly string[] SystemFontCandidates =
        {
            "C:/Windows/Fonts/msyh.ttf",       // 微软雅黑（只有老系统是单体 ttf；Win8+ 是 msyh.ttc，用不了）
            "C:/Windows/Fonts/simhei.ttf",     // 黑体
            "C:/Windows/Fonts/Deng.ttf",       // 等线
            "C:/Windows/Fonts/simkai.ttf",     // 楷体
            "C:/Windows/Fonts/simfang.ttf",    // 仿宋
        };

        /// <summary>工程里挑一份真正带中文的字体（字形 + 体积 + 名字三重判断）</summary>
        private static Font FindProjectChineseFont()
        {
            Font best = null;
            int bestScore = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Font"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".ttf") && !path.EndsWith(".otf"))
                {
                    continue;
                }

                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font == null || !HasCjkGlyphs(font))
                {
                    continue;
                }

                int score = 1;
                if (font.HasCharacter('你'))
                {
                    score += 5;
                }
                if (LooksLikeChineseFont(font.name))
                {
                    score += 3;
                }
                if (LooksLikeYahei(font.name))
                {
                    score += 20;      // ★ 测试字体默认就要雅黑，别的（黑体/楷体…）都排它后面
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = font;
                }
            }

            return best;
        }

        /// <summary>
        /// 这个字体里到底有没有中文字形。
        /// ★ 就是这一步把 HarmonyOS_Sans_Bold.ttf（拉丁版，142KB，0 个汉字）筛掉的。
        /// </summary>
        private static bool HasCjkGlyphs(Font font)
        {
            if (font == null)
            {
                return false;
            }

            // 最直接：问字体引擎有没有这几个常用字
            if (font.HasCharacter('你') && font.HasCharacter('好'))
            {
                return true;
            }

            // 兜底：带中文的字体体积一定有好几 MB，再加上名字里的线索
            string path = AssetDatabase.GetAssetPath(font);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var info = new FileInfo(path);
            return info.Exists && info.Length > 2L * 1024 * 1024 && LooksLikeChineseFont(font.name);
        }

        private static bool LooksLikeChineseFont(string fontName)
        {
            if (string.IsNullOrEmpty(fontName))
            {
                return false;
            }

            string lower = fontName.ToLowerInvariant();
            string[] hints =
            {
                "sc", "chs", "cn", "zh", "hei", "song", "kai", "fang",
                "yahei", "msyh", "雅黑", "deng", "noto", "sourcehan", "chinese", "cjk",
            };

            foreach (var hint in hints)
            {
                if (lower.Contains(hint))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>是不是微软雅黑：工程里那份叫「微软雅黑.ttf」，系统里叫 msyh（ttc 用不了）</summary>
        private static bool LooksLikeYahei(string fontName)
        {
            if (string.IsNullOrEmpty(fontName))
            {
                return false;
            }

            string lower = fontName.ToLowerInvariant();
            return lower.Contains("雅黑") || lower.Contains("yahei") || lower.Contains("msyh");
        }

        /// <summary>程序画一个圆角九宫格气泡底图（没有美术图也能先跑）</summary>
        internal static Sprite EnsureBubbleSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (existing != null)
            {
                return existing;
            }

            const int size = 128;
            const float radius = 26f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x, x - (size - 1 - radius), 0f);
                    float dy = Mathf.Max(radius - y, y - (size - 1 - radius), 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    Color32 color;
                    if (distance > radius)
                    {
                        color = new Color32(0, 0, 0, 0);                       // 圆角外透明
                    }
                    else if (distance > radius - 3f)
                    {
                        color = new Color32(90, 90, 105, 255);                 // 描边
                    }
                    else
                    {
                        color = new Color32(252, 252, 248, 255);               // 填充
                    }

                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(SpritePath);

            var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = new Vector4(40f, 40f, 40f, 40f);       // 九宫格边框
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        internal static BubbleAnimSet EnsureAnimSet()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BubbleAnimSet>(AnimSetPath);
            if (existing != null)
            {
                return existing;
            }

            var set = ScriptableObject.CreateInstance<BubbleAnimSet>();
            set.enterEffects = new List<MangaAnimEffect> { new ScaleEffect(), new CanvasGroupFadeEffect() };
            set.exitEffects = new List<MangaAnimEffect> { new ScaleEffect(), new CanvasGroupFadeEffect() };

            AssetDatabase.CreateAsset(set, AnimSetPath);

            // 效果的参数是 private [SerializeField]，用 SerializedObject 填
            ConfigureEffect(set, "enterEffects", 0, Vector3.one, 0.16f, Ease.OutBack, 0f);
            ConfigureFadeEffect(set, "enterEffects", 1, 1f, 0.12f);
            ConfigureEffect(set, "exitEffects", 0, new Vector3(0.8f, 0.8f, 1f), 0.12f, Ease.InQuad, 0f);
            ConfigureFadeEffect(set, "exitEffects", 1, 0f, 0.12f);

            set.typingSpeed = 0.035f;
            set.punctuationPause = 0.14f;

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return set;
        }

        internal static BubbleStyleSO EnsureStyle(Sprite sprite, BubbleAnimSet animSet)
        {
            var existing = AssetDatabase.LoadAssetAtPath<BubbleStyleSO>(StylePath);
            if (existing != null)
            {
                return existing;
            }

            var style = ScriptableObject.CreateInstance<BubbleStyleSO>();
            style.frameSprite = sprite;
            style.textColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            style.fontSize = 30f;
            style.maxTextWidth = 420f;
            style.padX = 30f;
            style.padY = 20f;
            style.showName = true;
            style.nameFontSize = 24f;
            style.nameColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            style.nameSpacing = 6f;
            style.showArrow = true;
            style.animSet = animSet;

            AssetDatabase.CreateAsset(style, StylePath);
            AssetDatabase.SaveAssets();
            return style;
        }

        // 效果参数的填写（字段都是 private [SerializeField]，只能走 SerializedObject）
        private static void ConfigureEffect(BubbleAnimSet set, string listName, int index,
            Vector3 targetScale, float duration, Ease ease, float delay)
        {
            var so = new SerializedObject(set);
            var element = so.FindProperty(listName).GetArrayElementAtIndex(index);
            element.FindPropertyRelative("targetScale").vector3Value = targetScale;
            element.FindPropertyRelative("duration").floatValue = duration;
            element.FindPropertyRelative("ease").enumValueIndex = (int)ease;
            element.FindPropertyRelative("delay").floatValue = delay;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureFadeEffect(BubbleAnimSet set, string listName, int index, float alpha, float duration)
        {
            var so = new SerializedObject(set);
            var element = so.FindProperty(listName).GetArrayElementAtIndex(index);
            element.FindPropertyRelative("alpha").floatValue = alpha;
            element.FindPropertyRelative("duration").floatValue = duration;
            element.FindPropertyRelative("ease").enumValueIndex = (int)Ease.OutQuad;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>[SerializeReference] 效果列表：先放实例，再调参数（参数都是 private [SerializeField]）</summary>
        private static void SetManagedEffects(Object owner, string listName, MangaAnimEffect[] effects)
        {
            var so = new SerializedObject(owner);
            var list = so.FindProperty(listName);
            list.arraySize = effects.Length;
            for (int i = 0; i < effects.Length; i++)
            {
                list.GetArrayElementAtIndex(i).managedReferenceValue = effects[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void ConfigureManagedEffect(Object owner, string listName, int index, Action<SerializedProperty> fill)
        {
            var so = new SerializedObject(owner);
            var element = so.FindProperty(listName).GetArrayElementAtIndex(index);
            fill(element);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ===================== 场景 =====================

        /// <summary>
        /// 找一个现成的 Canvas，没有就建一个（两个测试场景搭建器共用同一套缩放设置：
        /// Screen Space Overlay + 1920×1080 + match 0.5）。
        /// </summary>
        internal static Canvas EnsureCanvas()
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                return canvas;
            }

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "测试场景");
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
            }

            return canvas;
        }

        private static void BuildRig(Sprite sprite, BubbleStyleSO style, TMP_FontAsset font)
        {
            var oldRig = GameObject.Find(RigName);
            if (oldRig != null)
            {
                Undo.DestroyObjectImmediate(oldRig);
            }

            Canvas canvas = EnsureCanvas();

            var rig = new GameObject(RigName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(rig, "气泡测试场景");
            rig.transform.SetParent(canvas.transform, false);
            Stretch(rig.GetComponent<RectTransform>());

            var stageGo = new GameObject("BubbleStage", typeof(RectTransform));
            stageGo.transform.SetParent(rig.transform, false);
            Stretch(stageGo.GetComponent<RectTransform>());

            var stage = stageGo.AddComponent<BubbleStage>();
            var demo = stageGo.AddComponent<BubbleDemo>();

            // 槽位制：每个槽 = 一个位置（这里只放气泡；正式模板里槽还能带立绘框）
            var slots = new List<DialogueSlot>
            {
                CreateSlot(stageGo.transform, "阿岚", BubbleGrowDirection.RightUp, new Vector2(-340f, 140f), sprite, style, font),
                CreateSlot(stageGo.transform, "老张", BubbleGrowDirection.LeftUp, new Vector2(340f, 140f), sprite, style, font),
                CreateSlot(stageGo.transform, "旁白", BubbleGrowDirection.Up, new Vector2(0f, -260f), sprite, style, font),
            };

            // 舞台的私有字段
            var stageSo = new SerializedObject(stage);
            var slotList = stageSo.FindProperty("slots");
            slotList.arraySize = slots.Count;
            for (int i = 0; i < slots.Count; i++)
            {
                slotList.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            }
            stageSo.FindProperty("maxVisible").intValue = 3;
            stageSo.FindProperty("fallbackStyle").objectReferenceValue = style;
            stageSo.ApplyModifiedPropertiesWithoutUndo();

            // Demo 的私有字段
            var demoSo = new SerializedObject(demo);
            demoSo.FindProperty("stage").objectReferenceValue = stage;
            var styles = demoSo.FindProperty("styles");
            styles.arraySize = 1;
            styles.GetArrayElementAtIndex(0).objectReferenceValue = style;
            demoSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 选项面板（锚位 + 面板 + 按钮母版）----
            var choiceAnchorGo = new GameObject("ChoiceAnchor", typeof(RectTransform));
            choiceAnchorGo.transform.SetParent(rig.transform, false);

            var anchorRect = choiceAnchorGo.GetComponent<RectTransform>();
            anchorRect.anchorMin = anchorRect.anchorMax = new Vector2(0.5f, 0.5f);      // 屏幕中间偏上
            anchorRect.anchoredPosition = new Vector2(0f, 140f);
            anchorRect.sizeDelta = Vector2.zero;

            var choicePanel = CreateChoicePanel(anchorRect, sprite, font);

            // ---- 选项面板演示（C=弹 X=收）----
            var choiceDemo = rig.AddComponent<DialogueChoiceDemo>();
            var choiceDemoSo = new SerializedObject(choiceDemo);
            choiceDemoSo.FindProperty("panel").objectReferenceValue = choicePanel;
            choiceDemoSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- DialogueLayoutRefs：这个 rig 现在就是一个「布局模板」的形状了 ----
            var layoutRefs = rig.AddComponent<DialogueLayoutRefs>();
            var refsSo = new SerializedObject(layoutRefs);
            refsSo.FindProperty("templateId").intValue = 0;
            refsSo.FindProperty("bubbleStage").objectReferenceValue = stage;
            refsSo.FindProperty("choiceAnchor").objectReferenceValue = anchorRect;
            refsSo.FindProperty("choicePanel").objectReferenceValue = choicePanel;
            refsSo.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = stageGo;
            EditorGUIUtility.PingObject(stageGo);
        }

        internal static Bubble CreateBubble(Transform parent, string key, BubbleGrowDirection grow, Vector2 anchoredPosition,
            Sprite sprite, BubbleStyleSO style, TMP_FontAsset font)
        {
            string bubbleName = string.IsNullOrEmpty(key) ? "Bubble_Narrator" : $"Bubble_{key}";
            var go = new GameObject(bubbleName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);      // ★ 点锚点：尺寸随内容变而不跑位
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(420f, 150f);

            var frame = go.GetComponent<Image>();
            frame.sprite = sprite;
            frame.type = Image.Type.Sliced;
            frame.color = Color.white;

            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 1f;

            // ---- Body：名字 + 正文（靠 VerticalLayoutGroup 排）----
            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(go.transform, false);
            Stretch(body.GetComponent<RectTransform>());

            var layout = body.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 20, 20);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;

            TMP_Text nameText = CreateText(body.transform, "NameText", font, "阿岚", 24f, new Color(0.35f, 0.35f, 0.35f));
            TMP_Text bodyText = CreateText(body.transform, "BodyText", font, "台词占位", 30f, new Color(0.12f, 0.12f, 0.12f));
            var typewriter = bodyText.gameObject.AddComponent<TypewriterText>();

            // ---- 继续箭头（放在气泡根下，不受 LayoutGroup 影响）----
            var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
            arrowGo.transform.SetParent(go.transform, false);

            var arrowRect = arrowGo.GetComponent<RectTransform>();
            arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(1f, 0f);
            arrowRect.pivot = new Vector2(1f, 0f);
            arrowRect.anchoredPosition = new Vector2(-16f, 12f);
            arrowRect.sizeDelta = new Vector2(20f, 20f);

            var arrowImage = arrowGo.GetComponent<Image>();
            arrowImage.sprite = sprite;
            arrowImage.type = Image.Type.Sliced;
            arrowImage.color = new Color(0.3f, 0.3f, 0.35f, 1f);

            // ---- Bubble 组件 ----
            var bubble = go.AddComponent<Bubble>();
            var so = new SerializedObject(bubble);
            so.FindProperty("key").stringValue = key;
            so.FindProperty("grow").enumValueIndex = (int)grow;
            so.FindProperty("defaultStyle").objectReferenceValue = style;
            so.FindProperty("hiddenScale").vector3Value = new Vector3(0.8f, 0.8f, 1f);
            so.FindProperty("hiddenAlpha").floatValue = 0f;
            so.FindProperty("frame").objectReferenceValue = frame;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("bodyText").objectReferenceValue = bodyText;
            so.FindProperty("arrow").objectReferenceValue = arrowGo;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("typewriter").objectReferenceValue = typewriter;
            so.ApplyModifiedPropertiesWithoutUndo();

            return bubble;
        }

        /// <summary>
        /// 一个槽 = 容器（拉满）+ 一个气泡。
        /// 正式模板里的槽还能再带一个立绘框（DialogueTemplateBuilder），
        /// 这个测试场景只放气泡 —— 它测的是气泡本身。
        /// </summary>
        internal static DialogueSlot CreateSlot(Transform parent, string slotId, BubbleGrowDirection grow,
            Vector2 anchoredPosition, Sprite sprite, BubbleStyleSO style, TMP_FontAsset font)
        {
            var slotGo = new GameObject($"Slot_{slotId}", typeof(RectTransform));
            slotGo.transform.SetParent(parent, false);
            Stretch(slotGo.GetComponent<RectTransform>());

            var bubble = CreateBubble(slotGo.transform, slotId, grow, anchoredPosition, sprite, style, font);
            bubble.ApplyGrow();

            var slot = slotGo.AddComponent<DialogueSlot>();
            var so = new SerializedObject(slot);
            so.FindProperty("slotId").stringValue = slotId;
            so.FindProperty("bubble").objectReferenceValue = bubble;
            so.ApplyModifiedPropertiesWithoutUndo();

            return slot;
        }

        /// <summary>
        /// 选项面板：根（CanvasGroup，淡入淡出/挡射线用）+ 按钮容器（VerticalLayoutGroup）+ 按钮母版。
        /// 面板本体没有底图 —— 选项是飘在分镜上的，按钮自己带框。
        /// </summary>
        internal static DialogueChoicePanel CreateChoicePanel(RectTransform anchor, Sprite sprite, TMP_FontAsset font)
        {
            var go = new GameObject("ChoicePanel", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(anchor, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(560f, 320f);

            // 按钮容器
            var buttonsGo = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
            buttonsGo.transform.SetParent(go.transform, false);
            Stretch(buttonsGo.GetComponent<RectTransform>());

            var layout = buttonsGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;

            // 按钮母版（就是运行时的第一个实例）
            var buttonTemplate = CreateChoiceButton(buttonsGo.transform, sprite, font);

            // 面板组件
            var panel = go.AddComponent<DialogueChoicePanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("buttonTemplate").objectReferenceValue = buttonTemplate;
            so.FindProperty("buttonParent").objectReferenceValue = buttonsGo.GetComponent<RectTransform>();
            so.FindProperty("group").objectReferenceValue = go.GetComponent<CanvasGroup>();
            so.FindProperty("hiddenScale").vector3Value = new Vector3(0.9f, 0.9f, 1f);
            so.FindProperty("hiddenAlpha").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 弹出：缩放弹出 + 整体淡入
            SetManagedEffects(panel, "enterEffects", new MangaAnimEffect[] { new ScaleEffect(), new CanvasGroupFadeEffect() });
            ConfigureManagedEffect(panel, "enterEffects", 0, e =>
            {
                e.FindPropertyRelative("targetScale").vector3Value = Vector3.one;
                e.FindPropertyRelative("duration").floatValue = 0.18f;
                e.FindPropertyRelative("ease").enumValueIndex = (int)Ease.OutBack;
                e.FindPropertyRelative("delay").floatValue = 0f;
            });
            ConfigureManagedEffect(panel, "enterEffects", 1, e =>
            {
                e.FindPropertyRelative("alpha").floatValue = 1f;
                e.FindPropertyRelative("duration").floatValue = 0.15f;
                e.FindPropertyRelative("ease").enumValueIndex = (int)Ease.OutQuad;
            });

            // 收起：缩回 + 淡出
            SetManagedEffects(panel, "exitEffects", new MangaAnimEffect[] { new ScaleEffect(), new CanvasGroupFadeEffect() });
            ConfigureManagedEffect(panel, "exitEffects", 0, e =>
            {
                e.FindPropertyRelative("targetScale").vector3Value = new Vector3(0.92f, 0.92f, 1f);
                e.FindPropertyRelative("duration").floatValue = 0.12f;
                e.FindPropertyRelative("ease").enumValueIndex = (int)Ease.InQuad;
                e.FindPropertyRelative("delay").floatValue = 0f;
            });
            ConfigureManagedEffect(panel, "exitEffects", 1, e =>
            {
                e.FindPropertyRelative("alpha").floatValue = 0f;
                e.FindPropertyRelative("duration").floatValue = 0.12f;
                e.FindPropertyRelative("ease").enumValueIndex = (int)Ease.InQuad;
            });

            // 确认：小弹一下
            SetManagedEffects(panel, "confirmEffects", new MangaAnimEffect[] { new PunchScaleEffect() });
            ConfigureManagedEffect(panel, "confirmEffects", 0, e =>
            {
                e.FindPropertyRelative("punch").vector3Value = new Vector3(0.035f, 0.035f, 0f);
                e.FindPropertyRelative("duration").floatValue = 0.15f;
            });

            return panel;
        }

        private static DialogueChoiceButton CreateChoiceButton(Transform parent, Sprite sprite, TMP_FontAsset font)
        {
            var go = new GameObject("ChoiceButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(480f, 72f);

            var bg = go.GetComponent<Image>();
            bg.sprite = sprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.94f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = bg;

            // 文本（居中，选项不换行）
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo.GetComponent<RectTransform>());

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "选项占位";
            tmp.fontSize = 30f;
            tmp.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            if (font != null)
            {
                tmp.font = font;
            }

            var choiceButton = go.AddComponent<DialogueChoiceButton>();
            var so = new SerializedObject(choiceButton);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("label").objectReferenceValue = tmp;
            so.FindProperty("background").objectReferenceValue = bg;
            so.ApplyModifiedPropertiesWithoutUndo();

            return choiceButton;
        }

        private static TMP_Text CreateText(Transform parent, string name, TMP_FontAsset font, string text, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.enableWordWrapping = true;

            if (font != null)
            {
                tmp.font = font;
            }

            return tmp;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
