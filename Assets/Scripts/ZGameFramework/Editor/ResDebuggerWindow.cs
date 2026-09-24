using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using ZGameFramework.Core;
using Object = UnityEngine.Object;

namespace ZGameFramework.EditorTools
{
    public sealed class ResDebuggerWindow : EditorWindow
    {
        private const string MenuPath = "ZGame/Resource Debugger";

        private static readonly Dictionary<string, ResBase> EmptyRows = new();

        private Vector2 scrollPosition;
        private Vector2 detailScrollPosition;
        private string searchText = "";
        private bool onlyActive;
        private bool onlyProblems;
        private bool autoRefresh = true;
        private double nextAutoRefreshTime;

        private readonly List<ResRow> rows = new();
        private readonly List<LoaderInfo> loaders = new();
        private readonly StringBuilder summaryBuilder = new();

        private ResRow selectedRow;
        private int activeCount;
        private int loadingCount;
        private int missingCount;
        private int unusedCount;
        private int pooledLoaderCount;
        private int visibleLoaderCount;
        private bool reflectionReady;
        private string reflectionError = "";

        private FieldInfo resTableField;
        private FieldInfo loaderResListField;
        private FieldInfo recyclerLoaderSetField;
        private FieldInfo classPoolStackField;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<ResDebuggerWindow>();
            window.titleContent = new GUIContent("Res Debugger");
            window.minSize = new Vector2(720f, 420f);
        }

        private void OnEnable()
        {
            CacheReflection();
            Refresh();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!autoRefresh || EditorApplication.timeSinceStartup < nextAutoRefreshTime)
            {
                return;
            }

            Refresh();
            nextAutoRefreshTime = EditorApplication.timeSinceStartup + 1.0;
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (!reflectionReady)
            {
                EditorGUILayout.HelpBox($"Reflection init failed: {reflectionError}", MessageType.Error);
                return;
            }

            DrawSummary();
            EditorGUILayout.Space(4f);

            var halfHeight = Mathf.Max(160f, position.height * 0.55f);
            var listRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.Height(halfHeight));
            var bottomRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            GUILayout.BeginArea(listRect, EditorStyles.helpBox);
            DrawTable();
            GUILayout.EndArea();

            GUILayout.BeginArea(bottomRect, EditorStyles.helpBox);
            DrawDetails();
            GUILayout.EndArea();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                Refresh();
            }

            autoRefresh = GUILayout.Toggle(autoRefresh, "Auto", EditorStyles.toolbarButton, GUILayout.Width(52f));

            GUILayout.Space(8f);
            GUILayout.Label("Filter", EditorStyles.miniLabel, GUILayout.Width(38f));
            searchText = EditorGUILayout.TextField(searchText, EditorStyles.toolbarSearchField, GUILayout.MinWidth(160f));

            onlyActive = GUILayout.Toggle(onlyActive, "RefCount > 0", EditorStyles.toolbarButton, GUILayout.Width(92f));
            onlyProblems = GUILayout.Toggle(onlyProblems, "Problems", EditorStyles.toolbarButton, GUILayout.Width(78f));

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(!reflectionReady))
            {
                if (GUILayout.Button("Clean Table", EditorStyles.toolbarButton, GUILayout.Width(88f)))
                {
                    ResTable.ClearUnused();
                    Refresh();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary()
        {
            summaryBuilder.Clear();
            summaryBuilder.Append("Table: ").Append(rows.Count);
            summaryBuilder.Append("   Active: ").Append(activeCount);
            summaryBuilder.Append("   Loading: ").Append(loadingCount);
            summaryBuilder.Append("   Missing: ").Append(missingCount);
            summaryBuilder.Append("   Unused: ").Append(unusedCount);
            summaryBuilder.Append("   Visible Loaders: ").Append(visibleLoaderCount);
            summaryBuilder.Append("   Pooled Loaders: ").Append(pooledLoaderCount);

            EditorGUILayout.LabelField(summaryBuilder.ToString(), EditorStyles.boldLabel);

            if (visibleLoaderCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "Only loaders stored by ResLoaderRecycler and ClassPool<ResLoader> can be listed. A standalone active ResLoader cannot be discovered by this tool.",
                    MessageType.Info);
            }
        }

        private void DrawTable()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("State", EditorStyles.toolbarButton, GUILayout.Width(66f));
            GUILayout.Label("RefCount", EditorStyles.toolbarButton, GUILayout.Width(64f));
            GUILayout.Label("Loaders", EditorStyles.toolbarButton, GUILayout.Width(62f));
            GUILayout.Label("Key", EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            var matchingRows = GetFilteredRows();

            foreach (var row in matchingRows)
            {
                var rect = EditorGUILayout.BeginHorizontal(row == selectedRow ? EditorStyles.helpBox : GUIStyle.none);

                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    selectedRow = row;
                    GUI.FocusControl(null);
                    Repaint();

                    if (Event.current.clickCount >= 2 && row.Asset != null)
                    {
                        EditorGUIUtility.PingObject(row.Asset);
                        Selection.activeObject = row.Asset;
                    }

                    Event.current.Use();
                }

                var stateColor = GetStateColor(row.State);
                var oldColor = GUI.color;
                GUI.color = stateColor;
                GUILayout.Label(row.State.ToString(), EditorStyles.miniLabel, GUILayout.Width(66f));
                GUI.color = oldColor;

                GUILayout.Label(row.RefCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(64f));
                GUILayout.Label(row.LoaderNames.Count.ToString(), EditorStyles.miniLabel, GUILayout.Width(62f));
                GUILayout.Label(row.Key, EditorStyles.miniLabel);

                EditorGUILayout.EndHorizontal();
            }

            if (matchingRows.Count == 0)
            {
                EditorGUILayout.HelpBox("No matching resource.", MessageType.None);
            }

            EditorGUILayout.EndScrollView();
        }

        private List<ResRow> GetFilteredRows()
        {
            IEnumerable<ResRow> query = rows;

            if (onlyActive)
            {
                query = query.Where(row => row.RefCount > 0);
            }

            if (onlyProblems)
            {
                query = query.Where(row => row.State != ResState.Normal);
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var text = searchText.Trim();
                query = query.Where(row =>
                    row.Key.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    row.Path.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    row.LoaderNames.Any(name => name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            return query
                .OrderByDescending(row => row.RefCount)
                .ThenBy(row => row.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void DrawDetails()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Details", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(selectedRow == null || selectedRow.Asset == null))
            {
                if (GUILayout.Button("Ping Asset", GUILayout.Width(86f)))
                {
                    EditorGUIUtility.PingObject(selectedRow.Asset);
                    Selection.activeObject = selectedRow.Asset;
                }
            }

            EditorGUILayout.EndHorizontal();

            detailScrollPosition = EditorGUILayout.BeginScrollView(detailScrollPosition);

            if (selectedRow == null)
            {
                EditorGUILayout.HelpBox("Select a resource row to see details.", MessageType.None);
            }
            else
            {
                DrawDetailLine("Key", selectedRow.Key);
                DrawDetailLine("Path", selectedRow.Path);
                DrawDetailLine("State", selectedRow.State.ToString());
                DrawDetailLine("RefCount", selectedRow.RefCount.ToString());
                DrawDetailLine("Loaded", selectedRow.IsLoaded ? "Yes" : "No");
                DrawDetailLine("Loading", selectedRow.IsLoading ? "Yes" : "No");
                DrawDetailLine("Asset", selectedRow.Asset != null ? selectedRow.Asset.GetType().FullName : "null");
                DrawDetailLine("Asset Name", selectedRow.Asset != null ? selectedRow.Asset.name : "null");

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Visible Loaders", EditorStyles.boldLabel);

                if (selectedRow.LoaderNames.Count == 0)
                {
                    EditorGUILayout.LabelField("No visible loader holds this entry.", EditorStyles.miniLabel);
                }
                else
                {
                    foreach (var loaderName in selectedRow.LoaderNames)
                    {
                        EditorGUILayout.LabelField(loaderName, EditorStyles.miniLabel);
                    }
                }

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Possible Issues", EditorStyles.boldLabel);

                if (selectedRow.Issues.Count == 0)
                {
                    EditorGUILayout.LabelField("None.", EditorStyles.miniLabel);
                }
                else
                {
                    foreach (var issue in selectedRow.Issues)
                    {
                        EditorGUILayout.HelpBox(issue, MessageType.Warning);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawDetailLine(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(90f));
            EditorGUILayout.LabelField(value, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static Color GetStateColor(ResState state)
        {
            return state switch
            {
                ResState.Active => new Color(0.45f, 0.80f, 0.45f),
                ResState.Loading => new Color(0.85f, 0.75f, 0.30f),
                ResState.Missing => new Color(0.90f, 0.45f, 0.45f),
                ResState.Unused => new Color(0.65f, 0.65f, 0.65f),
                _ => Color.white
            };
        }

        private void CacheReflection()
        {
            try
            {
                resTableField = typeof(ResTable).GetField("table", BindingFlags.NonPublic | BindingFlags.Static);
                loaderResListField = typeof(ResLoader).GetField("resList", BindingFlags.NonPublic | BindingFlags.Instance);
                recyclerLoaderSetField = typeof(ResLoaderRecycler).GetField("resLoaders", BindingFlags.NonPublic | BindingFlags.Instance);
                classPoolStackField = typeof(ClassPool<ResLoader>).GetField("pool", BindingFlags.Public | BindingFlags.Static);

                if (resTableField == null)
                {
                    throw new MissingFieldException(nameof(ResTable), "table");
                }

                if (loaderResListField == null)
                {
                    throw new MissingFieldException(nameof(ResLoader), "resList");
                }

                if (recyclerLoaderSetField == null)
                {
                    throw new MissingFieldException(nameof(ResLoaderRecycler), "resLoaders");
                }

                if (classPoolStackField == null)
                {
                    throw new MissingFieldException($"ClassPool<{nameof(ResLoader)}>", "pool");
                }

                reflectionReady = true;
                reflectionError = "";
            }
            catch (Exception exception)
            {
                reflectionReady = false;
                reflectionError = exception.Message;
            }
        }

        private void Refresh()
        {
            if (!reflectionReady)
            {
                return;
            }

            CollectLoaders();
            CollectResTable();
        }

        private void CollectLoaders()
        {
            loaders.Clear();

            var recyclers = Object.FindObjectsByType<ResLoaderRecycler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var loaderIds = new HashSet<ResLoader>();

            foreach (var recycler in recyclers)
            {
                if (recycler == null || recyclerLoaderSetField.GetValue(recycler) is not HashSet<ResLoader> loaderSet)
                {
                    continue;
                }

                foreach (var loader in loaderSet)
                {
                    if (loader == null || !loaderIds.Add(loader))
                    {
                        continue;
                    }

                    var loaderName = recycler.gameObject.name;
                    loaders.Add(new LoaderInfo(loader, loaderName, false));
                }
            }

            if (classPoolStackField.GetValue(null) is Stack<ResLoader> pooledLoaders)
            {
                pooledLoaderCount = pooledLoaders.Count;

                foreach (var loader in pooledLoaders)
                {
                    if (loader == null || !loaderIds.Add(loader))
                    {
                        continue;
                    }

                    loaders.Add(new LoaderInfo(loader, "<pooled>", true));
                }
            }
            else
            {
                pooledLoaderCount = 0;
            }

            visibleLoaderCount = loaders.Count;
        }

        private void CollectResTable()
        {
            var resMap = resTableField.GetValue(null) as Dictionary<string, ResBase> ?? EmptyRows;
            var loaderMap = new Dictionary<ResBase, List<string>>();
            var index = 0;

            foreach (var loaderInfo in loaders)
            {
                index++;

                if (loaderResListField.GetValue(loaderInfo.Loader) is not List<ResBase> resList)
                {
                    continue;
                }

                foreach (var res in resList)
                {
                    if (res == null)
                    {
                        continue;
                    }

                    if (!loaderMap.TryGetValue(res, out var names))
                    {
                        names = new List<string>();
                        loaderMap.Add(res, names);
                    }

                    var label = loaderInfo.Name;
                    if (string.IsNullOrEmpty(label))
                    {
                        label = $"Loader {index}";
                    }

                    if (loaderInfo.IsPooled)
                    {
                        label += " [pooled]";
                    }

                    if (!names.Contains(label))
                    {
                        names.Add(label);
                    }
                }
            }

            rows.Clear();
            activeCount = 0;
            loadingCount = 0;
            missingCount = 0;
            unusedCount = 0;

            foreach (var pair in resMap)
            {
                var res = pair.Value;

                if (res == null)
                {
                    continue;
                }

                var state = GetState(res);
                var loaderNames = loaderMap.TryGetValue(res, out var names)
                    ? new List<string>(names)
                    : new List<string>();

                var row = new ResRow(res, state, loaderNames);
                CollectIssues(row);
                rows.Add(row);

                switch (state)
                {
                    case ResState.Active:
                        activeCount++;
                        break;
                    case ResState.Loading:
                        loadingCount++;
                        break;
                    case ResState.Missing:
                        missingCount++;
                        break;
                    case ResState.Unused:
                        unusedCount++;
                        break;
                }

                if (selectedRow != null && selectedRow.Res == res)
                {
                    selectedRow = row;
                }
            }

            if (selectedRow != null && rows.All(row => row.Res != selectedRow.Res))
            {
                selectedRow = null;
            }
        }

        private static ResState GetState(ResBase res)
        {
            if (res.IsLoading)
            {
                return ResState.Loading;
            }

            if (!res.IsLoaded)
            {
                return ResState.Missing;
            }

            return res.RefCount > 0 ? ResState.Active : ResState.Unused;
        }

        private static void CollectIssues(ResRow row)
        {
            if (row.RefCount < 0)
            {
                row.Issues.Add("RefCount is negative. Release/Acquire calls are unbalanced.");
            }

            if (row.State == ResState.Unused)
            {
                row.Issues.Add("RefCount is zero but the entry still exists. Clean Table can remove it.");
            }

            if (row.State == ResState.Missing && !row.IsLoading)
            {
                row.Issues.Add("The resource is still registered but no asset was loaded.");
            }

            if (row.LoaderNames.Count == 0 && row.RefCount > 0)
            {
                row.Issues.Add("RefCount > 0, but no visible loader holds it. It may be a standalone active ResLoader.");
            }
        }

        private enum ResState
        {
            Normal,
            Active,
            Loading,
            Missing,
            Unused
        }

        private sealed class LoaderInfo
        {
            public ResLoader Loader { get; }
            public string Name { get; }
            public bool IsPooled { get; }

            public LoaderInfo(ResLoader loader, string name, bool isPooled)
            {
                Loader = loader;
                Name = name;
                IsPooled = isPooled;
            }
        }

        private sealed class ResRow
        {
            public ResBase Res { get; }
            public string Key { get; }
            public string Path { get; }
            public Object Asset { get; }
            public int RefCount { get; }
            public bool IsLoaded { get; }
            public bool IsLoading { get; }
            public ResState State { get; }
            public List<string> LoaderNames { get; } = new();
            public List<string> Issues { get; } = new();

            public ResRow(ResBase res, ResState state, List<string> loaderNames)
            {
                Res = res;
                Key = res.Key;
                Path = res.Path;
                Asset = res.Asset;
                RefCount = res.RefCount;
                IsLoaded = res.IsLoaded;
                IsLoading = res.IsLoading;
                State = state;
                LoaderNames.AddRange(loaderNames);
            }
        }
    }
}
