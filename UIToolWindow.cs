using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace com.tencent.pandora.tools
{
    public class UIToolWindow : EditorWindow
    {
        class Item
        {
            public GameObject gameObject;
            public string guid;
            public Texture2D texture;
        }

        enum DisplayMode
        {
            Icon,
            Detailed,
        }

        #region 参数
        private const int _cellPadding = 4;
        private float _cellSizeScale = 1f;
        private int _cellSize = 80;

        private string[] _headTabNames = { "1", "2", "3" };
        private const string _selectedTabKey = "UI_TOOL_SELECTED_TAB";
        private int _selectedTab = 0;//选中的tab index

        private const string _searchFilterKey = "UI_TOOL_SEARCH_FILTER";
        private string _searchFilter = "";

        private const string _displayModeKey = "UI_TOOL_DISPLAY_MODE";
        private DisplayMode _displayMode = DisplayMode.Detailed;

        //拖拽类型
        private const string _dragType = "UI_TOOL";

        private Vector2 _scrollPosition = Vector2.zero;
        private bool _mouseInsideWindow = false;

        private int _itemIndexUnderMouse = -1;
        private List<int> _indices = new List<int>();

        private GUIContent _content;
        private GUIStyle _titleStyle;
        private GUIStyle _captionStyle;

        //存放加入的对象
        private List<Item> _items = new List<Item>();
        #endregion

        [MenuItem("PandoraTools/UI Tool")]
        private static void Init()
        {
            EditorWindow.GetWindow<UIToolWindow>(false, "UI Tool", true).Show();
        }

        private void OnEnable()
        {
            _content = new GUIContent();
            _titleStyle = new GUIStyle();
            _titleStyle.alignment = TextAnchor.MiddleCenter;
            _titleStyle.padding = new RectOffset(2, 2, 2, 2);
            _titleStyle.clipping = TextClipping.Clip;
            _titleStyle.wordWrap = true;
            _titleStyle.normal.textColor = new Color(1f, 1f, 1f, 0.5f);

            Load();
        }


        private void Load()
        {
            _selectedTab = EditorPrefs.GetInt(_selectedTabKey, _selectedTab);
            string modeName = EditorPrefs.GetString(_displayModeKey, _displayMode.ToString());
            _displayMode = (DisplayMode)System.Enum.Parse(typeof(DisplayMode), modeName);
            ClearItems();
            string config = EditorPrefs.GetString(ConfigKey, "");
            if (string.IsNullOrEmpty(config))
            {
                Default();
            }
            else
            {
                string[] guids = config.Split('|');
                foreach (string id in guids)
                {
                    AddItemByGUID(id, -1);
                }
            }
        }

        private void ClearItems()
        {
            for (int i = 0, count = _items.Count; i < count; i++)
            {
                ClearItem(_items[i]);
            }
            _items.Clear();
        }

        private void ClearItem( Item item )
        {
            if (item != null)
            {
                item.gameObject = null;
                item.texture = null;
                item.guid = "";
            }
        }

        private string ConfigKey
        {
            get
            {
                return string.Format("UI_TOOL_{0}_{1}", Application.dataPath, _selectedTab);
            }
        }

        private void Default()
        {
            ClearItems();
            if (_selectedTab != 0)
            {
                return;
            }

            //使用搜索方法，不限定路径，免除文件夹移动的影响。
            List<string> filtered = new List<string>();
            string[] paths = AssetDatabase.GetAllAssetPaths();
            foreach (string item in paths)
            {
                if (item.Contains("UITool-") && item.EndsWith(".prefab"))
                {
                    filtered.Add(item);
                }
            }
            filtered.Sort();//只有默认情况下才会排序
            foreach (string item in filtered)
            {
                AddItemByPath(item, -1);
            }
        }

        private void AddItemByGUID( string guid, int index )
        {
            GameObject go = UIToolManager.GUIDToObject<GameObject>(guid);
            if (go != null)
            {
                Item item = new Item();
                item.gameObject = go;
                item.guid = guid;
                AddItem(item, index);
            }
        }

        private void AddItemByGameObject( GameObject go, int index )
        {
            string guid = UIToolManager.ObjectToGUID(go);
            if (string.IsNullOrEmpty(guid) == true)
            {
                string savePath = EditorUtility.SaveFilePanelInProject("Save prefab", go.name, "prefab", "Save prefab as ...");
                if (string.IsNullOrEmpty(savePath) == true)
                {
                    return;
                }
                go = PrefabUtility.CreatePrefab(savePath, go);
                guid = UIToolManager.ObjectToGUID(go);
                if (string.IsNullOrEmpty(guid) == true)
                {
                    return;
                }
            }

            Item item = new Item();
            item.gameObject = go;
            item.guid = guid;
            AddItem(item, index);
        }

        private void AddItemByPath( string path, int index )
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            path = FileUtil.GetProjectRelativePath(path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid) == true)
            {
                return;
            }

            //GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject go = AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) as GameObject;

            Item item = new Item();
            item.gameObject = go;
            item.guid = guid;
            AddItem(item, index);
        }

        private void AddItem( Item item, int index )
        {
            if (FindItem(item.gameObject) != null)
            {
                EditorUtility.DisplayDialog("警告", "该组件已添加，不能重复添加！", "我知道了");

                return;
            }
            GenerateItemPreview(item);
            if (-1 < index && index < _items.Count)
            {
                _items.Insert(index, item);
            }
            else
            {
                _items.Add(item);
            }
            Save();
        }

        private void GenerateItemPreview( Item item )
        {
            string[] paths = AssetDatabase.GetAllAssetPaths();
            string name = item.gameObject.name;
            string texturePath = "";
            for (int i = 0, length = paths.Length; i < length; i++)
            {
                if (paths[i].EndsWith(".png") && paths[i].Contains(name))
                {
                    texturePath = paths[i];
                    break;
                }
            }

            //item.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            item.texture = AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D)) as Texture2D;
        }

        //保存配置
        private void Save()
        {
            if (_items.Count == 0)
            {
                return;
            }

            string guid = _items[0].guid;
            StringBuilder sb = new StringBuilder();
            sb.Append(guid);
            for (int i = 1, count = _items.Count; i < count; i++)
            {
                guid = _items[i].guid;
                sb.Append("|");
                sb.Append(guid);
            }
            EditorPrefs.SetString(ConfigKey, sb.ToString());
        }

        //注意：Screen.width,Screen.height都是针对本窗口的，Event.current.mousePositiony也是针对本窗口，以左上角点为（0，0）点。
        private int GetItemIndexUnderMouse()
        {
            Vector2 mousePosition = Event.current.mousePosition + _scrollPosition;  //因为window是可以滑动的，所以当前Mouse位置加上已滑动距离
            int topPadding = 40; //顶端预留给搜索框的距离

            int width = Screen.width + (int)_scrollPosition.x - _cellPadding - SpaceX;
            int height = Screen.height + (int)_scrollPosition.y - _cellPadding - 40;

            //x方向为避免icon被窗口右边缘部分遮挡，y方向为避免被窗口底端部分遮挡，均预留CellSize大小区域
            //y方向顶端预留40像素给tab和搜索框，低端预留40像素给CellSizeScale和DisplayMode设置

            if (width < mousePosition.x || mousePosition.x < _cellPadding || height < mousePosition.y || mousePosition.y < _cellPadding + topPadding)
            {
                return -1;
            }

            int index = -1;
            for (int y = _cellPadding + topPadding; y <= height; y += SpaceY)
            {
                for (int x = _cellPadding; x <= width; x += SpaceX)
                {
                    index++;
                    Rect rect = new Rect(x, y, SpaceX, SpaceY);
                    if (rect.Contains(mousePosition))
                    {
                        return index;
                    }
                }
            }
            return -1;
        }
        private void OnDisable()
        {
            ClearItems();
        }

        private void OnSeletionChange()
        {
            Repaint();
        }
        private void OnGUI()
        {
            SetCaptionStyle();
            DrawHeadTabs();
            DrawSearchArea();
            FillIndices();
            HandleEvent();
            DrawItems();
            DrawCellSizeScaleSetting();
            DrawDisplayModeOptions();
            //DrawHelpBoxes();
            //Debug.Log(string.Format("index:{0}",GetItemIndexUnderMouse()));
        }

        private void SetCaptionStyle()
        {
            if (_captionStyle != null)
            {
                return;
            }
            foreach (GUIStyle item in GUI.skin.customStyles)
            {
                if (item.name == "ProgressBarBack")
                {
                    _captionStyle = item;
                    break;
                }
            }
        }
        #region 绘制窗口元素
        private void DrawHeadTabs()
        {
            int newSelectedTab = _selectedTab;
            newSelectedTab = GUILayout.Toolbar(newSelectedTab, _headTabNames);
            if (newSelectedTab != _selectedTab)
            {
                _selectedTab = newSelectedTab;
                EditorPrefs.SetInt(_selectedTabKey, _selectedTab);
                Load();
            }
        }
        private void DrawCellSizeScaleSetting()
        {
            _cellSizeScale = EditorGUILayout.Slider("CellSizeScale", _cellSizeScale, 0.4f, 1f);
            _titleStyle.fontSize = Mathf.FloorToInt(15f * _cellSizeScale);
            _captionStyle.fontSize = Mathf.FloorToInt(11f * _cellSizeScale);
        }
        private void DrawDisplayModeOptions()
        {
            DisplayMode newMode = (DisplayMode)EditorGUILayout.EnumPopup("DisplayMode", _displayMode);
            if (newMode != _displayMode)
            {
                _displayMode = newMode;
                EditorPrefs.SetString(_displayModeKey, _displayMode.ToString());
            }
        }
        #endregion


        private void DrawSearchArea()
        {
            GUILayout.BeginHorizontal();
            _searchFilter = EditorPrefs.GetString(_searchFilterKey, "");
            string newSearchFilter = EditorGUILayout.TextField("", _searchFilter, "SearchTextField", GUILayout.Width(Screen.width - 20f));
            if (GUILayout.Button("", "SearchCancelButton", GUILayout.Width(18f)))
            {
                newSearchFilter = "";
                GUIUtility.keyboardControl = 0;//把焦点从输入框移走
            }
            if (newSearchFilter != _searchFilter)
            {
                _searchFilter = newSearchFilter;
                EditorPrefs.SetString(_searchFilterKey, _searchFilter);
            }
            GUILayout.EndHorizontal();
        }

        private void HandleEvent()
        {
            _itemIndexUnderMouse = GetItemIndexUnderMouse();
            Event currentEvent = Event.current;
            EventType eventType = currentEvent.type;
            bool draggable = (currentEvent.mousePosition.y < Screen.height - 40);//40 是预留给设置选项的区域
            switch (eventType)
            {
                case EventType.MouseDown:
                    _mouseInsideWindow = true;
                    SetDragObject(currentEvent);
                    break;

                case EventType.MouseDrag:
                    _mouseInsideWindow = true;
                    if (_itemIndexUnderMouse != -1 && draggable == true)
                    {
                        if (IsDraggedObjectBelongsToUITool == true)
                        {
                            DragAndDrop.StartDrag(_dragType);
                        }
                        currentEvent.Use();
                    }
                    break;

                case EventType.MouseUp:
                    _mouseInsideWindow = false;
                    DragAndDrop.PrepareStartDrag();
                    Repaint();
                    break;

                case EventType.DragUpdated:
                    _mouseInsideWindow = true;
                    UpdateDragVisual();
                    currentEvent.Use();
                    break;

                case EventType.DragPerform:
                    _mouseInsideWindow = false;
                    GameObject draggedObject = DraggedObject;
                    if (draggedObject != null)
                    {
                        Item alreadyExsited = FindItem(draggedObject);
                        if (alreadyExsited != null)
                        {
                            ClearItem(alreadyExsited);
                            _items.Remove(alreadyExsited);
                        }
                        AddItemByGameObject(draggedObject, _itemIndexUnderMouse);
                        DraggedObject = null;
                    }
                    currentEvent.Use();
                    break;

                default:
                    _mouseInsideWindow = false;
                    break;
            }

            if (_mouseInsideWindow == false)
            {
                DraggedObject = null;
            }
        }

        private int CellSize
        {
            get
            {
                return Mathf.FloorToInt(_cellSize * _cellSizeScale);
            }
        }
        private int SpaceX
        {
            get
            {
                return CellSize + Mathf.FloorToInt(_cellPadding * _cellSizeScale);
            }
        }

        private int SpaceY
        {
            get
            {
                if (_displayMode == DisplayMode.Icon)
                {
                    return SpaceX;
                }
                else
                {
                    return SpaceX + Mathf.FloorToInt(32 * _cellSizeScale);//32 是caption区域的搞定
                }
            }
        }

        private void SetDragObject( Event currentEvent )
        {
            if (_itemIndexUnderMouse != -1)
            {
                GUIUtility.keyboardControl = 0;
                //left button press and dragged object in window
                if (currentEvent.button == 0 && _itemIndexUnderMouse < _indices.Count)
                {
                    int index = _indices[_itemIndexUnderMouse];
                    if (index != -1 && index < _items.Count)
                    {
                        DraggedObject = _items[index].gameObject;
                        currentEvent.Use();
                    }
                }
            }
        }

        //主要用于处理筛选
        private void FillIndices()
        {
            _indices.Clear();
            for (int i = 0, count = _items.Count; i < count; i++)
            {
                if (DraggedObject != null && _indices.Count == _itemIndexUnderMouse)
                {
                    _indices.Add(-1);//被拖拽的对象拖放到的位置，其索引设置为-1，方便根据这个标识绘制Add icon
                }

                //被拖拽的对象是_items中的，其索引已被-1替代，不用在加入
                if (_items[i] != FindItem(DraggedObject))
                {
                    int filterIndex = _items[i].gameObject.name.IndexOf(_searchFilter, System.StringComparison.CurrentCultureIgnoreCase);
                    if (string.IsNullOrEmpty(_searchFilter) || filterIndex != -1)
                    {
                        _indices.Add(i);
                    }
                }
            }
            if (_indices.Contains(-1) == false)
            {
                _indices.Add(-1);
            }
        }

        private void DrawItems()
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            int startPositionX = _cellPadding;
            int startPositionY = _cellPadding;

            int index;
            Item item;

            for (int i = 0, count = _indices.Count; i < count; i++)
            {
                index = _indices[i];
                item = (index != -1) ? _items[index] : FindItem(DraggedObject);

                Rect outter = new Rect(startPositionX, startPositionY, CellSize, CellSize);
                Rect inner = outter;
                inner.xMin += 2;
                inner.xMax -= 2;
                inner.yMin += 2;
                inner.yMax -= 2;
                SetContentToolTip(item);
                DrawItemBackground(inner);
                DrawIcon(item, inner, outter, index);

                DrawCaption(item, new Rect(outter.x, outter.y + outter.height, outter.width, 32f * _cellSizeScale));

                startPositionX += SpaceX;

                //使用startPosition + SpaceX 判断，防止按钮部分区域被窗口右边界遮挡。
                if (startPositionX + SpaceX > Screen.width - _cellPadding)
                {
                    startPositionX = _cellPadding;
                    startPositionY += SpaceY;
                }
            }

            GUILayout.Space(startPositionY + SpaceY);
            GUILayout.EndScrollView();
        }

        private void SetContentToolTip( Item item )
        {
            if (DraggedObject == null)
            {
                _content.tooltip = (item == null) ? "Click to add" : item.gameObject.name;
            }
            else
            {
                //拖动中
                _content.tooltip = "";
            }
        }

        private void DrawItemBackground( Rect rect )
        {
            Color normal = new Color(1f, 1f, 1f, 0.5f);
            GUI.color = normal;
            DrawTiledTexture(rect, TextureManager.backgroundTexture);
            GUI.color = Color.white;
            GUI.backgroundColor = normal;
        }

        private void DrawIcon( Item item, Rect inner, Rect outter, int index )
        {
            if (item == null)
            {
                GUI.Label(inner, "Add", _titleStyle);
                if (GUI.Button(outter, _content, "Button"))
                {
                    string path = EditorUtility.OpenFilePanel("Add a prefab", Application.dataPath, "prefab");
                    if (string.IsNullOrEmpty(path) == false)
                    {
                        AddItemByPath(path, -1);
                    }
                }
            }
            else
            {
                if (item.texture != null)
                {
                    GUI.DrawTexture(inner, item.texture);
                }
                else if (_displayMode == DisplayMode.Icon)
                {
                    GUI.Label(inner, item.gameObject.name, _titleStyle);
                }

                //右键功能
                if (GUI.Button(outter, _content, "Button"))
                {
                    if (Event.current.button == 1)
                    {
                        //右键
                        AddContextMenu("Delete", false, RemoveItem, index);
                        ShowContextMenu();
                    }
                }
            }
        }

        //绘制detail模式下的说明
        private void DrawCaption( Item item, Rect position )
        {
            if (_displayMode != DisplayMode.Detailed)
            {
                return;
            }
            string caption = (item == null) ? "" : item.gameObject.name;
            GUI.backgroundColor = new Color(1f, 1f, 1f, 0.5f);
            GUI.contentColor = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(position, caption, _captionStyle);
            GUI.contentColor = Color.white;
            GUI.backgroundColor = Color.white;
        }
        static void DrawTiledTexture( Rect rect, Texture tex )
        {
            GUI.BeginGroup(rect);
            {
                int width = Mathf.RoundToInt(rect.width);
                int height = Mathf.RoundToInt(rect.height);

                for (int y = 0; y < height; y += tex.height)
                {
                    for (int x = 0; x < width; x += tex.width)
                    {
                        GUI.DrawTexture(new Rect(x, y, tex.width, tex.height), tex);
                    }
                }
            }
            GUI.EndGroup();
        }

        private GenericMenu contextMenu;
        private void AddContextMenu( string itemName, bool isChecked, GenericMenu.MenuFunction2 callback, object param )
        {
            if (callback == null)
            {
                Debug.LogError("AddContextMenu callback param is null");
                return;
            }
            if (contextMenu == null)
            {
                contextMenu = new GenericMenu();
            }
            contextMenu.AddItem(new GUIContent(itemName), isChecked, callback, param);
        }

        private void ShowContextMenu()
        {
            if (contextMenu != null)
            {
                contextMenu.ShowAsContext();
                contextMenu = null;
            }
        }

        private void RemoveItem( object obj )
        {
            int index = (int)obj;
            if (-1 < index && index < _items.Count)
            {
                Item item = _items[index];
                ClearItem(item);
                _items.RemoveAt(index);
                Save();
            }
        }

        private Item FindItem( GameObject go )
        {
            if (go == null)
            {
                return null;
            }
            for (int i = 0, count = _items.Count; i < count; i++)
            {
                if (go == _items[i].gameObject || go.name == _items[i].gameObject.name)
                {
                    return _items[i];
                }
            }
            return null;
        }
        #region  drag相关
        private void UpdateDragVisual()
        {
            if (DraggedObject == null)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            }
            else if (IsDraggedObjectBelongsToUITool == true)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            }
            else
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            }
        }

        private GameObject DraggedObject
        {
            get
            {
                Object[] objectReferences = DragAndDrop.objectReferences;
                if (objectReferences != null && objectReferences.Length == 1)
                {
                    return objectReferences[0] as GameObject;
                }
                else
                {
                    return null;
                }
            }

            set
            {
                if (value != null)
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new Object[1] { value };
                    IsDraggedObjectBelongsToUITool = true;
                }
                else
                {
                    DragAndDrop.AcceptDrag();
                }
            }
        }

        private bool IsDraggedObjectBelongsToUITool
        {
            get
            {
                object obj = DragAndDrop.GetGenericData(_dragType);
                if (obj != null)
                {
                    return (bool)obj;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                DragAndDrop.SetGenericData(_dragType, value);
            }
        }
        #endregion
    }
}