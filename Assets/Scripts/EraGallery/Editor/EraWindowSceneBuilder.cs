using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ZF.EraGallery.EditorTools
{
    /// <summary>
    /// 一键搭出「四个时代窗口」的场景。
    /// 菜单：Tools → 时代窗口 → 搭建四个时代窗口场景
    ///
    /// 会生成：
    ///   ① 一张 1x1 世界单位的白色方块图（唯一的美术资源，所有色块都是它缩放 + 染色拼的）
    ///   ② 场景里一个 EraWorld 根节点，下面 2x2 摆四个时代窗口
    ///   ③ 主相机上的 EraCameraRig（相机执行器）
    ///
    /// 时代名现在没有字体可画，所以窗口上只有「主题色 + 时代图案 + 序号点 + 锁/对勾标记」，
    /// 名字打在 Console 里。等工程里导入中文字体后，在 EraWindow 上加个 TMP 引用就能补上文字。
    /// </summary>
    public static class EraWindowSceneBuilder
    {
        private const string GeneratedFolder = "Assets/Scripts/EraGallery/Generated";
        private const string WhiteSpritePath = GeneratedFolder + "/EraWhite.png";
        private const string RootName = "EraWorld";

        // 占位窗口尺寸（世界单位）。改这里的话，EraWindow 上的聚焦框也要跟着改。
        private const float ContentWidth = 6f;
        private const float ContentHeight = 3.6f;
        private const float FrameThickness = 0.16f;
        private const float PitchX = 7.8f;
        private const float PitchY = 5.6f;
        private const float FocusWidth = 6.8f;
        private const float FocusHeight = 5.0f;
        private const float FocusOffsetY = -0.35f;
        private const float MarkY = -2.35f;

        [MenuItem("Tools/时代窗口/搭建四个时代窗口场景", false, 10)]
        public static void Build()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[时代窗口] Play 模式下不能改场景，先停下来再点这个菜单。");
                return;
            }

            EnsureFolder(GeneratedFolder);

            Sprite white = EnsureWhiteSprite();
            if (white == null)
            {
                Debug.LogError("[时代窗口] 占位方块图生成失败，搭不下去。");
                return;
            }

            Camera camera = EnsureCamera();
            if (camera == null)
            {
                Debug.LogError("[时代窗口] 找不到也建不出相机。");
                return;
            }

            ClearInternal();

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "搭建四个时代窗口");
            EraWorldController controller = root.AddComponent<EraWorldController>();

            EraCameraRig rig = camera.GetComponent<EraCameraRig>();
            if (rig == null)
            {
                rig = camera.gameObject.AddComponent<EraCameraRig>();
            }

            List<EraWindow> windows = new List<EraWindow>();

            for (int i = 0; i < EraCatalog.All.Length; i++)
            {
                EraPresentation data = EraCatalog.All[i];
                int column = i % 2;
                int row = i / 2;

                Vector3 center = new Vector3(
                    (column == 0 ? -0.5f : 0.5f) * PitchX,
                    (row == 0 ? 0.5f : -0.5f) * PitchY,
                    0f);

                windows.Add(BuildWindow(root.transform, data, i, center, white));
            }

            BindController(controller, camera, rig, windows);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("[时代窗口] 搭好了：四个时代窗口 + 相机执行器。\n" +
                      "  按 Play：左键点窗口进入 / 右键或 ESC 退回全景 / 数字键 1-4 直达 / C 把当前时代标记为通关（解锁下一个）。\n" +
                      "  时代名暂时只打在 Console 里 —— 工程里还没有中文字体，导入字体后再往窗口上挂文字。");
        }

        [MenuItem("Tools/时代窗口/清掉四个时代窗口场景", false, 11)]
        public static void Clear()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[时代窗口] Play 模式下不能改场景。");
                return;
            }

            if (GameObject.Find(RootName) == null)
            {
                Debug.Log($"[时代窗口] 场景里没有「{RootName}」。");
                return;
            }

            ClearInternal();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[时代窗口] 已清掉「{RootName}」。");
        }

        private static void ClearInternal()
        {
            GameObject root = GameObject.Find(RootName);
            if (root != null)
            {
                Undo.DestroyObjectImmediate(root);
            }
        }

        // ===================== 单个窗口 =====================

        private static EraWindow BuildWindow(Transform parent, EraPresentation data, int index, Vector3 center, Sprite white)
        {
            GameObject go = new GameObject($"EraWindow_{index}_{data.id}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;

            EraWindow window = go.AddComponent<EraWindow>();
            BoxCollider2D hitArea = go.AddComponent<BoxCollider2D>();
            hitArea.isTrigger = true;   // 只用来点选，不参与物理（以后窗口里的角色才不会被挡住）
            hitArea.offset = Vector2.zero;
            hitArea.size = new Vector2(ContentWidth + FrameThickness * 2f, ContentHeight + FrameThickness * 2f);

            Color theme = data.theme;

            // 遮光板：进到这个时代里时打开，挡住旁边三个窗口
            SpriteRenderer backdrop = CreateRect(go.transform, "Backdrop", Vector2.zero,
                new Vector2(ContentWidth * 5f, ContentHeight * 5f),
                new Color(0.043f, 0.047f, 0.07f, 1f), EraSortingOrder.Backdrop, white);
            backdrop.enabled = false;

            List<SpriteRenderer> tinted = new List<SpriteRenderer>();

            // 内容底色
            SpriteRenderer content = CreateRect(go.transform, "Content", Vector2.zero,
                new Vector2(ContentWidth, ContentHeight), theme, EraSortingOrder.Content, white);
            tinted.Add(content);

            // 地景：内容底部一条压暗的横条，让块面看着像「一片地方」而不是一个色板
            tinted.Add(CreateRect(go.transform, "Ground",
                new Vector2(0f, -ContentHeight * 0.5f + 0.25f),
                new Vector2(ContentWidth, 0.5f),
                Mul(theme, 0.55f), EraSortingOrder.Decoration, white));

            // 时代图案：四个时代各一套剪影，一眼能分辨
            tinted.AddRange(BuildEraSignature(go.transform, data.id, theme, white));

            // 时代序号点：亮几个就是第几个时代
            tinted.AddRange(BuildOrderDots(go.transform, index, theme, white));

            // 边框：四条纯色边拼的矩形框（省掉九宫格图）
            Color frameColor = Color.Lerp(theme, Color.white, 0.5f);
            float halfWidth = ContentWidth * 0.5f + FrameThickness * 0.5f;
            float halfHeight = ContentHeight * 0.5f + FrameThickness * 0.5f;
            float outerWidth = ContentWidth + FrameThickness * 2f;

            List<SpriteRenderer> frameParts = new List<SpriteRenderer>
            {
                CreateRect(go.transform, "FrameTop", new Vector2(0f, halfHeight),
                    new Vector2(outerWidth, FrameThickness), frameColor, EraSortingOrder.Frame, white),
                CreateRect(go.transform, "FrameBottom", new Vector2(0f, -halfHeight),
                    new Vector2(outerWidth, FrameThickness), frameColor, EraSortingOrder.Frame, white),
                CreateRect(go.transform, "FrameLeft", new Vector2(-halfWidth, 0f),
                    new Vector2(FrameThickness, ContentHeight), frameColor, EraSortingOrder.Frame, white),
                CreateRect(go.transform, "FrameRight", new Vector2(halfWidth, 0f),
                    new Vector2(FrameThickness, ContentHeight), frameColor, EraSortingOrder.Frame, white),
            };

            BuildStateMarks(go.transform, out GameObject markLocked, out GameObject markDone, white);

            // 默认规则下四个时代一开始就都能进，所以两个标记都先收起来。
            // 以后要改成一关一关解，EraWorldController 上的解锁规则换成 PreviousCompleted 即可，
            // 锁标记会自动出现在还没解锁的窗口上。
            markLocked.SetActive(false);
            markDone.SetActive(false);

            BindWindow(window, data, backdrop, content, frameParts, tinted, markLocked, markDone, hitArea);

            return window;
        }

        /// <summary>每个时代一套几何剪影，全部由白色方块缩放/旋转拼出来，不需要美术资源。</summary>
        private static List<SpriteRenderer> BuildEraSignature(Transform parent, EraId era, Color theme, Sprite white)
        {
            List<SpriteRenderer> shapes = new List<SpriteRenderer>();
            Color color = Color.Lerp(theme, Color.white, 0.62f);

            switch (era)
            {
                case EraId.Stone:
                {
                    // 金字塔：一层层窄上去
                    float[] widths = { 2.6f, 2.05f, 1.5f, 0.95f, 0.42f };
                    const float barHeight = 0.3f;
                    const float baseY = -0.75f;

                    for (int i = 0; i < widths.Length; i++)
                    {
                        shapes.Add(CreateRect(parent, $"Sig_Pyramid_{i}",
                            new Vector2(-1f, baseY + i * barHeight),
                            new Vector2(widths[i], barHeight), color, EraSortingOrder.Decoration, white));
                    }

                    // 太阳
                    shapes.Add(CreateRect(parent, "Sig_Sun", new Vector2(1.75f, 0.85f),
                        new Vector2(0.62f, 0.62f), color, EraSortingOrder.Decoration, white));
                    break;
                }

                case EraId.Steam:
                {
                    // 烟囱
                    float[] heights = { 1.5f, 1.1f, 0.75f };
                    float[] xs = { -1.9f, -1.35f, -0.8f };
                    const float baseY = -0.7f;

                    for (int i = 0; i < heights.Length; i++)
                    {
                        shapes.Add(CreateRect(parent, $"Sig_Chimney_{i}",
                            new Vector2(xs[i], baseY + heights[i] * 0.5f),
                            new Vector2(0.36f, heights[i]), color, EraSortingOrder.Decoration, white));
                    }

                    shapes.Add(CreateRect(parent, "Sig_Base", new Vector2(-1.35f, baseY - 0.14f),
                        new Vector2(2.6f, 0.28f), color, EraSortingOrder.Decoration, white));

                    // 齿轮：一个方块加十字
                    shapes.Add(CreateRect(parent, "Sig_GearHub", new Vector2(1.6f, 0.35f),
                        new Vector2(0.7f, 0.7f), color, EraSortingOrder.Decoration, white));
                    shapes.Add(CreateRect(parent, "Sig_GearV", new Vector2(1.6f, 0.35f),
                        new Vector2(0.22f, 1.15f), color, EraSortingOrder.Decoration, white));
                    shapes.Add(CreateRect(parent, "Sig_GearH", new Vector2(1.6f, 0.35f),
                        new Vector2(1.15f, 0.22f), color, EraSortingOrder.Decoration, white));
                    break;
                }

                case EraId.Electric:
                {
                    // 闪电：两段斜杠
                    shapes.Add(CreateBar(parent, "Sig_BoltA", new Vector2(-2f, 0.95f), new Vector2(-1.25f, -0.05f),
                        0.26f, color, EraSortingOrder.Decoration, white));
                    shapes.Add(CreateBar(parent, "Sig_BoltB", new Vector2(-1.65f, 0.15f), new Vector2(-0.85f, -0.95f),
                        0.26f, color, EraSortingOrder.Decoration, white));

                    // 灯 + 电线：电线压到灯下面（同一层级又重叠的话画的顺序不确定）
                    shapes.Add(CreateRect(parent, "Sig_Wire", new Vector2(0.35f, 0.6f),
                        new Vector2(1.7f, 0.09f), Mul(color, 0.7f), EraSortingOrder.Decoration, white));
                    shapes.Add(CreateRect(parent, "Sig_Bulb", new Vector2(1.5f, 0.6f),
                        new Vector2(0.66f, 0.66f), color, EraSortingOrder.Decoration + 1, white));
                    break;
                }

                case EraId.Information:
                {
                    // 节点网格 + 连线
                    const float step = 0.72f;
                    Vector2 origin = new Vector2(-1.75f, 0.1f);

                    shapes.Add(CreateRect(parent, "Sig_GridH", origin,
                        new Vector2(step * 2f + 0.22f, 0.09f), Mul(color, 0.6f), EraSortingOrder.Decoration, white));
                    shapes.Add(CreateRect(parent, "Sig_GridV", origin,
                        new Vector2(0.09f, step * 2f + 0.22f), Mul(color, 0.6f), EraSortingOrder.Decoration, white));

                    for (int row = 0; row < 3; row++)
                    {
                        for (int column = 0; column < 3; column++)
                        {
                            bool isCenter = row == 1 && column == 1;
                            float size = isCenter ? 0.34f : 0.24f;

                            shapes.Add(CreateRect(parent, $"Sig_Node_{row}_{column}",
                                origin + new Vector2((column - 1) * step, (row - 1) * step),
                                new Vector2(size, size),
                                isCenter ? Color.Lerp(color, Color.white, 0.5f) : color,
                                EraSortingOrder.Decoration + 1, white));
                        }
                    }

                    // 外圈
                    shapes.Add(CreateRect(parent, "Sig_Ring", new Vector2(1.85f, 0.1f),
                        new Vector2(0.7f, 0.7f), color, EraSortingOrder.Decoration, white));
                    break;
                }
            }

            return shapes;
        }

        /// <summary>内容左上角一排小点：亮几个就是第几个时代。</summary>
        private static List<SpriteRenderer> BuildOrderDots(Transform parent, int index, Color theme, Sprite white)
        {
            List<SpriteRenderer> dots = new List<SpriteRenderer>();
            Color on = Color.Lerp(theme, Color.white, 0.75f);
            Color off = Mul(theme, 0.32f);

            const float startX = -2.68f;
            const float step = 0.26f;
            const float y = 1.48f;
            const float size = 0.16f;

            for (int i = 0; i < EraCatalog.Count; i++)
            {
                dots.Add(CreateRect(parent, $"Order_{i}", new Vector2(startX + i * step, y),
                    new Vector2(size, size), i <= index ? on : off, EraSortingOrder.Decoration + 2, white));
            }

            return dots;
        }

        /// <summary>窗口下方那排状态标记：锁 / 对勾。都是两根斜杠拼的。</summary>
        private static void BuildStateMarks(Transform parent, out GameObject markLocked, out GameObject markDone, Sprite white)
        {
            GameObject lockedRoot = new GameObject("Mark_Locked");
            lockedRoot.transform.SetParent(parent, false);
            lockedRoot.transform.localPosition = new Vector3(0f, MarkY, 0f);

            Color lockedColor = new Color(0.66f, 0.63f, 0.70f, 1f);
            CreateBar(lockedRoot.transform, "LockA", new Vector2(-0.22f, 0.22f), new Vector2(0.22f, -0.22f),
                0.13f, lockedColor, EraSortingOrder.Mark, white);
            CreateBar(lockedRoot.transform, "LockB", new Vector2(-0.22f, -0.22f), new Vector2(0.22f, 0.22f),
                0.13f, lockedColor, EraSortingOrder.Mark, white);

            GameObject doneRoot = new GameObject("Mark_Done");
            doneRoot.transform.SetParent(parent, false);
            doneRoot.transform.localPosition = new Vector3(0f, MarkY, 0f);

            Color doneColor = new Color(0.70f, 0.94f, 0.72f, 1f);
            CreateBar(doneRoot.transform, "CheckA", new Vector2(-0.3f, 0.05f), new Vector2(-0.05f, -0.2f),
                0.12f, doneColor, EraSortingOrder.Mark, white);
            CreateBar(doneRoot.transform, "CheckB", new Vector2(-0.05f, -0.2f), new Vector2(0.32f, 0.26f),
                0.12f, doneColor, EraSortingOrder.Mark, white);

            markLocked = lockedRoot;
            markDone = doneRoot;
        }

        // ===================== 基础图元 =====================

        /// <summary>一个纯色矩形：1x1 世界单位的白色方块 + localScale。父子都按世界单位算（窗口根节点 scale = 1）。</summary>
        internal static SpriteRenderer CreateRect(Transform parent, string name, Vector2 center, Vector2 size,
            Color color, int sortingOrder, Sprite white)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(center.x, center.y, 0f);
            go.transform.localScale = new Vector3(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y), 1f);

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = white;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        /// <summary>两点之间的一根斜杠。注意父节点必须是等比缩放（scale = 1），否则旋转会被拉斜。</summary>
        private static SpriteRenderer CreateBar(Transform parent, string name, Vector2 from, Vector2 to,
            float thickness, Color color, int sortingOrder, Sprite white)
        {
            Vector2 delta = to - from;
            SpriteRenderer bar = CreateRect(parent, name, (from + to) * 0.5f,
                new Vector2(Mathf.Max(0.01f, delta.magnitude), thickness), color, sortingOrder, white);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            return bar;
        }

        internal static Color Mul(Color color, float k)
        {
            return new Color(color.r * k, color.g * k, color.b * k, color.a);
        }

        // ===================== 资源 =====================

        internal static Sprite EnsureWhiteSprite()
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(GeneratedFolder);

            const int size = 8;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(WhiteSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(WhiteSpritePath);

            TextureImporter importer = AssetImporter.GetAtPath(WhiteSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.alphaIsTransparency = true;

                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spritePixelsPerUnit = size;            // 8 像素 = 1 世界单位
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
        }

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

        private static Camera EnsureCamera()
        {
            Camera camera = Camera.main;

            if (camera == null)
            {
                camera = Object.FindObjectOfType<Camera>();
            }

            if (camera == null)
            {
                GameObject go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                go.tag = "MainCamera";
                camera = go.GetComponent<Camera>();
            }

            if (!camera.CompareTag("MainCamera"))
            {
                camera.gameObject.tag = "MainCamera";
            }

            camera.orthographic = true;
            camera.orthographicSize = 7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.043f, 0.047f, 0.07f, 1f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;

            return camera;
        }

        // ===================== 绑定（私有 [SerializeField] 只能用 SerializedObject 写） =====================

        private static void BindController(EraWorldController controller, Camera camera, EraCameraRig rig, List<EraWindow> windows)
        {
            SerializedObject so = new SerializedObject(controller);

            SetRef(so, "mainCamera", camera);
            SetRef(so, "cameraRig", rig);
            SetRefList(so, "windows", windows);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static void BindWindow(EraWindow window, EraPresentation data, SpriteRenderer backdrop,
            SpriteRenderer content, List<SpriteRenderer> frameParts, List<SpriteRenderer> tinted,
            GameObject markLocked, GameObject markDone, BoxCollider2D hitArea)
        {
            SerializedObject so = new SerializedObject(window);

            SetInt(so, "era", (int)data.id);
            SetString(so, "title", data.title);
            SetString(so, "timeline", data.timeline);
            SetColor(so, "theme", data.theme);

            SetRef(so, "backdrop", backdrop);
            SetRef(so, "contentRenderer", content);
            SetRefList(so, "frameParts", frameParts);
            SetRefList(so, "stateTinted", tinted);
            SetRef(so, "markLocked", markLocked);
            SetRef(so, "markDone", markDone);
            SetRef(so, "hitArea", hitArea);

            SetVector2(so, "focusSize", new Vector2(FocusWidth, FocusHeight));
            SetVector2(so, "focusOffset", new Vector2(0f, FocusOffsetY));

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty Find(SerializedObject so, string field)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[时代窗口] 在 {so.targetObject.GetType().Name} 上找不到字段「{field}」。" +
                               "搭建脚本和组件字段名对不上了 —— 改字段名的时候记得同步这里。");
            }

            return property;
        }

        private static void SetInt(SerializedObject so, string field, int value)
        {
            SerializedProperty property = Find(so, field);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetString(SerializedObject so, string field, string value)
        {
            SerializedProperty property = Find(so, field);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        private static void SetColor(SerializedObject so, string field, Color value)
        {
            SerializedProperty property = Find(so, field);
            if (property != null)
            {
                property.colorValue = value;
            }
        }

        private static void SetVector2(SerializedObject so, string field, Vector2 value)
        {
            SerializedProperty property = Find(so, field);
            if (property != null)
            {
                property.vector2Value = value;
            }
        }

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = Find(so, field);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetRefList<T>(SerializedObject so, string field, List<T> values) where T : Object
        {
            SerializedProperty property = Find(so, field);
            if (property == null)
            {
                return;
            }

            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
