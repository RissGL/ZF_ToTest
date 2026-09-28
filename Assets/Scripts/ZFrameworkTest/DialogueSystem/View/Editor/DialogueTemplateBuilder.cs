using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ZF.DialoguePresentation
{
    /// <summary>模板布局种类（搭建器用）</summary>
    internal enum TemplateLayout
    {
        /// <summary>左右两个角色位 + 中间下方旁白位</summary>
        TwoSpeakers,

        /// <summary>单个居中角色位 + 中间下方旁白位</summary>
        SingleCenter,
    }

    /// <summary>
    /// 一键生成「布局模板」Prefab。菜单：Tools → 对话系统 → 生成默认模板 Prefab
    ///
    /// 模板 = 一个分镜构图，由若干「槽（DialogueSlot）」组成：
    ///   槽 = 一个角色位 = 气泡（Bubble）+ 立绘框（PortraitFrame，框里再放立绘 Portrait）
    ///   ★ 模板不认识角色：谁站在哪个槽是运行时按数据分的（台词上的 slotId，空则自动领槽）。
    ///     所以三个角色换位置组合不需要做新模板 —— 这是「槽」存在的全部意义。
    ///
    /// 默认模板（TwoSpeakers）长这样：
    ///   Template_Default            DialogueLayoutRefs（接线板）
    ///   ├─ BubbleStage
    ///   │   ├─ Slot_左   ├─ Bubble_左（RightUp, -340,140）
    ///   │   │            └─ Frame_左（-520,-30）        ← 立绘框：框的视觉 + 入场动画
    ///   │   │                 └─ Portrait              ← 立绘：框里的画（运行时换成角色立绘）
    ///   │   ├─ Slot_右   ├─ Bubble_右（LeftUp, 340,140）
    ///   │   │            └─ Frame_右（520,-30）
    ///   │   │                 └─ Portrait
    ///   │   └─ Slot_旁白 └─ Bubble_旁白（Up, 0,-260）   ← 没有立绘框 = 旁白位
    ///   └─ ChoiceAnchor → ChoicePanel
    ///
    /// 底图 / 气泡样式 / 动画 / 字体复用气泡测试场景那套资产；框底图、立绘占位图、立绘动画集在这里生成。
    /// </summary>
    public static class DialogueTemplateBuilder
    {
        internal const string TemplateFolder = "Assets/Scripts/ZFrameworkTest/DialogueSystem/View/Templates";
        private const string DefaultTemplatePath = TemplateFolder + "/Template_Default.prefab";
        private const string FrameSpritePath = TemplateFolder + "/FramePlaceholder.png";
        private const string PortraitSpritePath = TemplateFolder + "/PortraitPlaceholder.png";
        private const string PortraitAnimPath = TemplateFolder + "/PortraitAnim_Default.asset";

        /// <summary>默认模板的 templateId（数据里 DialogueShowGroupData.templateId 对上它）</summary>
        private const int DefaultTemplateId = 0;

        [MenuItem("Tools/对话系统/生成默认模板 Prefab")]
        public static void BuildDefaultTemplate()
        {
            if (File.Exists(DefaultTemplatePath) &&
                !EditorUtility.DisplayDialog("覆盖默认模板",
                    $"已存在：\n{DefaultTemplatePath}\n\n重新生成会覆盖它，继续？", "覆盖", "取消"))
            {
                return;
            }

            BubbleTestSceneBuilder.EnsureFolder(TemplateFolder);

            TMP_FontAsset font = BubbleTestSceneBuilder.EnsureChineseFont(out string fontNote);
            Sprite bubbleSprite = BubbleTestSceneBuilder.EnsureBubbleSprite();
            BubbleAnimSet bubbleAnim = BubbleTestSceneBuilder.EnsureAnimSet();
            BubbleStyleSO bubbleStyle = BubbleTestSceneBuilder.EnsureStyle(bubbleSprite, bubbleAnim);
            Sprite frameSprite = EnsureFrameSprite();
            Sprite portraitSprite = EnsurePortraitSprite();
            PortraitAnimSet portraitAnim = EnsurePortraitAnimSet();

            GameObject root = BuildTemplateHierarchy("Template_Default", TemplateLayout.TwoSpeakers,
                bubbleSprite, bubbleStyle, frameSprite, portraitSprite, portraitAnim, font);

            SaveTemplate(root, DefaultTemplatePath);

            Debug.Log($"[对话模板] 已生成 {DefaultTemplatePath}（templateId = {DefaultTemplateId}）。{fontNote}\n" +
                      "结构：DialogueLayoutRefs + BubbleStage（左 / 右 / 旁白 三个槽）+ 选项面板。\n" +
                      "左/右槽里各有一个立绘框（框里是立绘，入场动画用 PortraitAnim_Default），旁白槽只有气泡。\n" +
                      "角色不用在模板里指定 —— 谁进哪个槽由数据决定（台词上的 slotId，空则自动领槽）。");
        }

        // ===================== 资产 =====================

        /// <summary>
        /// 框底图：程序画的圆角画格（半透明底板 + 亮边框），九宫格拉伸。
        /// ★ 这是"框"的视觉，和立绘无关 —— 美术给了正式框直接换这张 sprite。
        /// </summary>
        internal static Sprite EnsureFrameSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(FrameSpritePath);
            if (existing != null)
            {
                return existing;
            }

            BubbleTestSceneBuilder.EnsureFolder(TemplateFolder);

            const int size = 128;
            const float radius = 18f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            var fill = new Color32(20, 24, 34, 110);        // 底板：偏暗、半透明
            var border = new Color32(226, 232, 245, 235);   // 边框：亮

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
                        color = new Color32(0, 0, 0, 0);
                    }
                    else if (distance > radius - 4f)
                    {
                        color = border;
                    }
                    else
                    {
                        color = fill;
                    }

                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(FrameSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(FrameSpritePath);

            var importer = AssetImporter.GetAtPath(FrameSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = new Vector4(40f, 40f, 40f, 40f);   // 框要九宫格拉伸
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(FrameSpritePath);
        }

        /// <summary>
        /// 立绘占位图：程序画的人形剪影，只为在 Prefab 里一眼看清"这个位置站着人"。
        /// ★ 正式立绘不用改这张图 —— 运行时 Image 的 sprite 会被换成
        ///   `DialogueSpeaker.GetFace(表情)`，也就是美术填在角色资产上的那几张画。
        /// </summary>
        internal static Sprite EnsurePortraitSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitSpritePath);
            if (existing != null)
            {
                return existing;
            }

            BubbleTestSceneBuilder.EnsureFolder(TemplateFolder);

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            var fill = new Color32(96, 108, 138, 235);
            var rim = new Color32(142, 158, 192, 255);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 头：圆心 (64, 98)、半径 20；身体：中心 (64, 38) 的椭圆、半径 (44, 38)
                    float headDx = x - 64f;
                    float headDy = y - 98f;
                    bool inHead = headDx * headDx + headDy * headDy <= 400f;
                    bool inHeadRim = headDx * headDx + headDy * headDy <= 529f;

                    float bodyX = (x - 64f) / 44f;
                    float bodyY = (y - 38f) / 38f;
                    bool inBody = bodyX * bodyX + bodyY * bodyY <= 1f;

                    float rimX = (x - 64f) / 47f;
                    float rimY = (y - 38f) / 41f;
                    bool inBodyRim = rimX * rimX + rimY * rimY <= 1f;

                    Color32 color;
                    if (inHead || inBody)
                    {
                        color = fill;
                    }
                    else if (inHeadRim || inBodyRim)
                    {
                        color = rim;
                    }
                    else
                    {
                        color = new Color32(0, 0, 0, 0);
                    }

                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(PortraitSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(PortraitSpritePath);

            var importer = AssetImporter.GetAtPath(PortraitSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = Vector4.zero;        // 立绘是整张画，不是九宫格
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(PortraitSpritePath);
        }

        /// <summary>立绘动画集：入场 = 缩放弹出 + 整体淡入；退场 = 缩回 + 淡出</summary>
        internal static PortraitAnimSet EnsurePortraitAnimSet()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PortraitAnimSet>(PortraitAnimPath);
            if (existing != null)
            {
                return existing;
            }

            BubbleTestSceneBuilder.EnsureFolder(TemplateFolder);

            var set = ScriptableObject.CreateInstance<PortraitAnimSet>();
            set.enterEffects = new List<MangaAnimEffect> { new ScaleEffect(), new CanvasGroupFadeEffect() };
            set.exitEffects = new List<MangaAnimEffect> { new ScaleEffect(), new CanvasGroupFadeEffect() };

            AssetDatabase.CreateAsset(set, PortraitAnimPath);

            BubbleTestSceneBuilder.ConfigureManagedEffect(set, "enterEffects", 0, e =>
            {
                e.FindPropertyRelative("targetMode").enumValueIndex = (int)VisualScaleMode.Rect;   // 界面部件缩尺寸，不缩九宫格边框
                e.FindPropertyRelative("useFromScale").boolValue = true;
                e.FindPropertyRelative("fromScale").vector3Value = new Vector3(0.94f, 0.94f, 1f);
                e.FindPropertyRelative("targetScale").vector3Value = Vector3.one;
                e.FindPropertyRelative("duration").floatValue = 0.28f;
                e.FindPropertyRelative("ease").enumValueIndex = (int)Ease.OutBack;
                e.FindPropertyRelative("delay").floatValue = 0f;
            });
            BubbleTestSceneBuilder.ConfigureManagedEffect(set, "enterEffects", 1, e =>
            {
                e.FindPropertyRelative("alpha").floatValue = 1f;
                e.FindPropertyRelative("duration").floatValue = 0.22f;
                e.FindPropertyRelative("ease").enumValueIndex = (int)Ease.OutQuad;
            });
            BubbleTestSceneBuilder.ConfigureManagedEffect(set, "exitEffects", 0, e =>
            {
                e.FindPropertyRelative("targetMode").enumValueIndex = (int)VisualScaleMode.Rect;
                e.FindPropertyRelative("targetScale").vector3Value = new Vector3(0.94f, 0.94f, 1f);
                e.FindPropertyRelative("duration").floatValue = 0.15f;
                e.FindPropertyRelative("ease").enumValueIndex = (int)Ease.InQuad;
                e.FindPropertyRelative("delay").floatValue = 0f;
            });
            BubbleTestSceneBuilder.ConfigureManagedEffect(set, "exitEffects", 1, e =>
            {
                e.FindPropertyRelative("alpha").floatValue = 0f;
                e.FindPropertyRelative("duration").floatValue = 0.15f;
                e.FindPropertyRelative("ease").enumValueIndex = (int)Ease.InQuad;
            });

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return set;
        }

        // ===================== 层级 =====================

        /// <summary>搭出模板层级（还没存成资产）。调用方负责 SaveTemplate</summary>
        internal static GameObject BuildTemplateHierarchy(string name, TemplateLayout layout,
            Sprite bubbleSprite, BubbleStyleSO bubbleStyle,
            Sprite frameSprite, Sprite portraitSprite, PortraitAnimSet portraitAnim,
            TMP_FontAsset font)
        {
            var root = new GameObject(name, typeof(RectTransform));
            BubbleTestSceneBuilder.Stretch(root.GetComponent<RectTransform>());

            // ---- 气泡舞台 ----
            var stageGo = new GameObject("BubbleStage", typeof(RectTransform));
            stageGo.transform.SetParent(root.transform, false);
            BubbleTestSceneBuilder.Stretch(stageGo.GetComponent<RectTransform>());

            var stage = stageGo.AddComponent<BubbleStage>();
            var slots = new List<DialogueSlot>();

            if (layout == TemplateLayout.TwoSpeakers)
            {
                slots.Add(CreateSlot(stageGo.transform, "左", BubbleGrowDirection.RightUp, new Vector2(-340f, 140f),
                    new Vector2(-560f, -40f), new Vector2(-520f, -330f), true,
                    bubbleSprite, bubbleStyle, frameSprite, portraitSprite, portraitAnim, font));
                slots.Add(CreateSlot(stageGo.transform, "右", BubbleGrowDirection.LeftUp, new Vector2(340f, 140f),
                    new Vector2(560f, -40f), new Vector2(520f, -330f), true,
                    bubbleSprite, bubbleStyle, frameSprite, portraitSprite, portraitAnim, font));
            }
            else
            {
                slots.Add(CreateSlot(stageGo.transform, "中", BubbleGrowDirection.Up, new Vector2(0f, 280f),
                    new Vector2(0f, -60f), new Vector2(0f, -350f), true,
                    bubbleSprite, bubbleStyle, frameSprite, portraitSprite, portraitAnim, font));
            }

            // 旁白槽：只有气泡，没有立绘框和立绘（旁白优先领这种槽）
            slots.Add(CreateSlot(stageGo.transform, "旁白", BubbleGrowDirection.Up, new Vector2(0f, -260f),
                Vector2.zero, Vector2.zero, false,
                bubbleSprite, bubbleStyle, frameSprite, portraitSprite, portraitAnim, font));

            var stageSo = new SerializedObject(stage);
            var slotList = stageSo.FindProperty("slots");
            slotList.arraySize = slots.Count;
            for (int i = 0; i < slots.Count; i++)
            {
                slotList.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            }
            stageSo.FindProperty("maxVisible").intValue = 3;
            stageSo.FindProperty("fallbackStyle").objectReferenceValue = bubbleStyle;
            stageSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 选项面板（屏幕中间偏上）----
            var anchorGo = new GameObject("ChoiceAnchor", typeof(RectTransform));
            anchorGo.transform.SetParent(root.transform, false);

            var anchorRect = anchorGo.GetComponent<RectTransform>();
            anchorRect.anchorMin = anchorRect.anchorMax = new Vector2(0.5f, 0.5f);
            anchorRect.anchoredPosition = new Vector2(0f, 140f);
            anchorRect.sizeDelta = Vector2.zero;

            var choicePanel = BubbleTestSceneBuilder.CreateChoicePanel(anchorRect, bubbleSprite, font);

            // ---- 接线板 ----
            var refs = root.AddComponent<DialogueLayoutRefs>();
            var refsSo = new SerializedObject(refs);
            refsSo.FindProperty("templateId").intValue = DefaultTemplateId;
            refsSo.FindProperty("bubbleStage").objectReferenceValue = stage;
            refsSo.FindProperty("choiceAnchor").objectReferenceValue = anchorRect;
            refsSo.FindProperty("choicePanel").objectReferenceValue = choicePanel;
            refsSo.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        /// <summary>把搭好的层级存成 Prefab 资产（会销毁临时物体并选中资产）</summary>
        internal static DialogueLayoutRefs SaveTemplate(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }

            return prefab != null ? prefab.GetComponent<DialogueLayoutRefs>() : null;
        }

        /// <summary>一个槽 = 容器 + 气泡（可选）+ 立绘框（可选，框里再放立绘）</summary>
        private static DialogueSlot CreateSlot(Transform parent, string slotId,
            BubbleGrowDirection grow, Vector2 bubblePos,
            Vector2 framePos, Vector2 portraitPos, bool withFrame,
            Sprite bubbleSprite, BubbleStyleSO bubbleStyle,
            Sprite frameSprite, Sprite portraitSprite, PortraitAnimSet portraitAnim,
            TMP_FontAsset font)
        {
            var slotGo = new GameObject($"Slot_{slotId}", typeof(RectTransform));
            slotGo.transform.SetParent(parent, false);
            BubbleTestSceneBuilder.Stretch(slotGo.GetComponent<RectTransform>());

            var bubble = BubbleTestSceneBuilder.CreateBubble(slotGo.transform, slotId, grow, bubblePos,
                bubbleSprite, bubbleStyle, font);
            bubble.ApplyGrow();

            PortraitFrame frame = null;
            Portrait portrait = null;
            if (withFrame)
            {
                frame = CreateFrame(slotGo.transform, $"Frame_{slotId}", framePos, frameSprite, portraitAnim);
                portrait = CreatePortrait(slotGo.transform, $"Portrait_{slotId}", portraitPos,
                    portraitSprite, portraitAnim);
            }

            var slot = slotGo.AddComponent<DialogueSlot>();
            var so = new SerializedObject(slot);
            so.FindProperty("slotId").stringValue = slotId;
            so.FindProperty("bubble").objectReferenceValue = bubble;
            so.FindProperty("frame").objectReferenceValue = frame;
            so.FindProperty("portrait").objectReferenceValue = portrait;
            so.ApplyModifiedPropertiesWithoutUndo();

            return slot;
        }

        /// <summary>
        /// 立绘框：分镜里的「画格」—— 框的视觉 + 自己的进出场动画。
        /// 框内部想怎么搭（几条边、有没有底色）全在这个 Prefab 里，代码不关心；
        /// 代码只通过 animSet 给它"播动画"。
        /// ★ 它和立绘是**兄弟节点**（不是父子）。
        /// </summary>
        private static PortraitFrame CreateFrame(Transform parent, string name, Vector2 anchoredPosition,
            Sprite frameSprite, PortraitAnimSet animSet)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(400f, 660f);

            // 框的视觉：九宫格底图（底板 + 边框），美术给了正式框直接换这张 sprite
            var frameImage = go.GetComponent<Image>();
            frameImage.sprite = frameSprite;
            frameImage.type = Image.Type.Sliced;
            frameImage.color = Color.white;
            frameImage.raycastTarget = false;

            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 1f;               // ★ Prefab 里要看得见（美术得拖位置、对构图）；
                                            //   运行时的"收起"由 SlotVisual.Awake / ResetToEmpty 负责

            var frame = go.AddComponent<PortraitFrame>();
            var so = new SerializedObject(frame);
            so.FindProperty("frame").objectReferenceValue = frameImage;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("animSet").objectReferenceValue = animSet;
            so.FindProperty("hiddenScaleMode").enumValueIndex = (int)VisualScaleMode.Rect;   // 界面部件：收起/高亮改尺寸，不缩九宫格边框
            so.FindProperty("hiddenScale").vector3Value = Vector3.one;   // 静息态 = 编辑器一致；"弹出"由动画的起始缩放负责
            so.FindProperty("hiddenAlpha").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            return frame;
        }
        /// <summary>
        /// 立绘：框里那张画（兄弟节点，独立位置/独立动画）。
        /// ★ 尺寸就是这里摆的样子，运行时不会被代码改（所见即所得）；
        ///   fitMode 默认只保证"不变形"（preserveAspect），不动 rect。
        /// </summary>
        private static Portrait CreatePortrait(Transform parent, string name, Vector2 anchoredPosition,
            Sprite portraitSprite, PortraitAnimSet animSet)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);           // 立绘以中心为基准，缩放/动画都围绕中心
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(420f, 660f);

            var image = go.GetComponent<Image>();
            image.sprite = portraitSprite;
            image.type = Image.Type.Simple;                 // 立绘是整张画
            image.preserveAspect = true;                    // 只保比例，不动尺寸
            image.color = Color.white;
            image.raycastTarget = false;

            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 1f;

            var portrait = go.AddComponent<Portrait>();
            var so = new SerializedObject(portrait);
            so.FindProperty("image").objectReferenceValue = image;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("animSet").objectReferenceValue = animSet;
            so.FindProperty("hiddenScaleMode").enumValueIndex = (int)VisualScaleMode.Rect;   // 和上面的动画集保持一致（都改尺寸）
            so.FindProperty("hiddenScale").vector3Value = Vector3.one;   // 静息态 = 编辑器一致；"弹出"由动画的起始缩放负责
            so.FindProperty("hiddenAlpha").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            return portrait;
        }
    }
}
