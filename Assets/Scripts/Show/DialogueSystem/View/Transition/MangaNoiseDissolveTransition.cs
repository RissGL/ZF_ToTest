using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZGameFramework.Utility;

namespace ZF.DialoguePresentation
{
    /// <summary>
    /// 漫画网点噪波溶解（NEO 那种擦除）：旧画面被"网点 + 噪波 + 水平拉丝 + 墨边"侵蚀掉，
    /// 露出下面已经就位的新画面。
    ///
    /// 和另外两种擦除（遮罩平移 / 条纹）的根本区别：
    ///   那两种是**一层不透明纸盖上去**（观众看不见的时候换模板）；
    ///   这个是**旧画面当场化掉**，所以：
    ///     ① 实现 <see cref="IDialogueTransitionKeepsOldFrame"/>：旧画面活到溶解结束
    ///     ② `Play` 一开始就调 onCovered —— 新画面先在**下面**就位（模板 Host 会保留旧实例在上面）
    ///     ③ 只给**旧画面**的部件贴材质（新画面不用贴：旧画面溶掉多少就露多少）
    ///     ④ 溶解进度走 shader 的 `_Progress`，噪波场用**屏幕空间 UV** → 整屏一张统一的溶解图
    ///
    /// 需要的接线（搭建器会自动接好）：
    ///   本组件 + 一个 <see cref="IDialogueFrameSource"/>（= 场景里的 DialogueView）+ shader
    ///
    /// 调参都在 Inspector 上：方向 / 时长 / 网点大小与角度 / 噪波密度与流速 / 拉丝强度与条带 / 墨边。
    /// </summary>
    public class MangaNoiseDissolveTransition : MonoBehaviour, IDialogueTransition, IDialogueTransitionKeepsOldFrame,
        IDialogueTransitionStyle
    {
        private const string ShaderName = "ZF/Dialogue/MangaNoiseDissolve";
        private const string ShaderNameTMP = "ZF/Dialogue/MangaNoiseDissolveTMP";

        [Header("引用")]
        [Label("帧来源（拖场景里的 DialogueView；空 = 自动找）")]
        [SerializeField] private MonoBehaviour frameSource;

        [Label("溶解 Shader（图片/底图用；空 = 按名字找 ZF/Dialogue/MangaNoiseDissolve）")]
        [SerializeField] private Shader shader;

        [Label("溶解 Shader（TMP 文字用；空 = 按名字找 ZF/Dialogue/MangaNoiseDissolveTMP）")]
        [SerializeField] private Shader shaderTMP;

        [Label("噪波图（空 = 运行时生成一张可平铺的；美术想换成手绘噪波就替换这里）")]
        [SerializeField] private Texture2D noiseTexture;

        [Header("节奏")]
        [Label("溶解时长（秒）")]
        [SerializeField] private float duration = 0.55f;

        [Label("推迟多少秒再开始（0 = 立刻）")]
        [SerializeField] private float startDelay = 0f;

        [Label("进度曲线（横轴 = 时间比例，纵轴 = 溶解进度；留空 = 线性）")]
        [SerializeField] private AnimationCurve progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Label("溶解音效（可空）")]
        [SerializeField] private ShowAudioEventSO wipeAudio;

        [Header("① 方向 + ② 前沿")]
        [Label("方向（整页往哪边被擦掉）")]
        [SerializeField] private WipeDirection direction = WipeDirection.LeftToRight;

        [Label("前沿毛糙度（0 = 笔直一条线；0.1~0.2 = 手绘撕裂感）")]
        [Range(0f, 0.5f)]
        [SerializeField] private float frontJitter = 0.12f;

        [Label("零散颗粒（0 = 整页一起擦；调高会变成全屏随机溶解，慎用）")]
        [Range(0f, 1f)]
        [SerializeField] private float scatter = 0.12f;

        [Label("噪波密度（越大前沿越碎）")]
        [Range(0.5f, 40f)]
        [SerializeField] private float noiseScale = 6f;

        [Label("噪波流动速度 XY（让前沿一直在动，不是死图）")]
        [SerializeField] private Vector2 noiseScroll = new Vector2(0.35f, -0.12f);

        [Header("③ 网点（漫画印刷纹理）")]
        [Label("网点参与程度（0 = 纯噪波，1 = 网点感最强）")]
        [Range(0f, 1f)]
        [SerializeField] private float halftoneAmount = 0.55f;

        [Label("网点大小（屏幕像素）")]
        [Range(1f, 40f)]
        [SerializeField] private float halftoneScale = 7f;

        [Label("网点角度（度）")]
        [Range(0f, 180f)]
        [SerializeField] private float halftoneAngle = 45f;

        [Label("网点锐度（越小越糊）")]
        [Range(0.01f, 1f)]
        [SerializeField] private float halftoneSharpness = 0.6f;

        [Header("④ 水平拉丝（撕裂 / 拖拽）")]
        [Label("拉丝强度（屏幕像素；0 = 不要拉丝）")]
        [Range(0f, 300f)]
        [SerializeField] private float smear = 55f;

        [Label("拉丝条带密度")]
        [Range(1f, 80f)]
        [SerializeField] private float smearBands = 22f;

        [Label("条带随机度（1 = 完全随机条带）")]
        [Range(0f, 1f)]
        [SerializeField] private float smearBandRandom = 0.75f;

        [Header("⑤ 墨边")]
        [Label("溶解带宽度（越大墨边越宽）")]
        [Range(0.001f, 0.6f)]
        [SerializeField] private float edgeWidth = 0.16f;

        [Label("溶解带软度（越大越雾）")]
        [Range(0.001f, 0.5f)]
        [SerializeField] private float edgeSoftness = 0.05f;

        [Label("墨边强度")]
        [Range(0f, 3f)]
        [SerializeField] private float edgeInk = 1.3f;

        [Label("墨边颜色")]
        [SerializeField] private Color edgeColor = new Color(0.04f, 0.04f, 0.05f, 1f);

        [Header("排查")]
        [Label("每次擦除打印目标部件（文字不溶解之类的问题看这一行）")]
        [SerializeField] private bool logTargets = true;

        private readonly List<Graphic> m_Targets = new List<Graphic>();
        private readonly List<Graphic> m_Applied = new List<Graphic>();
        private readonly List<Material> m_OriginalMaterials = new List<Material>();
        private readonly List<Material> m_Instances = new List<Material>();

        private IDialogueFrameSource m_FrameSource;

        /// <summary>图片 / 底图用的溶解材质模板</summary>
        private Material m_Material;

        /// <summary>TMP 文字用的溶解材质模板（基于 TMP 自己的 SDF shader，只是最后削 alpha）</summary>
        private Material m_MaterialTMP;
        private Action m_OnFinished;
        private float m_Elapsed;
        private bool m_Playing;
        private bool m_WaitingDelay;

        /// <summary>true = 反过来：让新画面从网点噪波里聚起来（同一个模板换格、没旧画面可溶时用）</summary>
        private bool m_RevealMode;

        /// <summary>组件上配的默认方向 / 墨边色：组数据没覆盖时用它（别被上一次的覆盖值污染）</summary>
        private WipeDirection m_DefaultDirection;
        private Color m_DefaultEdgeColor;

        /// <summary>组件上配的默认时长（组数据没覆盖时用它）</summary>
        private float m_DefaultDuration;

        public bool IsPlaying => m_Playing;

        private void Awake()
        {
            m_DefaultDirection = direction;
            m_DefaultEdgeColor = edgeColor;
            m_DefaultDuration = duration;

            if (frameSource != null)
            {
                m_FrameSource = frameSource as IDialogueFrameSource;
            }

            if (m_FrameSource == null)
            {
                var view = FindObjectOfType<DialogueView>();
                m_FrameSource = view;
                if (view == null)
                {
                    Debug.LogError("[对话擦除] MangaNoiseDissolveTransition 找不到帧来源（场景里的 DialogueView）", this);
                }
            }

            EnsureMaterial();
        }

        private void OnDestroy()
        {
            RestoreMaterials();

            if (m_Material != null)
            {
                Destroy(m_Material);
                m_Material = null;
            }

            if (m_MaterialTMP != null)
            {
                Destroy(m_MaterialTMP);
                m_MaterialTMP = null;
            }
        }

        // ===================== IDialogueTransition =====================

        public void Play(Action onCovered, Action onFinished)
        {
            if (!EnsureMaterial() || m_FrameSource == null)
            {
                // 没配好别把流程卡住：直接切
                onCovered?.Invoke();
                onFinished?.Invoke();
                return;
            }

            RestoreMaterials();

            m_OnFinished = onFinished;
            m_Elapsed = 0f;
            m_WaitingDelay = startDelay > 0f;

            wipeAudio?.Play();

            // ★ 溶解型：一开局就换内容 —— 旧画面（被 Host 保留着）仍压在上面，新画面在下面就位
            onCovered?.Invoke();

            // 换完内容之后再决定溶谁：
            //   ① 真换了模板 → 溶**被保留的旧画面**（旧画面被网点噪波吃掉，露出下面已经就位的新画面）= NEO 那种
            //   ② 同一个模板换格（旧画面就是这套物体自己，没得溶）→ 反过来**让新画面从网点噪波里聚起来**
            bool dissolveOut = m_FrameSource.HasHeldPreviousFrame;
            CollectTargets(dissolveOut);

            if (m_Targets.Count == 0)
            {
                onFinished?.Invoke();
                return;
            }

            ApplyMaterialToTargets();

            m_RevealMode = !dissolveOut;
            m_Playing = true;
            SetProgress(ProgressOf(0f));
        }

        public void Stop()
        {
            if (!m_Playing && m_Targets.Count == 0)
            {
                return;
            }

            m_Playing = false;
            m_OnFinished = null;

            // 立刻收干净：先放掉旧画面，再还原材质（顺序反了旧画面会冒回来一下）
            m_FrameSource?.ReleasePreviousFrame();
            RestoreMaterials();
        }

        /// <summary>组数据传进来的方向 / 颜色 / 时长（没填的项退回组件上配的默认值）</summary>
        public void ApplyStyle(DialogueTransitionStyle style)
        {
            direction = style.HasDirection ? style.Direction : m_DefaultDirection;
            edgeColor = style.HasColor ? style.Color : m_DefaultEdgeColor;
            duration = style.HasDuration ? style.Duration : m_DefaultDuration;
        }

        // ===================== 驱动 =====================

        private void Update()
        {
            if (!m_Playing)
            {
                return;
            }

            if (m_WaitingDelay)
            {
                m_Elapsed += Time.deltaTime;
                if (m_Elapsed < startDelay)
                {
                    return;
                }

                m_Elapsed -= startDelay;
                m_WaitingDelay = false;
            }

            m_Elapsed += Time.deltaTime;

            float t = duration <= 0f ? 1f : Mathf.Clamp01(m_Elapsed / duration);
            SetProgress(ProgressOf(Evaluate(t)));

            if (t >= 1f)
            {
                Finish();
            }
        }

        private void Finish()
        {
            SetProgress(ProgressOf(1f));
            m_Playing = false;

            // ★ 顺序很重要：**先把旧画面放掉，再还原材质**。
            //   反过来就会出现"旧画面整块冒回来、几帧后才消失"—— 溶解只是材质效果，
            //   旧画面本身还在场上，一还原材质它就恢复成正常渲染了。
            m_FrameSource?.ReleasePreviousFrame();

            RestoreMaterials();

            var finished = m_OnFinished;
            m_OnFinished = null;
            finished?.Invoke();
        }

        /// <summary>把"播放进度"换算成 shader 的 _Progress（显现模式要反过来：1 → 0）</summary>
        private float ProgressOf(float t)
        {
            return m_RevealMode ? 1f - t : t;
        }

        /// <summary>进度曲线：留空/只有一帧就是线性</summary>
        private float Evaluate(float t)
        {
            if (progressCurve == null || progressCurve.length < 2)
            {
                return t;
            }

            return Mathf.Clamp01(progressCurve.Evaluate(t));
        }

        /// <summary>组件被禁用时也要把回调补上，不然输入会永远锁着</summary>
        private void OnDisable()
        {
            if (!m_Playing)
            {
                return;
            }

            m_Playing = false;
            RestoreMaterials();

            var finished = m_OnFinished;
            m_OnFinished = null;
            finished?.Invoke();
        }

        // ===================== 内部 =====================

        /// <summary>
        /// 收起要贴材质的部件：
        ///   dissolveOut = true  → 收**被保留的旧画面**（NEO 那种：旧画面被网点噪波吃掉）
        ///   dissolveOut = false → 收**当前新画面**（同一个模板换格时的回退：新画面从噪波里聚起来）
        /// </summary>
        private void CollectTargets(bool dissolveOut)
        {
            m_Targets.Clear();

            var graphics = dissolveOut ? m_FrameSource.HeldPreviousGraphics : m_FrameSource.CurrentGraphics;
            if (graphics == null)
            {
                return;
            }

            foreach (var graphic in graphics)
            {
                if (graphic != null && graphic.material != null && graphic.material.shader != null)
                {
                    m_Targets.Add(graphic);
                }
            }
        }

        /// <summary>
        /// 给每个部件贴一份**独立的**材质实例，并把原材质的属性**全量继承**过来（贴图 + 关键字 + TMP 那些参数）。
        ///
        /// ★ 为什么要全量继承（`CopyPropertiesFromMaterial`）而不是只拷 `_MainTex`：
        ///   TMP 文字不按普通 Graphic 那套渲染 —— 它自己维护字体材质，并且在 `UpdateMaterial()` 里
        ///   校验"材质的 `_MainTex` 是不是这张字体图集"，不合格就把材质换回字体自带的；
        ///   而且它还需要 `_ScaleFactor` / `_FaceColor` 等属性。只拷一张贴图 → 文字要么被顶掉（不溶解）、要么变白块。
        ///   全量继承之后 TMP 的校验能过，SDF 参数也齐。Image 那边贴图由 CanvasRenderer 按 sprite 喂 `_MainTex`，同样正常。
        /// </summary>
        private void ApplyMaterialToTargets()
        {
            m_Applied.Clear();
            m_OriginalMaterials.Clear();
            m_Instances.Clear();

            foreach (var graphic in m_Targets)
            {
                var tmp = graphic as TMP_Text;

                // ★ 文字必须用 TMP 版 shader：TMP 的 shader 里有一套 SDF 计算（_ScaleFactor/_GradientScale/_FaceDilate…），
                //   拿通用 UI 溶解 shader 去顶，采出来的是"距离值"不是覆盖率 → 字会直接没掉。
                var baseMaterial = tmp != null ? m_MaterialTMP : m_Material;
                if (baseMaterial == null)
                {
                    continue;       // TMP shader 没配好 → 文字这次不参与擦除（总比整片消失好）
                }

                // TMP 要用它自己的 fontSharedMaterial / fontMaterial 这一对 API（直接改 material 会被 TMP 顶回去）
                var original = tmp != null ? tmp.fontSharedMaterial : graphic.material;

                var instance = new Material(baseMaterial) { name = $"{baseMaterial.name}({graphic.name})" };

                if (original != null)
                {
                    instance.CopyPropertiesFromMaterial(original);      // 贴图 / 关键字 / TMP 参数全带过来
                }

                PushParameters(instance);                              // 再盖上我们自己的溶解参数

                m_Applied.Add(graphic);
                m_OriginalMaterials.Add(original);
                m_Instances.Add(instance);

                if (tmp != null)
                {
                    tmp.fontMaterial = instance;
                }
                else
                {
                    graphic.material = instance;
                }
            }

            if (logTargets)
            {
                Debug.Log($"[对话擦除] 「{name}」本次擦除目标 {m_Targets.Count} 个（实际贴材质 {m_Applied.Count} 个）：" +
                          $"{DescribeTargets()}；图片材质={m_Material?.shader.name}，文字材质={m_MaterialTMP?.shader.name}", this);
            }
        }

        /// <summary>排查用：目标里各是什么类型（TMP 有没有被收进来一看就知道）</summary>
        private string DescribeTargets()
        {
            var counts = new Dictionary<string, int>();
            foreach (var graphic in m_Targets)
            {
                string key = graphic is TMP_Text ? "TMP文字" : graphic.GetType().Name;
                counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
            }

            var parts = new List<string>();
            foreach (var pair in counts)
            {
                parts.Add($"{pair.Key}×{pair.Value}");
            }

            return string.Join("、", parts);
        }

        private void RestoreMaterials()
        {
            for (int i = 0; i < m_Applied.Count && i < m_OriginalMaterials.Count; i++)
            {
                var graphic = m_Applied[i];
                if (graphic == null)
                {
                    continue;
                }

                if (graphic is TMP_Text tmp)
                {
                    tmp.fontSharedMaterial = m_OriginalMaterials[i];
                }
                else
                {
                    graphic.material = m_OriginalMaterials[i];
                }
            }

            foreach (var instance in m_Instances)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }
            }

            m_Targets.Clear();
            m_Applied.Clear();
            m_OriginalMaterials.Clear();
            m_Instances.Clear();
        }

        private void SetProgress(float progress)
        {
            foreach (var instance in m_Instances)
            {
                if (instance != null)
                {
                    instance.SetFloat("_Progress", progress);
                }
            }
        }

        private bool EnsureMaterial()
        {
            if (m_Material == null)
            {
                var target = shader != null ? shader : Shader.Find(ShaderName);
                if (target == null)
                {
                    Debug.LogError($"[对话擦除] 找不到 shader「{ShaderName}」，这次只能直接切", this);
                    return false;
                }

                m_Material = new Material(target) { name = "MangaNoiseDissolve(运行时)" };
                m_Material.SetTexture("_NoiseTex", EnsureNoiseTexture());
            }

            if (m_MaterialTMP == null)
            {
                var targetTMP = shaderTMP != null ? shaderTMP : Shader.Find(ShaderNameTMP);

                if (targetTMP != null)
                {
                    m_MaterialTMP = new Material(targetTMP) { name = "MangaNoiseDissolveTMP(运行时)" };
                    m_MaterialTMP.SetTexture("_NoiseTex", EnsureNoiseTexture());
                }
                else
                {
                    Debug.LogWarning($"[对话擦除] 找不到 TMP 用的 shader「{ShaderNameTMP}」：" +
                                     "这次文字不参与擦除（框 / 立绘 / 气泡底图照常擦）", this);
                }
            }

            return true;
        }

        /// <summary>把 Inspector 上的参数推给材质（属性名和 shader 里一一对应）</summary>
        private void PushParameters(Material target)
        {
            if (target == null)
            {
                return;
            }

            target.SetTexture("_NoiseTex", EnsureNoiseTexture());
            target.SetFloat("_NoiseScale", noiseScale);
            target.SetVector("_NoiseScroll", new Vector4(noiseScroll.x, noiseScroll.y, 0f, 0f));
            target.SetFloat("_FrontJitter", frontJitter);
            target.SetFloat("_Scatter", scatter);
            target.SetFloat("_Angle", WipeDirectionUtil.ToAngle(direction));

            target.SetFloat("_EdgeWidth", edgeWidth);
            target.SetFloat("_EdgeSoftness", edgeSoftness);

            target.SetFloat("_HalftoneAmount", halftoneAmount);
            target.SetFloat("_HalftoneScale", halftoneScale);
            target.SetFloat("_HalftoneAngle", halftoneAngle);
            target.SetFloat("_HalftoneSharpness", halftoneSharpness);

            target.SetFloat("_Smear", smear);
            target.SetFloat("_SmearBands", smearBands);
            target.SetFloat("_SmearBandRandom", smearBandRandom);

            target.SetFloat("_EdgeInk", edgeInk);
            target.SetColor("_EdgeColor", edgeColor);
        }

        /// <summary>
        /// 没配噪波图就现场生成一张**可平铺**的值噪声（像素格子 + 双线性 + 三层叠加）。
        /// 这样零美术资源也能跑；美术以后想换成手绘撕纸/网点噪波，直接拖一张到「噪波图」上即可。
        /// </summary>
        private Texture2D EnsureNoiseTexture()
        {
            if (noiseTexture != null)
            {
                return noiseTexture;
            }

            const int size = 256;
            const int seed = 20240917;

            noiseTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "MangaNoise(生成)",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float value = TileableFbm(x, y, size, seed);
                    byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(v, v, v, 255);
                }
            }

            noiseTexture.SetPixels32(pixels);
            noiseTexture.Apply(false, false);
            return noiseTexture;
        }

        /// <summary>可平铺的三层值噪声（格点用取模 → 接缝处也对得上）</summary>
        private static float TileableFbm(int x, int y, int size, int seed)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float total = 0f;
            int cell = 16;

            for (int octave = 0; octave < 3; octave++)
            {
                sum += amplitude * TileableValueNoise(x, y, size, cell, seed + octave * 977);
                total += amplitude;
                amplitude *= 0.5f;
                cell *= 2;
            }

            return total > 0f ? Mathf.Clamp01(sum / total) : 0.5f;
        }

        private static float TileableValueNoise(int x, int y, int size, int cell, int seed)
        {
            float fx = (float)x / size * cell;
            float fy = (float)y / size * cell;

            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;

            float v00 = Hash01(x0, y0, cell, seed);
            float v10 = Hash01(x0 + 1, y0, cell, seed);
            float v01 = Hash01(x0, y0 + 1, cell, seed);
            float v11 = Hash01(x0 + 1, y0 + 1, cell, seed);

            // 平滑插值（smoothstep）→ 看着更像噪声而不是网格
            float sx = tx * tx * (3f - 2f * tx);
            float sy = ty * ty * (3f - 2f * ty);

            float a = Mathf.Lerp(v00, v10, sx);
            float b = Mathf.Lerp(v01, v11, sx);
            return Mathf.Lerp(a, b, sy);
        }

        /// <summary>格点哈希（取模保证平铺接缝一致；确定性，不依赖 Random 状态）</summary>
        private static float Hash01(int x, int y, int cell, int seed)
        {
            x = ((x % cell) + cell) % cell;
            y = ((y % cell) + cell) % cell;

            unchecked
            {
                int h = seed;
                h = h * 31 + x;
                h = h * 31 + y;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0x7fffffff) / (float)0x7fffffff;
            }
        }
    }
}
