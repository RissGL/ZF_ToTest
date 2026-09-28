using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 一键搭建「完整对话流程」测试场景：章节数据 → Model → DialogueView → 换模板 / 立绘 / 气泡 / 选项，
    /// 把整条链一次接通。菜单：Tools → 对话系统 → 搭建对话流程测试场景
    ///
    /// 和另外两个搭建器的分工：
    ///   - 气泡测试场景：只测气泡本身，按 1/2/3/4 手动弹
    ///   - 生成默认模板 Prefab：产出**模板资产**（做分镜用）
    ///   - 这个：产出**测试场景**，Play 就能看到换格编排
    ///   ★ 别和气泡测试 rig 同时留在场景里 —— BubbleDemo 会抢空格和数字键。
    ///
    /// 会生成：
    ///   ① 两个模板 Prefab：Template_0（左右二人构图）/ Template_1（单人居中构图）
    ///   ② 模板表 DialogueTemplateTable_Demo（0 → Template_0，1 → Template_1）
    ///   ③ 测试角色 Speaker_Alan（阿岚）/ Speaker_Zhang（老张）+ 测试章节 Chapter_Demo
    ///      章节里轮流用 templateId 0 / 1，正好能看到「换格」发生在哪
    ///   ④ 场景：DialogueTemplateHost（模板实例都在它下面）+ DialogueFlow（DialogueView + 控制器 + 转场）
    /// 底图 / 样式 / 动画 / 字体复用气泡测试场景那套资产。
    /// </summary>
    public static class DialogueFlowSceneBuilder
    {
        private const string DemoFolder = "Assets/Scripts/ZFrameworkTest/DialogueSystem/View/Generated";
        private const string AlanPath = DemoFolder + "/Speaker_Alan.asset";
        private const string ZhangPath = DemoFolder + "/Speaker_Zhang.asset";
        private const string ChapterPath = DemoFolder + "/Chapter_Demo.asset";
        private const string TablePath = DemoFolder + "/DialogueTemplateTable_Demo.asset";
        private const string RigName = "DialogueFlowRig";
        private const string WipeLayerName = "DialogueWipeLayer";
        private const string MangaDissolveShaderPath =
            "Assets/Scripts/ZFrameworkTest/DialogueSystem/View/Transition/Shaders/MangaNoiseDissolve.shader";
        private const string MangaDissolveTMPShaderPath =
            "Assets/Scripts/ZFrameworkTest/DialogueSystem/View/Transition/Shaders/MangaNoiseDissolve_TMP.shader";

        /// <summary>★ 角色资产 name 只是显示名；谁进哪个槽由数据决定（不靠名字配对）</summary>
        private const string AlanName = "阿岚";
        private const string ZhangName = "老张";

        [MenuItem("Tools/对话系统/搭建对话流程测试场景")]
        public static void Build()
        {
            BubbleTestSceneBuilder.EnsureFolder(DemoFolder);
            BubbleTestSceneBuilder.EnsureFolder(DialogueTemplateBuilder.TemplateFolder);

            TMP_FontAsset font = BubbleTestSceneBuilder.EnsureChineseFont(out string fontNote);
            Sprite bubbleSprite = BubbleTestSceneBuilder.EnsureBubbleSprite();
            BubbleAnimSet bubbleAnim = BubbleTestSceneBuilder.EnsureAnimSet();
            BubbleStyleSO bubbleStyle = BubbleTestSceneBuilder.EnsureStyle(bubbleSprite, bubbleAnim);
            Sprite frameSprite = DialogueTemplateBuilder.EnsureFrameSprite();
            Sprite portraitSprite = DialogueTemplateBuilder.EnsurePortraitSprite();
            PortraitAnimSet portraitAnim = DialogueTemplateBuilder.EnsurePortraitAnimSet();

            DialogueSpeaker alan = EnsureSpeaker(AlanPath, AlanName, 1);
            DialogueSpeaker zhang = EnsureSpeaker(ZhangPath, ZhangName, 2);

            // 两套模板 + 表
            var template0 = EnsureTemplate("Template_0", 0, TemplateLayout.TwoSpeakers,
                bubbleSprite, bubbleStyle, frameSprite, portraitSprite, portraitAnim, font);
            var template1 = EnsureTemplate("Template_1", 1, TemplateLayout.SingleCenter,
                bubbleSprite, bubbleStyle, frameSprite, portraitSprite, portraitAnim, font);
            DialogueTemplateTable table = EnsureTable(template0, template1);

            DialogueChapterSO chapter = EnsureDemoChapter(alan, zhang);

            BuildRig(bubbleSprite, bubbleStyle, font, chapter, table);

            WarnAboutOtherSystems();

            Debug.Log($"[对话流程测试] 搭好了。{fontNote}\n" +
                      "按 Play 直接进对话：空格 / 回车 / 点击 = 继续，打字中按 = 补全，↑↓ = 选，回车 / 数字键 = 确认选项。\n" +
                      "流程覆盖：阿岚连说两句（同一槽收掉重弹）→ 老张插话（另一个槽，两人同时在场）\n" +
                      "          → 旁白（旁白槽，没立绘）→ 换格（templateId 0 → 1，看擦除/换模板/立绘入场）\n" +
                      "          → 组尾弹选项 → 选择跳组 → 章末全收");
        }

        [MenuItem("Tools/对话系统/清掉对话流程测试场景")]
        public static void Clear()
        {
            var rig = GameObject.Find(RigName);
            if (rig == null)
            {
                Debug.Log("[对话流程测试] 场景里没有对话流程测试 rig");
                return;
            }

            Undo.DestroyObjectImmediate(rig);
            Debug.Log("[对话流程测试] 已清掉对话流程测试 rig（生成的资产留着，下次搭建直接复用）");
        }

        // ===================== 资产 =====================

        /// <summary>测试角色：name 只是显示名（气泡按槽走，不再按角色名配对）</summary>
        private static DialogueSpeaker EnsureSpeaker(string path, string speakerName, int id)
        {
            var existing = AssetDatabase.LoadAssetAtPath<DialogueSpeaker>(path);
            if (existing != null)
            {
                return existing;
            }

            var speaker = ScriptableObject.CreateInstance<DialogueSpeaker>();
            speaker.name = speakerName;      // ← DialogueSpeaker 自己声明的 name 字段（不是资产文件名）
            speaker.id = id;

            AssetDatabase.CreateAsset(speaker, path);
            AssetDatabase.SaveAssets();
            return speaker;
        }

        /// <summary>模板 Prefab：每次搭建都重建，保证形状和代码里的定义一致</summary>
        private static DialogueLayoutRefs EnsureTemplate(string templateName, int templateId, TemplateLayout layout,
            Sprite bubbleSprite, BubbleStyleSO bubbleStyle,
            Sprite frameSprite, Sprite portraitSprite, PortraitAnimSet portraitAnim,
            TMP_FontAsset font)
        {
            GameObject root = DialogueTemplateBuilder.BuildTemplateHierarchy(templateName, layout,
                bubbleSprite, bubbleStyle, frameSprite, portraitSprite, portraitAnim, font);

            // 备注一下自己的 id（运行时靠表里的映射，不靠这个字段）
            var refs = root.GetComponent<DialogueLayoutRefs>();
            if (refs != null)
            {
                var so = new SerializedObject(refs);
                so.FindProperty("templateId").intValue = templateId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            string path = $"{DialogueTemplateBuilder.TemplateFolder}/{templateName}.prefab";
            return DialogueTemplateBuilder.SaveTemplate(root, path);
        }

        /// <summary>模板表：0 → 左右二人构图，1 → 单人居中构图</summary>
        private static DialogueTemplateTable EnsureTable(DialogueLayoutRefs template0, DialogueLayoutRefs template1)
        {
            var table = AssetDatabase.LoadAssetAtPath<DialogueTemplateTable>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<DialogueTemplateTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            table.entries = new List<DialogueTemplateTable.Entry>
            {
                new DialogueTemplateTable.Entry { templateId = 0, prefab = template0 },
                new DialogueTemplateTable.Entry { templateId = 1, prefab = template1 },
            };

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            return table;
        }

        /// <summary>
        /// 测试章节（每次搭建都重建内容，免得旧数据里没有 templateId / slotId）：
        ///
        ///   Group_1（模板 0）阿岚 / 阿岚 / 老张   → 同角色同槽重弹、两人同时在场的两个槽
        ///   Group_2（模板 0）旁白 / 阿岚 / 老张   → 旁白走"没有立绘"的旁白槽（反向擦）
        ///   Group_3（模板 1）阿岚 + 两个选项     → 换模板（二人构图 → 单人居中）+ 组尾弹选项（条纹擦）
        ///   Group_4（模板 0）「进店看看」落点     → 再换回模板 0
        ///   Group_5（模板 1）「还是别多事」落点
        /// </summary>
        private static DialogueChapterSO EnsureDemoChapter(DialogueSpeaker alan, DialogueSpeaker zhang)
        {
            var chapter = AssetDatabase.LoadAssetAtPath<DialogueChapterSO>(ChapterPath);
            if (chapter == null)
            {
                chapter = ScriptableObject.CreateInstance<DialogueChapterSO>();
                AssetDatabase.CreateAsset(chapter, ChapterPath);
            }

            chapter.chapterId = "demo";
            chapter.startGroupId = "Group_1";
            chapter.groups = new List<DialogueShowGroupData>
            {
                // ★ 离场动画：字段挂在**本格**上，**离开本格**那一刻播（「从 A 擦到 B」用的是 A 的值）
                //   所以这里把值配在"要走的那一格"上：Group_1 走时擦 · Group_2 走时溶解 · Group_3 走时条纹 …
                //   最后一格的值不会播（章末现在不播动画）
                Group("Group_1", 0, "Group_2", "wipe", WipeDirection.RightToLeft,
                    Line(alan, "喂，你看见刚才那个影子了吗？"),
                    Line(alan, "别装没听见，就在便利店那边。"),
                    Line(zhang, "……我什么都没看见。")),

                Group("Group_2", 0, "Group_3", "manga_dissolve",
                    Line(null, "便利店的灯闪了两下。"),
                    Line(alan, "看吧，我就说有东西。"),
                    Line(zhang, "那是电压不稳。")),

                GroupWithChoices("Group_3", 1, "bars_in",
                    new[] { Line(alan, "要不要进去看看？") },
                    new[]
                    {
                        Choice("进店看看", "Group_4"),
                        Choice("还是别多事", "Group_5"),
                    }),

                // wipe = 同一套组件，方向由组数据传
                Group("Group_4", 0, null, "wipe", WipeDirection.LeftToRight, Line(alan, "……好吧，跟紧我。")),
                Group("Group_5", 1, null, null, Line(zhang, "这才对嘛，回家。")),
            };

            // 本格出场角色：Group_3 里只有阿岚说话，但老张也站在场上（演示"不说话的也算"）
            chapter.groups[2].cast = new List<DialogueSpeaker> { alan, zhang };

            EditorUtility.SetDirty(chapter);
            AssetDatabase.SaveAssets();
            return chapter;
        }

        private static DialogueShowGroupData Group(string groupId, int templateId, string nextGroupId,
            string transitionId, params DialogueShowTextItem[] lines)
        {
            return Group(groupId, templateId, nextGroupId, transitionId, WipeDirection.ComponentDefault, lines);
        }

        private static DialogueShowGroupData Group(string groupId, int templateId, string nextGroupId,
            string transitionId, WipeDirection direction, params DialogueShowTextItem[] lines)
        {
            return new DialogueShowGroupData
            {
                groupId = groupId,
                templateId = templateId,
                transitionId = transitionId,
                transitionDirection = direction,
                nextGroupId = nextGroupId,
                groupTexts = new List<DialogueShowTextItem>(lines),
            };
        }

        private static DialogueShowGroupData GroupWithChoices(string groupId, int templateId, string transitionId,
            DialogueShowTextItem[] lines, DialogueChoiceData[] choices)
        {
            var group = Group(groupId, templateId, null, transitionId, lines);
            group.choices = new List<DialogueChoiceData>(choices);
            return group;
        }

        /// <summary>speaker 传 null = 旁白（会自动领"没有立绘框"的旁白槽）</summary>
        private static DialogueShowTextItem Line(DialogueSpeaker speaker, string text)
        {
            return new DialogueShowTextItem
            {
                speaker = speaker,
                text = text,
                slotId = null,          // 空 = 自动分配
            };
        }

        private static DialogueChoiceData Choice(string text, string targetGroupId)
        {
            return new DialogueChoiceData
            {
                text = text,
                targetGroupId = targetGroupId,
            };
        }

        // ===================== 场景 =====================

        private static void BuildRig(Sprite bubbleSprite, BubbleStyleSO bubbleStyle, TMP_FontAsset font,
            DialogueChapterSO chapter, DialogueTemplateTable table)
        {
            var oldRig = GameObject.Find(RigName);
            if (oldRig != null)
            {
                Undo.DestroyObjectImmediate(oldRig);
            }

            Canvas canvas = BubbleTestSceneBuilder.EnsureCanvas();

            var rig = new GameObject(RigName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(rig, "对话流程测试场景");
            rig.transform.SetParent(canvas.transform, false);
            BubbleTestSceneBuilder.Stretch(rig.GetComponent<RectTransform>());

            // ---- 模板容器：模板实例都挂在它下面（隐藏保留复用）----
            var hostGo = new GameObject("TemplateHost", typeof(RectTransform));
            hostGo.transform.SetParent(rig.transform, false);
            BubbleTestSceneBuilder.Stretch(hostGo.GetComponent<RectTransform>());

            var host = hostGo.AddComponent<DialogueTemplateHost>();
            var hostSo = new SerializedObject(host);
            hostSo.FindProperty("table").objectReferenceValue = table;
            hostSo.FindProperty("fallbackTemplateId").intValue = 0;
            hostSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 流程驱动：DialogueView + 控制器 + 转场 ----
            var flowGo = new GameObject("DialogueFlow", typeof(RectTransform));
            flowGo.transform.SetParent(rig.transform, false);
            BubbleTestSceneBuilder.Stretch(flowGo.GetComponent<RectTransform>());

            var transition = flowGo.AddComponent<InstantDialogueTransition>();   // 兜底：不擦直接切

            var view = flowGo.AddComponent<DialogueView>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("templateHost").objectReferenceValue = host;
            viewSo.FindProperty("transitionSource").objectReferenceValue = transition;
            viewSo.FindProperty("transitionOnEveryGroup").boolValue = true;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            // 擦除层：默认擦除（左→右）+ 登记表（组头填「擦除 id」就能选种类/方向）
            BuildWipeLayer(canvas, rig.transform, view);

            var controller = flowGo.AddComponent<DialoguePlayerController>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("chapter").objectReferenceValue = chapter;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = flowGo;
            EditorGUIUtility.PingObject(flowGo);
        }

        /// <summary>
        /// 擦除层（挂在 Canvas 上、排在 rig 之后 = 画在模板上面）：
        ///   默认擦除 = 左→右的遮罩平移；登记表登记 4 个 id，组头的「擦除 id」填哪个就用哪个。
        ///
        /// ★ 擦除层**不能放进模板 Prefab**：擦除盖住的那一瞬正是换模板的时候，
        ///   放进模板里的擦除层会跟着模板一起被隐藏，画面会闪。
        /// </summary>
        private static DialogueTransitionLibrary BuildWipeLayer(Canvas canvas, Transform rig, DialogueView view)
        {
            var old = GameObject.Find(WipeLayerName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old);
            }

            var layerGo = new GameObject(WipeLayerName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(layerGo, "对话擦除层");
            layerGo.transform.SetParent(canvas.transform, false);
            BubbleTestSceneBuilder.Stretch(layerGo.GetComponent<RectTransform>());

            if (rig != null)
            {
                layerGo.transform.SetSiblingIndex(Mathf.Min(rig.GetSiblingIndex() + 1, canvas.transform.childCount - 1));
            }

            var wipeLr = CreateMaskWipe(layerGo.transform, "Wipe_LR", WipeDirection.LeftToRight);
            var wipeRl = CreateMaskWipe(layerGo.transform, "Wipe_RL", WipeDirection.RightToLeft);
            var bars = CreateBarsWipe(layerGo.transform, "Bars_In", WipeDirection.LeftToRight);
            var manga = CreateMangaDissolve(layerGo.transform, "Manga_Dissolve", view);

            // 「instant」用 rig 上那个兜底组件：某一格想直接切就填它
            var instant = rig != null ? rig.GetComponentInChildren<InstantDialogueTransition>(true) : null;

            var library = layerGo.AddComponent<DialogueTransitionLibrary>();
            var librarySo = new SerializedObject(library);
            var entries = librarySo.FindProperty("entries");
            entries.arraySize = 6;
            SetTransitionEntry(entries.GetArrayElementAtIndex(0), "wipe", wipeLr);       // ★ 方向由组数据传：一套组件服务所有方向
            SetTransitionEntry(entries.GetArrayElementAtIndex(1), "wipe_lr", wipeLr);    // 老 id 保留（方向写死在组件上）
            SetTransitionEntry(entries.GetArrayElementAtIndex(2), "wipe_rl", wipeRl);
            SetTransitionEntry(entries.GetArrayElementAtIndex(3), "bars_in", bars);
            SetTransitionEntry(entries.GetArrayElementAtIndex(4), "instant", instant);
            SetTransitionEntry(entries.GetArrayElementAtIndex(5), "manga_dissolve", manga);
            librarySo.ApplyModifiedPropertiesWithoutUndo();

            // 默认擦除（只在组头填的 id 找不到时兜底；组头**空** = 这一格离场时直接切）
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("transitionSource").objectReferenceValue = manga;
            viewSo.FindProperty("transitionLibrary").objectReferenceValue = library;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            return library;
        }

        /// <summary>漫画网点噪波溶解：不需要子物体（材质贴在旧画面的部件上，噪波图运行时生成）</summary>
        private static MangaNoiseDissolveTransition CreateMangaDissolve(Transform parent, string name, DialogueView view)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            BubbleTestSceneBuilder.Stretch(go.GetComponent<RectTransform>());

            var dissolve = go.AddComponent<MangaNoiseDissolveTransition>();

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(MangaDissolveShaderPath);
            if (shader == null)
            {
                Debug.LogWarning($"[对话擦除] 找不到 shader：{MangaDissolveShaderPath}" +
                                 "（运行时只能按名字 Shader.Find，打包时可能被裁掉）");
            }

            var shaderTMP = AssetDatabase.LoadAssetAtPath<Shader>(MangaDissolveTMPShaderPath);
            if (shaderTMP == null)
            {
                Debug.LogWarning($"[对话擦除] 找不到 TMP 版 shader：{MangaDissolveTMPShaderPath}（文字将不参与擦除）");
            }

            var so = new SerializedObject(dissolve);
            so.FindProperty("frameSource").objectReferenceValue = view;      // DialogueView 就是 IDialogueFrameSource
            so.FindProperty("shader").objectReferenceValue = shader;
            so.FindProperty("shaderTMP").objectReferenceValue = shaderTMP;
            so.FindProperty("duration").floatValue = 0.55f;
            so.FindProperty("direction").enumValueIndex = (int)WipeDirection.LeftToRight;
            so.ApplyModifiedPropertiesWithoutUndo();

            return dissolve;
        }

        private static void SetTransitionEntry(SerializedProperty entry, string id, MonoBehaviour source)
        {
            entry.FindPropertyRelative("id").stringValue = id;
            entry.FindPropertyRelative("source").objectReferenceValue = source;
        }

        /// <summary>遮罩平移擦除：一个容器 + 一张「纸」（纯色方块就够，Image 不给 sprite = 纯色块）</summary>
        private static MaskWipeTransition CreateMaskWipe(Transform parent, string name, WipeDirection direction)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            BubbleTestSceneBuilder.Stretch(go.GetComponent<RectTransform>());

            var paperGo = new GameObject("Paper", typeof(RectTransform), typeof(Image));
            paperGo.transform.SetParent(go.transform, false);

            var paper = paperGo.GetComponent<RectTransform>();
            paper.anchorMin = paper.anchorMax = new Vector2(0.5f, 0.5f);
            paper.pivot = new Vector2(0.5f, 0.5f);
            paper.sizeDelta = new Vector2(1920f, 1080f);      // 运行时按画布重算，这里的值只是编辑器里看着顺眼
            paperGo.GetComponent<Image>().raycastTarget = false;

            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;                                 // 编辑器里先藏好，不然它会盖住模板挡着你摆位

            var wipe = go.AddComponent<MaskWipeTransition>();
            var so = new SerializedObject(wipe);
            so.FindProperty("cover").objectReferenceValue = paper;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("direction").enumValueIndex = (int)direction;
            so.FindProperty("duration").floatValue = 0.55f;
            so.FindProperty("edgeWidth").floatValue = 0f;      // 硬边；有撕纸边贴图再把前缘接上、这里给宽度
            so.ApplyModifiedPropertiesWithoutUndo();

            return wipe;
        }

        /// <summary>条纹刷入擦除：条容器 + 第一条（其余条运行时按它拷）</summary>
        private static BarsWipeTransition CreateBarsWipe(Transform parent, string name, WipeDirection direction)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            BubbleTestSceneBuilder.Stretch(go.GetComponent<RectTransform>());

            var rootGo = new GameObject("BarRoot", typeof(RectTransform));
            rootGo.transform.SetParent(go.transform, false);
            var root = rootGo.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(1920f, 1080f);

            var barGo = new GameObject("Bar_0", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(rootGo.transform, false);
            var bar = barGo.GetComponent<RectTransform>();
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
            bar.pivot = new Vector2(0.5f, 0.5f);
            bar.sizeDelta = new Vector2(1920f, 135f);
            barGo.GetComponent<Image>().raycastTarget = false;

            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;

            var bars = go.AddComponent<BarsWipeTransition>();
            var so = new SerializedObject(bars);
            so.FindProperty("barRoot").objectReferenceValue = root;
            so.FindProperty("barTemplate").objectReferenceValue = bar;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("barCount").intValue = 8;
            so.FindProperty("direction").enumValueIndex = (int)direction;
            so.FindProperty("stagger").floatValue = 0.045f;
            so.FindProperty("segmentDuration").floatValue = 0.28f;
            so.ApplyModifiedPropertiesWithoutUndo();

            return bars;
        }

        /// <summary>场景里还有别的东西在吃输入时提醒一句（不然测出来的现象会怪）</summary>
        private static void WarnAboutOtherSystems()
        {
            if (GameObject.Find("BubbleTestRig") != null)
            {
                Debug.LogWarning("[对话流程测试] 场景里还有气泡测试 rig（BubbleTestRig）。" +
                                 "它的 BubbleDemo 会抢空格和 1/2/3/4，测流程前建议先点 Tools/气泡系统/清掉测试场景。");
            }

            if (Object.FindObjectOfType<GravityAniController>() != null)
            {
                Debug.LogWarning("[对话流程测试] 场景里有漫画控制器（GravityAniController），" +
                                 "它也可能在吃空格/点击，和对话推进打架。测对话流程建议先把它禁用或换个空场景。");
            }
        }
    }
}
