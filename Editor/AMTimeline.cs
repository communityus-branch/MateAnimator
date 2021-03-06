using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System;

using Holoville.HOTween.Core.Easing;

public class AMTimeline : EditorWindow {
    public struct AnimatorEditData {
        public int currentTakeInd;
        public int prevTakeInd;
    }

    [MenuItem("Window/Cutscene Editor")]
    static void Init() {
        EditorWindow.GetWindow(typeof(AMTimeline), false, "Cutscene Editor");
    }

    #region Declarations

    public static AMTimeline window = null;

    const BindingFlags methodFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    private AnimatorData _aData;
    public AnimatorData aData {
        get {
            return _aData;
        }
        set {
            if(_aData != value) {
                ClearKeysBuffer();
                contextSelectionTracksBuffer.Clear();
                cachedContextSelection.Clear();

                if(_aData != null) {
                    //Debug.Log("previous data: " + _aData.name + " hash: " + _aData.GetHashCode());
                    _aData.e_isAnimatorOpen = false;

                    if(mAnimEdits.ContainsKey(_aData))
                        mAnimEdits[_aData] = new AnimatorEditData() { currentTakeInd=mCurTakeInd, prevTakeInd=mPrevTakeInd };
                    else
                        mAnimEdits.Add(_aData, new AnimatorEditData() { currentTakeInd=mCurTakeInd, prevTakeInd=mPrevTakeInd });

                    if(_aData._takes != null) {
                        foreach(AMTakeData take in _aData._takes) {
                            if(take != null) {
                                take.maintainCaches(_aData);

                                foreach(AMTrack track in take.trackValues) {
                                    if(track) {
                                        setDirtyKeys(track);
                                        EditorUtility.SetDirty(track);
                                    }
                                }
                            }
                        }
                    }

                    EditorUtility.SetDirty(_aData);
                }

                _aData = value;

                if(_aData) {
                    if(window != null) {
                        _aData.e_isAnimatorOpen = true;
                    }

                    if(_aData._takes == null || _aData._takes.Count == 0) {
                        //this is a newly created data

                        // add take
                        _aData.e_addTake();

                        mCurTakeInd = 0;

                        // save data
                        EditorUtility.SetDirty(_aData);
                    }
                    else {
                        if(mAnimEdits.ContainsKey(_aData)) {
                            AnimatorEditData dat = mAnimEdits[_aData];
                            mCurTakeInd = dat.currentTakeInd;
                            mPrevTakeInd = dat.prevTakeInd;
                        }

                        _aData.e_maintainTakes();	// upgrade takes to current version if necessary
                        // save data
                        EditorUtility.SetDirty(_aData);
                        // preview last selected frame
                        if(mCurTakeInd == -1) mCurTakeInd = 0;
                        if(_aData._takes != null && _aData._takes.Count > 0 && !isPlayMode) {
                            if(mCurTakeInd >= _aData._takes.Count) mCurTakeInd = 0;
                            _aData._takes[mCurTakeInd].previewFrame(_aData, (float)_aData._takes[mCurTakeInd].selectedFrame);
                        }
                    }

                    //!DEBUG
                    Transform holder = _aData.TargetGetDataHolder();
                    if(holder.GetComponent<AnimatorDataHolder>() == null)
                        holder.gameObject.AddComponent<AnimatorDataHolder>();
                    if(!holder.gameObject.activeSelf)
                        holder.gameObject.SetActive(true);

                    indexMethodInfo = -1;	// re-check for methodinfo

                    //Debug.Log("new data: " + _aData.name + " hash: " + _aData.GetHashCode());

                    ReloadOtherWindows();
                }
                //else
                //Debug.Log("no data");
            }
        }
    }// AnimatorData component, holds all data
    public AMTakeData currentTake {
        get {
            if(aData) {
                if(mCurTakeInd == -1 || mCurTakeInd >= aData._takes.Count) mCurTakeInd = 0;
                return aData._takes[mCurTakeInd];
            }
            return null;
        }
    }
    public int currentTakeInd {
        get { return mCurTakeInd; }
        set {
            if(mCurTakeInd != value) {
                if(mCurTakeInd < aData._takes.Count)
                    mPrevTakeInd = mCurTakeInd;
                mCurTakeInd = value;
                currentTakeInd = mCurTakeInd;
            }
        }
    }
    public AMTakeData prevTake {
        get {
            if(aData && mPrevTakeInd != -1 && mPrevTakeInd < aData._takes.Count) {
                return aData._takes[mPrevTakeInd];
            }
            return null;
        }
    }
    public bool currentTakeIsPlayStart {
        get {
            AMTakeData t = currentTake;
            return t != null && t.name == aData.defaultTakeName;
        }
    }
    public AMOptionsFile oData;
    private Vector2 scrollViewValue;			// current value in scrollview (vertical)
    private string[] playbackSpeed = { "x.25", "x.5", "x1", "x2", "x4" };
    private float[] playbackSpeedValue = { .25f, .5f, 1f, 2f, 4f };
    private float cachedZoom = 0f;
    private float numFramesToRender;			// number of frames to render, based on window size
    private bool isPlaying;						// is preview player playing
    private float playerStartTime;				// preview player start time
    private int playerStartFrame;				// preview player start frame
    private int playerStartedFrame;
    private int playerCurLoop;                  // current number of loops made
    private bool playerBackward;                // playing backwards
    public static string[] easeTypeNames = {
		"linear",
        "easeInSine",
        "easeOutSine",
        "easeInOutSine",
        "easeInQuad",
        "easeOutQuad",
        "easeInOutQuad",
        "easeInCubic",
        "easeOutCubic",
        "easeInOutCubic",
        "easeInQuart",
        "easeOutQuart",
        "easeInOutQuart",
        "easeInQuint",
        "easeOutQuint",
        "easeInOutQuint",
        "easeInExpo",
        "easeOutExpo",
        "easeInOutExpo",
        "easeInCirc",
        "easeOutCirc",
        "easeInOutCirc",
        "easeInElastic",
        "easeOutElastic",
        "easeInOutElastic",
        "easeInBack",
        "easeOutBack",
        "easeInOutBack",
        "easeInBounce",
        "easeOutBounce",
        "easeInOutBounce",
		"Custom",
		"None"
	};
    private string[] wrapModeNames = {
		"Once",	
		"Loop",
		"ClampForever",
		"PingPong"
	};
    public enum MessageBoxType {
        Info = 0,
        Warning = 1,
        Error = 2
    }
    public enum DragType {
        None = -1,
        ContextSelection = 0,
        MoveSelection = 1,
        TimeScrub = 2,
        GroupElement = 3,
        ResizeTrack = 4,
        FrameScrub = 5,
        TimelineScrub = 8,
        ResizeAction = 9,
        ResizeHScrollbarLeft = 10,
        ResizeHScrollbarRight = 11,
        CursorZoom = 12,
        CursorHand = 13
    }
    public enum ElementType {
        None = -1,
        TimeScrub = 0,
        Group = 1,				// dragged to inside group
        GroupOutside = 2,		// dragged to outside group
        Track = 3,
        ResizeTrack = 4,
        Button = 5,
        Other = 6,
        FrameScrub = 7,
        TimelineScrub = 8,
        ResizeAction = 9,
        TimelineAction = 10,
        ResizeHScrollbarLeft = 11,
        ResizeHScrollbarRight = 12,
        CursorZoom = 13,
        HScrollbarThumb = 14,
        CursorHand = 15
    }
    public enum CursorType {
        None = -1,
        Zoom = 1,
        Hand = 2
    }
    [SerializeField]
    public enum Track {
        Translation = 0,
        Rotation = 1,
        Orientation = 2,
        Animation = 3,
        Audio = 4,
        Property = 5,
        Event = 6,
        GOSetActive = 7,
        CameraSwitcher = 8,
        Trigger = 9
    }

    public static string[] TrackNames = new string[] {
		"Translation",
		"Rotation",
		"Orientation",
		"Animation",
		"Audio",
		"Property",
		"Event",
        "GOSetActive",
        "Camera Switcher",
        "Trigger"
	};
    // skins
    public static string global_skin = "am_skin_dark";
    private GUISkin skin = null;
    private string cachedSkinName = null;
    // dimensions
    private float margin = 2f;
    private float width_window_minimum = 810;
    private float padding_track = 3f;
    private float width_track = 150f;
    private float width_track_min = 135f;
    private float height_track = 58f;
    private float height_track_foldin = 20f;
    private float height_action_min = 45f;
    //private float width_frame = 30f;
    //private float height_frame = 60f;
    private float width_inspector_open = 250f;
    private float width_inspector_closed = 40f;
    private float width_take_popup = 200f;
    private float height_menu_bar = /*30f*/20f;
    private float height_control_bar = 20f;
    private float width_playback_speed = 43f;
    private float width_playback_controls = 245f;
    private float height_playback_controls = 22f;
    private float height_indicator_offset_y = 33f;		// indicator vertical offset
    private float width_indicator_line = 3f;
    private float width_indicator_head = 11f;
    private float height_indicator_head = 12f;
    private float height_indicator_footer = 20f;
    public static float width_button_delete = 22f;
    private float height_button_delete = 22f;
    private float height_inspector_space = 6f;
    private float height_group = 20f;
    private float height_element_position = 2f;
    private float width_subtrack_space = 15f;
    private float width_scrub_control = 45f;
    private float width_frame_birdseye_min = 7f;
    // dynamic dimensions
    private float current_width_frame;
    private float current_height_frame;
    // colors
    private Color colBirdsEyeFrames;
    // textures
    private Texture tex_cursor_zoomin;
    private Texture tex_cursor_zoomout;
    private Texture tex_cursor_zoom_blank;
    private Texture tex_cursor_zoom = null;
    private Texture tex_cursor_grab;
    private Texture tex_icon_track;
    private Texture tex_icon_track_hover;
    private Texture tex_icon_group_closed;
    private Texture tex_icon_group_open;
    private Texture tex_icon_group_hover;
    private Texture tex_element_position;
    private Texture texFrKey;
    private Texture texFrSet;
    //private Texture texFrU;
    //private Texture texFrM;
    //private Texture texFrUS; 
    //private Texture texFrMS; 
    //private Texture texFrUG; 
    private Texture texKeyBirdsEye;
    private Texture texIndLine;
    private Texture texIndHead;
    private Texture texProperties;
    private Texture texPropertiesTop;
    private Texture texRightArrow;// inspector right arrow
    private Texture texLeftArrow;	// inspector left arrow
    private Texture[] texInterpl = new Texture[2];
    private Texture texBoxBorder;
    private Texture texBoxRed;
    //private Texture texBoxBlue;
    private Texture texBoxLightBlue;
    private Texture texBoxDarkBlue;
    private Texture texBoxGreen;
    private Texture texBoxPink;
    private Texture texBoxYellow;
    private Texture texBoxOrange;
    private Texture texBoxPurple;
    private Texture texIconTranslation;
    private Texture texIconRotation;
    private Texture texIconAnimation;
    private Texture texIconAudio;
    private Texture texIconProperty;
    private Texture texIconEvent;
    private Texture texIconOrientation;
    private Texture texIconCameraSwitcher;
    public static bool texLoaded = false;

    // temporary variables
    private bool isPlayMode = false; 		// whether the user is in play mode, used to close AMTimeline when in play mode
    private bool isRenamingTake = false;	// true if the user is renaming the current take
    private int isRenamingTrack = -1;		// the track the is user renaming, -1 if not renaming a track
    private int isRenamingGroup = 0;		// the group that the user is renaming, 0 if not renaming a group
    //private int repaintBuffer = 0;
    //private int repaintRefreshRate = 1;		// repaint every x update frames if necessary
    private static List<MethodInfo> cachedMethodInfo = new List<MethodInfo>();
    private List<string> cachedMethodNames = new List<string>();
    private List<Component> cachedMethodInfoComponents = new List<Component>();
    private int updateRateMethodInfoCache = 2;		// update method info cache every x update frames if necessary
    private int updateMethodInfoCacheBuffer = 0;	// temporary value
    private static int cachedIndexMethodInfo = -1;
    private static int indexMethodInfo {
        get { return cachedIndexMethodInfo; }
        set {
            if(cachedIndexMethodInfo != value) {
                cachedIndexMethodInfo = value;
                cacheSelectedMethodParameterInfos();
            }
            cachedIndexMethodInfo = value;
        }
    }
    private static ParameterInfo[] cachedParameterInfos = new ParameterInfo[] { };
    private static Dictionary<string, bool> arrayFieldFoldout = new Dictionary<string, bool>();	// used to store the foldout values for arrays in event methods
    private Vector2 inspectorScrollView = new Vector2(0f, 0f);
    private GenericMenu menu = new GenericMenu(); 			// add track menu
    private GenericMenu menu_drag = new GenericMenu(); 		// add track menu, on drag to window
    private GenericMenu contextMenu = new GenericMenu();	// context selection menu
    private float height_all_tracks = 0f;

    // context selection variables
    private bool isControlDown = false;
    private bool isShiftDown = false;
    private bool isSpaceBarDown = false;
    private bool isDragging = false;
    private int dragType = -1;
    private bool cachedIsDragging = false;				// used to determine dragging is started or stopped
    private float scrubSpeed = 0f;
    private int startDragFrame = 0;
    private int ghostStartDragFrame = 0;	// temporary value used to drag the ghost selection
    private int endDragFrame = 0;
    private int contextMenuFrame = 0;
    //private int contextSelectionTrack = 0;
    private Vector2 contextSelectionRange = new Vector2(0f, 0f);			// holds the first and last frame of the copied selection
    //private List<AMKey> contextSelectionKeysBuffer = new List<AMKey>();	// stores the copied context selection keys
    private List<List<AMKey>> contextSelectionKeysBuffer = new List<List<AMKey>>();
    private List<AMTrack> contextSelectionTracksBuffer = new List<AMTrack>();
    private List<int> cachedContextSelection = new List<int>();			// cache context selection when user copies, used for paste
    private bool isChangingTimeControl = false;
    private bool isChangingFrameControl = false;
    private int startScrubFrame = 0;
    private Vector2 startScrubMousePosition = new Vector2(0f, 0f);
    private Vector2 startZoomMousePosition = new Vector2(0f, 0f);
    private Vector2 zoomDirectionMousePosition = new Vector2(0f, 0f);
    private Vector2 cachedZoomMousePosition = new Vector2(0f, 0f);
    private Vector2 endHandMousePosition = new Vector2(0f, 0f);
    private int justFinishedHandDragTicker = 0;
    private int handDragAccelaration = 0;
    private bool dragPan;
    private float startScrollViewValue;
    private float startFrame;
    private bool didPeakZoom = false;
    private bool wasZoomingIn = false;
    private float startZoomValue = 0f;
    private int startZoomXOverFrame = 0;
    private float startResize_width_track = 0f;
    private int mouseOverFrame = 0;					// the frame number that the mouse X and Y is over, 0 if one
    private int mouseXOverFrame = 0;				// the frame number that the mouse X is over, 0 if none
    private int mouseOverTrack = -1;				// mouse over frame track, -1 if no track
    private int mouseOverElement = -1;
    private int mouseMoveTrack = -1;				// mouse move frame track, -1 if no track
    private int mouseMoveFrame = 0;                 // mouse move frame, 0 if none
    private int mouseXOverHScrollbarFrame = 0;
    private Vector2 mouseOverGroupElement = new Vector2(-1, -1);	// group_id, track id
    private bool mouseOverSelectedFrame = false;
    private Vector2 currentMousePosition = new Vector2(0f, 0f);
    private Vector2 draggingGroupElement = new Vector2(-1, -1);
    private int draggingGroupElementType = -1;
    private bool mouseAboveGroupElements = false;	// is mouse above group elements? used to determine whether a dropped group element should be placed in the first position
    private int ticker = 0;
    private int tickerSpeed = 50;
    private float scrollAmountVertical = 0f;
    private List<GameObject> objects_window = new List<GameObject>();
    private int startResizeActionFrame = -1;
    private int resizeActionFrame = -1;
    private int endResizeActionFrame = -1;
    private float[] arrKeyRatiosLeft;
    private float[] arrKeyRatiosRight;
    AMKey[] arrKeysLeft;
    AMKey[] arrKeysRight;
    private bool justStartedHandGrab = false;
    //double click variables
    private double doubleClickTime = 0.3f;
    private double doubleClickCachedTime = 0f;
    private string doubleClickElementID = null;
    private bool cursorZoom = false;
    private bool cursorHand = false;
    private int tooltipTicker = 0;
    private int tooltipTime = 70;
    private string lastTooltip;
    private string tooltip = "";
    private bool showTooltip = false;
    //private	int cachedGameObjectCount = 0;
    //private int cachedDependenciesCount = 0;
    public static bool shouldCheckDependencies = false;
    // keyboard config
    //KeyCode key_zoom = KeyCode.Z;
    private float height_event_parameters = 0f;

    private AnimatorMeta mMeta = null;
    private string mMetaName = "";
    private string mMetaPath = "";

    private Dictionary<AMTakeData, AMTakeEdit> mTakeEdits = new Dictionary<AMTakeData, AMTakeEdit>();

    private Dictionary<AnimatorData, AnimatorEditData> mAnimEdits = new Dictionary<AnimatorData, AnimatorEditData>();
    private int mCurTakeInd = -1;
    private int mPrevTakeInd = -1;

    private GameObject mTempHolder;

    #endregion

    #region Main

    void OnEnable() {
        LoadEditorTextures();

        this.title = "Cutscene Editor";
        this.minSize = new Vector2(width_track + width_playback_controls + width_inspector_open + 70f, 190f);
        this.wantsMouseMove = true;
        window = this;
        // find component
        if(!aData && !EditorApplication.isPlayingOrWillChangePlaymode) {
            GameObject go = Selection.activeGameObject;
            if(go && PrefabUtility.GetPrefabType(go) != PrefabType.Prefab) {
                aData = go.GetComponent<AnimatorData>();
            }
        }

        oData = AMOptionsFile.loadFile();
        // set default current dimensions of frames
        //current_width_frame = width_frame;
        //current_height_frame = height_frame;
        // set is playing to false
        isPlaying = false;

        // add track menu
        buildAddTrackMenu();

        // playmode callback
        EditorApplication.playmodeStateChanged += OnPlayMode;

        // check for pro license
        AMTakeData.isProLicense = PlayerSettings.advancedLicense;

        //autoRepaintOnSceneChange = true;

        mTempHolder = new GameObject();
        mTempHolder.hideFlags = HideFlags.HideAndDontSave;

        Undo.undoRedoPerformed += OnUndoRedo;
    }
    void OnDisable() {
        EditorApplication.playmodeStateChanged -= OnPlayMode;

        window = null;
        if(currentTake != null) {
            // stop audio if it's playing
            currentTake.stopAudio(aData);

            // preview first frame
            currentTake.previewFrame(aData, 1f);

            aData = null;

            // reset property select track
            //aData.propertySelectTrack = null;
        }

        if(AMCameraFade.hasInstance() && AMCameraFade.isPreview())
            AMCameraFade.destroyImmediateInstance();

        Undo.undoRedoPerformed -= OnUndoRedo;
    }
    void OnUndoRedo() {
        if(aData == null) return;

        foreach(AMTakeData take in aData._takes)
            take.undoRedoPerformed();

        ResetDragging();
        Repaint();
    }
    void OnFocus() {
        ResetDragging();
        Repaint();
    }
    void OnLostFocus() {
        ResetDragging();
    }
    void ResetDragging() {
        isDragging = false;
        dragType = (int)DragType.None;
    }

    void LoadEditorTextures() {
        if(!texLoaded) {
            colBirdsEyeFrames = EditorGUIUtility.isProSkin ? new Color(44f / 255f, 43f / 255f, 43f / 255f, 1f) : new Color(210f / 255f, 210f / 255f, 210f / 255f, 1f);
            tex_cursor_zoomin = AMEditorResource.LoadEditorTexture("am_cursor_zoomin");
            tex_cursor_zoomout = AMEditorResource.LoadEditorTexture("am_cursor_zoomout");
            tex_cursor_zoom_blank = AMEditorResource.LoadEditorTexture("am_cursor_zoom_blank");
            tex_cursor_zoom = null;
            tex_cursor_grab = AMEditorResource.LoadEditorTexture("am_cursor_grab");
            tex_icon_track = AMEditorResource.LoadEditorTexture("am_icon_track");
            tex_icon_track_hover = AMEditorResource.LoadEditorTexture("am_icon_track_hover");
            tex_icon_group_closed = AMEditorResource.LoadEditorTexture("am_icon_group_closed");
            tex_icon_group_open = AMEditorResource.LoadEditorTexture("am_icon_group_open");
            tex_icon_group_hover = AMEditorResource.LoadEditorTexture("am_icon_group_hover");
            tex_element_position = AMEditorResource.LoadEditorTexture("am_element_position");
            texFrKey = AMEditorResource.LoadEditorTexture(EditorGUIUtility.isProSkin ? "am_key" : "am_key_light");
            texFrSet = AMEditorResource.LoadEditorTexture(EditorGUIUtility.isProSkin ? "am_frame_set" : "am_frame_set_light");
            //texFrU = AMEditorResource.LoadTexture("am_frame");
            //texFrM = AMEditorResource.LoadTexture("am_frame-m");
            //texFrUS = AMEditorResource.LoadTexture("am_frame-s"); 
            //texFrMS = AMEditorResource.LoadTexture("am_frame-m-s"); 
            //texFrUG = AMEditorResource.LoadTexture("am_frame-g"); 
            texKeyBirdsEye = AMEditorResource.LoadEditorTexture("am_key_birdseye");
            texIndLine = AMEditorResource.LoadEditorTexture("am_indicator_line");
            texIndHead = AMEditorResource.LoadEditorTexture("am_indicator_head");
            texProperties = AMEditorResource.LoadEditorTexture(EditorGUIUtility.isProSkin ? "am_information" : "am_information_light");
            texRightArrow = AMEditorResource.LoadEditorTexture(EditorGUIUtility.isProSkin ? "am_nav_right" : "am_nav_right_light");// inspector right arrow
            texLeftArrow = AMEditorResource.LoadEditorTexture(EditorGUIUtility.isProSkin ? "am_nav_left" : "am_nav_left_light");	// inspector left arrow
            texInterpl[0] = AMEditorResource.LoadEditorTexture(EditorGUIUtility.isProSkin ? "am_interpl_curve" : "am_interpl_curve_light");
            texInterpl[1] = AMEditorResource.LoadEditorTexture(EditorGUIUtility.isProSkin ? "am_interpl_linear" : "am_interpl_linear_light");
            texBoxBorder = AMEditorResource.LoadEditorTexture("am_box_border");
            texBoxRed = AMEditorResource.LoadEditorTexture("am_box_red");
            //texBoxBlue = AMEditorResource.LoadTexture("am_box_blue");
            texBoxLightBlue = AMEditorResource.LoadEditorTexture("am_box_lightblue");
            texBoxDarkBlue = AMEditorResource.LoadEditorTexture("am_box_darkblue");
            texBoxGreen = AMEditorResource.LoadEditorTexture("am_box_green");
            texBoxPink = AMEditorResource.LoadEditorTexture("am_box_pink");
            texBoxYellow = AMEditorResource.LoadEditorTexture("am_box_yellow");
            texBoxOrange = AMEditorResource.LoadEditorTexture("am_box_orange");
            texBoxPurple = AMEditorResource.LoadEditorTexture("am_box_purple");
            texIconTranslation = AMEditorResource.LoadEditorTexture("am_icon_translation");
            texIconRotation = AMEditorResource.LoadEditorTexture("am_icon_rotation");
            texIconAnimation = AMEditorResource.LoadEditorTexture("am_icon_animation");
            texIconAudio = AMEditorResource.LoadEditorTexture("am_icon_audio");
            texIconProperty = AMEditorResource.LoadEditorTexture("am_icon_property");
            texIconEvent = AMEditorResource.LoadEditorTexture("am_icon_event");
            texIconOrientation = AMEditorResource.LoadEditorTexture("am_icon_orientation");
            texIconCameraSwitcher = AMEditorResource.LoadEditorTexture("am_icon_cameraswitcher");

            texLoaded = true;
        }
    }

    public bool MetaInstantiate(string label) {
        if(aData.e_metaCanInstantiatePrefab) {
            //preserve the current take edit info
            AMTakeEdit curTakeEdit = null;
            AMTakeData curTake = currentTake;
            foreach(AMTakeData take in aData._takes) {
                if(take == curTake)
                    window.mTakeEdits.TryGetValue(take, out curTakeEdit);
                window.mTakeEdits.Remove(take);
            }

            aData.e_metaInstantiatePrefab(label);

            if(curTakeEdit != null) {
                mTakeEdits.Add(currentTake, curTakeEdit);
                currentTake.selectedFrame = curTakeEdit.selectedFrame;
            }

            EditorUtility.SetDirty(aData);

            return true;
        }
        return false;
    }
    AMTakeEdit TakeEdit(AMTakeData take) {
        AMTakeEdit ret = null;
        if(!mTakeEdits.TryGetValue(take, out ret)) {
            ret = new AMTakeEdit();
            mTakeEdits.Add(take, ret);
        }
        return ret;
    }
    AMTakeEdit TakeEditCurrent() {
        return TakeEdit(currentTake);
    }
    void TakeEditRemove(AMTakeData take) {
        mTakeEdits.Remove(take);
    }
    void ClearKeysBuffer() {
        foreach(List<AMKey> keys in contextSelectionKeysBuffer) {
            foreach(AMKey key in keys)
                DestroyImmediate(key);
        }
        contextSelectionKeysBuffer.Clear();
    }
    AMKey OnAddKeyUndoComp(GameObject go, System.Type type) {
        AMKey key = Undo.AddComponent(go, type) as AMKey;
        key.enabled = false;
        return key;
    }
    AMKey OnAddKeyComp(GameObject go, System.Type type) {
        AMKey key = go.AddComponent(type) as AMKey;
        key.enabled = false;
        return key;
    }
    void Update() {
        isPlayMode = EditorApplication.isPlayingOrWillChangePlaymode;

        if(isPlayMode) return;
        // drag logic
        if(!isPlaying) {

            processDragLogic();
            // show warning for lost references
            /*if(oData.showWarningForLostReferences) {
                int newGameObjectCount = GameObject.FindObjectsOfType(typeof(GameObject)).Length;
                int newDependencyCount = aData.getDependencies().Count;
                if(newGameObjectCount < cachedGameObjectCount && shouldCheckDependencies) {
                    if(newDependencyCount < cachedDependenciesCount) {
                        int total = (cachedDependenciesCount-newDependencyCount);
                        Debug.LogWarning("Animator: "+total+" GameObject reference"+(total > 1 ? "s" : "")+" lost. Undo to avoid possible issues.");
                    }
                }
                cachedDependenciesCount = newDependencyCount;
                cachedGameObjectCount = newGameObjectCount;
                if(!shouldCheckDependencies) shouldCheckDependencies = true;
            }*/
            // show or hide tooltip
            if(tooltip != "" && lastTooltip == tooltip && !showTooltip) {
                tooltipTicker++;
                if(tooltipTicker >= tooltipTime) {
                    showTooltip = true;
                    tooltipTicker = 0;
                }
            }
            else if(showTooltip && lastTooltip != tooltip) {
                showTooltip = false;
            }
            lastTooltip = tooltip;
        }
        /*if(repaintBuffer>0) {
            repaintBuffer--;
            this.Repaint();	
        }*/


        // if preview is playing
        if(isPlaying || dragType == (int)DragType.TimeScrub || dragType == (int)DragType.FrameScrub) {
            float timeRunning = Time.realtimeSinceStartup - playerStartTime;
            // determine current frame
            float curFrame = currentTake.selectedFrame;
            // if scrubbing
            if(dragType == (int)DragType.TimeScrub || dragType == (int)DragType.FrameScrub) {
                if(scrubSpeed < 0) scrubSpeed *= 5;
                curFrame += Mathf.CeilToInt(scrubSpeed);
                curFrame = Mathf.Clamp(curFrame, 1, currentTake.numFrames);
            }
            else {
                // determine speed
                AMTakeData take = currentTake;
                float speed = (float)take.frameRate * playbackSpeedValue[take.playbackSpeedIndex];
                curFrame = playerStartFrame + (playerBackward ? -timeRunning * speed : timeRunning * speed);
                int curFrameI = Mathf.FloorToInt(curFrame);
                int lastFrame = take.getLastFrame();
                //reached end?
                if((playerBackward && curFrameI < 0) || curFrameI >= lastFrame) {
                    bool restart = true;
                    // loop
                    if(take.numLoop > 0) {
                        playerCurLoop++;
                        restart = playerCurLoop < take.numLoop;
                    }

                    int startFrame = 1;
                    if(restart) {
                        if((take.loopMode == Holoville.HOTween.LoopType.Yoyo || take.loopMode == Holoville.HOTween.LoopType.YoyoInverse))
                            playerBackward = !playerBackward;

                        if(!playerBackward && take.loopBackToFrame > 0)
                            startFrame = take.loopBackToFrame;
                        else
                            startFrame = 1;
                    }

                    if(restart) {
                        playerStartTime = Time.realtimeSinceStartup;

                        if(curFrameI < 0) {
                            curFrame = 1.0f;
                        }
                        else {
                            curFrame = curFrame - (float)lastFrame;
                            if(playerBackward)
                                curFrame += (float)lastFrame;
                        }

                        playerStartFrame = Mathf.FloorToInt(curFrame);

                        if(playerBackward) {
                            if(playerStartFrame > lastFrame) playerStartFrame = lastFrame;
                        }
                        else {
                            if(playerStartFrame < startFrame) playerStartFrame = startFrame;
                        }
                    }
                    else {
                        isPlaying = false;
                        curFrame = playerStartedFrame;
                    }

                    // stop audio
                    take.stopAudio(aData);
                }
            }
            // select the appropriate frame
            if(Mathf.FloorToInt(curFrame) != currentTake.selectedFrame) {
                TakeEditCurrent().selectFrame(currentTake, TakeEditCurrent().selectedTrack, Mathf.FloorToInt(curFrame), numFramesToRender, false, false);
                if(dragType != (int)DragType.TimeScrub && dragType != (int)DragType.FrameScrub) {
                    // sample audio
                    currentTake.sampleAudioAtFrame(aData, Mathf.FloorToInt(curFrame), playbackSpeedValue[currentTake.playbackSpeedIndex]);
                }
                this.Repaint();
            }
            currentTake.previewFrame(aData, curFrame);
        }
        else {
            // autokey
            if(!isDragging && aData != null && aData.e_autoKey) {
                AMTakeData take;
                AMTrack.OnAddKey addCall;
                MonoBehaviour[] dats;
                if(MetaInstantiate("Auto Key")) {
                    take = currentTake;
                    addCall = OnAddKeyComp;
                    dats = null;
                }
                else {
                    //NOTE: may need to selectively gather which ones to record if there are too many tracks, for now this is guaranteed
                    take = currentTake;
                    dats = getKeysAndTracks(take);
                    Undo.RecordObjects(dats, "Auto Key");
                    addCall = OnAddKeyUndoComp;
                }

                bool autoKeyMade = currentTake.autoKey(aData, addCall, Selection.activeTransform, currentTake.selectedFrame);

                if(autoKeyMade) {
                    // preview frame, update orientation only
                    currentTake.previewFrame(aData, currentTake.selectedFrame, true, false);

                    if(dats != null) {
                        foreach(MonoBehaviour dat in dats)
                            EditorUtility.SetDirty(dat);
                    }
                    // repaint
                    this.Repaint();
                }
            }
            // process property if it has been selected
            //processSelectProperty();
            // update methodinfo cache if necessary, used for event track inspector
            processUpdateMethodInfoCache();
        }
    }
    void OnSelectionChange() {
        if(!aData) {
            if(Selection.activeGameObject && PrefabUtility.GetPrefabType(Selection.activeGameObject) != PrefabType.Prefab) {
                AnimatorData newDat = Selection.activeGameObject.GetComponent<AnimatorData>();
                if(newDat != aData) {
                    aData = newDat;
                    Repaint();
                }
            }
        }
    }
    void OnGUI() {
        /*if(Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout) {
            Debug.Log("event type: " + Event.current.type);
        }*/
        if(!oData) {
            oData = AMOptionsFile.loadFile();
        }



        if(EditorApplication.isPlayingOrWillChangePlaymode) {
            this.ShowNotification(new GUIContent("Play Mode"));
            return;
        }
        if(EditorApplication.isCompiling) {
            this.ShowNotification(new GUIContent("Code Compiling"));
            return;
        }
        #region no data component
        if(!aData) {
            // recheck for component
            GameObject go = Selection.activeGameObject;
            if(go) {
                if(PrefabUtility.GetPrefabType(go) != PrefabType.Prefab) {
                    aData = go.GetComponent<AnimatorData>();
                }
                else
                    go = null;
            }
            if(!aData) {
                GUI.skin = null;

                // no data component message
                if(go) {
                    MessageBox("Animator requires an AnimatorData component in your game object.", MessageBoxType.Info);
                }
                else {
                    MessageBox("Animator requires an AnimatorData in scene.", MessageBoxType.Info);
                }

                //meta stuff
                GUILayout.Label("Use AnimatorMeta to create a shared animation data. Leave it blank to have the data stored to the AnimatorData.");

                GUILayout.BeginHorizontal();

                bool createNewMeta = false;

                if(mMeta == null) {
                    GUI.backgroundColor = Color.green;
                    createNewMeta = GUILayout.Button("Create Meta", GUILayout.Width(100f));

                    GUI.backgroundColor = Color.white;
                    mMetaName = GUILayout.TextField(mMetaName);
                }

                GUILayout.EndHorizontal();

                if(mMeta != null) {
                    mMetaName = mMeta.name;
                    mMetaPath = AssetDatabase.GetAssetPath(mMeta);
                }
                else if(!string.IsNullOrEmpty(mMetaName)) {
                    mMetaPath = AMEditorUtil.GetSelectionFolder() + mMetaName + ".prefab";
                }

                if(createNewMeta && !string.IsNullOrEmpty(mMetaName)) {
                    GameObject metago = new GameObject(mMetaName);
                    metago.AddComponent<AnimatorMeta>();
                    UnityEngine.Object pref = PrefabUtility.CreateEmptyPrefab(mMetaPath);
                    GameObject metagopref = PrefabUtility.ReplacePrefab(metago, pref);
                    UnityEngine.Object.DestroyImmediate(metago);
                    mMeta = metagopref.GetComponent<AnimatorMeta>();

                    AssetDatabase.Refresh();
                }

                GUILayout.BeginHorizontal();

                GUILayout.Label("Select Meta: ");

                mMeta = (AnimatorMeta)EditorGUILayout.ObjectField(mMeta, typeof(AnimatorMeta), false);

                GUILayout.EndHorizontal();

                if(!string.IsNullOrEmpty(mMetaPath))
                    GUILayout.Label("Path: " + mMetaPath);
                else {
                    GUILayout.Label("Path: <none>");
                }

                AMEditorUtil.DrawSeparator();
                //

                if(GUILayout.Button(go ? "Add Component" : "Create AnimatorData")) {
                    // create component
                    if(!go) {
                        go = new GameObject("AnimatorData");
                        Undo.RegisterCreatedObjectUndo(go, "Create AnimatorData");
                    }

                    AnimatorData nData = Undo.AddComponent<AnimatorData>(go);

                    nData.e_setMeta(mMeta, false);
                    aData = nData;
                }
            }

            if(!aData) //still no data
                return;
        }

        AMTimeline.loadSkin(ref skin, ref cachedSkinName, position);
        LoadEditorTextures();

        //retain animator open, this is mostly when recompiling AnimatorData
        aData.e_isAnimatorOpen = true;

        #endregion
        #region window resize
        if(!oData.ignoreMinSize && (position.width < width_window_minimum)) {
            MessageBox("Window is too small! Animator requires a width of at least " + width_window_minimum + " pixels to function correctly.", MessageBoxType.Warning);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Ignore (Not Recommended)")) {
                oData.ignoreMinSize = true;
                // save
                EditorUtility.SetDirty(aData);
                // repaint
                this.Repaint();
            }
            if(GUILayout.Button("Resize")) {
                Rect rectDimensions = position;
                rectDimensions.width = width_window_minimum + 1f;
                position = rectDimensions;
                GUIUtility.ExitGUI();
            }

            GUILayout.EndHorizontal();
            return;
        }
        #endregion

        if(tickerSpeed <= 0) tickerSpeed = 1;
        ticker = (ticker + 1) % tickerSpeed;
        EditorGUIUtility.LookLikeControls();
        // reset mouse over element
        mouseOverElement = (int)ElementType.None;
        mouseOverFrame = 0;
        mouseXOverFrame = 0;
        mouseOverTrack = -1;
        mouseOverGroupElement = new Vector2(0, 0);
        tooltip = "";
        int difference = 0;

        //if(oData.disableTimelineActions) current_height_frame = height_track;
        //else current_height_frame = height_frame;
        if(oData.disableTimelineActions) height_action_min = 0f;
        else height_action_min = 45f;

        #region temporary variables
        Rect rectWindow = new Rect(0f, 0f, position.width, position.height);
        Event e = Event.current;

        if(e.type == EventType.ScrollWheel) {
            scrollViewValue.y -= e.delta.y*20;
            if(Mathf.Abs(e.delta.y) > 0) {
                aData.zoom += Mathf.Clamp(Mathf.Abs(e.delta.y) *0.04f, 0.01f, 0.1f) * Mathf.Sign(e.delta.y);
            }
        }
        // get global mouseposition
        Vector2 globalMousePosition = getGlobalMousePosition(e);
        // resize track
        if(dragType == (int)DragType.ResizeTrack) {
            aData.width_track = startResize_width_track + e.mousePosition.x - startScrubMousePosition.x;
        }
        width_track = Mathf.Clamp(aData.width_track, width_track_min, position.width - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed) - width_playback_controls - 70f);
        if(aData.width_track != width_track) aData.width_track = width_track;

        bool compact = ((oData.ignoreMinSize) && (position.width < width_window_minimum)); // when true, display compact GUI
        currentMousePosition = e.mousePosition;
        bool clickedZoom = false;
        #endregion
        #region drag logic events
        bool wasDragging = false;
        UnityEngine.Object[] dragItems = null;
        Rect dropArea = new Rect(width_track, height_menu_bar + height_control_bar + 2f, position.width - 5f - 15f - width_track - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed), position.height - height_indicator_footer - height_menu_bar - height_control_bar);
        if(e.type == EventType.mouseDown) {
            dragPan = (e.button == 0);
        }

        if(e.type == EventType.mouseDrag && EditorWindow.mouseOverWindow == this) {
            isDragging = true;
        }
        else if(EditorWindow.mouseOverWindow == this && (e.type == EventType.DragUpdated || e.type == EventType.DragPerform) && DragAndDrop.objectReferences.Length > 0 && dropArea.Contains(currentMousePosition)) {
            ResetDragging();

            //Debug.Log("track: " + mouseMoveTrack + " frame: "+mouseMoveFrame);

            if(e.type == EventType.DragPerform) {
                DragAndDrop.AcceptDrag();

                UnityEngine.Object[] objs = DragAndDrop.objectReferences;

                if(mouseMoveTrack != -1 && mouseMoveFrame > 0) {
                    if(!addSpriteKeysToTrack(objs, mouseMoveTrack, mouseMoveFrame))
                        dragItems = objs;
                    else {
                        Repaint();
                        return;
                    }
                }
                else if(mouseMoveTrack == -1) { //check to see if we can create a track
                    if(!addSpriteTrackWithKeyObjects(objs, 1))
                        dragItems = objs;
                    else {
                        Repaint();
                        return;
                    }
                }
                else
                    dragItems = objs;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Link;
        }
        else if((dragType == (int)DragType.CursorZoom && EditorWindow.mouseOverWindow != this) || e.type == EventType.mouseUp || /*EditorWindow.mouseOverWindow!=this*/Event.current.rawType == EventType.MouseUp /*|| e.mousePosition.y < 0f*/) {
            if(isDragging) {
                wasDragging = true;
                isDragging = false;
            }
        }

        #endregion
        #region keyboard events
        if(e.Equals(Event.KeyboardEvent("[enter]")) || e.Equals(Event.KeyboardEvent("return"))) {
            // apply renaming when pressing enter
            cancelTextEditting();
            if(isChangingTimeControl) isChangingTimeControl = false;
            if(isChangingFrameControl) isChangingFrameControl = false;
            Repaint();
            e.Use();
            return;
        }
        else if(GUIUtility.keyboardControl == 0 && !isTextEditting()) {
            bool done = false;

            if(e.Equals(Event.KeyboardEvent("delete")) || e.Equals(Event.KeyboardEvent("backspace"))) {
                deleteSelectedKeys(false);
                done = true;
            }
            else if(e.Equals(Event.KeyboardEvent("up"))) {
                done = timelineSelectGroupOrTrackFromCurrent(-1);
                if(done) {
                    AMTakeEdit takeEdit = TakeEditCurrent();
                    takeEdit.contextSelection.Clear();
                    if(takeEdit.selectedTrack != -1)
                        contextSelectFrame(takeEdit.selectedFrame, takeEdit.selectedFrame);
                }
            }
            else if(e.Equals(Event.KeyboardEvent("down"))) {
                done = timelineSelectGroupOrTrackFromCurrent(1);
                if(done) {
                    AMTakeEdit takeEdit = TakeEditCurrent();
                    takeEdit.contextSelection.Clear();
                    if(takeEdit.selectedTrack != -1)
                        contextSelectFrame(takeEdit.selectedFrame, takeEdit.selectedFrame);
                }
            }
            else if(e.Equals(Event.KeyboardEvent("left"))) {
                AMTakeEdit takeEdit = TakeEditCurrent();
                if(takeEdit.selectedTrack != -1 && takeEdit.selectedFrame > 1) {
                    int f = takeEdit.selectedFrame - 1;
                    takeEdit.contextSelection.Clear();
                    contextSelectFrame(f, f);
                    timelineSelectFrame(takeEdit.selectedTrack, f);
                    done = true;
                }
            }
            else if(e.Equals(Event.KeyboardEvent("#left"))) {
                AMTakeEdit takeEdit = TakeEditCurrent();
                if(takeEdit.selectedTrack != -1 && takeEdit.selectedFrame > 1) {
                    int f = takeEdit.selectedFrame;
                    if(takeEdit.isFrameInContextSelection(f-1) && !takeEdit.isFrameInContextSelection(f+1))
                        takeEdit.contextSelectFrame(f, true);
                    else
                        contextSelectFrame(f-1, f-1);
                    timelineSelectFrame(takeEdit.selectedTrack, f-1);
                    done = true;
                }
            }
            else if(e.Equals(Event.KeyboardEvent("right"))) {
                AMTakeEdit takeEdit = TakeEditCurrent();
                if(takeEdit.selectedTrack != -1 && takeEdit.selectedFrame < currentTake.numFrames) {
                    int f = takeEdit.selectedFrame + 1;
                    takeEdit.contextSelection.Clear();
                    contextSelectFrame(f, f);
                    timelineSelectFrame(takeEdit.selectedTrack, f);
                    done = true;
                }
            }
            else if(e.Equals(Event.KeyboardEvent("#right"))) {
                AMTakeEdit takeEdit = TakeEditCurrent();
                if(takeEdit.selectedTrack != -1 && takeEdit.selectedFrame < currentTake.numFrames) {
                    int f = takeEdit.selectedFrame;
                    if(takeEdit.isFrameInContextSelection(f+1) && !takeEdit.isFrameInContextSelection(f-1))
                        takeEdit.contextSelectFrame(f, true);
                    else
                        contextSelectFrame(f+1, f+1);
                    timelineSelectFrame(takeEdit.selectedTrack, f+1);
                    done = true;
                }
            }
            //cut/copy/paste/duplicate
            else if(e.type == EventType.ValidateCommand) {
                if(e.commandName == "Copy") {
                    //are there keys?
                    if(TakeEditCurrent().contextSelectionTracks.Count > 1 || TakeEditCurrent().contextSelectionHasKeys(currentTake)) {
                        contextCopyFrames();
                        done = true;
                    }
                }
                else if(e.commandName == "Paste") {
                    if(canPaste()) {
                        contextMenuFrame = TakeEditCurrent().selectedFrame;
                        contextPasteKeys();
                        done = true;
                    }
                }
                else if(e.commandName == "Cut") {
                    if(TakeEditCurrent().contextSelectionTracks.Count > 1 || TakeEditCurrent().contextSelectionHasKeys(currentTake)) {
                        contextCutKeys();
                        done = true;
                    }
                }
                else if(e.commandName == "Duplicate") {
                    AMTakeEdit takeEdit = TakeEditCurrent();
                    if(takeEdit.contextSelectionTracks.Count > 1 || takeEdit.contextSelectionHasKeys(currentTake)) {
                        contextCopyFrames();
                        contextMenuFrame = takeEdit.contextSelection.Count > 0 ? takeEdit.contextSelection[takeEdit.contextSelection.Count-1] + 1 : takeEdit.selectedFrame;
                        contextPasteKeys();
                        timelineSelectFrame(takeEdit.selectedTrack, contextMenuFrame);
                        done = true;
                    }
                }
            }

            if(done) {
                Repaint();
                e.Use();
                return;
            }
        }

        // check if control or shift are down
        isControlDown = e.control || e.command;
        isShiftDown = e.shift;
        if(e.type == EventType.keyDown && e.keyCode == KeyCode.Space) isSpaceBarDown = true;
        else if(e.type == EventType.keyUp && e.keyCode == KeyCode.Space) isSpaceBarDown = false;
        #endregion
        #region set cursor
        int customCursor = (int)CursorType.None;
        bool showCursor = true;
        if(!isRenamingTake && isRenamingGroup >= 0 && isRenamingTrack <= -1 && (dragType == (int)DragType.CursorHand || (!cursorZoom && isSpaceBarDown && EditorWindow.mouseOverWindow == this))) {
            cursorHand = true;
            showCursor = false;
            customCursor = (int)CursorType.Hand;
            mouseOverElement = (int)ElementType.CursorHand;
            // unused button to catch clicks
            GUI.Button(rectWindow, "", "label");
        }
        else if(dragType == (int)DragType.CursorZoom || (!cursorHand && e.alt && EditorWindow.mouseOverWindow == this)) {
            cursorZoom = true;
            showCursor = false;
            customCursor = (int)CursorType.Zoom;
            if(!isDragging) {
                if(isControlDown) tex_cursor_zoom = tex_cursor_zoomout;
                else tex_cursor_zoom = tex_cursor_zoomin;
            }
            mouseOverElement = (int)ElementType.CursorZoom;
            if(!wasDragging) {
                if(GUI.Button(rectWindow, "", "label")) {
                    if(isControlDown) {
                        if(aData.zoom < 1f) {
                            aData.zoom += 0.2f;
                            if(aData.zoom > 1f) aData.zoom = 1f;
                            clickedZoom = true;
                        }
                    }
                    else {
                        if(aData.zoom > 0f) {
                            aData.zoom -= 0.2f;
                            if(aData.zoom < 0f) aData.zoom = 0f;
                            clickedZoom = true;
                        }
                    }

                }
            }
        }
        else {
            if(!showCursor) showCursor = true;
            cursorHand = false;
            cursorZoom = false;
        }
        if(Screen.showCursor != showCursor) {
            Screen.showCursor = showCursor;
        }
        if(isRenamingTake || isRenamingTrack != -1 || isRenamingGroup < 0) EditorGUIUtility.AddCursorRect(rectWindow, MouseCursor.Text);
        else if(dragType == (int)DragType.TimeScrub || dragType == (int)DragType.FrameScrub || dragType == (int)DragType.MoveSelection) EditorGUIUtility.AddCursorRect(rectWindow, MouseCursor.SlideArrow);
        else if(dragType == (int)DragType.ResizeTrack || dragType == (int)DragType.ResizeAction || dragType == (int)DragType.ResizeHScrollbarLeft || dragType == (int)DragType.ResizeHScrollbarRight) EditorGUIUtility.AddCursorRect(rectWindow, MouseCursor.ResizeHorizontal);
        #endregion
        #region calculations
        processHandDragAcceleration();
        // calculate number of frames to render
        calculateNumFramesToRender(clickedZoom, e);
        //current_height_frame = (oData.disableTimelineActions ? height_track : height_frame);
        // if is playing, disable all gui elements
        GUI.enabled = !(isPlaying);
        // if selected frame is out of range
        if(currentTake.selectedFrame > currentTake.numFrames) {
            // select last frame
            timelineSelectFrame(TakeEditCurrent().selectedTrack, currentTake.numFrames);
        }
        // get number of tracks in current take, use for tracks and keys, disabling zoom slider
        int trackCount = currentTake.getTrackCount();
        #endregion
        #region menu bar
        GUIStyle styleLabelMenu = new GUIStyle(EditorStyles.toolbarButton);
        styleLabelMenu.normal.background = null;
        //GUI.color = new Color(190f/255f,190f/255f,190f/255f,1f);
        GUI.DrawTexture(new Rect(0f, 0f, position.width, height_menu_bar - 2f), EditorStyles.toolbar.normal.background);
        //GUI.color = Color.white;
        #region select name
        GUI.color = aData.e_metaCanSavePrefabInstance ? Color.red : Color.white;
        GUIContent selectLabel = new GUIContent(aData.gameObject.name);
        Vector2 selectLabelSize = EditorStyles.toolbarButton.CalcSize(selectLabel);
        Rect rectSelectLabel = new Rect(margin, 0f, selectLabelSize.x, height_button_delete);

        if(GUI.Button(rectSelectLabel, selectLabel, EditorStyles.toolbarButton)) {
            EditorGUIUtility.PingObject(aData.gameObject);
            Selection.activeGameObject = aData.gameObject;
        }
        GUI.color = Color.white;
        #endregion

        #region options button
        Rect rectBtnOptions = new Rect(rectSelectLabel.x + rectSelectLabel.width + margin, 0f, 60f, height_button_delete);
        if(GUI.Button(rectBtnOptions, "Options", EditorStyles.toolbarButton)) {
            EditorWindow windowOptions = ScriptableObject.CreateInstance<AMOptions>();
            //windowOptions.Show();
            windowOptions.ShowUtility();
            //EditorWindow.GetWindow (typeof (AMOptions));
        }
        if(rectBtnOptions.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) mouseOverElement = (int)ElementType.Button;
        #endregion
        #region refresh button
        bool doRefresh = false;
        Rect rectBtnCodeView = new Rect(rectBtnOptions.x + rectBtnOptions.width + margin, rectBtnOptions.y, 80f, rectBtnOptions.height);
        if(GUI.Button(rectBtnCodeView, "Refresh", EditorStyles.toolbarButton)) {
            //EditorWindow.GetWindow(typeof(AMCodeView));
            doRefresh = true;
        }
        if(rectBtnCodeView.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
            mouseOverElement = (int)ElementType.Button;
        }
        #endregion
        #region take popup, change take or create new take
        Rect rectLabelCurrentTake = new Rect(rectBtnCodeView.x, rectBtnCodeView.y, rectBtnCodeView.width, rectBtnCodeView.height);
        if(!compact) {
            rectLabelCurrentTake = new Rect(rectBtnCodeView.x + rectBtnCodeView.width + margin, rectBtnCodeView.y, 80f, rectBtnCodeView.height);
            GUI.Label(rectLabelCurrentTake, "Current Take:", styleLabelMenu);
        }
        Rect rectTakePopup = new Rect(rectLabelCurrentTake.x + rectLabelCurrentTake.width + margin, rectLabelCurrentTake.y/*+3f*/, width_take_popup, 20f);
        // if renaming take, show textfield
        if(isRenamingTake) {
            GUI.SetNextControlName("RenameTake");
            Rect rectRenameTake = new Rect(rectTakePopup);
            rectRenameTake.x += 4f;
            rectRenameTake.width -= 4f;
            rectRenameTake.y += 3f;
            currentTake.name = GUI.TextField(rectRenameTake, currentTake.name, EditorStyles.toolbarTextField);
            GUI.FocusControl("RenameTake");
        }
        else {
            // show popup
            int ntake = EditorGUI.Popup(rectTakePopup, currentTakeInd, aData.e_getTakeNames(), EditorStyles.toolbarPopup);
            if(currentTakeInd != ntake) {
                currentTakeInd = ntake;

                // take changed
                // destroy camera fade
                if(AMCameraFade.hasInstance()) AMCameraFade.destroyImmediateInstance();
                // reset code view dictionaries
                AMCodeView.resetTrackDictionary();
                // if not creating new take
                if(currentTakeInd < aData._takes.Count) {
                    // select current frame
                    timelineSelectFrame(TakeEditCurrent().selectedTrack, currentTake.selectedFrame);
                    // save data
                    EditorUtility.SetDirty(aData);
                }
            }
        }
        #endregion
        #region rename take button
        Texture texRenameTake;
        if(isRenamingTake) texRenameTake = getSkinTextureStyleState("accept").background;
        else texRenameTake = getSkinTextureStyleState("rename").background;
        Rect rectBtnRenameTake = new Rect(rectTakePopup.x + rectTakePopup.width + margin, rectLabelCurrentTake.y, width_button_delete, height_button_delete);
        // button
        if(GUI.Button(rectBtnRenameTake, new GUIContent(texRenameTake, (isRenamingTake ? "Accept" : "Rename Take")),/*GUI.skin.GetStyle("ButtonImage")*/EditorStyles.toolbarButton)) {
            if(!isRenamingTake) registerTakesUndo(aData, "Rename Take", false);
            GUIUtility.keyboardControl = 0;
            cancelTextEditting(true);	// toggle isRenamingTake
            setDirtyTakes(aData);
        }
        if(rectBtnRenameTake.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) mouseOverElement = (int)ElementType.Button;
        #endregion
        #region delete take button
        Rect rectBtnDeleteTake = new Rect(rectBtnRenameTake.x + rectBtnRenameTake.width + margin, rectBtnRenameTake.y, width_button_delete, height_button_delete);
        if(GUI.Button(rectBtnDeleteTake, new GUIContent("", "Delete Take"),/*GUI.skin.GetStyle("ButtonImage")*/EditorStyles.toolbarButton)) {
            AMTakeData take = currentTake;

            if((EditorUtility.DisplayDialog("Delete Take", "Are you sure you want to delete take '" + take.name + "'?", "Delete", "Cancel"))) {
                string label = "Delete Take: "+take.name;

                if(aData._takes.Count == 1) {
                    bool instantiated = registerTakesUndo(aData, label, false);
                    take = currentTake;
                    MonoBehaviour[] behaviours = getKeysAndTracks(take);

                    //just delete the tracks and keys
                    if(instantiated) {
                        foreach(MonoBehaviour b in behaviours)
                            DestroyImmediate(b);
                    }
                    else {
                        foreach(MonoBehaviour b in behaviours)
                            Undo.DestroyObjectImmediate(b);
                    }

                    take.RevertToDefault();
                    TakeEdit(take).Reset();
                }
                else {
                    bool instantiated = registerTakesUndo(aData, label, true);
                    take = currentTake;
                    if(!string.IsNullOrEmpty(aData.defaultTakeName)) {
                        if(take.name == aData.defaultTakeName)
                            aData.defaultTakeName = "";
                        else {
                            aData.defaultTakeName = aData.defaultTakeName; //this will readjust index if necessary
                        }
                    }

                    int delTakeInd = aData._takes.IndexOf(take);
                    if(currentTakeInd > 0 && delTakeInd <= currentTakeInd)
                        currentTakeInd--;

                    aData._takes.RemoveAt(delTakeInd);
                    TakeEditRemove(take);

                    MonoBehaviour[] behaviours = getKeysAndTracks(take);
                    if(instantiated) {
                        foreach(MonoBehaviour b in behaviours)
                            DestroyImmediate(b);
                    }
                    else {
                        foreach(MonoBehaviour b in behaviours)
                            Undo.DestroyObjectImmediate(b);
                    }
                }

                AMCodeView.resetTrackDictionary();
                // save data
                setDirtyTakes(aData);
            }
        }
        if(!GUI.enabled) GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, 0.25f);
        GUI.DrawTexture(new Rect(rectBtnDeleteTake.x + (rectBtnDeleteTake.height - 10f) / 2f, rectBtnDeleteTake.y + (rectBtnDeleteTake.width - 10f) / 2f - 2f, 10f, 10f), (getSkinTextureStyleState((GUI.enabled && rectBtnDeleteTake.Contains(e.mousePosition) ? "delete_hover" : "delete")).background));
        if(rectBtnDeleteTake.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) mouseOverElement = (int)ElementType.Button;
        if(GUI.color.a < 1f) GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, 1f);
        #endregion
        #region Create/Duplicate Take
        if(currentTakeInd == aData._takes.Count) {
            isRenamingTake = false;
            cancelTextEditting();

            string label = "New Take";

            registerTakesUndo(aData, label, false);

            aData.e_addTake();

            currentTakeInd = aData._takes.Count - 1;

            // save data
            setDirtyTakes(aData);
        }
        else if(currentTakeInd == aData._takes.Count + 1) {
            isRenamingTake = false;
            cancelTextEditting();

            string label = "New Duplicate Take";

            bool addCompUndo = !registerTakesUndo(aData, label, false);

            if(prevTake != null) {
                aData.e_duplicateTake(prevTake, true, addCompUndo);
            }
            else {
                aData.e_addTake();
            }

            currentTakeInd = aData._takes.Count - 1;

            // save data
            setDirtyTakes(aData);
        }
        #endregion
        #region play on start button
        Rect rectBtnPlayOnStart = new Rect(rectBtnDeleteTake.x + rectBtnDeleteTake.width + margin, rectBtnDeleteTake.y, width_button_delete, height_button_delete);
        GUIStyle styleBtnPlayOnStart = new GUIStyle(/*GUI.skin.GetStyle("ButtonImage")*/EditorStyles.toolbarButton);
        if(currentTakeIsPlayStart) {
            styleBtnPlayOnStart.normal.background = styleBtnPlayOnStart.onNormal.background;
            styleBtnPlayOnStart.hover.background = styleBtnPlayOnStart.onNormal.background;
        }
        if(GUI.Button(rectBtnPlayOnStart, new GUIContent(getSkinTextureStyleState("playonstart").background, "Play On Start"), styleBtnPlayOnStart)) {
            Undo.RecordObject(aData, "Set Play On Start");
            if(!currentTakeIsPlayStart) aData.defaultTakeName = currentTake.name;
            else aData.defaultTakeName = "";
            EditorUtility.SetDirty(aData);
        }
        #endregion
        #region settings
        Rect rectLabelSettings = new Rect(rectBtnPlayOnStart.x + rectBtnPlayOnStart.width + margin, rectBtnPlayOnStart.y, 200f, rectLabelCurrentTake.height);

        if(compact) {
            rectLabelSettings.width = GUI.skin.label.CalcSize(new GUIContent("Settings")).x;
            GUI.Label(rectLabelSettings, "Settings", styleLabelMenu);
        }
        else {
            string strSettings = "Settings: " + currentTake.numFrames + " Frames; " + currentTake.frameRate + " Fps";
            rectLabelSettings.width = GUI.skin.label.CalcSize(new GUIContent(strSettings)).x;
            GUI.Label(rectLabelSettings, strSettings, styleLabelMenu);
        }
        Rect rectBtnModify = new Rect(rectLabelSettings.x + rectLabelSettings.width + margin, rectLabelSettings.y, 60f, rectBtnOptions.height);
        if(GUI.Button(rectBtnModify, "Modify", EditorStyles.toolbarButton)) {
            EditorWindow windowSettings = ScriptableObject.CreateInstance<AMSettings>();
            windowSettings.ShowUtility();
        }
        if(rectBtnModify.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) mouseOverElement = (int)ElementType.Button;
        #endregion
        #region zoom slider
        if(trackCount <= 0) GUI.enabled = false;	// disable slider if there are no tracks
        // adjust zoom slider width
        float width_zoom_slider_dynamic = Mathf.Clamp(position.width - (rectBtnModify.x + rectBtnModify.width) - margin - 25f, 0f, 250f);
        Rect rectZoomSlider = new Rect(position.width - 25f - width_zoom_slider_dynamic + 5f, rectBtnModify.y, width_zoom_slider_dynamic - 5f, 20f);
        if(dragType != (int)DragType.CursorZoom) aData.zoom = GUI.HorizontalSlider(rectZoomSlider, aData.zoom, 1f, 0f);
        else GUI.HorizontalSlider(rectZoomSlider, aData.zoom, 1f, 0f);
        if(rectZoomSlider.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
            mouseOverElement = (int)ElementType.Other;
        }
        GUI.enabled = !isPlaying;

        bool birdseye = (current_width_frame <= width_frame_birdseye_min ? true : false);
        // show or hide zoom texture
        if(position.width > 788 || (compact)) {
            GUI.DrawTexture(new Rect(position.width - 25f, 0f, 20f, 20f), getSkinTextureStyleState("zoom").background);
        }
        #endregion
        #endregion
        #region control bar
        #region auto-key button
        GUIStyle styleBtnAutoKey = new GUIStyle(GUI.skin.button);
        styleBtnAutoKey.clipping = TextClipping.Overflow;
        if(aData.e_autoKey) {
            styleBtnAutoKey.normal.background = GUI.skin.button.active.background;
            styleBtnAutoKey.normal.textColor = Color.red;
            styleBtnAutoKey.hover.background = GUI.skin.button.active.background;
            styleBtnAutoKey.hover.textColor = Color.red;
        }
        Rect rectBtnAutoKey = new Rect(margin, height_menu_bar + margin, 40f, 15f);
        if(GUI.Button(rectBtnAutoKey, new GUIContent("Auto", "Auto-Key"), styleBtnAutoKey)) aData.e_autoKey = !aData.e_autoKey;
        if(rectBtnAutoKey.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) mouseOverElement = (int)ElementType.Button;
        #endregion
        if(trackCount <= 0 || TakeEditCurrent().selectedTrack == -1) GUI.enabled = false;	// disable key controls if there are no tracks
        #region select previous key
        Rect rectBtnPrevKey = new Rect(rectBtnAutoKey.x + rectBtnAutoKey.width + margin, rectBtnAutoKey.y, 30f, 15f);
        if(GUI.Button(rectBtnPrevKey, new GUIContent((getSkinTextureStyleState("prev_key").background), "Prev. Key"), GUI.skin.GetStyle("ButtonImage"))) {
            timelineSelectPrevKey();
        }
        if(rectBtnPrevKey.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) mouseOverElement = (int)ElementType.Button;
        #endregion
        # region insert key
        Rect rectBtnInsertKey = new Rect(rectBtnPrevKey.x + rectBtnPrevKey.width + margin, rectBtnPrevKey.y, 23f, 15f);
        if(GUI.Button(rectBtnInsertKey, new GUIContent("K", "Insert Key"))) {
            addKeyToSelectedFrame();
        }
        if(rectBtnInsertKey.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) mouseOverElement = (int)ElementType.Button;
        #endregion
        #region select next key
        Rect rectBtnNextKey = new Rect(rectBtnInsertKey.x + rectBtnInsertKey.width + margin, rectBtnInsertKey.y, 30f, 15f);
        if(GUI.Button(rectBtnNextKey, new GUIContent((getSkinTextureStyleState("next_key").background), "Next Key"), GUI.skin.GetStyle("ButtonImage"))) {
            timelineSelectNextKey();
        }
        if(rectBtnNextKey.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) mouseOverElement = (int)ElementType.Button;
        #endregion
        GUI.enabled = !isPlaying;
        #endregion
        #region playback controls
        Rect rectAreaPlaybackControls = new Rect(0f, position.height - height_indicator_footer, width_track + width_playback_controls, height_playback_controls);
        GUI.BeginGroup(rectAreaPlaybackControls);
        #region new track button
        Rect rectNewTrack = new Rect(5f, height_indicator_footer / 2f - 15f / 2f, 15f, 15f);
        Rect rectBtnNewTrack = new Rect(rectNewTrack.x, 0f, rectNewTrack.width, height_indicator_footer);
        if(GUI.Button(rectBtnNewTrack, new GUIContent("", "New Track"), "label")) {
            if(objects_window.Count > 0) objects_window = new List<GameObject>();
            if(menu.GetItemCount() <= 0) buildAddTrackMenu();
            menu.ShowAsContext();
        }
        GUI.DrawTexture(rectNewTrack, (rectBtnNewTrack.Contains(e.mousePosition) ? tex_icon_track_hover : tex_icon_track));
        if(rectBtnNewTrack.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
            mouseOverElement = (int)ElementType.Button;
        }
        #endregion
        #region new group button
        Rect rectNewGroup = new Rect(rectNewTrack.x + rectNewTrack.width + 5f, height_indicator_footer / 2f - 15f / 2f, 15f, 15f);
        Rect rectBtnNewGroup = new Rect(rectNewGroup.x, 0f, rectNewGroup.width, height_indicator_footer);
        if(GUI.Button(rectBtnNewGroup, new GUIContent("", "New Group"), "label")) {
            registerTakesUndo(aData, "New Group", false);
            cancelTextEditting();
            TakeEditCurrent().addGroup(currentTake);
            setDirtyTakes(aData);
            setScrollViewValue(maxScrollView());
        }
        GUI.DrawTexture(rectNewGroup, (rectBtnNewGroup.Contains(e.mousePosition) ? tex_icon_group_hover : tex_icon_group_closed));
        if(rectBtnNewGroup.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
            mouseOverElement = (int)ElementType.Button;
        }
        #endregion
        #region delete track button
        Rect rectDeleteElement = new Rect(rectNewGroup.x + rectNewGroup.width + 5f + 1f, height_indicator_footer / 2f - 11f / 2f, 11f, 11f);
        Rect rectBtnDeleteElement = new Rect(rectDeleteElement.x, 0f, rectDeleteElement.width, height_indicator_footer);
        if(TakeEditCurrent().selectedGroup >= 0) GUI.enabled = false;
        if(TakeEditCurrent().selectedGroup >= 0 && (trackCount <= 0 || (TakeEditCurrent().contextSelectionTracks != null && TakeEditCurrent().contextSelectionTracks.Count <= 0))) GUI.enabled = false;
        else GUI.enabled = !isPlaying;
        if(!GUI.enabled) GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, 0.25f);
        GUIContent gcDeleteButton;
        string strTitleDeleteTrack = (TakeEditCurrent().contextSelectionTracks != null && TakeEditCurrent().contextSelectionTracks.Count > 1 ? "Tracks" : "Track");
        if(!GUI.enabled) gcDeleteButton = new GUIContent("");
        else gcDeleteButton = new GUIContent("", "Delete " + (TakeEditCurrent().contextSelectionTracks != null && TakeEditCurrent().contextSelectionTracks.Count > 0 ? strTitleDeleteTrack : "Group"));
        if(GUI.Button(rectBtnDeleteElement, gcDeleteButton, "label")) {
            cancelTextEditting();
            if(TakeEditCurrent().contextSelectionTracks.Count > 0) {
                AMTakeData curTake = currentTake;
                AMTrack track = TakeEdit(curTake).getSelectedTrack(curTake);
                if(track) {
                    string strMsgDeleteTrack = (TakeEdit(curTake).contextSelectionTracks.Count > 1 ? "multiple tracks" : "track '" + track.name + "'");
                    if((EditorUtility.DisplayDialog("Delete " + strTitleDeleteTrack, "Are you sure you want to delete " + strMsgDeleteTrack + "?", "Delete", "Cancel"))) {
                        isRenamingTrack = -1;

                        bool instantiated = registerTakesUndo(aData, "Delete Track", true);

                        // delete camera fade
                        if(TakeEdit(curTake).selectedTrack != -1 && track == curTake.cameraSwitcher && AMCameraFade.hasInstance() && AMCameraFade.isPreview()) {
                            AMCameraFade.destroyImmediateInstance();
                        }

                        List<MonoBehaviour> items = new List<MonoBehaviour>();

                        foreach(int track_id in TakeEdit(curTake).contextSelectionTracks) {
                            curTake.deleteTrack(track_id, true, ref items);
                        }

                        if(instantiated) {
                            foreach(MonoBehaviour item in items)
                                DestroyImmediate(item);
                        }
                        else {
                            foreach(MonoBehaviour item in items)
                                Undo.DestroyObjectImmediate(item);
                        }

                        TakeEdit(curTake).contextSelectionTracks = new List<int>();

                        // deselect track
                        TakeEdit(curTake).selectedTrack = -1;
                        // deselect group
                        TakeEdit(curTake).selectedGroup = 0;

                        // save data
                        setDirtyTakes(aData);

                        AMCodeView.refresh();
                    }
                }
                else {
                    // deselect track/group
                    TakeEdit(curTake).selectedTrack = -1;
                    TakeEdit(curTake).selectedGroup = 0;
                }
            }
            else {
                bool delete = true;
                bool deleteContents = false;
                AMTakeData take = currentTake;
                AMGroup grp = take.getGroup(TakeEdit(take).selectedGroup);
                List<MonoBehaviour> items = null;

                if(grp.elements.Count > 0) {
                    int choice = EditorUtility.DisplayDialogComplex("Delete Contents?", "'" + grp.group_name + "' contains contents that can be deleted with the group.", "Delete Contents", "Keep Contents", "Cancel");
                    if(choice == 2) delete = false;
                    else if(choice == 0) deleteContents = true;
                    if(delete) {
                        if(deleteContents) {
                            registerTakesUndo(aData, "Delete Group", true);

                            items = new List<MonoBehaviour>();
                            TakeEdit(take).deleteSelectedGroup(take, true, ref items);

                            foreach(MonoBehaviour item in items)
                                Undo.DestroyObjectImmediate(item);
                        }
                        else {
                            registerTakesUndo(aData, "Delete Group", false);

                            TakeEdit(take).deleteSelectedGroup(take, false, ref items);
                        }

                        setDirtyTakes(aData);
                        AMCodeView.refresh();
                    }
                }
                else { //no tracks inside group
                    registerTakesUndo(aData, "Delete Group", false);
                    TakeEditCurrent().deleteSelectedGroup(currentTake, false, ref items);
                    setDirtyTakes(aData);
                }
            }
        }
        GUI.DrawTexture(rectDeleteElement, (getSkinTextureStyleState((GUI.enabled && rectBtnDeleteElement.Contains(e.mousePosition) ? "delete_hover" : "delete")).background));
        if(rectBtnDeleteElement.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
            mouseOverElement = (int)ElementType.Button;
        }
        if(GUI.color.a < 1f) GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, 1f);
        GUI.enabled = !isPlaying;
        #endregion
        #region resize track
        Rect rectResizeTrack = new Rect(width_track - 5f - 10f, height_indicator_footer / 2f - 10f / 2f - 4f, 15f, 15f);
        GUI.Button(rectResizeTrack, "", GUI.skin.GetStyle("ResizeTrackThumb"));
        if(rectResizeTrack.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
            mouseOverElement = (int)ElementType.ResizeTrack;
        }
        if(GUI.enabled) EditorGUIUtility.AddCursorRect(rectResizeTrack, MouseCursor.ResizeHorizontal);
        GUI.enabled = (currentTake.rootGroup != null && currentTake.rootGroup.elements.Count > 0 ? !isPlaying : false);
        #endregion
        #region select first frame button
        Rect rectBtnSkipBack = new Rect(width_track + margin, margin, 32f, height_playback_controls - margin * 2);
        if(GUI.Button(rectBtnSkipBack, getSkinTextureStyleState("nav_skip_back").background, GUI.skin.GetStyle("ButtonImage"))) timelineSelectFrame(TakeEditCurrent().selectedTrack, 1);
        if(rectBtnSkipBack.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
            mouseOverElement = (int)ElementType.Button;
        }
        #endregion
        #region toggle play button
        // change label if already playing
        Texture playToggleTexture;
        if(isPlaying) playToggleTexture = getSkinTextureStyleState("nav_stop").background;
        else playToggleTexture = getSkinTextureStyleState("nav_play").background;
        GUI.enabled = (currentTake.rootGroup != null && currentTake.rootGroup.elements.Count > 0 ? true : false);
        Rect rectBtnTogglePlay = new Rect(rectBtnSkipBack.x + rectBtnSkipBack.width + margin, rectBtnSkipBack.y, rectBtnSkipBack.width, rectBtnSkipBack.height);
        if(GUI.Button(rectBtnTogglePlay, playToggleTexture, GUI.skin.GetStyle("ButtonImage"))) {
            if(isChangingTimeControl) isChangingTimeControl = false;
            if(isChangingFrameControl) isChangingFrameControl = false;
            // cancel renaming
            cancelTextEditting();
            // toggle play
            playerTogglePlay();
        }
        if(rectBtnTogglePlay.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
            mouseOverElement = (int)ElementType.Button;
        }
        #endregion
        #region select last frame button
        GUI.enabled = (currentTake.rootGroup != null && currentTake.rootGroup.elements.Count > 0 ? !isPlaying : false);
        Rect rectSkipForward = new Rect(rectBtnTogglePlay.x + rectBtnTogglePlay.width + margin, rectBtnTogglePlay.y, rectBtnTogglePlay.width, rectBtnTogglePlay.height);
        if(GUI.Button(rectSkipForward, getSkinTextureStyleState("nav_skip_forward").background, GUI.skin.GetStyle("ButtonImage"))) timelineSelectFrame(TakeEditCurrent().selectedTrack, currentTake.numFrames);
        if(rectSkipForward.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
            mouseOverElement = (int)ElementType.Button;
        }
        #endregion
        #region playback speed popup
        Rect rectPopupPlaybackSpeed = new Rect(rectSkipForward.x + rectSkipForward.width + margin, height_indicator_footer / 2f - 15f / 2f, width_playback_speed, rectBtnTogglePlay.height);
        aData._takes[currentTakeInd].playbackSpeedIndex = EditorGUI.Popup(rectPopupPlaybackSpeed, aData._takes[currentTakeInd].playbackSpeedIndex, playbackSpeed);
        #endregion
        #region scrub controls
        GUIStyle styleScrubControl = new GUIStyle(GUI.skin.label);
        string stringTime = frameToTime(currentTake.selectedFrame, (float)currentTake.frameRate).ToString("N2") + " s";
        string stringFrame = currentTake.selectedFrame.ToString() + " fr";
        int timeFontSize = findWidthFontSize(width_scrub_control, styleScrubControl, new GUIContent(stringTime), 8, 11);
        int frameFontSize = findWidthFontSize(width_scrub_control, styleScrubControl, new GUIContent(stringFrame), 8, 11);
        styleScrubControl.fontSize = (timeFontSize <= frameFontSize ? timeFontSize : frameFontSize);
        #region frame control
        Rect rectFrameControl = new Rect(rectPopupPlaybackSpeed.x + rectPopupPlaybackSpeed.width + margin, 1f, width_scrub_control, height_indicator_footer);
        // frame control button
        if(!isChangingFrameControl) {
            // set time control font size
            if(GUI.Button(rectFrameControl, stringFrame, styleScrubControl)) {
                if(dragType != (int)DragType.FrameScrub) {
                    if(isChangingTimeControl) isChangingTimeControl = false;
                    cancelTextEditting();
                    isChangingFrameControl = true;

                }
            }
            // scrubbing cursor
            if(!isPlaying && currentTake.rootGroup != null && currentTake.rootGroup.elements.Count > 0) EditorGUIUtility.AddCursorRect(rectFrameControl, MouseCursor.SlideArrow);
            // check for drag
            if(rectFrameControl.Contains(e.mousePosition) && currentTake.rootGroup != null && currentTake.rootGroup.elements.Count > 0) {
                mouseOverElement = (int)ElementType.FrameScrub;
            }
        }
        else {
            // changing frame control
            selectFrame((int)Mathf.Clamp(EditorGUI.FloatField(new Rect(rectFrameControl.x, rectFrameControl.y + 2f, rectFrameControl.width, rectFrameControl.height), currentTake.selectedFrame, GUI.skin.textField/*,styleButtonTimeControlEdit*/), 1, currentTake.numFrames));
            if(rectFrameControl.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
                mouseOverElement = (int)ElementType.Other;
            }
        }
        #endregion
        #region time control
        Rect rectTimeControl = new Rect(rectFrameControl.x + rectFrameControl.width + margin, rectFrameControl.y, rectFrameControl.width, rectFrameControl.height);
        if(!isChangingTimeControl) {
            // set time control font size
            if(GUI.Button(rectTimeControl, stringTime, styleScrubControl)) {
                if(dragType != (int)DragType.TimeScrub) {
                    if(isChangingFrameControl) isChangingFrameControl = false;
                    cancelTextEditting();
                    isChangingTimeControl = true;
                }
            }
            // scrubbing cursor
            if(!isPlaying && currentTake.rootGroup != null && currentTake.rootGroup.elements.Count > 0) EditorGUIUtility.AddCursorRect(rectTimeControl, MouseCursor.SlideArrow);
            // check for drag
            if(rectTimeControl.Contains(e.mousePosition) && currentTake.rootGroup != null && currentTake.rootGroup.elements.Count > 0) {
                mouseOverElement = (int)ElementType.TimeScrub;
            }
        }
        else {
            // changing time control
            selectFrame(Mathf.Clamp(timeToFrame(EditorGUI.FloatField(new Rect(rectTimeControl.x, rectTimeControl.y + 2f, rectTimeControl.width, rectTimeControl.height), frameToTime(currentTake.selectedFrame, (float)currentTake.frameRate), GUI.skin.textField/*,styleButtonTimeControlEdit*/), (float)currentTake.frameRate), 1, currentTake.numFrames));
            if(rectTimeControl.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
                mouseOverElement = (int)ElementType.Other;
            }
        }
        GUI.enabled = !isPlaying;
        #endregion
        #endregion
        GUI.EndGroup();
        Rect rectFooter = new Rect(rectAreaPlaybackControls.x, rectAreaPlaybackControls.y, position.width - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed) - 5f, rectAreaPlaybackControls.height);
        if(rectFooter.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
            mouseOverElement = (int)ElementType.Other;
        }
        #endregion
        #region horizontal scrollbar
        // check if mouse is over inspector and scroll if dragging
        if(globalMousePosition.y >= (height_control_bar + height_menu_bar + 2f)) {
            difference = 0;
            // drag right, over inspector
            if(globalMousePosition.x >= position.width - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed) - 5f) {
                difference = Mathf.CeilToInt(globalMousePosition.x - (position.width - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed) - 5f));
                tickerSpeed = Mathf.Clamp(50 - Mathf.CeilToInt(difference / 1.5f), 1, 50);
                if(!aData.isInspectorOpen) tickerSpeed /= 10;
                // if mouse over inspector, set mouseOverElement to Other
                mouseOverElement = (int)ElementType.Other;
                if(dragType == (int)DragType.MoveSelection || dragType == (int)DragType.ContextSelection || dragType == (int)DragType.ResizeAction) {
                    if(ticker == 0) {
                        currentTake.startFrame = Mathf.Clamp(++currentTake.startFrame, 1, currentTake.numFrames);
                        mouseXOverFrame = Mathf.Clamp((int)currentTake.startFrame + (int)numFramesToRender, 1, currentTake.numFrames);
                    }
                    else {
                        mouseXOverFrame = Mathf.Clamp((int)currentTake.startFrame + (int)numFramesToRender, 1, currentTake.numFrames);
                    }
                }
                // drag left, over tracks
            }
            else if(globalMousePosition.x <= width_track - 5f) {
                difference = Mathf.CeilToInt((width_track - 5f) - globalMousePosition.x);
                tickerSpeed = Mathf.Clamp(50 - Mathf.CeilToInt(difference / 1.5f), 1, 50);
                if(dragType == (int)DragType.MoveSelection || dragType == (int)DragType.ContextSelection || dragType == (int)DragType.ResizeAction) {
                    if(ticker == 0) {
                        currentTake.startFrame = Mathf.Clamp(--currentTake.startFrame, 1, currentTake.numFrames);
                        mouseXOverFrame = Mathf.Clamp((int)currentTake.startFrame - 2, 1, currentTake.numFrames);
                    }
                    else {
                        mouseXOverFrame = Mathf.Clamp((int)currentTake.startFrame, 1, currentTake.numFrames);
                    }
                }
            }
        }
        Rect rectHScrollbar = new Rect(width_track + width_playback_controls, position.height - height_indicator_footer + 2f, position.width - (width_track + width_playback_controls) - (aData.isInspectorOpen ? width_inspector_open - 4f : width_inspector_closed) - 21f, height_indicator_footer - 2f);
        float frame_width_HScrollbar = ((rectHScrollbar.width - 44f - (aData.isInspectorOpen ? 4f : 0f)) / ((float)currentTake.numFrames - 1f));
        Rect rectResizeHScrollbarLeft = new Rect(rectHScrollbar.x + 18f + frame_width_HScrollbar * (currentTake.startFrame - 1f), rectHScrollbar.y + 2f, 15f, 15f);
        Rect rectResizeHScrollbarRight = new Rect(rectHScrollbar.x + 18f + frame_width_HScrollbar * (currentTake.endFrame - 1f) - 3f, rectHScrollbar.y + 2f, 15f, 15f);
        Rect rectHScrollbarThumb = new Rect(rectResizeHScrollbarLeft.x, rectResizeHScrollbarLeft.y - 2f, rectResizeHScrollbarRight.x - rectResizeHScrollbarLeft.x + rectResizeHScrollbarRight.width, rectResizeHScrollbarLeft.height);
        if(!aData.isInspectorOpen) rectHScrollbar.width += 4f;
        // if number of frames fit on screen, disable horizontal scrollbar and set startframe to 1
        if(currentTake.numFrames < numFramesToRender) {
            GUI.HorizontalScrollbar(rectHScrollbar, 1f, 1f, 1f, 1f);
            currentTake.startFrame = 1;
        }
        else {
            bool hideResizeThumbs = false;
            if(rectHScrollbarThumb.width < rectResizeHScrollbarLeft.width * 2) {
                hideResizeThumbs = true;
                rectResizeHScrollbarLeft = new Rect(rectHScrollbarThumb.x - 4f, rectResizeHScrollbarLeft.y, rectHScrollbarThumb.width / 2f + 4f, rectResizeHScrollbarLeft.height);
                rectResizeHScrollbarRight = new Rect(rectHScrollbarThumb.x + rectHScrollbarThumb.width - rectHScrollbarThumb.width / 2f, rectResizeHScrollbarRight.y, rectResizeHScrollbarLeft.width, rectResizeHScrollbarRight.height);
            }
            mouseXOverHScrollbarFrame = Mathf.CeilToInt(currentTake.numFrames * ((e.mousePosition.x - rectHScrollbar.x - GUI.skin.horizontalScrollbarLeftButton.fixedWidth) / (rectHScrollbar.width - GUI.skin.horizontalScrollbarLeftButton.fixedWidth * 2)));
            if(!rectResizeHScrollbarLeft.Contains(e.mousePosition) && !rectResizeHScrollbarRight.Contains(e.mousePosition) && EditorWindow.mouseOverWindow == this && dragType != (int)DragType.ResizeHScrollbarLeft && dragType != (int)DragType.ResizeHScrollbarRight && mouseOverElement != (int)ElementType.ResizeHScrollbarLeft && mouseOverElement != (int)ElementType.ResizeHScrollbarRight)
                currentTake.startFrame = Mathf.Clamp((int)GUI.HorizontalScrollbar(rectHScrollbar, (float)currentTake.startFrame, (int)numFramesToRender - 1f, 1f, currentTake.numFrames), 1, currentTake.numFrames);
            else Mathf.Clamp(GUI.HorizontalScrollbar(rectHScrollbar, (float)currentTake.startFrame, (int)numFramesToRender - 1f, 1f, currentTake.numFrames), 1f, currentTake.numFrames);
            // scrollbar bg overlay (used to hide inconsistent thumb)
            GUI.Box(new Rect(rectHScrollbar.x + 18f, rectHScrollbar.y, rectHScrollbar.width - 18f * 2f, rectHScrollbar.height), "", GUI.skin.horizontalScrollbar);
            // scrollbar thumb overlay (used to hide inconsistent thumb)
            GUI.Box(rectHScrollbarThumb, "", GUI.skin.horizontalScrollbarThumb);


            if(!hideResizeThumbs) {
                if(!GUI.enabled) GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, 0.25f);
                GUI.DrawTexture(rectResizeHScrollbarLeft, GUI.skin.GetStyle("ResizeTrackThumb").normal.background);
                GUI.DrawTexture(rectResizeHScrollbarRight, GUI.skin.GetStyle("ResizeTrackThumb").normal.background);
                GUI.color = Color.white;
            }
            if(GUI.enabled && !isDragging) {
                EditorGUIUtility.AddCursorRect(rectResizeHScrollbarLeft, MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(rectResizeHScrollbarRight, MouseCursor.ResizeHorizontal);
            }
            // show horizontal scrollbar
            if(rectResizeHScrollbarLeft.Contains(e.mousePosition) && customCursor == (int)CursorType.None) {

                mouseOverElement = (int)ElementType.ResizeHScrollbarLeft;
            }
            else if(rectResizeHScrollbarRight.Contains(e.mousePosition) && customCursor == (int)CursorType.None) {
                mouseOverElement = (int)ElementType.ResizeHScrollbarRight;
            }
        }
        currentTake.endFrame = currentTake.startFrame + (int)numFramesToRender - 1;
        if(rectHScrollbar.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
            mouseOverElement = (int)ElementType.Other;
        }

        #endregion
        #region inspector toggle button
        Rect rectPropertiesButton = new Rect(position.width - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed), height_menu_bar + height_control_bar + 2f, 36, position.height);
        Rect rectPropertiesTop = new Rect(position.width - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed), height_menu_bar - 2f, 251, 24);
        GUI.color = getSkinTextureStyleState("properties_bg").textColor;
        GUI.DrawTexture(rectPropertiesButton, getSkinTextureStyleState("properties_bg").background);
        GUI.DrawTexture(rectPropertiesTop, getSkinTextureStyleState("properties_top").background);
        GUI.color = Color.white;
        // inspector toggle button
        if(GUI.Button(rectPropertiesButton, "", "label")) {
            aData.isInspectorOpen = !aData.isInspectorOpen;
        }
        if(rectPropertiesButton.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
            mouseOverElement = (int)ElementType.Button;
        }
        #endregion
        #region key numbering
        Rect rectKeyNumbering = new Rect(width_track, height_control_bar + height_menu_bar + 2f - 22f, position.width - width_track - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed) - 20f, 20f);
        if(rectKeyNumbering.Contains(e.mousePosition) && (mouseOverElement == (int)ElementType.None)) {
            mouseOverElement = (int)ElementType.TimelineScrub;
        }
        int key_dist = 5;
        if(numFramesToRender >= 100) key_dist = Mathf.FloorToInt(numFramesToRender / 100) * 10;
        int firstMarkedKey = (int)currentTake.startFrame;
        if(firstMarkedKey % key_dist != 0 && firstMarkedKey != 1) {
            firstMarkedKey += key_dist - firstMarkedKey % key_dist;
        }
        float lastNumberX = -1f;
        for(int i = firstMarkedKey; i <= (int)currentTake.endFrame; i += key_dist) {
            float newKeyNumberX = width_track + current_width_frame * (i - (int)currentTake.startFrame) - 1f;
            string key_number;
            if(oData.time_numbering) key_number = frameToTime(i, (float)currentTake.frameRate).ToString("N2");
            else key_number = i.ToString();
            Rect rectKeyNumber = new Rect(newKeyNumberX, height_menu_bar, GUI.skin.label.CalcSize(new GUIContent(key_number)).x, height_control_bar);
            bool didCutLabel = false;
            if(rectKeyNumber.x + rectKeyNumber.width >= position.width - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed) - 20f) {
                rectKeyNumber.width = position.width - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed) - 20f - rectKeyNumber.x;
                didCutLabel = true;
            }
            if(!(didCutLabel && currentTake.endFrame == currentTake.numFrames)) {
                if(rectKeyNumber.x > lastNumberX + 3f) {
                    GUI.Label(rectKeyNumber, key_number);
                    lastNumberX = rectKeyNumber.x + GUI.skin.label.CalcSize(new GUIContent(key_number)).x;
                }
            }
            if(i == 1) i--;
        }

        #endregion
        #region main scrollview
        height_all_tracks = currentTake.getElementsHeight(0, height_track, height_track_foldin, height_group);
        float height_scrollview = position.height - (height_control_bar + height_menu_bar) - height_indicator_footer;
        // check if mouse is beyond tracks and dragging group element
        difference = 0;
        // drag up
        if(dragType == (int)DragType.GroupElement && globalMousePosition.y <= height_control_bar + height_menu_bar + 2f) {
            difference = Mathf.CeilToInt((height_control_bar + height_menu_bar + 2f) - globalMousePosition.y);
            scrollAmountVertical = -difference;	// set scroll amount
            // drag down
        }
        else if(dragType == (int)DragType.GroupElement && globalMousePosition.y >= position.height - height_playback_controls) {
            difference = Mathf.CeilToInt(globalMousePosition.y - (position.height - height_playback_controls));
            scrollAmountVertical = difference; // set scroll amount
        }
        else {
            scrollAmountVertical = 0f;
        }
        // frames bg
        GUI.DrawTexture(new Rect(0f, height_control_bar + height_menu_bar + 2f, position.width - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed) - 5f, position.height - height_control_bar - height_menu_bar - height_indicator_footer), GUI.skin.GetStyle("GroupElementBG").onNormal.background);
        // tracks bg
        GUI.Box(new Rect(0f, height_control_bar + height_menu_bar + 2f, width_track, position.height - height_control_bar - height_menu_bar - height_indicator_footer), "", GUI.skin.GetStyle("GroupElementBG"));
        Rect rectScrollView = new Rect(0f, height_control_bar + height_menu_bar + 2f, position.width - (aData.isInspectorOpen ? width_inspector_open/*+3f*/ : width_inspector_closed), height_scrollview);
        Rect rectView = new Rect(0f, 0f, rectScrollView.width - 20f, (height_all_tracks > rectScrollView.height ? height_all_tracks : rectScrollView.height));
        scrollViewValue = GUI.BeginScrollView(rectScrollView, scrollViewValue, rectView, false, true);
        scrollViewValue.y = Mathf.Clamp(scrollViewValue.y, 0f, height_all_tracks - height_scrollview);
        Vector2 scrollViewBounds = new Vector2(scrollViewValue.y, scrollViewValue.y + height_scrollview); // min and max y displayed onscreen
        bool isAnyTrackFoldedOut = false;
        GUILayout.BeginHorizontal(GUILayout.Height(height_all_tracks));
        GUILayout.BeginVertical(GUILayout.Width(width_track));
        float track_y = 0f;		// the next track's y position
        // tracks vertical start
        for(int i = 0; i < currentTake.rootGroup.elements.Count; i++) {
            if(track_y > scrollViewBounds.y) break;	// if start y is beyond max y
            int id = currentTake.rootGroup.elements[i];
            float height_group_elements = 0f;
            showGroupElement(id, 0, ref track_y, ref isAnyTrackFoldedOut, ref height_group_elements, e, scrollViewBounds);
        }
        // draw element position indicator
        if(dragType == (int)DragType.GroupElement) {
            if(mouseOverElement != (int)ElementType.Group && mouseOverElement != (int)ElementType.GroupOutside && mouseOverElement != (int)ElementType.Track) {
                float element_position_y;
                if(e.mousePosition.y < (height_menu_bar + height_control_bar)) element_position_y = 2f;
                else element_position_y = track_y;
                GUI.DrawTexture(new Rect(0f, element_position_y - height_element_position, width_track, height_element_position), tex_element_position);
            }
        }
        GUILayout.EndVertical();
        GUILayout.BeginVertical();
        // frames vertical	
        GUILayout.BeginHorizontal(GUILayout.Height(height_track));
        mouseXOverFrame = (int)currentTake.startFrame + Mathf.CeilToInt((e.mousePosition.x - width_track) / current_width_frame) - 1;
        if(dragType == (int)DragType.CursorHand && justStartedHandGrab) {
            startScrubFrame = mouseXOverFrame;
            justStartedHandGrab = false;
        }
        track_y = 0f;	// reset track y
        showFramesForGroup(0, ref track_y, e, birdseye, scrollViewBounds);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUI.EndScrollView();
        #endregion
        #region inspector
        Texture inspectorArrow;

        if(aData.isInspectorOpen) {
            Rect rectInspector = new Rect(position.width - width_inspector_open - 4f + width_inspector_closed, +height_menu_bar + height_control_bar + 6f, width_inspector_open, position.height - height_menu_bar - height_control_bar);
            GUI.BeginGroup(rectInspector);
            // inspector vertical
            GUI.enabled = true;
            GUI.enabled = !isPlaying;
            // backup editor styles
            GUIStyle styleEditorTextField = new GUIStyle(EditorStyles.textField);
            GUIStyle styleEditorLabel = new GUIStyle(EditorStyles.label);
            // modify editor styles
            EditorStyles.textField.normal = GUI.skin.textField.normal;
            EditorStyles.textField.focused = GUI.skin.textField.focused;
            EditorStyles.label.normal = GUI.skin.label.normal;
            showInspectorPropertiesFor(rectInspector, TakeEditCurrent().selectedTrack, currentTake.selectedFrame, e);
            // reset editor styles
            EditorStyles.textField.normal = styleEditorTextField.normal;
            EditorStyles.textField.focused = styleEditorTextField.focused;
            EditorStyles.label.normal = styleEditorLabel.normal;
            inspectorArrow = texRightArrow;
            GUI.EndGroup();
        }
        else {
            GUI.enabled = true;
            GUI.enabled = !isPlaying;
            inspectorArrow = texLeftArrow;
        }

        GUI.DrawTexture(new Rect(position.width - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed) + 9f, 47f, inspectorArrow.width, inspectorArrow.height), inspectorArrow);
        GUI.DrawTexture(new Rect(position.width - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed) + 6f, 65f, texProperties.width, texProperties.height), texProperties);
        #endregion
        #region indicator
        if((oData.showFramesForCollapsedTracks || isAnyTrackFoldedOut) && (trackCount > 0)) drawIndicator(currentTake.selectedFrame);
        #endregion
        #region horizontal scrollbar tooltip
        string strHScrollbarLeftTooltip = (oData.time_numbering ? frameToTime((int)currentTake.startFrame, (float)currentTake.frameRate).ToString("N2") : currentTake.startFrame.ToString());
        string strHScrollbarRightTooltip = (oData.time_numbering ? frameToTime((int)currentTake.endFrame, (float)currentTake.frameRate).ToString("N2") : currentTake.endFrame.ToString());
        GUIStyle styleLabelCenter = new GUIStyle(GUI.skin.label);
        styleLabelCenter.alignment = TextAnchor.MiddleCenter;
        Vector2 _label_size;
        if(customCursor == (int)CursorType.None && ((mouseOverElement == (int)ElementType.ResizeHScrollbarLeft && !isDragging) || dragType == (int)DragType.ResizeHScrollbarLeft) && (dragType != (int)DragType.ResizeHScrollbarRight)) {
            _label_size = GUI.skin.button.CalcSize(new GUIContent(strHScrollbarLeftTooltip));
            _label_size.x += 2f;
            GUI.Label(new Rect(rectResizeHScrollbarLeft.x + rectResizeHScrollbarLeft.width / 2f - _label_size.x / 2f, rectResizeHScrollbarLeft.y - 22f, _label_size.x, 20f), strHScrollbarLeftTooltip, GUI.skin.button);
        }
        if(customCursor == (int)CursorType.None && ((mouseOverElement == (int)ElementType.ResizeHScrollbarRight && !isDragging) || dragType == (int)DragType.ResizeHScrollbarRight) && (dragType != (int)DragType.ResizeHScrollbarLeft)) {
            _label_size = GUI.skin.button.CalcSize(new GUIContent(strHScrollbarRightTooltip));
            _label_size.x += 2f;
            GUI.Label(new Rect(rectResizeHScrollbarRight.x + rectResizeHScrollbarRight.width / 2f - _label_size.x / 2f, rectResizeHScrollbarRight.y - 22f, _label_size.x, 20f), strHScrollbarRightTooltip, GUI.skin.button);
        }
        #endregion
        #region click window
        if(GUI.Button(new Rect(0f, 0f, position.width, position.height), "", "label") && dragType != (int)DragType.TimelineScrub && dragType != (int)DragType.ResizeAction) {
            //bool didRegisterUndo = false;

            if(TakeEditCurrent().contextSelectionTracks != null && TakeEditCurrent().contextSelectionTracks.Count > 0) {
                //registerTakesUndo(aData, "Deselect Tracks", false);
                //didRegisterUndo = true;
                TakeEditCurrent().contextSelectionTracks = new List<int>();
                setDirtyTakes(aData);
            }
            if(TakeEditCurrent().contextSelection != null && TakeEditCurrent().contextSelection.Count > 0) {
                //if(!didRegisterUndo) registerTakesUndo(aData, "Deselect Frames", false);
                //didRegisterUndo = true;
                TakeEditCurrent().contextSelection = new List<int>();
                setDirtyTakes(aData);
            }
            if(TakeEditCurrent().ghostSelection != null && TakeEditCurrent().ghostSelection.Count > 0) {
                TakeEditCurrent().ghostSelection = new List<int>();
            }

            if(objects_window.Count > 0) objects_window = new List<GameObject>();
            if(isRenamingGroup < 0) isRenamingGroup = 0;
            if(isRenamingTake) {
                aData.e_makeTakeNameUnique(currentTake);
                isRenamingTake = false;
            }
            if(isRenamingTrack != -1) isRenamingTrack = -1;
            if(isChangingTimeControl) isChangingTimeControl = false;
            if(isChangingFrameControl) isChangingFrameControl = false;
            // if clicked on inspector, do nothing
            if(e.mousePosition.y > (float)height_menu_bar + (float)height_control_bar && e.mousePosition.x > position.width - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed)) return;
            if(TakeEditCurrent().selectedGroup != 0) timelineSelectGroup(0);
            if(TakeEditCurrent().selectedTrack != -1) TakeEditCurrent().selectedTrack = -1;

            if(objects_window.Count > 0) objects_window = new List<GameObject>();

        }
        #endregion
        #region drag logic
        if(dragType == (int)DragType.GroupElement) {
            // show element near cursor
            Rect rectDragElement = new Rect(e.mousePosition.x + 20f, e.mousePosition.y, 90f, 20f);
            string dragElementName = "Unknown";
            Texture dragElementIcon = null;
            float dragElementIconWidth = 12f;
            if(draggingGroupElementType == (int)ElementType.Group) {
                dragElementName = currentTake.getGroup((int)draggingGroupElement.x).group_name;
                dragElementIcon = tex_icon_group_closed;
                dragElementIconWidth = 16f;
            }
            else if(draggingGroupElementType == (int)ElementType.Track) {
                AMTrack dragTrack = currentTake.getTrack((int)draggingGroupElement.y);
                if(dragTrack) {
                    dragElementName = dragTrack.name;
                    dragElementIcon = getTrackIconTexture(dragTrack);
                }
            }
            GUI.DrawTexture(rectDragElement, GUI.skin.GetStyle("GroupElementActive").normal.background);
            dragElementName = trimString(dragElementName, 8);
            if(dragElementIcon) GUI.DrawTexture(new Rect(rectDragElement.x + 3f + (draggingGroupElementType == (int)ElementType.Track ? 1.45f : 0f), rectDragElement.y + rectDragElement.height / 2 - dragElementIconWidth / 2, dragElementIconWidth, dragElementIconWidth), dragElementIcon);
            GUI.Label(new Rect(rectDragElement.x + 15f + 4f, rectDragElement.y, rectDragElement.width - 15f - 4f, rectDragElement.height), dragElementName);
        }
        mouseAboveGroupElements = e.mousePosition.y < (height_menu_bar + height_control_bar);
        if(currentTake.rootGroup == null || currentTake.rootGroup.elements.Count <= 0) {

            if(aData.isInspectorOpen) aData.isInspectorOpen = false;
            float width_helpbox = position.width - width_inspector_closed - 28f - width_track;
            EditorGUI.HelpBox(new Rect(width_track + 5f, height_menu_bar + height_control_bar + 7f, width_helpbox, 50f), "Click the track icon below or drag a GameObject here to add a new track.", MessageType.Info);
            //GUI.DrawTexture(new Rect(width_track+75f,height_menu_bar+height_control_bar+19f-(width_helpbox<=355.5f ? 6f: 0f),15f,15f),tex_icon_track);
        }
        #endregion
        #region quick add
        /*GUIStyle styleObjectField = new GUIStyle(EditorStyles.objectField);
        GUIStyle styleObjectFieldThumb = new GUIStyle(EditorStyles.objectFieldThumb);
        EditorStyles.objectField.normal.textColor = new Color(0f, 0f, 0f, 0f);
        EditorStyles.objectField.contentOffset = new Vector2(width_track * -1 - 300f, 0f);
        EditorStyles.objectField.normal.background = null;
        EditorStyles.objectField.onNormal.background = null;*/

        GameObject tempGO = null;
        //tempGO = (GameObject)EditorGUI.ObjectField(new Rect(width_track, height_menu_bar + height_control_bar + 2f, position.width - 5f - 15f - width_track - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed), position.height - height_indicator_footer - height_menu_bar - height_control_bar), "", tempGO, typeof(GameObject), true);
        if(dragItems != null && dragItems.Length > 0) {
            tempGO = dragItems[0] as GameObject;
        }

        if(tempGO != null) {
            objects_window = new List<GameObject>();
            objects_window.Add(tempGO);
            buildAddTrackMenu_Drag();
            menu_drag.ShowAsContext();
        }
        /*EditorStyles.objectField.contentOffset = styleObjectField.contentOffset;
        EditorStyles.objectField.normal = styleObjectField.normal;
        EditorStyles.objectField.onNormal = styleObjectField.onNormal;
        EditorStyles.objectFieldThumb.normal = styleObjectFieldThumb.normal;*/
        #endregion
        #region tooltip

        if(!oData.disableTimelineActions && !oData.disableTimelineActionsTooltip && dragType == (int)DragType.None && showTooltip && tooltip != "") {
            Vector2 tooltipSize = GUI.skin.label.CalcSize(new GUIContent(tooltip));
            tooltipSize.x += GUI.skin.button.padding.left + GUI.skin.button.padding.right;
            tooltipSize.y += GUI.skin.button.padding.top + GUI.skin.button.padding.bottom;
            Rect rectTooltip = new Rect(e.mousePosition.x - tooltipSize.x / 2f, (e.mousePosition.y + 30f + tooltipSize.y <= position.height - height_playback_controls ? e.mousePosition.y + 30f : e.mousePosition.y - 12f - tooltipSize.y), tooltipSize.x, tooltipSize.y);
            GUI.Box(rectTooltip, tooltip, GUI.skin.button);
        }
        #endregion
        #region custom cursor
        if(customCursor != (int)CursorType.None) {
            if(customCursor == (int)CursorType.Zoom) {
                if(!tex_cursor_zoom) tex_cursor_zoom = tex_cursor_zoomin;
                if(tex_cursor_zoom == tex_cursor_zoomin && aData.zoom <= 0f) tex_cursor_zoom = tex_cursor_zoom_blank;
                else if(tex_cursor_zoom == tex_cursor_zoomout && aData.zoom >= 1f) tex_cursor_zoom = tex_cursor_zoom_blank;
                GUI.DrawTexture(new Rect(e.mousePosition.x - 6f, e.mousePosition.y - 5f, 16f, 16f), tex_cursor_zoom);
            }
            else if(customCursor == (int)CursorType.Hand) {
                GUI.DrawTexture(new Rect(e.mousePosition.x - 8f, e.mousePosition.y - 7f, 16f, 16f), (tex_cursor_grab));
            }
        }
        #endregion
        if(e.alt && !isDragging) startZoomXOverFrame = mouseXOverFrame;
        if(isDragging && dragType != (int)DragType.None)
            e.Use();
        else if(e.type == EventType.MouseMove)
            Repaint();

        if(doRefresh) {
            ClearKeysBuffer();
            contextSelectionTracksBuffer.Clear();
            cachedContextSelection.Clear();

            GameObject go = _aData.gameObject;
            _aData.e_isAnimatorOpen = false;
            _aData = null;
            Selection.activeGameObject = go;
        }

        if(e.type == EventType.MouseMove || e.type == EventType.DragUpdated) {
            mouseMoveTrack = mouseOverTrack;
            mouseMoveFrame = mouseOverFrame;
        }
    }
    void ReloadOtherWindows() {
        if(AMOptions.window) AMOptions.window.reloadAnimatorData();
        if(AMSettings.window) AMSettings.window.reloadAnimatorData();
        if(AMCodeView.window) AMCodeView.window.reloadAnimatorData();
        if(AMEasePicker.window) AMEasePicker.window.reloadAnimatorData();
        if(AMPropertySelect.window) AMPropertySelect.window.reloadAnimatorData();
        if(AMTakeExport.window) AMTakeExport.window.reloadAnimatorData();
    }

    void OnPlayMode() {
        bool justHitPlay = EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying;
        // entered playmode
        if(justHitPlay) {
            if(dragType == (int)DragType.TimeScrub || dragType == (int)DragType.FrameScrub) dragType = (int)DragType.None;
            // destroy camerafade
            if(AMCameraFade.hasInstance() && AMCameraFade.isPreview()) {
                AMCameraFade.destroyImmediateInstance();
            }
        }
    }

    #endregion

    #region Functions

    #region Static

    /// <summary>
    /// Return true if meta was instantiated
    /// </summary>
    public static bool registerTakesUndo(AnimatorData dat, string label, bool complete) {
        //instantiate meta for editing
        if(dat.e_metaCanInstantiatePrefab) {
            //preserve the current take edit info
            AMTakeEdit curTakeEdit = null;
            if(window) {
                AMTakeData curTake = window.currentTake;
                foreach(AMTakeData take in dat._takes) {
                    if(take == curTake)
                        window.mTakeEdits.TryGetValue(take, out curTakeEdit);
                    window.mTakeEdits.Remove(take);
                }
            }

            dat.e_metaInstantiatePrefab(label);

            if(curTakeEdit != null) {
                window.mTakeEdits.Add(window.currentTake, curTakeEdit);
                window.currentTake.selectedFrame = curTakeEdit.selectedFrame;
            }

            EditorUtility.SetDirty(dat);
            return true;
        }
        else {
            UnityEngine.Object obj = dat.e_meta ? (UnityEngine.Object)dat.e_meta : (UnityEngine.Object)dat;

            if(complete)
                UnityEditor.Undo.RegisterCompleteObjectUndo(obj, label);
            else
                UnityEditor.Undo.RecordObject(obj, label);
            return false;
        }
    }

    public static void setDirtyTakes(AnimatorData dat) {
        UnityEditor.EditorUtility.SetDirty(dat.e_meta ? (UnityEngine.Object)dat.e_meta : (UnityEngine.Object)dat);
    }

    public static MonoBehaviour[] getKeysAndTracks(AMTakeData take) {
        List<MonoBehaviour> behaviours = new List<MonoBehaviour>();

        if(take.trackValues != null) {
            foreach(AMTrack track in take.trackValues) {
                if(track.keys != null) {
                    foreach(AMKey key in track.keys)
                        behaviours.Add(key);
                }

                behaviours.Add(track);
            }
        }

        return behaviours.ToArray();
    }

    public static void MessageBox(string message, MessageBoxType type) {

        MessageType messageType;
        if(type == MessageBoxType.Error) messageType = MessageType.Error;
        else if(type == MessageBoxType.Warning) messageType = MessageType.Warning;
        else messageType = MessageType.Info;

        EditorGUILayout.HelpBox(message, messageType);
    }
    public static void loadSkin(ref GUISkin _skin, ref string skinName, Rect position) {
        string newSkinName = EditorGUIUtility.isProSkin ? "am_skin_dark" : "am_skin_light";
        if(_skin == null || newSkinName != skinName) {
            _skin = (GUISkin)AMEditorResource.LoadSkin(newSkinName); /*global_skin*/
            skinName = newSkinName;
            AMTransitionPicker.texLoaded = false;
            texLoaded = false;
        }

        GUI.skin = _skin;
        GUI.color = GUI.skin.window.normal.textColor;
        GUI.DrawTexture(new Rect(0f, 0f, position.width, position.height), EditorGUIUtility.whiteTexture);
        GUI.color = Color.white;

    }
    public static GUIStyleState getSkinTextureStyleState(string name) {
        if(name == "properties_bg") return GUI.skin.GetStyle("Textures_1").normal;
        if(name == "properties_top") return GUI.skin.GetStyle("Textures_3").active;
        if(name == "delete") return GUI.skin.GetStyle("Textures_1").hover;
        if(name == "rename") return GUI.skin.GetStyle("Textures_1").active;
        if(name == "zoom") return GUI.skin.GetStyle("Textures_1").focused;
        if(name == "nav_skip_back") return GUI.skin.GetStyle("Textures_1").onNormal;
        if(name == "nav_play") return GUI.skin.GetStyle("Textures_1").onHover;
        if(name == "nav_skip_forward") return GUI.skin.GetStyle("Textures_1").onActive;
        if(name == "next_key") return GUI.skin.GetStyle("Textures_1").onFocused;
        if(name == "prev_key") return GUI.skin.GetStyle("Textures_2").normal;
        if(name == "nav_stop") return GUI.skin.GetStyle("Textures_2").hover;
        if(name == "accept") return GUI.skin.GetStyle("Textures_2").focused;
        if(name == "delete_hover") return GUI.skin.GetStyle("Textures_2").active;
        if(name == "popup") return GUI.skin.GetStyle("Textures_2").onNormal;
        if(name == "playonstart") return GUI.skin.GetStyle("Textures_2").onHover;
        if(name == "nav_stop_white") return GUI.skin.GetStyle("Textures_2").onActive;
        if(name == "select_all") return GUI.skin.GetStyle("Textures_2").onFocused;
        if(name == "x") return GUI.skin.GetStyle("Textures_3").normal;
        if(name == "select_this") return GUI.skin.GetStyle("Textures_3").hover;
        //if(name == "select_exclusive") return GUI.skin.GetStyle("Textures_3").active;
        Debug.LogWarning("Animator: Skin texture " + name + " not found.");
        return GUI.skin.label.normal;
    }
    public static void resetIndexMethodInfo() {
        indexMethodInfo = -1;
    }
    public static float frameToTime(int frame, float frameRate) {
        return (float)Math.Round((float)frame / frameRate, 2);
    }
    public static int timeToFrame(float time, float frameRate) {
        return Mathf.FloorToInt(time * frameRate);
    }
    private static void cacheSelectedMethodParameterInfos() {
        if(cachedMethodInfo == null || indexMethodInfo == -1 || (indexMethodInfo >= cachedMethodInfo.Count)) {
            cachedParameterInfos = new ParameterInfo[] { };
            arrayFieldFoldout = new Dictionary<string, bool>();	// reset array foldout dictionary
            return;
        }
        cachedParameterInfos = cachedMethodInfo[indexMethodInfo].GetParameters();
    }

    #endregion

    #region Show/Draw

    bool showGroupElement(int id, int group_lvl, ref float track_y, ref bool isAnyTrackFoldedOut, ref float height_group_elements, Event e, Vector2 scrollViewBounds) {
        // returns true if mouse over track
        if(id >= 0) {
            AMTrack _track = currentTake.getTrack(id);
            return _track ? showTrack(_track, id, group_lvl, ref track_y, ref isAnyTrackFoldedOut, ref height_group_elements, e, scrollViewBounds) : false;
        }
        else {
            return showGroup(id, group_lvl, ref track_y, ref isAnyTrackFoldedOut, ref height_group_elements, e, scrollViewBounds);
        }
    }
    bool showGroup(int id, int group_lvl, ref float track_y, ref bool isAnyTrackFoldedOut, ref float height_group_elements, Event e, Vector2 scrollViewBounds) {
        if(track_y > scrollViewBounds.y) return false;	// if beyond lower bound, return
        bool isBeyondUpperBound = (track_y + height_group < scrollViewBounds.x);
        // show group
        float group_x = width_subtrack_space * group_lvl;
        group_lvl++;	// increment group_lvl for sub-elements
        Rect rectGroup = new Rect(group_x, track_y, width_track - group_x, height_group);
        AMGroup grp = currentTake.getGroup(id);
        bool isGroupSelected = (TakeEditCurrent().selectedGroup == id && TakeEditCurrent().selectedTrack == -1);
        float local_height_group_elements = height_group;
        if(!isBeyondUpperBound) {
            if(rectGroup.width > 4f) {
                if(isRenamingGroup != id) {
                    if(GUI.Button(new Rect(group_x + 15f, track_y, width_track - 15f, height_group), "", "label")) {
                        timelineSelectGroup(id);
                        if(didDoubleClick("group" + id + "foldout")) {
                            //grp.foldout = !grp.foldout;
                            isRenamingGroup = id;
                        }
                    }
                }
                //foldout button
                if(GUI.Button(new Rect(group_x, track_y, 15f, height_group), "", "label")) {
                    grp.foldout = !grp.foldout;

                    timelineSelectGroup(id);
                    if(!grp.foldout) {
                        //bool found = false;
                        foreach(int __id in grp.elements) {
                            if(isRenamingTrack == __id) {
                                isRenamingTrack = -1;
                                break;
                            }
                        }
                    }
                }
                string strStyle;
                int numTracks = 0;
                if(((grp.foldout && TakeEditCurrent().contextSelectionTracks.Count > 1) || (!grp.foldout && TakeEditCurrent().contextSelectionTracks.Count > 0)) && TakeEditCurrent().isGroupSelected(currentTake, id, ref numTracks) && numTracks > 0) {
                    if(isGroupSelected) strStyle = "GroupElementSelectedActive";
                    else strStyle = "GroupElementSelected";
                }
                else {
                    if(isGroupSelected) strStyle = "GroupElementActive";
                    else strStyle = "GroupElementNormal";
                }
                // group element
                GUI.BeginGroup(rectGroup, GUI.skin.GetStyle(strStyle));
                if(isRenamingGroup == id) {
                    GUI.SetNextControlName("RenameGroup" + id);
                    grp.group_name = GUI.TextField(new Rect(33f, 2f, rectGroup.width, rectGroup.height), grp.group_name);
                    GUI.FocusControl("RenameGroup" + id);
                }
                else {
                    GUI.Label(new Rect(33f, 0f, rectGroup.width, rectGroup.height), currentTake.getGroup(id).group_name);
                }
                GUI.EndGroup();
                if(rectGroup.width >= 15f) GUI.DrawTexture(new Rect(group_x, track_y + (height_group - 16f) / 2f, 16f, 16f), (grp.foldout ? GUI.skin.GetStyle("GroupElementFoldout").normal.background : GUI.skin.GetStyle("GroupElementFoldout").active.background));
                if(rectGroup.width >= 32f) GUI.DrawTexture(new Rect(group_x + 15f, track_y + (height_group - 16f) / 2f, 16f, 16f), (grp.foldout ? tex_icon_group_open : tex_icon_group_closed));
                // mouse over
                Rect rectGroupNoFoldout = new Rect(rectGroup.x + 15f, rectGroup.y, rectGroup.width - 15f, rectGroup.height);
                if(rectGroupNoFoldout.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
                    mouseOverElement = (int)ElementType.Group;
                    mouseOverGroupElement = new Vector2(id, -1);
                }
            }
            else {
                // tooltip hidden group
                GUI.enabled = false;
                GUI.Button(new Rect(width_track - 80f, track_y, 80f, height_group), trimString(grp.group_name, 8));
                GUI.enabled = !isPlaying;
            }
        }
        Rect rectGroupOutside = new Rect(group_x, track_y, 15f, height_group);
        track_y += height_group;
        if(grp.foldout) {

            if(grp.elements.Count <= 0) {
                Rect rectGroupEmpty = new Rect(group_x + width_subtrack_space, track_y, width_track - group_x - width_subtrack_space, height_group);
                local_height_group_elements += rectGroupEmpty.height;
                track_y += height_group;
                // if "no tracks" label in bounds, show
                if(track_y > scrollViewBounds.x && track_y - height_group < scrollViewBounds.y) {
                    GUI.Label(rectGroupEmpty, "No Tracks");
                }

                if(rectGroupEmpty.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
                    mouseOverElement = (int)ElementType.Group;
                    mouseOverGroupElement = new Vector2(id, -1);
                }
            }
            else {
                for(int j = 0; j < grp.elements.Count; j++) {
                    int _id = grp.elements[j];
                    showGroupElement(_id, group_lvl, ref track_y, ref isAnyTrackFoldedOut, ref local_height_group_elements, e, scrollViewBounds);
                }
            }
            rectGroupOutside.height = local_height_group_elements;

        }
        // mouse over group outside
        if(rectGroupOutside.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
            mouseOverElement = (int)ElementType.GroupOutside;
            mouseOverGroupElement = new Vector2(id, -1);
        }
        // draw element position indicator after group
        if(dragType == (int)DragType.GroupElement) {
            if(mouseOverGroupElement.x == id && mouseOverElement == (int)ElementType.GroupOutside) {
                GUI.DrawTexture(new Rect(rectGroup.x, track_y - height_element_position, rectGroup.width, height_element_position), tex_element_position);
            }
            else {
                if(mouseOverGroupElement.x == id && mouseOverElement == (int)ElementType.Group) {
                    GUI.DrawTexture(new Rect(rectGroup.x + 15f, rectGroup.y + rectGroup.height - height_element_position, rectGroup.width - 15f, height_element_position), tex_element_position);
                }
            }
        }
        height_group_elements += local_height_group_elements;
        return false;
    }
    bool showTrack(AMTrack _track, int t, int group_level, ref float track_y, ref bool isAnyTrackFoldedOut, ref float height_group_elements, Event e, Vector2 scrollViewBounds) {
        // track is beyond bounds
        if(track_y + (_track.foldout ? height_track : height_track_foldin) < scrollViewBounds.x || track_y > scrollViewBounds.y) {
            if(_track.foldout) {
                track_y += height_track;
                isAnyTrackFoldedOut = true;
            }
            else {
                track_y += height_track_foldin;
            }
            return false;
        }
        // returns true if mouse over track
        bool mouseOverTrack = false;
        float track_x = width_subtrack_space * group_level;
        bool inGroup = group_level > 0;
        bool isTrackSelected = TakeEditCurrent().selectedTrack == t;
        bool isTrackContextSelected = TakeEditCurrent().contextSelectionTracks.Contains(t);
        Rect rectTrack;
        string strStyle;
        if(isTrackSelected) {
            if(TakeEditCurrent().contextSelectionTracks.Count <= 1)
                strStyle = "GroupElementActive";
            else
                strStyle = "GroupElementSelectedActive";
        }
        else if(isTrackContextSelected) strStyle = "GroupElementSelected";
        else strStyle = "GroupElementNormal";

        rectTrack = new Rect(track_x, track_y, width_track - track_x, height_track_foldin);
        // renaming track
        if(isRenamingTrack != t) {
            Rect rectTrackFoldin = new Rect(rectTrack.x, rectTrack.y, rectTrack.width, rectTrack.height);	// used to toggle track foldout
            if(_track.foldout) {
                rectTrackFoldin.width -= 55f;
            }
            if(GUI.Button(new Rect(rectTrack.x, rectTrack.y, 15f, rectTrack.height), "", "label")) {
                _track.foldout = !_track.foldout;
                timelineSelectTrack(t);
            }
            if(GUI.Button(new Rect(rectTrack.x + 15f, rectTrack.y, rectTrack.width - 15f, rectTrack.height), "", "label")) {
                timelineSelectTrack(t);
                if(didDoubleClick("track" + t + "foldout")) {
                    cancelTextEditting();
                    //_track.foldout = !_track.foldout;
                    isRenamingTrack = t;

                    if(!_track.foldout) _track.foldout = true;
                }
            }
        }
        // set track icon texture
        Texture texIcon = getTrackIconTexture(_track);
        // track start, foldin
        if(!_track.foldout) {
            if(rectTrack.width > 4f) {
                if(rectTrack.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
                    mouseOverTrack = true;
                    mouseOverElement = (int)ElementType.Track;
                    mouseOverGroupElement = new Vector2((inGroup ? currentTake.getTrackGroup(t) : 0), t);
                }
                GUI.BeginGroup(rectTrack, GUI.skin.GetStyle(strStyle));
                GUI.DrawTexture(new Rect(17f, (height_track_foldin - 12f) / 2f, 12f, 12f), texIcon);
                GUI.Label(new Rect(17f + 12f + 2f, 0f, rectTrack.width - (12f + 15f + 2f), height_track_foldin), _track.name);
                GUI.EndGroup();
                // draw foldout
                if(rectTrack.width >= 10f) GUI.DrawTexture(new Rect(track_x, track_y + (height_group - 16f) / 2f, 16f, 16f), (_track.foldout ? GUI.skin.GetStyle("GroupElementFoldout").normal.background : GUI.skin.GetStyle("GroupElementFoldout").active.background));
            }
            else {
                // tooltip hidden track, foldin
                GUI.enabled = false;
                GUI.Button(new Rect(width_track - 80f, track_y, 80f, height_group), trimString(_track.name, 8));
                GUI.enabled = !isPlaying;
            }
            track_y += height_track_foldin;
        }
        else {
            // track start, foldout
            // track rect
            rectTrack = new Rect(track_x, track_y, width_track - track_x, height_track);
            if(rectTrack.width > 4f) {
                // select track texture
                if(rectTrack.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
                    mouseOverTrack = true;
                    mouseOverElement = (int)ElementType.Track;
                    mouseOverGroupElement = new Vector2((inGroup ? currentTake.getTrackGroup(t) : 0), t);
                }
                // draw track texture
                GUI.BeginGroup(rectTrack, GUI.skin.GetStyle(strStyle));
                // track name
                if(isRenamingTrack == t) {
                    GUI.SetNextControlName("RenameTrack" + t);
                    _track.name = GUI.TextField(new Rect(15f, 2f, rectTrack.width - 15f, 20f), _track.name, 20);
                    GUI.FocusControl("RenameTrack" + t);

                }
                else {
                    GUI.Label(new Rect(15f, 0f, rectTrack.width - 15f, 20f), _track.name);
                }
                // track type
                Rect rectTrackIcon = new Rect(4f, 20f, 12f, 12f);
                GUI.Box(rectTrackIcon, texIcon);
                string trackType = _track.getTrackType();
                Rect rectTrackType = new Rect(rectTrackIcon.x + rectTrackIcon.width + 2f, height_track - 39f, rectTrack.width - 20f, 15f);
                if((_track is AMPropertyTrack) && (trackType == "Not Set"))
                    rectTrackType.width -= 48f;
                GUI.Label(rectTrackType, trackType);
                // if property track, show set property button
                if(_track is AMPropertyTrack) {
                    if(!_track.GetTarget(aData)) GUI.enabled = false;
                    GUIStyle styleButtonSet = new GUIStyle(GUI.skin.button);
                    styleButtonSet.clipping = TextClipping.Overflow;
                    if(GUI.Button(new Rect(width_track - 48f - width_subtrack_space * group_level - 4f, height_track - 38f, 48f, 15f), "Set", styleButtonSet)) {
                        // show property select window 
                        AMPropertySelect.setValues((_track as AMPropertyTrack));
                        EditorWindow.GetWindow(typeof(AMPropertySelect));
                    }
                    GUI.enabled = !isPlaying;
                }
                // track object
                float width_object_field = width_track - track_x;
                showObjectFieldFor(_track, width_object_field, new Rect(padding_track, 39f, width_track - width_subtrack_space * group_level - padding_track * 2, 16f));
                GUI.EndGroup();
            }
            else {
                // tooltip hidden track, foldout
                GUI.enabled = false;
                GUI.Button(new Rect(width_track - 80f, track_y, 80f, height_group), trimString(_track.name, 8));
                GUI.enabled = !isPlaying;
            }
            // track button
            if(GUI.Button(rectTrack, "", "label")) {
                timelineSelectTrack(t);
            }
            // draw foldout
            if(rectTrack.width >= 15f) GUI.DrawTexture(new Rect(track_x, track_y + (height_group - 16f) / 2f, 16f, 16f), (_track.foldout ? GUI.skin.GetStyle("GroupElementFoldout").normal.background : GUI.skin.GetStyle("GroupElementFoldout").active.background));
            track_y += height_track;
            isAnyTrackFoldedOut = true;
            // track end
        }
        // draw element position texture after track
        if(dragType == (int)DragType.GroupElement) {
            if(mouseOverElement == (int)ElementType.Track && mouseOverGroupElement.y == t) {
                GUI.DrawTexture(new Rect(rectTrack.x, rectTrack.y + rectTrack.height - height_element_position, rectTrack.width, height_element_position), tex_element_position);
            }
        }
        height_group_elements += rectTrack.height;
        return mouseOverTrack;
    }
    void showFramesForGroup(int group_id, ref float track_y, Event e, bool birdseye, Vector2 scrollViewBounds) {
        if(track_y > scrollViewBounds.y) return;	// if start y is beyond max y
        AMGroup grp = currentTake.getGroup(group_id);
        // add to track y
        if(group_id < 0) {
            // group height
            track_y += height_group;
            // "no tracks" label
            if(grp.foldout && grp.elements.Count <= 0) {
                track_y += height_group;
            }
        }
        if(group_id == 0 || grp.foldout) {
            foreach(int id in grp.elements) {
                if(id <= 0) {
                    if(track_y > scrollViewBounds.y) return;	// if start y is beyond max y
                    showFramesForGroup(id, ref track_y, e, birdseye, scrollViewBounds);
                }
                else {
                    if(track_y > scrollViewBounds.y) return;	// if start y is beyond max y
                    AMTrack track = currentTake.getTrack(id);
                    if(track)
                        showFrames(track, ref track_y, e, birdseye, scrollViewBounds);
                }
            }
        }
    }
    void drawBox(int cached_action_startFrame, int cached_action_endFrame, int _startFrame, int _endFrame, Rect rectTimelineActions, float birdsEyeWidth, Texture texBox) {
        if(cached_action_startFrame > 0 && cached_action_endFrame > 0 && cached_action_endFrame > _startFrame && cached_action_startFrame < _endFrame) {
            if(cached_action_startFrame <= _startFrame) {
                rectTimelineActions.x = 0f;
            }
            else {
                rectTimelineActions.x = (cached_action_startFrame - _startFrame) * current_width_frame;
            }
            if(cached_action_endFrame >= _endFrame) {
                rectTimelineActions.width = birdsEyeWidth;//rectFramesBirdsEye.width;
            }
            else {
                rectTimelineActions.width = (cached_action_endFrame - (_startFrame >= cached_action_startFrame ? _startFrame : cached_action_startFrame) + 1) * current_width_frame;
            }
            // draw timeline action texture
            if(rectTimelineActions.width > 0f) GUI.DrawTextureWithTexCoords(rectTimelineActions, texBox, new Rect(0, 0, rectTimelineActions.width/32f, rectTimelineActions.height/64f));
        }
    }
    void showFrames(AMTrack _track, ref float track_y, Event e, bool birdseye, Vector2 scrollViewBounds) {
        //string tooltip = "";
        int t = _track.id;
        AMTakeData curTake = currentTake;
        int selectedTrack = TakeEdit(curTake).selectedTrack;
        // frames start
        if(!_track.foldout && !oData.showFramesForCollapsedTracks) {
            track_y += height_track_foldin;
            return;
        }
        float numFrames = (curTake.numFrames < numFramesToRender ? curTake.numFrames : numFramesToRender);
        Rect rectFrames = new Rect(width_track, track_y, current_width_frame * numFrames, height_track);
        if(!_track.foldout) track_y += height_track_foldin;
        else track_y += height_track;
        if(track_y < scrollViewBounds.x) return; // if end y is before min y
        float _current_height_frame = (_track.foldout ? current_height_frame : height_track_foldin);
        #region frames
        GUI.BeginGroup(rectFrames);
        // draw frames
        bool selected;
        bool ghost = isDragging && TakeEditCurrent().hasGhostSelection();
        bool isTrackSelected = t == selectedTrack || TakeEditCurrent().contextSelectionTracks.Contains(t);
        Rect rectFramesBirdsEye = new Rect(0f, 0f, rectFrames.width, _current_height_frame);
        float width_birdseye = current_height_frame * 0.5f;
        if(birdseye) {
            GUI.color = colBirdsEyeFrames;
            GUI.DrawTexture(rectFramesBirdsEye, EditorGUIUtility.whiteTexture);
        }
        else {
            texFrSet.wrapMode = TextureWrapMode.Repeat;
            float startPos = curTake.startFrame % 5f;
            GUI.DrawTextureWithTexCoords(rectFramesBirdsEye, texFrSet, new Rect(startPos / 5f, 0f, numFrames / 5f, 1f));
            float birdsEyeFadeAlpha = (1f - (current_width_frame - width_frame_birdseye_min)) / 1.2f;
            if(birdsEyeFadeAlpha > 0f) {
                GUI.color = new Color(colBirdsEyeFrames.r, colBirdsEyeFrames.g, colBirdsEyeFrames.b, birdsEyeFadeAlpha);
                GUI.DrawTexture(rectFramesBirdsEye, EditorGUIUtility.whiteTexture);
            }
        }
        GUI.color = new Color(72f / 255f, 72f / 255f, 72f / 255f, 1f);
        GUI.DrawTexture(new Rect(rectFramesBirdsEye.x, rectFramesBirdsEye.y, rectFramesBirdsEye.width, 1f), EditorGUIUtility.whiteTexture);
        GUI.DrawTexture(new Rect(rectFramesBirdsEye.x, rectFramesBirdsEye.y + rectFramesBirdsEye.height - 1f, rectFramesBirdsEye.width, 1f), EditorGUIUtility.whiteTexture);
        GUI.color = Color.white;
        // draw birds eye selection
        if(isTrackSelected) {
            if(ghost) {
                // dragging only one frame that has a key. do not show ghost selection
                if(birdseye && TakeEditCurrent().contextSelection.Count == 2 && TakeEditCurrent().contextSelection[0] == TakeEditCurrent().contextSelection[1] && _track.hasKeyOnFrame(TakeEditCurrent().contextSelection[0])) {
                    GUI.color = new Color(0f, 0f, 1f, .5f);
                    GUI.DrawTexture(new Rect(current_width_frame * (TakeEditCurrent().ghostSelection[0] - curTake.startFrame) - width_birdseye / 2f + current_width_frame / 2f, 0f, width_birdseye, _current_height_frame), texKeyBirdsEye);
                    GUI.color = Color.white;
                }
                else if(TakeEditCurrent().ghostSelection != null) {
                    // birds eye ghost selection
                    GUI.color = new Color(156f / 255f, 162f / 255f, 216f / 255f, .9f);
                    for(int i = 0; i < TakeEditCurrent().ghostSelection.Count; i += 2) {
                        int contextFrameStart = TakeEditCurrent().ghostSelection[i];
                        int contextFrameEnd = TakeEditCurrent().ghostSelection[i + 1];
                        if(contextFrameStart < (int)curTake.startFrame) contextFrameStart = (int)curTake.startFrame;
                        if(contextFrameEnd > (int)curTake.endFrame) contextFrameEnd = (int)curTake.endFrame;
                        float contextWidth = (contextFrameEnd - contextFrameStart + 1) * current_width_frame;
                        GUI.DrawTexture(new Rect(rectFramesBirdsEye.x + (contextFrameStart - curTake.startFrame) * current_width_frame, rectFramesBirdsEye.y + 1f, contextWidth, rectFramesBirdsEye.height - 2f), EditorGUIUtility.whiteTexture);
                    }
                    // draw birds eye ghost key frames
                    GUI.color = new Color(0f, 0f, 1f, .5f);
                    foreach(int _key_frame in TakeEditCurrent().getKeyFramesInGhostSelection(curTake, (int)curTake.startFrame, (int)curTake.endFrame, t)) {
                        if(birdseye)
                            GUI.DrawTexture(new Rect(current_width_frame * (_key_frame - curTake.startFrame) - width_birdseye / 2f + current_width_frame / 2f, 0f, width_birdseye, _current_height_frame), texKeyBirdsEye);
                        else {
                            Rect rectFrame = new Rect(current_width_frame * (_key_frame - curTake.startFrame), 0f, current_width_frame, _current_height_frame);
                            GUI.DrawTexture(new Rect(rectFrame.x + 2f, rectFrame.y + rectFrame.height - (rectFrame.width - 4f) - 2f, rectFrame.width - 4f, rectFrame.width - 4f), texFrKey);
                        }
                    }
                    GUI.color = Color.white;
                }
            }
            else if(TakeEditCurrent().contextSelection.Count > 0 && /*do not show single frame selection in birdseye*/!(birdseye && TakeEditCurrent().contextSelection.Count == 2 && TakeEditCurrent().contextSelection[0] == TakeEditCurrent().contextSelection[1])) {
                // birds eye context selection
                for(int i = 0; i < TakeEditCurrent().contextSelection.Count; i += 2) {
                    //GUI.color = new Color(121f/255f,127f/255f,184f/255f,(birdseye ? 1f : .9f));
                    GUI.color = new Color(86f / 255f, 95f / 255f, 178f / 255f, .8f);
                    int contextFrameStart = TakeEditCurrent().contextSelection[i];
                    int contextFrameEnd = TakeEditCurrent().contextSelection[i + 1];
                    if(contextFrameStart < (int)curTake.startFrame) contextFrameStart = (int)curTake.startFrame;
                    if(contextFrameEnd > (int)curTake.endFrame) contextFrameEnd = (int)curTake.endFrame;
                    float contextWidth = (contextFrameEnd - contextFrameStart + 1) * current_width_frame;
                    Rect rectContextSelection = new Rect(rectFramesBirdsEye.x + (contextFrameStart - curTake.startFrame) * current_width_frame, rectFramesBirdsEye.y + 1f, contextWidth, rectFramesBirdsEye.height - 2f);
                    GUI.DrawTexture(rectContextSelection, EditorGUIUtility.whiteTexture);
                    if(dragType != (int)DragType.ContextSelection) EditorGUIUtility.AddCursorRect(rectContextSelection, MouseCursor.SlideArrow);
                }
                GUI.color = Color.white;
            }
        }
        // birds eye keyframe information, used to draw buttons in proper order
        List<int> birdseyeKeyFrames = new List<int>();
        List<Rect> birdseyeKeyRects = new List<Rect>();
        if(birdseye) {
            // draw birds eye keyframe textures, prepare button rects
            foreach(AMKey key in _track.keys) {
                if(!key) continue;

                selected = ((isTrackSelected) && TakeEditCurrent().isFrameSelected(key.frame));
                //_track.sortKeys();
                if(key.frame < curTake.startFrame) continue;
                if(key.frame > curTake.endFrame) break;
                Rect rectKeyBirdsEye = new Rect(current_width_frame * (key.frame - curTake.startFrame) - width_birdseye / 2f + current_width_frame / 2f, 0f, width_birdseye, _current_height_frame);
                if(selected) GUI.color = Color.blue;
                GUI.DrawTexture(rectKeyBirdsEye, texKeyBirdsEye);
                GUI.color = Color.white;
                birdseyeKeyFrames.Add(key.frame);
                birdseyeKeyRects.Add(rectKeyBirdsEye);
            }

            // birds eye buttons
            if(birdseyeKeyFrames.Count > 0) {
                for(int i = birdseyeKeyFrames.Count - 1; i >= 0; i--) {
                    selected = ((isTrackSelected) && TakeEditCurrent().isFrameSelected(birdseyeKeyFrames[i]));
                    if(dragType != (int)DragType.MoveSelection && dragType != (int)DragType.ContextSelection && !isRenamingTake && isRenamingTrack == -1 && mouseOverFrame == 0 && birdseyeKeyRects[i].Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
                        mouseOverFrame = birdseyeKeyFrames[i];
                        mouseOverTrack = t;
                        mouseOverSelectedFrame = (selected);
                    }
                    if(selected && dragType != (int)DragType.ContextSelection) EditorGUIUtility.AddCursorRect(birdseyeKeyRects[i], MouseCursor.SlideArrow);
                }
            }
        }
        else {
            selected = (isTrackSelected);
            foreach(AMKey key in _track.keys) {
                if(!key) continue;

                //_track.sortKeys();
                if(key.frame < curTake.startFrame) continue;
                if(key.frame > curTake.endFrame) break;
                Rect rectFrame = new Rect(current_width_frame * (key.frame - curTake.startFrame), 0f, current_width_frame, _current_height_frame);
                GUI.DrawTexture(new Rect(rectFrame.x + 2f, rectFrame.y + rectFrame.height - (rectFrame.width - 4f) - 2f, rectFrame.width - 4f, rectFrame.width - 4f), texFrKey);
            }
        }
        // click on empty frames
        if(GUI.Button(rectFramesBirdsEye, "", "label") && dragType == (int)DragType.None) {
            int prevFrame = curTake.selectedFrame;
            bool clickedOnBirdsEyeKey = false;
            for(int i = birdseyeKeyFrames.Count - 1; i >= 0; i--) {
                if(birdseyeKeyFrames[i] > (int)curTake.endFrame) continue;
                if(birdseyeKeyFrames[i] < (int)curTake.startFrame) break;
                if(birdseyeKeyRects[i].Contains(e.mousePosition)) {
                    clickedOnBirdsEyeKey = true;
                    // left click
                    if(e.button == 0) {
                        // select the frame
                        timelineSelectFrame(t, birdseyeKeyFrames[i]);
                        // add frame to context selection
                        contextSelectFrame(birdseyeKeyFrames[i], prevFrame);
                        // right click
                    }
                    else if(e.button == 1) {

                        // select track
                        timelineSelectTrack(t);
                        // if context selection is empty, select frame
                        buildContextMenu(birdseyeKeyFrames[i]);
                        // show context menu
                        contextMenu.ShowAsContext();
                    }
                    break;
                }
            }
            if(!clickedOnBirdsEyeKey) {
                int _frame_num_birdseye = (int)curTake.startFrame + Mathf.CeilToInt(e.mousePosition.x / current_width_frame) - 1;
                // left click
                if(e.button == 0) {
                    // select the frame
                    timelineSelectFrame(t, _frame_num_birdseye);
                    // add frame to context selection
                    contextSelectFrame(_frame_num_birdseye, prevFrame);
                    // right click
                }
                else if(e.button == 1) {
                    timelineSelectTrack(t);
                    // if context selection is empty, select frame
                    buildContextMenu(_frame_num_birdseye);
                    // show context menu
                    contextMenu.ShowAsContext();
                }
            }
        }
        if(!isRenamingTake && isRenamingTrack == -1 && mouseOverFrame == 0 && e.mousePosition.x >= rectFramesBirdsEye.x && e.mousePosition.x <= (rectFramesBirdsEye.x + rectFramesBirdsEye.width)) {
            if(rectFramesBirdsEye.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
                mouseOverFrame = mouseXOverFrame;
                mouseOverTrack = t;
            }
            mouseOverSelectedFrame = ((isTrackSelected) && TakeEditCurrent().isFrameSelected(mouseXOverFrame));
        }
        #endregion
        if(!oData.disableTimelineActions && _track.foldout) {
            #region timeline actions
            //AudioClip audioClip = null;
            bool drawEachAction = false;
            if(_track is AMAnimationTrack || _track is AMAudioTrack) drawEachAction = true;	// draw each action with seperate textures and buttons for these tracks
            int _startFrame = (int)curTake.startFrame;
            int _endFrame = (int)(_startFrame + numFrames - 1);
            int action_startFrame, action_endFrame, renderFrameStart, renderFrameEnd;
            int cached_action_startFrame = -1, cached_action_endFrame = -1;
            Texture texBox = texBoxBorder;
            #region group textures / buttons (performance increase)
            Rect rectTimelineActions = new Rect(0f, _current_height_frame, 0f, height_track - current_height_frame);	// used to group textures into one draw call
            if(!drawEachAction) {
                if(_track.keys.Count > 0) {
                    if(_track is AMEventTrack || _track is AMTriggerTrack) {
                        texBox = texBoxDarkBlue;

                        for(int i = 0; i < _track.keys.Count; i++) {
                            AMKey key = _track.keys[i];

                            if(key.frame + 1 < _startFrame) continue;
                            else if(key.frame > _endFrame) break;

                            drawBox(key.frame, key.frame, _startFrame, _endFrame, rectTimelineActions, rectFramesBirdsEye.width, texBox);
                        }
                    }
                    else if(_track is AMPropertyTrack) {
                        // event track, from first action start frame to end frame
                        cached_action_startFrame = _track.keys[0].getStartFrame();
                        cached_action_endFrame = _track.keys[_track.keys.Count-1].getStartFrame();
                        texBox = texBoxLightBlue;
                        drawBox(cached_action_startFrame, cached_action_endFrame, _startFrame, _endFrame, rectTimelineActions, rectFramesBirdsEye.width, texBox);
                    }
                    else if(_track is AMGOSetActiveTrack) {
                        texBox = texBoxLightBlue;

                        int lastStartInd = -1;

                        for(int i = 0; i < _track.keys.Count; i++) {
                            AMGOSetActiveKey key = _track.keys[i] as AMGOSetActiveKey;

                            bool draw = false;

                            if(key.setActive) {
                                if(lastStartInd == -1)
                                    lastStartInd = i;

                                if(i == _track.keys.Count - 1)
                                    draw = true;
                            }
                            else if(lastStartInd != -1 || (i == 0 && (_track as AMGOSetActiveTrack).startActive)) {
                                draw = true;
                            }

                            if(draw) {
                                if(i == 0 && !key.setActive) {
                                    cached_action_startFrame = _startFrame;
                                    cached_action_endFrame = _track.keys[i].getStartFrame() - 1;
                                }
                                else {
                                    cached_action_startFrame = _track.keys[lastStartInd].getStartFrame();
                                    cached_action_endFrame = i == _track.keys.Count - 1 && key.setActive ? _endFrame : _track.keys[i].getStartFrame() - 1;
                                }

                                drawBox(cached_action_startFrame, cached_action_endFrame, _startFrame, _endFrame, rectTimelineActions, rectFramesBirdsEye.width, texBox);

                                lastStartInd = -1;
                                if(cached_action_endFrame >= _endFrame)
                                    break;
                            }
                        }
                    }
                    else {
                        int lastStartInd = -1;

                        for(int i = 0; i < _track.keys.Count; i++) {
                            bool draw = false;

                            if(_track.keys[i].easeType == AMKey.EaseTypeNone) {
                                if(i == 0 || _track.keys[i - 1].easeType == AMKey.EaseTypeNone) {
                                    lastStartInd = i;
                                    draw = true;
                                }
                                else if(lastStartInd == -1)
                                    continue;
                                else {
                                    draw = true;
                                }
                            }
                            else if(lastStartInd == -1) {
                                lastStartInd = i;
                            }
                            else if(i == _track.keys.Count - 1)
                                draw = true;

                            if(draw) {
                                if(_track is AMTranslationTrack) {
                                    // translation track, from first action frame to end action frame
                                    cached_action_startFrame = _track.keys[lastStartInd].getStartFrame();
                                    cached_action_endFrame = _track.keys[lastStartInd].easeType == AMKey.EaseTypeNone ? cached_action_startFrame : 
										_track.keys[i].easeType == AMKey.EaseTypeNone ? (_track.keys[i] as AMTranslationKey).startFrame : 
											(_track.keys[i] as AMTranslationKey).endFrame;
                                    texBox = texBoxGreen;
                                }
                                else {
                                    cached_action_startFrame = _track.keys[lastStartInd].getStartFrame();
                                    cached_action_endFrame = _track.keys[i].getStartFrame();

                                    if(_track is AMRotationTrack)
                                        texBox = texBoxYellow;
                                    else if(_track is AMOrientationTrack)
                                        texBox = texBoxOrange;
                                    else if(_track is AMCameraSwitcherTrack)
                                        texBox = texBoxPurple;
                                }

                                drawBox(cached_action_startFrame, cached_action_endFrame, _startFrame, _endFrame, rectTimelineActions, rectFramesBirdsEye.width, texBox);

                                lastStartInd = -1;
                                if(cached_action_endFrame >= _endFrame)
                                    break;
                            }
                        }
                    }
                }
            }
            #endregion
            string txtInfo;
            Rect rectBox;
            // draw box for each action in track
            bool didClampBackwards = false;	// whether or not clamped backwards, used to break infinite loop
            int last_action_startFrame = -1;
            for(int i = 0; i < _track.keys.Count; i++) {
                if(_track.keys[i] == null) continue;

                #region calculate dimensions
                int clamped = 0; // 0 = no clamp, -1 = backwards clamp, 1 = forwards clamp
                if(_track.keys[i].version != _track.version) {
                    // if cache is null, recheck for component and update caches
                    //aData = (AnimatorData)GameObject.Find("AnimatorData").GetComponent("AnimatorData");
                    curTake.maintainCaches(aData);
                }
                if((_track is AMAudioTrack || _track is AMAnimationTrack) && _track.keys[i].getNumberOfFrames(curTake.frameRate) > -1 && (_track.keys[i].getStartFrame() + _track.keys[i].getNumberOfFrames(curTake.frameRate) <= curTake.numFrames)) {
                    //based on content length
                    action_startFrame = _track.keys[i].getStartFrame();
                    action_endFrame = _track.keys[i].getStartFrame() + _track.keys[i].getNumberOfFrames(curTake.frameRate);
                    //audioClip = (_track.cache[i] as AMAudioAction).audioClip;
                    // if intersects new audio clip, then cut
                    if(i < _track.keys.Count - 1) {
                        if(action_endFrame > _track.keys[i + 1].getStartFrame()) action_endFrame = _track.keys[i + 1].getStartFrame();
                    }
                }
                else if((i == 0) && (!didClampBackwards) && (_track is AMPropertyTrack || _track is AMGOSetActiveTrack || _track is AMCameraSwitcherTrack)) {
                    // clamp behind if first action
                    action_startFrame = 1;
                    action_endFrame = _track.keys[0].getStartFrame();
                    i--;
                    didClampBackwards = true;
                    clamped = -1;
                }
                else if(_track is AMAnimationTrack || _track is AMAudioTrack || _track is AMPropertyTrack || _track is AMEventTrack || _track is AMGOSetActiveTrack || _track is AMCameraSwitcherTrack || _track is AMTriggerTrack) {
                    // single frame tracks (clamp box to last frame) (if audio track not set, clamp)
                    action_startFrame = _track.keys[i].getStartFrame();
                    if(i < _track.keys.Count - 1) {
                        action_endFrame = _track.keys[i + 1].getStartFrame();
                    }
                    else {
                        clamped = 1;
                        action_endFrame = _endFrame;
                        if(action_endFrame > curTake.numFrames) action_endFrame = curTake.numFrames + 1;
                    }
                }
                else {
                    // tracks with start frame and end frame (do not clamp box, stop before last key)
                    if(_track.keys[i].getNumberOfFrames(curTake.frameRate) <= 0) continue;
                    action_startFrame = _track.keys[i].getStartFrame();
                    action_endFrame = _track.keys[i].getStartFrame() + _track.keys[i].getNumberOfFrames(curTake.frameRate);
                }
                if(action_startFrame > _endFrame) {
                    last_action_startFrame = action_startFrame;
                    continue;
                }
                if(action_endFrame < _startFrame) {
                    last_action_startFrame = action_startFrame;
                    continue;
                }
                if(i >= 0) txtInfo = getInfoTextForAction(_track, _track.keys[i], false, clamped);
                else txtInfo = getInfoTextForAction(_track, _track.keys[0], true, clamped);
                float rectLeft, rectWidth; ;
                float rectTop = current_height_frame;
                float rectHeight = height_track - current_height_frame;
                // set info box position and dimensions
                bool showLeftAnchor = true;
                bool showRightAnchor = true;
                if(action_startFrame < _startFrame) {
                    rectLeft = 0f;
                    renderFrameStart = _startFrame;
                    showLeftAnchor = false;
                }
                else {
                    rectLeft = (action_startFrame - _startFrame) * current_width_frame;
                    renderFrameStart = action_startFrame;
                }
                if(action_endFrame > _endFrame) {
                    renderFrameEnd = _endFrame;
                    showRightAnchor = false;
                }
                else {
                    renderFrameEnd = action_endFrame;
                }
                rectWidth = (renderFrameEnd - renderFrameStart + 1) * current_width_frame;
                rectBox = new Rect(rectLeft, rectTop, rectWidth, rectHeight);
                #endregion
                #region draw action
                if(_track is AMAnimationTrack) texBox = texBoxRed;
                else if(_track is AMPropertyTrack) texBox = texBoxLightBlue;
                else if(_track is AMTranslationTrack) texBox = texBoxGreen;
                else if(_track is AMAudioTrack) texBox = texBoxPink;
                else if(_track is AMRotationTrack) texBox = texBoxYellow;
                else if(_track is AMOrientationTrack) texBox = texBoxOrange;
                else if(_track is AMEventTrack) texBox = texBoxDarkBlue;
                else if(_track is AMGOSetActiveTrack) texBox = texBoxDarkBlue;
                else if(_track is AMCameraSwitcherTrack) texBox = texBoxPurple;
                else if(_track is AMTriggerTrack) texBox = texBoxDarkBlue;
                else texBox = texBoxBorder;
                if(drawEachAction) {
                    GUI.DrawTextureWithTexCoords(rectBox, texBox, new Rect(0, 0, rectBox.width / 32f, rectBox.height / 64f));
                    //if(audioClip) GUI.DrawTexture(rectBox,AssetPreview.GetAssetPreview(audioClip));
                }
                // for audio draw waveform
                if(_track is AMAudioTrack) {
                    AMAudioKey _key = (AMAudioKey)((i >= 0) ? _track.keys[i] : _track.keys[0]);
                    if(_key.audioClip) {
                        float oneShotLength = _key.audioClip.length * curTake.frameRate;
                        Rect waveFormRect = new Rect(0, 0, 1, 1);
                        float endFrame = _key.getStartFrame() + _key.getNumberOfFrames(curTake.frameRate);
                        if(_key.loop) {
                            endFrame = curTake.endFrame;
                            waveFormRect.width = (endFrame - _key.getStartFrame()) / oneShotLength;
                        }
                        if(_track.keys.Count > i + 1) {
                            if(_track.keys[i+1].getStartFrame() < endFrame) {
                                endFrame = _track.keys[i+1].getStartFrame();
                                waveFormRect.width = (_track.keys[i+1].getStartFrame() - _key.getStartFrame())/oneShotLength;
                            }
                        }
                        if(_key.getStartFrame() < curTake.startFrame) {
                            waveFormRect.x = (curTake.startFrame - _key.getStartFrame()) / oneShotLength;
                            waveFormRect.width -= waveFormRect.x;
                        }
                        if(curTake.endFrame < endFrame) {
                            waveFormRect.width += (curTake.endFrame - endFrame) / oneShotLength;
                        }
                        Texture2D waveform = AssetPreview.GetAssetPreview(_key.audioClip);
                        if(waveform) {
                            if(!EditorGUIUtility.isProSkin) GUI.color = Color.black;
                            GUI.DrawTextureWithTexCoords(rectBox, waveform, waveFormRect);
                            GUI.color = Color.white;
                        }
                    }
                }
                // info tex label
                bool hideTxtInfo = (GUI.skin.label.CalcSize(new GUIContent(txtInfo)).x > rectBox.width);
                GUIStyle styleTxtInfo = new GUIStyle(GUI.skin.label);
                styleTxtInfo.normal.textColor = Color.white;
                styleTxtInfo.alignment = (hideTxtInfo ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                bool isLastAction;
                if(_track is AMPropertyTrack || _track is AMEventTrack || _track is AMGOSetActiveTrack || _track is AMCameraSwitcherTrack || _track is AMTriggerTrack) isLastAction = (i == _track.keys.Count - 1);
                else if(_track is AMAudioTrack || _track is AMAnimationTrack) isLastAction = false;
                else isLastAction = (i == _track.keys.Count - 2);
                if(rectBox.width > 5f) EditorGUI.DropShadowLabel(new Rect(rectBox.x, rectBox.y, rectBox.width - (!isLastAction ? current_width_frame : 0f), rectBox.height), txtInfo, styleTxtInfo);
                // if clicked on info box, select the starting frame for action. show tooltip if text does not fit
                if(drawEachAction && GUI.Button(rectBox, /*(hideTxtInfo ? new GUIContent("",txtInfo) : new GUIContent(""))*/"", "label") && dragType != (int)DragType.ResizeAction) {
                    int prevFrame = curTake.selectedFrame;
                    // timeline select
                    timelineSelectFrame(t, (clamped == -1 ? action_endFrame : action_startFrame));
                    // clear and add frame to context selection
                    contextSelectFrame((clamped == -1 ? action_endFrame : action_startFrame), prevFrame);
                }
                if(rectBox.Contains(e.mousePosition) && mouseOverElement == (int)ElementType.None) {
                    mouseOverElement = (int)ElementType.TimelineAction;
                    mouseOverTrack = t;
                    if(hideTxtInfo) tooltip = txtInfo;
                }
                #endregion
                #region draw anchors
                if(showLeftAnchor) {
                    Rect rectBoxAnchorLeft = new Rect(rectBox.x - 1f, rectBox.y, 2f, rectBox.height);
                    GUI.DrawTexture(rectBoxAnchorLeft, texBoxBorder);
                    Rect rectBoxAnchorLeftOffset = new Rect(rectBoxAnchorLeft);
                    rectBoxAnchorLeftOffset.width += 6f;
                    rectBoxAnchorLeftOffset.x -= 3f;
                    // info box anchor cursor 
                    if(i >= 0) {
                        EditorGUIUtility.AddCursorRect(new Rect(rectBoxAnchorLeftOffset.x + 1f, rectBoxAnchorLeftOffset.y, rectBoxAnchorLeftOffset.width - 2f, rectBoxAnchorLeftOffset.height), MouseCursor.ResizeHorizontal);
                        if(rectBoxAnchorLeftOffset.Contains(e.mousePosition) && (mouseOverElement == (int)ElementType.None || mouseOverElement == (int)ElementType.TimelineAction)) {
                            mouseOverElement = (int)ElementType.ResizeAction;
                            if(dragType == (int)DragType.None) {
                                if(_track.hasKeyOnFrame(last_action_startFrame)) startResizeActionFrame = last_action_startFrame;
                                else startResizeActionFrame = -1;
                                resizeActionFrame = action_startFrame;
                                if(_track is AMAnimationTrack || _track is AMAudioTrack) {
                                    endResizeActionFrame = _track.getKeyFrameAfterFrame(action_startFrame, false);
                                }
                                else endResizeActionFrame = action_endFrame;
                                mouseOverTrack = t;
                                arrKeyRatiosLeft = _track.getKeyFrameRatiosInBetween(startResizeActionFrame, resizeActionFrame);
                                arrKeyRatiosRight = _track.getKeyFrameRatiosInBetween(resizeActionFrame, endResizeActionFrame);
                                arrKeysLeft = _track.getKeyFramesInBetween(startResizeActionFrame, resizeActionFrame);
                                arrKeysRight = _track.getKeyFramesInBetween(resizeActionFrame, endResizeActionFrame);
                            }
                        }
                    }
                }
                // draw right anchor if last timeline action
                if(showRightAnchor && isLastAction) {
                    Rect rectBoxAnchorRight = new Rect(rectBox.x + rectBox.width - 1f, rectBox.y, 2f, rectBox.height);
                    GUI.DrawTexture(rectBoxAnchorRight, texBoxBorder);
                    Rect rectBoxAnchorRightOffset = new Rect(rectBoxAnchorRight);
                    rectBoxAnchorRightOffset.width += 6f;
                    rectBoxAnchorRightOffset.x -= 3f;
                    EditorGUIUtility.AddCursorRect(new Rect(rectBoxAnchorRightOffset.x + 1f, rectBoxAnchorRightOffset.y, rectBoxAnchorRightOffset.width - 2f, rectBoxAnchorRightOffset.height), MouseCursor.ResizeHorizontal);
                    if(rectBoxAnchorRightOffset.Contains(e.mousePosition) && (mouseOverElement == (int)ElementType.None || mouseOverElement == (int)ElementType.TimelineAction)) {
                        mouseOverElement = (int)ElementType.ResizeAction;
                        if(dragType == (int)DragType.None) {
                            startResizeActionFrame = action_startFrame;
                            resizeActionFrame = action_endFrame;
                            endResizeActionFrame = -1;
                            mouseOverTrack = t;
                            arrKeyRatiosLeft = _track.getKeyFrameRatiosInBetween(startResizeActionFrame, resizeActionFrame);
                            arrKeyRatiosRight = _track.getKeyFrameRatiosInBetween(resizeActionFrame, endResizeActionFrame);
                            arrKeysLeft = _track.getKeyFramesInBetween(startResizeActionFrame, resizeActionFrame);
                            arrKeysRight = _track.getKeyFramesInBetween(resizeActionFrame, endResizeActionFrame);
                        }
                    }
                }
                #endregion
                last_action_startFrame = action_startFrame;
            }
            if(!drawEachAction) {
                // timeline action button
                if(GUI.Button(rectTimelineActions,/*new GUIContent("",tooltip)*/"", "label") && dragType == (int)DragType.None) {
                    int _frame_num_action = (int)curTake.startFrame + Mathf.CeilToInt(e.mousePosition.x / current_width_frame) - 1;
                    AMKey _action = _track.getKeyContainingFrame(_frame_num_action);
                    int prevFrame = curTake.selectedFrame;
                    // timeline select
                    timelineSelectFrame(t, _action.getStartFrame());
                    // clear and add frame to context selection
                    contextSelectFrame(_action.getStartFrame(), prevFrame);
                }
            }


            #endregion
        }
        GUI.EndGroup();
    }
    void showInspectorPropertiesFor(Rect rect, int _track, int _frame, Event e) {
        //if meta is prefab, then ask user to instantiate it for editing
        if(aData.e_meta && PrefabUtility.GetPrefabType(aData.e_meta) == PrefabType.Prefab) {
            GUIStyle _styleLabelWordwrap = new GUIStyle(GUI.skin.label);
            _styleLabelWordwrap.wordWrap = true;
            GUI.Label(new Rect(0f, 0f, width_inspector_open - width_inspector_closed - width_button_delete - margin, 100f),
                "In order to make changes, the AnimatorMeta prefab has to be instantiated.  Make sure to press the 'Save' button in the AnimatorData inspector to save changes to the AnimatorMeta prefab.", _styleLabelWordwrap);

            if(GUI.Button(new Rect(0f, 110f, width_inspector_open - width_inspector_closed - width_button_delete - margin, 20f), "Edit")) {
                MetaInstantiate("Animator Meta Edit");
            }
            return;
        }

        AMTakeData ctake = currentTake;

        // if there are no tracks, return
        if(ctake.getTrackCount() <= 0) return;
        string track_name = "";
        AMTrack sTrack = null;
        if(_track > -1) {
            int trackInd = ctake.getTrackIndex(_track);
            // get the selected track
            if(trackInd != -1 && trackInd < ctake.trackValues.Count)
                sTrack = ctake.trackValues[trackInd];
        }
        if(!sTrack) return;
        track_name = sTrack.name + ", ";
        GUIStyle styleLabelWordwrap = new GUIStyle(GUI.skin.label);
        styleLabelWordwrap.wordWrap = true;
        string strFrameInfo = track_name;
        if(oData.time_numbering) strFrameInfo += "Time " + frameToTime(_frame, (float)ctake.frameRate).ToString("N2") + " s";
        else strFrameInfo += "Frame " + _frame;
        GUI.Label(new Rect(0f, 0f, width_inspector_open - width_inspector_closed - width_button_delete - margin, 20f), strFrameInfo, styleLabelWordwrap);
        Rect rectBtnDeleteKey = new Rect(width_inspector_open - width_inspector_closed - width_button_delete - margin, 0f, width_button_delete, height_button_delete);
        // if frame has no key or isPlaying, return
        if(_track <= -1 || !(sTrack.hasKeyOnFrame(_frame) || sTrack.hasTrackSettings) || isPlaying) {
            GUI.enabled = false;
            // disabled delete key button
            GUI.Button(rectBtnDeleteKey, (getSkinTextureStyleState("delete").background), GUI.skin.GetStyle("ButtonImage"));
            GUI.enabled = !isPlaying;
            return;
        }
        // delete key button
        if(GUI.Button(rectBtnDeleteKey, new GUIContent("", "Delete Key"), GUI.skin.GetStyle("ButtonImage"))) {
            deleteKeyFromSelectedFrame();
            return;
        }
        GUI.DrawTexture(new Rect(rectBtnDeleteKey.x + (rectBtnDeleteKey.height - 10f) / 2f, rectBtnDeleteKey.y + (rectBtnDeleteKey.width - 10f) / 2f, 10f, 10f), (getSkinTextureStyleState((rectBtnDeleteKey.Contains(e.mousePosition) ? "delete_hover" : "delete")).background));
        float width_inspector = width_inspector_open - width_inspector_closed;
        float start_y = 30f + height_inspector_space;
        #region translation inspector
        if(sTrack is AMTranslationTrack) {
            AMTranslationTrack tTrack = (AMTranslationTrack)sTrack;
            Rect rectPosition = new Rect(0f, start_y, 50f, 20f);
            if(sTrack.hasKeyOnFrame(_frame)) {
                AMTranslationKey tKey = (AMTranslationKey)sTrack.getKeyOnFrame(_frame);
                // translation interpolation
                Rect rectLabelInterp = rectPosition;
                GUI.Label(rectLabelInterp, "Interpl.");
                Rect rectSelGrid = new Rect(rectLabelInterp.x + rectLabelInterp.width + margin, rectLabelInterp.y, width_inspector - rectLabelInterp.width - margin * 2f, rectLabelInterp.height);
                int nInterp = GUI.SelectionGrid(rectSelGrid, tKey.interp, texInterpl, 2, GUI.skin.GetStyle("ButtonImage"));
                if(tKey.interp != nInterp) {
                    recordUndoTrackAndKeys(sTrack, false, "Change Interpolation");
                    tKey.interp = nInterp;
                    sTrack.updateCache(aData);
                    AMCodeView.refresh();
                    // select the current frame
                    timelineSelectFrame(_track, _frame);
                    // save data
                    EditorUtility.SetDirty(sTrack);
                    setDirtyKeys(sTrack);
                }
                // translation position
                rectPosition = new Rect(0f, rectSelGrid.y + rectSelGrid.height + height_inspector_space, width_inspector - margin, 40f);
                Vector3 nPos = EditorGUI.Vector3Field(rectPosition, "Position", tKey.position);
                if(tKey.position != nPos) {
                    recordUndoTrackAndKeys(sTrack, false, "Change Position");
                    tKey.position = nPos;
                    // update cache when modifying varaibles
                    sTrack.updateCache(aData);
                    AMCodeView.refresh();
                    // preview new position
                    ctake.previewFrame(aData, ctake.selectedFrame);
                    // save data
                    EditorUtility.SetDirty(sTrack);
                    setDirtyKeys(sTrack);
                }

                // if not only key, show ease
                bool isTKeyLastFrame = tKey == tTrack.keys[(sTrack as AMTranslationTrack).keys.Count - 1];

                if(!isTKeyLastFrame) {
                    rectPosition = new Rect(0f, rectPosition.y + rectPosition.height + height_inspector_space, width_inspector - margin, 0f);
                    if(!isTKeyLastFrame && tKey.interp == (int)AMTranslationKey.Interpolation.Linear) {
                        showEasePicker(sTrack, tKey, aData, rectPosition.x, rectPosition.y, rectPosition.width);
                    }
                    else {
                        showEasePicker(sTrack, tTrack.getKeyStartFor(tKey.frame), aData, rectPosition.x, rectPosition.y, rectPosition.width);
                    }

                    rectPosition.height = 80.0f;
                }
            }

            //display pixel snap option
            rectPosition = new Rect(0f, rectPosition.y + rectPosition.height + height_inspector_space, width_inspector - margin, 20.0f);
            bool nPixelSnap = EditorGUI.Toggle(rectPosition, "Pixel-Snap", tTrack.pixelSnap);
            if(nPixelSnap != tTrack.pixelSnap) {
                Undo.RecordObject(tTrack, "Set Pixel-Snap");
                tTrack.pixelSnap = nPixelSnap;
                EditorUtility.SetDirty(tTrack);
            }
            //display pixel-per-unit
            if(tTrack.pixelSnap) {
                rectPosition = new Rect(0f, rectPosition.y + rectPosition.height + height_inspector_space, width_inspector - margin, 20.0f);
                float nppu = EditorGUI.FloatField(rectPosition, "Pixel/Unit", tTrack.pixelPerUnit);

                rectPosition = new Rect(5f, rectPosition.y + rectPosition.height + height_inspector_space, width_inspector - margin - 10f, 20.0f);
                if(GUI.Button(rectPosition, "Pixel/Unit Default"))
                    nppu = oData.pixelPerUnitDefault;

                if(nppu <= 0.0f) nppu = 0.001f;
                if(tTrack.pixelPerUnit != nppu) {
                    Undo.RecordObject(tTrack, "Set Pixel-Per-Unit");
                    tTrack.pixelPerUnit = nppu;
                    EditorUtility.SetDirty(tTrack);
                }
            }
            return;
        }
        #endregion
        #region rotation inspector
        if(sTrack is AMRotationTrack) {
            AMRotationKey rKey = (AMRotationKey)sTrack.getKeyOnFrame(_frame);
            Rect rectQuaternion = new Rect(0f, start_y, width_inspector - margin, 40f);
            // quaternion
            Vector3 rot = rKey.rotation.eulerAngles;
            Vector3 nrot = EditorGUI.Vector3Field(rectQuaternion, "Euler", rot);
            if(rot != nrot) {
                recordUndoTrackAndKeys(sTrack, false, "Change Rotation");
                rKey.rotation = Quaternion.Euler(nrot);
                // update cache when modifying varaibles
                sTrack.updateCache(aData);
                AMCodeView.refresh();
                // preview new position
                ctake.previewFrame(aData, ctake.selectedFrame);
                // save data
                EditorUtility.SetDirty(sTrack);
                setDirtyKeys(sTrack);
            }
            // if not last key, show ease
            if(rKey != (sTrack as AMRotationTrack).keys[(sTrack as AMRotationTrack).keys.Count - 1]) {
                Rect recEasePicker = new Rect(0f, rectQuaternion.y + rectQuaternion.height + height_inspector_space, width_inspector - margin, 0f);
                if((sTrack as AMRotationTrack).getKeyIndexForFrame(_frame) > -1) {
                    showEasePicker(sTrack, rKey, aData, recEasePicker.x, recEasePicker.y, recEasePicker.width);
                }
            }
            return;
        }
        #endregion
        #region orientation inspector
        if(sTrack is AMOrientationTrack) {
            AMOrientationKey oKey = (AMOrientationKey)(sTrack as AMOrientationTrack).getKeyOnFrame(_frame);
            // target
            Rect rectLabelTarget = new Rect(0f, start_y, 50f, 22f);
            GUI.Label(rectLabelTarget, "Target");
            Rect rectObjectTarget = new Rect(rectLabelTarget.x + rectLabelTarget.width + 3f, rectLabelTarget.y + 3f, width_inspector - rectLabelTarget.width - 3f - margin - width_button_delete, 16f);
            Transform ntgt = (Transform)EditorGUI.ObjectField(rectObjectTarget, oKey.GetTarget(aData), typeof(Transform), true);
            if(oKey.GetTarget(aData) != ntgt) {
                recordUndoTrackAndKeys(sTrack, false, "Change Target");
                oKey.SetTarget(aData, ntgt);
                // update cache when modifying varaibles
                (sTrack as AMOrientationTrack).updateCache(aData);
                AMCodeView.refresh();
                // preview
                ctake.previewFrame(aData, ctake.selectedFrame);
                // save data
                EditorUtility.SetDirty(sTrack);
                setDirtyKeys(sTrack);
            }
            Rect rectNewTarget = new Rect(width_inspector - width_button_delete - margin, rectLabelTarget.y, width_button_delete, width_button_delete);
            if(GUI.Button(rectNewTarget, "+")) {
                GenericMenu addTargetMenu = new GenericMenu();
                addTargetMenu.AddItem(new GUIContent("With Translation"), false, addTargetWithTranslationTrack, oKey);
                addTargetMenu.AddItem(new GUIContent("Without Translation"), false, addTargetWithoutTranslationTrack, oKey);
                addTargetMenu.ShowAsContext();
            }
            // if not last key, show ease
            if(oKey != (sTrack as AMOrientationTrack).keys[(sTrack as AMOrientationTrack).keys.Count - 1]) {
                int oActionIndex = (sTrack as AMOrientationTrack).getKeyIndexForFrame(_frame);
                if(oActionIndex > -1 && (sTrack.keys[oActionIndex] as AMOrientationKey).GetTarget(aData) != (sTrack.keys[oActionIndex] as AMOrientationKey).GetTargetEnd(aData)) {
                    Rect recEasePicker = new Rect(0f, rectNewTarget.y + rectNewTarget.height + height_inspector_space, width_inspector - margin, 0f);
                    showEasePicker(sTrack, oKey, aData, recEasePicker.x, recEasePicker.y, recEasePicker.width);
                }
            }
            return;
        }
        #endregion
        #region animation inspector
        if(sTrack is AMAnimationTrack) {
            AMAnimationKey aKey = (AMAnimationKey)(sTrack as AMAnimationTrack).getKeyOnFrame(_frame);
            // animation clip
            Rect rectLabelAnimClip = new Rect(0f, start_y, 100f, 22f);
            GUI.Label(rectLabelAnimClip, "Animation Clip");
            Rect rectObjectField = new Rect(rectLabelAnimClip.x + rectLabelAnimClip.width + 2f, rectLabelAnimClip.y + 3f, width_inspector - rectLabelAnimClip.width - margin, 16f);
            AnimationClip nclip = (AnimationClip)EditorGUI.ObjectField(rectObjectField, aKey.amClip, typeof(AnimationClip), false);
            if(aKey.amClip != nclip) {
                Undo.RecordObject(aKey, "Change Animation Clip");
                aKey.amClip = nclip;
                AMCodeView.refresh();
                // preview new position
                ctake.previewFrame(aData, ctake.selectedFrame);
                // save data
                EditorUtility.SetDirty(sTrack);
                setDirtyKeys(sTrack);
            }
            // wrap mode
            Rect rectLabelWrapMode = new Rect(0f, rectLabelAnimClip.y + rectLabelAnimClip.height + height_inspector_space, 85f, 22f);
            GUI.Label(rectLabelWrapMode, "Wrap Mode");
            Rect rectPopupWrapMode = new Rect(rectLabelWrapMode.x + rectLabelWrapMode.width, rectLabelWrapMode.y + 3f, 120f, 22f);
            WrapMode nwrapmode = indexToWrapMode(EditorGUI.Popup(rectPopupWrapMode, wrapModeToIndex(aKey.wrapMode), wrapModeNames));
            if(aKey.wrapMode != nwrapmode) {
                Undo.RecordObject(aKey, "Wrap Mode");
                aKey.wrapMode = nwrapmode;
                AMCodeView.refresh();
                // preview new position
                ctake.previewFrame(aData, ctake.selectedFrame);
                // save data
                EditorUtility.SetDirty(aKey);
            }
            // crossfade
            Rect rectLabelCrossfade = new Rect(0f, rectLabelWrapMode.y + rectPopupWrapMode.height + height_inspector_space, 85f, 22f);
            GUI.Label(rectLabelCrossfade, "Crossfade");
            Rect rectToggleCrossfade = new Rect(rectLabelCrossfade.x + rectLabelCrossfade.width, rectLabelCrossfade.y + 2f, 20f, rectLabelCrossfade.height);
            bool ncrossfade = EditorGUI.Toggle(rectToggleCrossfade, aKey.crossfade);
            if(aKey.crossfade != ncrossfade) {
                Undo.RecordObject(aKey, "Cross Fade");
                aKey.crossfade = ncrossfade;
                AMCodeView.refresh();
                // preview new position
                ctake.previewFrame(aData, ctake.selectedFrame);
                // save data
                EditorUtility.SetDirty(aKey);
            }
            Rect rectLabelCrossFadeTime = new Rect(rectToggleCrossfade.x + rectToggleCrossfade.width + 10f, rectLabelCrossfade.y, 35f, rectToggleCrossfade.height);
            if(!aKey.crossfade) GUI.enabled = false;
            GUI.Label(rectLabelCrossFadeTime, "Time");
            Rect rectFloatFieldCrossFade = new Rect(rectLabelCrossFadeTime.x + rectLabelCrossFadeTime.width + margin, rectLabelCrossFadeTime.y + 3f, 40f, rectLabelCrossFadeTime.height);
            float ncrossfadet = EditorGUI.FloatField(rectFloatFieldCrossFade, aKey.crossfadeTime);
            if(aKey.crossfadeTime != ncrossfadet) {
                Undo.RecordObject(aKey, "Cross Fade Time");
                aKey.crossfadeTime = ncrossfadet;
                AMCodeView.refresh();
                // save data
                EditorUtility.SetDirty(aKey);
            }
            Rect rectLabelSeconds = new Rect(rectFloatFieldCrossFade.x + rectFloatFieldCrossFade.width + margin, rectLabelCrossFadeTime.y, 20f, rectLabelCrossFadeTime.height);
            GUI.Label(rectLabelSeconds, "s");
            GUI.enabled = true;
            return;
        }
        #endregion
        #region audio inspector
        if(sTrack is AMAudioTrack) {
            AMAudioKey auKey = (AMAudioKey)(sTrack as AMAudioTrack).getKeyOnFrame(_frame);
            // audio clip
            Rect rectLabelAudioClip = new Rect(0f, start_y, 80f, 22f);
            GUI.Label(rectLabelAudioClip, "Audio Clip");
            Rect rectObjectField = new Rect(rectLabelAudioClip.x + rectLabelAudioClip.width + margin, rectLabelAudioClip.y + 3f, width_inspector - rectLabelAudioClip.width - margin, 16f);
            AudioClip nclip = (AudioClip)EditorGUI.ObjectField(rectObjectField, auKey.audioClip, typeof(AudioClip), false);
            if(auKey.audioClip != nclip) {
                Undo.RecordObject(auKey, "Set Audio Clip");
                auKey.audioClip = nclip;
                AMCodeView.refresh();
                // save data
                EditorUtility.SetDirty(auKey);

            }
            Rect rectLabelLoop = new Rect(0f, rectLabelAudioClip.y + rectLabelAudioClip.height + height_inspector_space, 80f, 22f);
            // loop audio
            GUI.Label(rectLabelLoop, "Loop");
            Rect rectToggleLoop = new Rect(rectLabelLoop.x + rectLabelLoop.width + margin, rectLabelLoop.y + 2f, 22f, 22f);
            bool nloop = EditorGUI.Toggle(rectToggleLoop, auKey.loop);
            if(auKey.loop != nloop) {
                Undo.RecordObject(auKey, "Set Audio Loop");
                auKey.loop = nloop;
                AMCodeView.refresh();
                // save data
                EditorUtility.SetDirty(auKey);
            }

            // One Shot?
            Rect rectLabelOneShot = new Rect(0f, rectLabelLoop.y + rectLabelLoop.height, 80f, 22f);
            GUI.Label(rectLabelOneShot, "One Shot");
            Rect rectToggleOneShot = new Rect(rectLabelOneShot.x + rectLabelOneShot.width + margin, rectLabelOneShot.y + 2f, 22f, 22f);
            bool nOneShot = EditorGUI.Toggle(rectToggleOneShot, auKey.oneShot);
            if(auKey.oneShot != nOneShot) {
                Undo.RecordObject(auKey, "Set Audio One Shot");
                auKey.oneShot = nOneShot;
                AMCodeView.refresh();
                // save data
                EditorUtility.SetDirty(auKey);
            }
            return;
        }
        #endregion
        #region property inspector
        if(sTrack is AMPropertyTrack) {
            AMPropertyTrack pTrack = sTrack as AMPropertyTrack;
            AMPropertyKey pKey = (AMPropertyKey)pTrack.getKeyOnFrame(_frame);
            // value
            string propertyLabel = pTrack.getTrackType();
            Rect rectField = new Rect(0f, start_y, width_inspector - margin, 22f);
            bool isUpdated = false;
            if((pTrack.valueType == (int)AMPropertyTrack.ValueType.Integer) || (pTrack.valueType == (int)AMPropertyTrack.ValueType.Long)) {
                int val = Convert.ToInt32(pKey.val);
                int nval = EditorGUI.IntField(rectField, propertyLabel, val);
                if(val != nval) {
                    recordUndoTrackAndKeys(sTrack, false, "Change Property Value");
                    pKey.setValue(nval);
                    isUpdated = true;
                }
            }
            else if((pTrack.valueType == (int)AMPropertyTrack.ValueType.Float) || (pTrack.valueType == (int)AMPropertyTrack.ValueType.Double)) {
                float val = (float)pKey.val;
                float nval = EditorGUI.FloatField(rectField, propertyLabel, val);
                if(val != nval) {
                    recordUndoTrackAndKeys(sTrack, false, "Change Property Value");
                    pKey.setValue(nval);
                    isUpdated = true;
                }
            }
            else if(pTrack.valueType == (int)AMPropertyTrack.ValueType.Bool) {
                bool val = pKey.val > 0.0;
                bool nval = EditorGUI.Toggle(rectField, propertyLabel, val);
                if(val != nval) {
                    recordUndoTrackAndKeys(sTrack, false, "Change Property Value");
                    pKey.setValue(nval);
                    isUpdated = true;
                }
            }
            else if(pTrack.valueType == (int)AMPropertyTrack.ValueType.String) {
                string val = pKey.valString;
                string nval = EditorGUI.TextField(rectField, propertyLabel, val);
                if(val != nval) {
                    recordUndoTrackAndKeys(sTrack, false, "Change Property Value");
                    pKey.setValue(nval);
                    isUpdated = true;
                }
            }
            else if(pTrack.valueType == (int)AMPropertyTrack.ValueType.Vector2) {
                rectField.height = 40f;
                Vector2 nval = EditorGUI.Vector2Field(rectField, propertyLabel, pKey.vect2);
                if(pKey.vect2 != nval) {
                    recordUndoTrackAndKeys(sTrack, false, "Change Property Value");
                    pKey.setValue(nval);
                    isUpdated = true;
                }
            }
            else if(pTrack.valueType == (int)AMPropertyTrack.ValueType.Vector3) {
                rectField.height = 40f;
                Vector3 nval = EditorGUI.Vector3Field(rectField, propertyLabel, pKey.vect3);
                if(pKey.vect3 != nval) {
                    recordUndoTrackAndKeys(sTrack, false, "Change Property Value");
                    pKey.setValue(nval);
                    isUpdated = true;
                }
            }
            else if(pTrack.valueType == (int)AMPropertyTrack.ValueType.Color) {
                rectField.height = 22f;
                Color nclr = EditorGUI.ColorField(rectField, propertyLabel, pKey.color);
                if(pKey.color != nclr) {
                    recordUndoTrackAndKeys(sTrack, false, "Change Property Value");
                    pKey.setValue(nclr);
                    isUpdated = true;
                }
            }
            else if(pTrack.valueType == (int)AMPropertyTrack.ValueType.Rect) {
                rectField.height = 60f;
                Rect nrect = EditorGUI.RectField(rectField, propertyLabel, pKey.rect);
                if(pKey.rect != nrect) {
                    recordUndoTrackAndKeys(sTrack, false, "Change Property Value");
                    pKey.setValue(nrect);
                    isUpdated = true;
                }
            }
            else if(pTrack.valueType == (int)AMPropertyTrack.ValueType.Vector4
                || pTrack.valueType == (int)AMPropertyTrack.ValueType.Quaternion) {
                rectField.height = 40f;
                Vector4 nvec = EditorGUI.Vector4Field(rectField, propertyLabel, pKey.vect4);
                if(pKey.vect4 != nvec) {
                    recordUndoTrackAndKeys(sTrack, false, "Change Property Value");
                    pKey.setValue(nvec);
                    isUpdated = true;
                }
            }
            else if(pTrack.valueType == (int)AMPropertyTrack.ValueType.Sprite) {
                UnityEngine.Object val = pKey.valObj;
                GUI.skin = null; EditorGUIUtility.LookLikeControls();
                rectField.height = 16.0f;
                UnityEngine.Object nval = EditorGUI.ObjectField(rectField, val, typeof(Sprite), false);
                GUI.skin = skin; EditorGUIUtility.LookLikeControls();
                if(val != nval) {
                    recordUndoTrackAndKeys(sTrack, false, "Change Property Value");
                    pKey.setValue(nval);
                    isUpdated = true;
                }
            }
            else if(pTrack.valueType == (int)AMPropertyTrack.ValueType.Enum) {
                rectField.height = 20.0f;
                GUI.Label(rectField, propertyLabel);
                rectField.y += rectField.height + 4.0f;
                rectField.height = 16f;
                Enum curEnum = Enum.ToObject(pTrack.GetCachedInfoType(aData), (int)pKey.val) as Enum;
                Enum newEnum = EditorGUI.EnumPopup(rectField, curEnum);
                if(curEnum != newEnum) {
                    recordUndoTrackAndKeys(sTrack, false, "Change Property Value");
                    pKey.setValue(Convert.ToDouble(newEnum));
                    isUpdated = true;
                }
            }
            if(isUpdated) {
                // update cache when modifying varaibles
                sTrack.updateCache(aData);
                AMCodeView.refresh();
                // preview new value
                ctake.previewFrame(aData, ctake.selectedFrame);
                // save data
                EditorUtility.SetDirty(sTrack);
                setDirtyKeys(sTrack);
            }
            // property ease, show if not last key (check for action; there is no rotation action for last key). do not show for morph channels, because it is shown before the parameters
            // don't show on non-tweenable
            if(pTrack.canTween && pKey != pTrack.keys[pTrack.keys.Count - 1]) {
                Rect rectEasePicker = new Rect(0f, rectField.y + rectField.height + height_inspector_space, width_inspector - margin, 0f);
                showEasePicker(sTrack, pKey, aData, rectEasePicker.x, rectEasePicker.y, rectEasePicker.width);
            }
            return;
        }
        #endregion
        #region event set go active
        if(sTrack is AMGOSetActiveTrack) {
            AMGOSetActiveTrack goActiveTrack = sTrack as AMGOSetActiveTrack;
            AMGOSetActiveKey pKey = (AMGOSetActiveKey)goActiveTrack.getKeyOnFrame(_frame);

            bool doRefreshGizmo = false;

            bool newStartVal;

            // value
            if(sTrack.keys[0] == pKey) {
                Rect rectStartField = new Rect(0f, start_y, width_inspector - margin, 22f);

                newStartVal = EditorGUI.Toggle(rectStartField, "Default Active", goActiveTrack.startActive);

                start_y += rectStartField.height + 4.0f;
            }
            else
                newStartVal = goActiveTrack.startActive;

            Rect rectField = new Rect(0f, start_y, width_inspector - margin, 22f);

            bool newVal = EditorGUI.Toggle(rectField, sTrack.getTrackType(), pKey.setActive);

            if(newStartVal != goActiveTrack.startActive) {
                Undo.RecordObject(goActiveTrack, "Set GameObject Start Active");
                goActiveTrack.startActive = newStartVal;
                EditorUtility.SetDirty(goActiveTrack);

                doRefreshGizmo = true;
            }

            if(newVal != pKey.setActive) {
                Undo.RecordObject(pKey, "Set GameObject Active");
                pKey.setActive = newVal;

                // update cache when modifying varaibles
                sTrack.updateCache(aData);
                // preview new value
                ctake.previewFrame(aData, ctake.selectedFrame);
                // save data
                EditorUtility.SetDirty(pKey);

                doRefreshGizmo = true;
            }

            if(doRefreshGizmo) {
                AMCodeView.refresh();
            }

            return;
        }
        #endregion
        #region event inspector
        if(sTrack is AMEventTrack) {
            GameObject tgtGo = sTrack.GetTarget(aData) as GameObject;
            AMEventKey eKey = (AMEventKey)(sTrack as AMEventTrack).getKeyOnFrame(_frame);
            // value
            if(indexMethodInfo == -1 || cachedMethodInfo.Count <= 0) {
                Rect rectLabel = new Rect(0f, start_y, width_inspector - margin * 2f - 20f, 22f);
                GUI.Label(rectLabel, "No usable methods found.");
                Rect rectButton = new Rect(width_inspector - 20f - margin, start_y + 1f, 20f, 20f);
                if(GUI.Button(rectButton, "?")) {
                    EditorUtility.DisplayDialog("Usable Methods", "Methods should be made public and be placed in scripts that are not directly derived from Component or Behaviour to be used in the Event Track (MonoBehaviour is fine).", "Okay");
                }
                return;
            }
                        
            bool sendMessageToggleEnabled = true;
            Rect rectPopup = new Rect(0f, start_y, width_inspector - margin, 22f);
            int curIndexMethod = indexMethodInfo;
            indexMethodInfo = EditorGUI.Popup(rectPopup, curIndexMethod, getMethodNames());

            bool showObjectMessage = false;
            Type showObjectType = null;
            foreach(ParameterInfo p in cachedParameterInfos) {
                Type elemType = p.ParameterType.GetElementType();
                if(elemType != null && (elemType.BaseType == typeof(UnityEngine.Object) || elemType.BaseType == typeof(UnityEngine.Behaviour))) {
                    showObjectMessage = true;
                    showObjectType = elemType;
                    break;
                }
            }
            Rect rectLabelObjectMessage = new Rect(0f, rectPopup.y + rectPopup.height, width_inspector - margin * 2f - 20f, 0f);
            if(showObjectMessage) {
                rectLabelObjectMessage.height = 22f;
                Rect rectButton = new Rect(width_inspector - 20f - margin, rectLabelObjectMessage.y + 1f, 20f, 20f);
                GUI.color = Color.red;
                GUI.Label(rectLabelObjectMessage, "* Use Object[] instead!");
                GUI.color = Color.white;
                if(GUI.Button(rectButton, "?")) {
                    EditorUtility.DisplayDialog("Use Object[] Parameter Instead", "Array types derived from Object, such as GameObject[], cannot be cast correctly on runtime.\n\nUse UnityEngine.Object[] as a parameter type and then cast to (GameObject[]) in your method.\n\nIf you're trying to pass components" + (showObjectType != typeof(GameObject) ? " (such as " + showObjectType.ToString() + ")" : "") + ", you should get them from the casted GameObjects on runtime.\n\nPlease see the documentation for more information.", "Okay");
                }
                return;
            }

            // if index out of range
            bool paramMatched = eKey.isMatch(cachedParameterInfos);
            if((indexMethodInfo != curIndexMethod && indexMethodInfo < cachedMethodInfo.Count) || !paramMatched) {
                // process change
                // update cache when modifying varaibles
                if(eKey.setMethodInfo(tgtGo, cachedMethodInfoComponents[indexMethodInfo], !aData.TargetIsMeta(), cachedMethodInfo[indexMethodInfo], cachedParameterInfos, !paramMatched)) {
                    AMCodeView.refresh();
                    // save data
                    EditorUtility.SetDirty(eKey);
                    // deselect fields
                    GUIUtility.keyboardControl = 0;
                }
            }
            if(cachedParameterInfos.Length > 1) {
                // if method has more than 1 parameter, set sendmessage to false, and disable toggle
                if(eKey.setUseSendMessage(false)) {
                    AMCodeView.refresh();
                    // save data
                    EditorUtility.SetDirty(eKey);
                }
                sendMessageToggleEnabled = false;	// disable sendmessage toggle
            }
            
            GUI.enabled = sendMessageToggleEnabled;
            Rect rectLabelSendMessage = new Rect(0f, rectLabelObjectMessage.y + rectLabelObjectMessage.height + height_inspector_space, 150f, 20f);
            GUI.Label(rectLabelSendMessage, "Use SendMessage");
            Rect rectToggleSendMessage = new Rect(rectLabelSendMessage.x + rectLabelSendMessage.width + margin, rectLabelSendMessage.y, 20f, 20f);
            if(eKey.setUseSendMessage(GUI.Toggle(rectToggleSendMessage, eKey.useSendMessage, ""))) {
                sTrack.updateCache(aData);
                AMCodeView.refresh();
                // save data
                EditorUtility.SetDirty(eKey);
            }
            GUI.enabled = !isPlaying;
            Rect rectButtonSendMessageInfo = new Rect(width_inspector - 20f - margin, rectLabelSendMessage.y, 20f, 20f);
            if(GUI.Button(rectButtonSendMessageInfo, "?")) {
                EditorUtility.DisplayDialog("SendMessage vs. Invoke", "SendMessage can only be used with methods that have no more than one parameter (which can be an array).\n\nAnimator will use Invoke when SendMessage is disabled, which is slightly faster but requires caching when the take is played. Use SendMessage if caching is a problem.", "Okay");
            }
            if(cachedParameterInfos.Length > 0) {
                // show method parameters
                float scrollview_y = rectLabelSendMessage.y + rectLabelSendMessage.height + height_inspector_space;
                Rect rectScrollView = new Rect(0f, scrollview_y, width_inspector - margin, rect.height - scrollview_y);
                float width_view = width_inspector - margin - (height_event_parameters > rectScrollView.height ? 20f + margin : 0f);
                Rect rectView = new Rect(0f, 0f, width_view, height_event_parameters);
                inspectorScrollView = GUI.BeginScrollView(rectScrollView, inspectorScrollView, rectView);
                Rect rectField = new Rect(0f, 0f, width_view, 20f);
                float height_all_fields = 0f;
                // there are parameters
                for(int i = 0; i < cachedParameterInfos.Length; i++) {
                    rectField.y += height_inspector_space;
                    if(i > 0) height_all_fields += height_inspector_space;
                    // show field for each parameter
                    float height_field = 0f;
                    if(showFieldFor(rectField, i.ToString(), cachedParameterInfos[i].Name, eKey.parameters[i], cachedParameterInfos[i].ParameterType, 0, ref height_field)) {
                        //sTrack.updateCache(aData);
                        AMCodeView.refresh();
                        // save data
                        EditorUtility.SetDirty(eKey);
                    }
                    rectField.y += height_field;
                    height_all_fields += height_field;
                }
                GUI.EndScrollView();
                height_all_fields += height_inspector_space;
                if(height_event_parameters != height_all_fields) height_event_parameters = height_all_fields;
            }
            return;
        }
        #endregion
        #region camerea switcher inspector
        if(sTrack is AMCameraSwitcherTrack) {
            AMCameraSwitcherKey cKey = (AMCameraSwitcherKey)(sTrack as AMCameraSwitcherTrack).getKeyOnFrame(_frame);
            bool showExtras = false;
            bool notLastKey = cKey != (sTrack as AMCameraSwitcherTrack).keys[(sTrack as AMCameraSwitcherTrack).keys.Count-1];
            if(notLastKey) {
                int cActionIndex = (sTrack as AMCameraSwitcherTrack).getKeyIndexForFrame(_frame);
                showExtras = cActionIndex>-1 && !(sTrack.keys[cActionIndex] as AMCameraSwitcherKey).targetsAreEqual(aData);
            }
            float height_cs = 44f+height_inspector_space + (showExtras ? 22f*3f+height_inspector_space*3f : 0f);
            Rect rectScrollView = new Rect(0f, start_y, width_inspector-margin, rect.height-start_y);
            Rect rectView = new Rect(0f, 0f, rectScrollView.width-(height_cs > rectScrollView.height ? 20f : 0f), height_cs);
            inspectorScrollView = GUI.BeginScrollView(rectScrollView, inspectorScrollView, rectView);
            Rect rectLabelType = new Rect(0f, 0f, 56f, 22f);
            GUI.Label(rectLabelType, "Type");
            Rect rectSelGridType = new Rect(rectLabelType.x+rectLabelType.width+margin, rectLabelType.y, rectView.width-margin-rectLabelType.width, 22f);
            int newType = GUI.SelectionGrid(rectSelGridType, cKey.type, new string[] { "Camera", "Color" }, 2);
            if(cKey.type != newType) {
                recordUndoTrackAndKeys(sTrack, false, "Set Camera Track Type");
                cKey.type = newType;
                // update cache when modifying varaibles
                (sTrack as AMCameraSwitcherTrack).updateCache(aData);
                AMCodeView.refresh();
                // preview
                currentTake.previewFrame(aData, TakeEditCurrent().selectedFrame);
                // save data
                EditorUtility.SetDirty(sTrack);
                setDirtyKeys(sTrack);
            }
            // camera
            Rect rectLabelCameraColor = new Rect(0f, rectLabelType.y+rectLabelType.height+height_inspector_space, 56f, 22f);
            GUI.Label(rectLabelCameraColor, (cKey.type == 0 ? "Camera" : "Color"));
            Rect rectCameraColor = new Rect(rectLabelCameraColor.x+rectLabelCameraColor.width+margin, rectLabelCameraColor.y+3f, rectView.width-rectLabelCameraColor.width-margin, 16f);
            if(cKey.type == 0) {
                Camera newCam = (Camera)EditorGUI.ObjectField(rectCameraColor, cKey.getCamera(aData), typeof(Camera), true);
                if(cKey.getCamera(aData) != newCam) {
                    recordUndoTrackAndKeys(sTrack, false, "Set Camera Track Camera");
                    cKey.setCamera(aData, newCam);
                    // update cache when modifying varaibles
                    (sTrack as AMCameraSwitcherTrack).updateCache(aData);
                    AMCodeView.refresh();
                    // preview
                    currentTake.previewFrame(aData, currentTake.selectedFrame);
                    // save data
                    EditorUtility.SetDirty(sTrack);
                    setDirtyKeys(sTrack);
                }
            }
            else {
                Color newColor = EditorGUI.ColorField(rectCameraColor, cKey.color);
                if(cKey.color != newColor) {
                    recordUndoTrackAndKeys(sTrack, false, "Set Camera Track Color");
                    cKey.color = newColor;
                    // update cache when modifying varaibles
                    (sTrack as AMCameraSwitcherTrack).updateCache(aData);
                    AMCodeView.refresh();
                    // preview
                    currentTake.previewFrame(aData, currentTake.selectedFrame);
                    // save data
                    EditorUtility.SetDirty(sTrack);
                    setDirtyKeys(sTrack);
                }
            }
            GUI.enabled = true;
            // if not last key, show transition and ease
            if(notLastKey && showExtras) {
                // transition picker
                Rect rectTransitionPicker = new Rect(0f, rectLabelCameraColor.y+rectLabelCameraColor.height+height_inspector_space, rectView.width, 22f);
                showTransitionPicker(sTrack, cKey, rectTransitionPicker.x, rectTransitionPicker.y, rectTransitionPicker.width);
                if(cKey.cameraFadeType != (int)AMCameraSwitcherKey.Fade.None) {
                    // ease picker
                    Rect rectEasePicker = new Rect(0f, rectTransitionPicker.y+rectTransitionPicker.height+height_inspector_space, rectView.width, 22f);
                    showEasePicker(sTrack, cKey, aData, rectEasePicker.x, rectEasePicker.y, rectEasePicker.width);
                    // render texture
                    Rect rectLabelRenderTexture = new Rect(0f, rectEasePicker.y+rectEasePicker.height+height_inspector_space+55.0f, 175f, 22f);
                    GUI.Label(rectLabelRenderTexture, "Render Texture (Pro Only)");
                    Rect rectToggleRenderTexture = new Rect(rectView.width-22f, rectLabelRenderTexture.y, 22f, 22f);
                    bool newStill = !GUI.Toggle(rectToggleRenderTexture, !cKey.still, "");
                    if(cKey.still != newStill) {
                        recordUndoTrackAndKeys(sTrack, false, "Set Camera Track Still");
                        cKey.still = newStill;
                        // update cache when modifying varaibles
                        sTrack.updateCache(aData);
                        AMCodeView.refresh();
                        // preview
                        currentTake.previewFrame(aData, currentTake.selectedFrame);
                        // save data
                        EditorUtility.SetDirty(sTrack);
                        setDirtyKeys(sTrack);
                    }
                }
            }
            GUI.EndScrollView();
            return;
        }
        #endregion
        #region trigger inspector
        if(sTrack is AMTriggerTrack) {
            EditorGUIUtility.labelWidth = 55.0f;
            AMTriggerKey tKey = (AMTriggerKey)sTrack.getKeyOnFrame(_frame);
            Rect rectString = new Rect(0f, start_y, width_inspector - margin, 20f);
            string str = EditorGUI.TextField(rectString, "String", tKey.valueString);
            if(tKey.valueString != str) {
                Undo.RecordObject(tKey, "Trigger Set Value String");
                tKey.valueString = str;
                EditorUtility.SetDirty(tKey);
            }
            Rect rectInt = new Rect(0f, rectString.y + rectString.height + height_inspector_space, width_inspector - margin, 20f);
            int i = EditorGUI.IntField(rectInt, "Integer", tKey.valueInt);
            if(tKey.valueInt != i) {
                Undo.RecordObject(tKey, "Trigger Set Value Int");
                tKey.valueInt = i;
                EditorUtility.SetDirty(tKey);
            }
            Rect rectFloat = new Rect(0f, rectInt.y + rectInt.height + height_inspector_space, width_inspector - margin, 20f);
            float f = EditorGUI.FloatField(rectFloat, "Float", tKey.valueFloat);
            if(tKey.valueFloat != f) {
                Undo.RecordObject(tKey, "Trigger Set Value Float");
                tKey.valueFloat = f;
                EditorUtility.SetDirty(tKey);
            }
            EditorGUIUtility.labelWidth = 0.0f;
            return;
        }
        #endregion
    }

    bool showFieldFor(Rect rect, string id, string name, AMEventData data, Type t, int level, ref float height_field) {
        rect.x = 5f * level;
        name = typeStringBrief(t) + " " + name;
        bool saveChanges = false;
        if(t.IsArray) {
            if(t.GetElementType().IsArray) {
                GUI.skin.label.wordWrap = true;
                rect.height = 40f;
                height_field += rect.height;
                GUI.Label(rect, "Multi-dimensional arrays are currently unsupported.");
                return false;
            }
            if(!arrayFieldFoldout.ContainsKey(id)) arrayFieldFoldout.Add(id, true);
            Rect rectArrayFoldout = new Rect(rect.x, rect.y + 3f, 15f, 15f);
            if(GUI.Button(rectArrayFoldout, "", "label")) arrayFieldFoldout[id] = !arrayFieldFoldout[id];
            GUI.DrawTexture(rectArrayFoldout, (arrayFieldFoldout[id] ? GUI.skin.GetStyle("GroupElementFoldout").normal.background : GUI.skin.GetStyle("GroupElementFoldout").active.background));
            Rect rectLabelArrayName = new Rect(rectArrayFoldout.x + rectArrayFoldout.width + margin, rect.y, rect.width - rectArrayFoldout.width - margin, rect.height);
            GUI.Label(rectLabelArrayName, name);
            height_field += rectLabelArrayName.height;
            if(arrayFieldFoldout[id]) {
                AMEventParameter parameter = data as AMEventParameter;

                // show elements if folded out
                if(parameter.lsArray.Count <= 0) {
                    AMEventData a = new AMEventData();
                    a.setValueType(t.GetElementType());
                    parameter.lsArray.Add(a);
                    saveChanges = true;
                }
                Rect rectElement = new Rect(rect);
                rectElement.y += rect.height + margin;
                for(int i = 0; i < parameter.lsArray.Count; i++) {
                    float prev_height = height_field;
                    if((showFieldFor(rectElement, id + "_" + i, "(" + i.ToString() + ")", parameter.lsArray[i], t.GetElementType(), (level + 1), ref height_field)) && !saveChanges) saveChanges = true;
                    rectElement.y += height_field - prev_height;
                }
                // add to array button
                Rect rectLabelElement = new Rect(rect.x, rectElement.y, rect.width - 40f - margin * 2f, 25f);
                height_field += rectLabelElement.height;
                GUIStyle styleLabelRight = new GUIStyle(GUI.skin.label);
                styleLabelRight.alignment = TextAnchor.MiddleRight;
                GUI.Label(rectLabelElement, typeStringBrief(t.GetElementType()), styleLabelRight);
                if(parameter.lsArray.Count <= 1) GUI.enabled = false;
                Rect rectButtonRemoveElement = new Rect(rect.x + rect.width - 40f, rectLabelElement.y, 20f, 20f);
                if(GUI.Button(rectButtonRemoveElement, "-")) {
                    parameter.lsArray.RemoveAt(parameter.lsArray.Count - 1);
                    saveChanges = true;
                }
                Rect rectButtonAddElement = new Rect(rectButtonRemoveElement);
                rectButtonAddElement.x += rectButtonRemoveElement.width + margin;
                GUI.enabled = !isPlaying;
                if(GUI.Button(rectButtonAddElement, "+")) {
                    AMEventData a = new AMEventData();
                    a.setValueType(t.GetElementType());
                    parameter.lsArray.Add(a);
                    saveChanges = true;
                }
            }
        }
        else if(t == typeof(bool)) {
            // int field
            height_field += 20f;
            if(data.setBool(EditorGUI.Toggle(rect, name, data.val_bool))) saveChanges = true;
        }
        else if((t == typeof(int)) || (t == typeof(long))) {
            // int field
            height_field += 20f;
            if(data.setInt(EditorGUI.IntField(rect, name, (int)data.val_int))) saveChanges = true;
        }
        else if((t == typeof(float)) || (t == typeof(double))) {
            // float field
            height_field += 20f;
            if(data.setFloat(EditorGUI.FloatField(rect, name, (float)data.val_float))) saveChanges = true;
        }
        else if(t == typeof(Vector2)) {
            // vector2 field
            height_field += 40f;
            if(data.setVector2(EditorGUI.Vector2Field(rect, name, (Vector2)data.val_vect2))) saveChanges = true;
        }
        else if(t == typeof(Vector3)) {
            // vector3 field
            height_field += 40f;
            if(data.setVector3(EditorGUI.Vector3Field(rect, name, (Vector3)data.val_vect3))) saveChanges = true;
        }
        else if(t == typeof(Vector4)) {
            // vector4 field
            height_field += 40f;
            if(data.setVector4(EditorGUI.Vector4Field(rect, name, (Vector4)data.val_vect4))) saveChanges = true;
        }
        else if(t == typeof(Color)) {
            // color field
            height_field += 40f;
            if(data.setColor(EditorGUI.ColorField(rect, name, (Color)data.val_color))) saveChanges = true;
        }
        else if(t == typeof(Rect)) {
            // rect field
            height_field += 60f;
            if(data.setRect(EditorGUI.RectField(rect, name, (Rect)data.val_rect))) saveChanges = true;
        }
        else if(t == typeof(string)) {
            height_field += 20f;
            // set default
            if(data.val_string == null) data.val_string = "";
            // string field
            if(data.setString(EditorGUI.TextField(rect, name, (string)data.val_string))) saveChanges = true;
        }
        else if(t == typeof(char)) {
            height_field += 20f;
            // set default
            if(data.val_string == null) data.val_string = "";
            // char (string) field
            Rect rectLabelCharField = new Rect(rect.x, rect.y, 146f, rect.height);
            GUI.Label(rectLabelCharField, name);
            Rect rectTextFieldChar = new Rect(rectLabelCharField.x + rectLabelCharField.width + margin, rectLabelCharField.y, rect.width - rectLabelCharField.width - margin, rect.height);
            if(data.setString(GUI.TextField(rectTextFieldChar, data.val_string, 1))) saveChanges = true;
        }
        else if(t == typeof(GameObject)) {
            height_field += 40f + margin;
            // label
            Rect rectLabelField = new Rect(rect);
            GUI.Label(rectLabelField, name);
            // GameObject field
            GUI.skin = null;
            Rect rectObjectField = new Rect(rect.x, rectLabelField.y + rectLabelField.height + margin, rect.width, 16f);
            if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(GameObject), true))) saveChanges = true;
            GUI.skin = skin;
        }
        else if(t.BaseType == typeof(Behaviour) || t.BaseType == typeof(Component)) {
            height_field += 40f + margin;
            // label
            Rect rectLabelField = new Rect(rect);
            GUI.Label(rectLabelField, name);
            GUI.skin = null;
            Rect rectObjectField = new Rect(rect.x, rectLabelField.y + rectLabelField.height + margin, rect.width, 16f);
            EditorGUIUtility.LookLikeControls();
            // field
            if(t == typeof(Transform)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(Transform), true))) saveChanges = true; }
            else if(t == typeof(MeshFilter)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(MeshFilter), true))) saveChanges = true; }
            else if(t == typeof(TextMesh)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(TextMesh), true))) saveChanges = true; }
            else if(t == typeof(MeshRenderer)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(MeshRenderer), true))) saveChanges = true; }
            //else if(t == typeof(ParticleSystem)) { if(parameter.setObject(EditorGUI.ObjectField(rectObjectField,parameter.val_obj,typeof(ParticleSystem),true))) saveChanges = true; }
            else if(t == typeof(TrailRenderer)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(TrailRenderer), true))) saveChanges = true; }
            else if(t == typeof(LineRenderer)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(LineRenderer), true))) saveChanges = true; }
            else if(t == typeof(LensFlare)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(LensFlare), true))) saveChanges = true; }
            // halo
            else if(t == typeof(Projector)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(Projector), true))) saveChanges = true; }
            else if(t == typeof(Rigidbody)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(Rigidbody), true))) saveChanges = true; }
            else if(t == typeof(CharacterController)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(CharacterController), true))) saveChanges = true; }
            else if(t == typeof(BoxCollider)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(BoxCollider), true))) saveChanges = true; }
            else if(t == typeof(SphereCollider)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(SphereCollider), true))) saveChanges = true; }
            else if(t == typeof(CapsuleCollider)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(CapsuleCollider), true))) saveChanges = true; }
            else if(t == typeof(MeshCollider)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(MeshCollider), true))) saveChanges = true; }
            else if(t == typeof(WheelCollider)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(WheelCollider), true))) saveChanges = true; }
            else if(t == typeof(TerrainCollider)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(TerrainCollider), true))) saveChanges = true; }
            else if(t == typeof(InteractiveCloth)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(InteractiveCloth), true))) saveChanges = true; }
            else if(t == typeof(SkinnedCloth)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(SkinnedCloth), true))) saveChanges = true; }
            else if(t == typeof(ClothRenderer)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(ClothRenderer), true))) saveChanges = true; }
            else if(t == typeof(HingeJoint)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(HingeJoint), true))) saveChanges = true; }
            else if(t == typeof(FixedJoint)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(FixedJoint), true))) saveChanges = true; }
            else if(t == typeof(SpringJoint)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(SpringJoint), true))) saveChanges = true; }
            else if(t == typeof(CharacterJoint)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(CharacterJoint), true))) saveChanges = true; }
            else if(t == typeof(ConfigurableJoint)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(ConfigurableJoint), true))) saveChanges = true; }
            else if(t == typeof(ConstantForce)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(ConstantForce), true))) saveChanges = true; }
            //else if(t == typeof(NavMeshAgent)) { if(parameter.setObject(EditorGUI.ObjectField(rectObjectField,parameter.val_obj,typeof(NavMeshAgent),true))) saveChanges = true; }
            //else if(t == typeof(OffMeshLink)) { if(parameter.setObject(EditorGUI.ObjectField(rectObjectField,parameter.val_obj,typeof(OffMeshLink),true))) saveChanges = true; }
            else if(t == typeof(AudioListener)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(AudioListener), true))) saveChanges = true; }
            else if(t == typeof(AudioSource)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(AudioSource), true))) saveChanges = true; }
            else if(t == typeof(AudioReverbZone)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(AudioReverbZone), true))) saveChanges = true; }
            else if(t == typeof(AudioLowPassFilter)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(AudioLowPassFilter), true))) saveChanges = true; }
            else if(t == typeof(AudioHighPassFilter)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(AudioHighPassFilter), true))) saveChanges = true; }
            else if(t == typeof(AudioEchoFilter)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(AudioEchoFilter), true))) saveChanges = true; }
            else if(t == typeof(AudioDistortionFilter)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(AudioDistortionFilter), true))) saveChanges = true; }
            else if(t == typeof(AudioReverbFilter)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(AudioReverbFilter), true))) saveChanges = true; }
            else if(t == typeof(AudioChorusFilter)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(AudioChorusFilter), true))) saveChanges = true; }
            else if(t == typeof(Camera)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(Camera), true))) saveChanges = true; }
            else if(t == typeof(Skybox)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(Skybox), true))) saveChanges = true; }
            // flare layer
            else if(t == typeof(GUILayer)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(GUILayer), true))) saveChanges = true; }
            else if(t == typeof(Light)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(Light), true))) saveChanges = true; }
            //else if(t == typeof(LightProbeGroup)) { if(parameter.setObject(EditorGUI.ObjectField(rectObjectField,parameter.val_obj,typeof(LightProbeGroup),true))) saveChanges = true; }
            else if(t == typeof(OcclusionArea)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(OcclusionArea), true))) saveChanges = true; }
            //else if(t == typeof(OcclusionPortal)) { if(parameter.setObject(EditorGUI.ObjectField(rectObjectField,parameter.val_obj,typeof(OcclusionPortal),true))) saveChanges = true; }
            //else if(t == typeof(LODGroup)) { if(parameter.setObject(EditorGUI.ObjectField(rectObjectField,parameter.val_obj,typeof(LODGroup),true))) saveChanges = true; }
            else if(t == typeof(GUITexture)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(GUITexture), true))) saveChanges = true; }
            else if(t == typeof(GUIText)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(GUIText), true))) saveChanges = true; }
            else if(t == typeof(Animation)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(Animation), true))) saveChanges = true; }
            else if(t == typeof(NetworkView)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(NetworkView), true))) saveChanges = true; }
            // wind zone
            else {

                if(t.BaseType == typeof(Behaviour)) { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(Behaviour), true))) saveChanges = true; }
                else { if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(Component), true))) saveChanges = true; }

            }
            GUI.skin = skin;
            EditorGUIUtility.LookLikeControls();
            //return;
        }
        else if(t == typeof(UnityEngine.Object)) {
            height_field += 40f + margin;
            Rect rectLabelField = new Rect(rect);
            GUI.Label(rectLabelField, name);
            Rect rectObjectField = new Rect(rect.x, rectLabelField.y + rectLabelField.height + margin, rect.width, 16f);
            GUI.skin = null;
            EditorGUIUtility.LookLikeControls();
            if(data.setObject(EditorGUI.ObjectField(rectObjectField, data.val_obj, typeof(UnityEngine.Object), true))) saveChanges = true;
            GUI.skin = skin;
            EditorGUIUtility.LookLikeControls();
        }
        else if(t.IsEnum) {
            height_field += 40f + margin;
            // label
            Rect rectLabelField = new Rect(rect);
            GUI.Label(rectLabelField, name);
            Rect rectObjectField = new Rect(rect.x, rectLabelField.y + rectLabelField.height + margin, rect.width, 16f);
            if(data.setEnum(EditorGUI.EnumPopup(rectObjectField, data.val_enum))) saveChanges = true;
        }
        else {
            height_field += 20f;
            GUI.skin.label.wordWrap = true;
            GUI.Label(rect, "Unsupported parameter type " + t.ToString() + ".");
            GUI.skin.label.wordWrap = false;
        }

        return saveChanges;

    }
    void showObjectFieldFor(AMTrack amTrack, float width_track, Rect rect) {
        if(rect.width < 22f) return;
        // show object field for track, used in OnGUI. Needs to be updated for every track type.
        GUI.skin = null;
        EditorGUIUtility.LookLikeControls();
        // add objectfield for every track type
        // translation/rotation
        if(amTrack is AMTranslationTrack || amTrack is AMRotationTrack) {
            Transform nt = (Transform)EditorGUI.ObjectField(rect, amTrack.GetTarget(aData), typeof(Transform), true/*,GUILayout.Width (width_track-padding_track*2)*/);
            if(!amTrack.isTargetEqual(aData, nt)) {
                Undo.RecordObject(amTrack, "Set Transform");
                amTrack.SetTarget(aData, nt);
                EditorUtility.SetDirty(amTrack);
            }
        }
        // orient
        else if(amTrack is AMOrientationTrack) {
            AMOrientationTrack otrack = amTrack as AMOrientationTrack;
            Transform nt = (Transform)EditorGUI.ObjectField(rect, amTrack.GetTarget(aData), typeof(Transform), true);
            if(!otrack.isTargetEqual(aData, nt)) {
                recordUndoTrackAndKeys(otrack, false, "Set Transform");
                otrack.SetTarget(aData, nt);
                otrack.updateCache(aData);
                AMCodeView.refresh();
                EditorUtility.SetDirty(otrack);
                setDirtyKeys(otrack);
            }
        }
        // animation/go active
        else if(amTrack is AMAnimationTrack || amTrack is AMGOSetActiveTrack) {
            GameObject nobj = (GameObject)EditorGUI.ObjectField(rect, amTrack.GetTarget(aData), typeof(GameObject), true);
            if(nobj && amTrack is AMAnimationTrack && nobj.animation == null) {
                EditorUtility.DisplayDialog("No Animation Component", "You must add an Animation component to the GameObject before you can use it in an Animation Track.", "Okay");
            }
            else if(!amTrack.isTargetEqual(aData, nobj)) {
                Undo.RecordObject(amTrack, "Set GameObject");
                amTrack.SetTarget(aData, nobj.transform);
                EditorUtility.SetDirty(amTrack);
            }
        }
        // audio
        else if(amTrack is AMAudioTrack) {
            AudioSource nsrc = (AudioSource)EditorGUI.ObjectField(rect, amTrack.GetTarget(aData), typeof(AudioSource), true);
            if(!amTrack.isTargetEqual(aData, nsrc)) {
                Undo.RecordObject(amTrack, "Set Audio Source");
                if(nsrc != null) nsrc.playOnAwake = false;
                amTrack.SetTarget(aData, nsrc.transform);
                EditorUtility.SetDirty(amTrack);
            }
        }
        // property/event
        else if(amTrack is AMPropertyTrack || amTrack is AMEventTrack) {
            GameObject ngo = (GameObject)EditorGUI.ObjectField(rect, amTrack.GetTarget(aData), typeof(GameObject), true);
            if(!amTrack.isTargetEqual(aData, ngo)) {
                bool changeGO = true;

                //check if new target has the required components
                bool componentsMatch = amTrack.VerifyComponents(ngo);

                if(!componentsMatch && (amTrack.keys.Count > 0) && (!EditorUtility.DisplayDialog("Data Will Be Lost", "Certain keyframes on track '" + amTrack.name + "' will be removed if you continue.", "Continue Anway", "Cancel"))) {
                    changeGO = false;
                }
                if(changeGO) {
                    Undo.RegisterCompleteObjectUndo(amTrack, "Set GameObject");

                    if(!componentsMatch) {
                        if(amTrack is AMPropertyTrack) { //remove all keys if required component is missing
                            // delete all keys
                            if(amTrack.keys.Count > 0) {
                                foreach(AMKey key in amTrack.keys)
                                    Undo.DestroyObjectImmediate(key);

                                amTrack.keys = new List<AMKey>();
                                ((AMPropertyTrack)amTrack).clearInfo();
                            }
                        }
                        else {
                            //delete keys that require component
                            List<AMKey> nkeys = new List<AMKey>(amTrack.keys.Count);
                            foreach(AMKey key in amTrack.keys) {
                                if(ngo.GetComponent(key.GetRequiredComponent()) != null)
                                    nkeys.Add(key);
                                else
                                    Undo.DestroyObjectImmediate(key);
                            }

                            amTrack.keys = nkeys;
                        }
                    }

                    amTrack.SetTarget(aData, ngo.transform);

                    amTrack.updateCache(aData);
                    AMCodeView.refresh();

                    EditorUtility.SetDirty(amTrack);
                }
            }
        }

        GUI.skin = skin;
        EditorGUIUtility.LookLikeControls();
    }
    void showAlertMissingObjectType(string type) {
        EditorUtility.DisplayDialog("Missing " + type, "You must add a " + type + " to the track before you can add keys.", "Okay");
    }
    void showTransitionPicker(AMTrack track, AMCameraSwitcherKey key, float x = -1f, float y = -1f, float width = -1f) {
        if(x >= 0f && y >= 0f && width >= 0f) {
            width--;
            float height = 22f;
            Rect rectLabel = new Rect(x, y-1f, 40f, height);
            int index = 0;

            GUI.Label(rectLabel, "Fade");
            Rect rectPopup = new Rect(rectLabel.x + rectLabel.width + 2f, y+3f, width - rectLabel.width - width_button_delete -3f, height);
            for(int i=0; i<AMTransitionPicker.TransitionOrder.Length; i++) {
                if(AMTransitionPicker.TransitionOrder[i] == key.cameraFadeType) {
                    index = i;
                    break;
                }
            }
            int newIndex = EditorGUI.Popup(rectPopup, index, AMTransitionPicker.TransitionNames);
            if(key.cameraFadeType != AMTransitionPicker.TransitionOrder[newIndex]) {
                recordUndoTrackAndKeys(track, false, "Set Camera Track Fade Type");
                key.cameraFadeType = AMTransitionPicker.TransitionOrder[newIndex];
                // reset parameters
                AMTransitionPicker.setDefaultParametersForKey(ref key);
                // update cache when modifying variables
                track.updateCache(aData);
                // save data
                EditorUtility.SetDirty(track);
                setDirtyKeys(track);
                // preview current frame
                currentTake.previewFrame(aData, currentTake.selectedFrame);
                // refresh values
                AMTransitionPicker.refreshValues();
                // refresh code view
                AMCodeView.refresh();
            }
            Rect rectButton = new Rect(width-width_button_delete+1f, y, width_button_delete, width_button_delete);
            if(GUI.Button(rectButton, getSkinTextureStyleState("popup").background, GUI.skin.GetStyle("ButtonImage"))) {
                AMTransitionPicker.setValues(key, track);
                EditorWindow.GetWindow(typeof(AMTransitionPicker));
            }
        }
        else {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Space(1f);
            GUILayout.Label("Fade");
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Space(3f);
            int index = 0;
            for(int i=0; i<AMTransitionPicker.TransitionOrder.Length; i++) {
                if(AMTransitionPicker.TransitionOrder[i] == key.cameraFadeType) {
                    index = i;
                    break;
                }
            }
            int newIndex = EditorGUILayout.Popup(index, AMTransitionPicker.TransitionNames);
            if(key.cameraFadeType != AMTransitionPicker.TransitionOrder[newIndex]) {
                recordUndoTrackAndKeys(track, false, "Set Camera Track Fade Type");
                key.cameraFadeType = AMTransitionPicker.TransitionOrder[newIndex];
                // reset parameters
                AMTransitionPicker.setDefaultParametersForKey(ref key);
                // update cache when modifying variables
                track.updateCache(aData);
                // save data
                EditorUtility.SetDirty(track);
                setDirtyKeys(track);
                // preview current frame
                currentTake.previewFrame(aData, currentTake.selectedFrame);
                // refresh values
                AMTransitionPicker.refreshValues();
                // refresh code view
                AMCodeView.refresh();
            }

            GUILayout.EndVertical();
            if(GUILayout.Button(getSkinTextureStyleState("popup").background, GUI.skin.GetStyle("ButtonImage"), GUILayout.Width(width_button_delete), GUILayout.Height(width_button_delete))) {
                AMTransitionPicker.setValues(key, track);
                EditorWindow.GetWindow(typeof(AMTransitionPicker));
            }
            GUILayout.Space(1f);
            GUILayout.EndHorizontal();
        }
    }
    public static bool showEasePicker(AMTrack track, AMKey key, AnimatorData aData, float x = -1f, float y = -1f, float width = -1f) {
        bool didUpdate = false;
        if(x >= 0f && y >= 0f && width >= 0f) {
            width--;
            float height = 22f;
            Rect rectLabel = new Rect(x, y - 1f, 40f, height);
            GUI.Label(rectLabel, "Ease");
            Rect rectPopup = new Rect(rectLabel.x + rectLabel.width + 2f, y + 3f, width - rectLabel.width - width_button_delete - 3f, height);

            int popInd = key.easeType == AMKey.EaseTypeNone ? easeTypeNames.Length - 1 : key.easeType;
            int nease = EditorGUI.Popup(rectPopup, popInd, easeTypeNames);
            if(nease == easeTypeNames.Length - 1) nease = AMKey.EaseTypeNone;
            if(key.easeType != nease) {
                recordUndoTrackAndKeys(track, false, "Change Ease");
                key.setEaseType(nease);
                // update cache when modifying varaibles
                track.updateCache(aData);
                AMCodeView.refresh();
                // preview new position
                window.currentTake.previewFrame(aData, window.currentTake.selectedFrame);
                // save data
                EditorUtility.SetDirty(track);
                setDirtyKeys(track);
                // refresh component
                didUpdate = true;
                // refresh values
                AMEasePicker.refreshValues();
            }

            Rect rectButton = new Rect(width - width_button_delete + 1f, y, width_button_delete, width_button_delete);
            if(GUI.Button(rectButton, getSkinTextureStyleState("popup").background, GUI.skin.GetStyle("ButtonImage"))) {
                AMEasePicker.setValues(/*aData,*/key, track);
                EditorWindow.GetWindow(typeof(AMEasePicker));
            }

            //display specific variable for certain tweens
            //TODO: only show this for specific tweens
            if(!key.hasCustomEase()) {
                y += rectButton.height + 4;
                Rect rectAmp = new Rect(x, y, 200f, height);

                float namp = EditorGUI.FloatField(rectAmp, "Amplitude", key.amplitude);
                if(key.amplitude != namp) {
                    Undo.RecordObject(key, "Change Amplitude");
                    key.amplitude = namp;
                    EditorUtility.SetDirty(key);
                }

                y += rectAmp.height + 4;
                Rect rectPer = new Rect(x, y, 200f, height);
                float nperiod = EditorGUI.FloatField(rectPer, "Period", key.period);
                if(key.period != nperiod) {
                    Undo.RecordObject(key, "Change Period");
                    key.period = nperiod;
                    EditorUtility.SetDirty(key);
                }
            }
        }
        else {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Space(1f);
            GUILayout.Label("Ease");
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Space(3f);
            int popInd = key.easeType == AMKey.EaseTypeNone ? easeTypeNames.Length - 1 : key.easeType;
            int nease = EditorGUILayout.Popup(popInd, easeTypeNames);
            if(nease == easeTypeNames.Length - 1) nease = AMKey.EaseTypeNone;
            if(key.easeType != nease) {
                recordUndoTrackAndKeys(track, false, "Change Ease");
                key.setEaseType(nease);
                // update cache when modifying varaibles
                track.updateCache(aData);
                AMCodeView.refresh();
                // preview new position
                window.currentTake.previewFrame(aData, window.currentTake.selectedFrame);
                // save data
                EditorUtility.SetDirty(track);
                setDirtyKeys(track);
                // refresh component
                didUpdate = true;
                // refresh values
                AMEasePicker.refreshValues();
            }
            GUILayout.EndVertical();
            if(GUILayout.Button(getSkinTextureStyleState("popup").background, GUI.skin.GetStyle("ButtonImage"), GUILayout.Width(width_button_delete), GUILayout.Height(width_button_delete))) {
                AMEasePicker.setValues(/*aData,*/key, track);
                EditorWindow.GetWindow(typeof(AMEasePicker));
            }
            GUILayout.Space(1f);
            GUILayout.EndHorizontal();

            //display specific variable for certain tweens
            //TODO: only show this for specific tweens
            if(!key.hasCustomEase()) {
                key.amplitude = EditorGUILayout.FloatField("Amplitude", key.amplitude);

                key.period = EditorGUILayout.FloatField("Period", key.period);
            }
        }
        return didUpdate;
    }

    void drawIndicator(int frame) {
        // draw the indicator texture on the timeline
        int _startFrame = (int)currentTake.startFrame;
        // abort if frame not rendered
        if(frame < _startFrame) return;
        if(frame > (_startFrame + numFramesToRender - 1)) return;
        // offset frame based on render start frame
        frame -= _startFrame;
        // draw textures
        GUI.DrawTexture(new Rect(width_track + (frame) * current_width_frame + (current_width_frame / 2) - width_indicator_head / 2 - 1f, height_indicator_offset_y, width_indicator_head, height_indicator_head), texIndHead);
        GUI.DrawTexture(new Rect(width_track + (frame) * current_width_frame + (current_width_frame / 2) - width_indicator_line / 2 - 1f, height_indicator_offset_y + height_indicator_head, width_indicator_line, position.height - (height_indicator_offset_y + height_indicator_head) - height_indicator_footer + 2f), texIndLine);
    }

    #endregion

    #region Process/Calculate

    void processDragLogic() {
        #region hand tool acceleration
        if(justFinishedHandDragTicker > 0) {
            justFinishedHandDragTicker--;
            if(justFinishedHandDragTicker <= 0) {
                handDragAccelaration = (int)((endHandMousePosition.x - currentMousePosition.x) * 1.5f);
            }
        }
        #endregion
        bool justStartedDrag = false;
        bool justFinishedDrag = false;
        if(isDragging != cachedIsDragging) {
            if(isDragging) justStartedDrag = true;
            else justFinishedDrag = true;
            cachedIsDragging = isDragging;
        }
        #region just started drag
        // set start and end drag frames
        if(justStartedDrag) {
            if(isRenamingTrack != -1 || isRenamingTake || isRenamingGroup != 0) return;
            #region track / group
            if(mouseOverElement == (int)ElementType.Group || mouseOverElement == (int)ElementType.Track) {
                draggingGroupElement = mouseOverGroupElement;
                draggingGroupElementType = mouseOverElement;
                dragType = (int)DragType.GroupElement;
                if(mouseOverElement == (int)ElementType.Group) timelineSelectGroup((int)mouseOverGroupElement.x);
                else timelineSelectTrack((int)mouseOverGroupElement.y);
            }
            #endregion
            #region frame
            // if dragged from frame
            else if(mouseOverFrame != 0) {
                // change track if necessary
                if(!TakeEditCurrent().contextSelectionTracks.Contains(mouseOverTrack) && TakeEditCurrent().selectedTrack != mouseOverTrack) timelineSelectTrack(mouseOverTrack);

                // if dragged from selected frame, move
                if(mouseOverSelectedFrame) {
                    dragType = (int)DragType.MoveSelection;
                    TakeEditCurrent().setGhostSelection();
                }
                else {
                    // else, start context selection
                    dragType = (int)DragType.ContextSelection;
                }
                startDragFrame = mouseOverFrame;
                ghostStartDragFrame = startDragFrame;
                endDragFrame = mouseOverFrame;
            #endregion
                #region time scrub
                // if dragged from time scrub
            }
            else if(mouseOverElement == (int)ElementType.TimeScrub) {
                // set start scrub mouse position
                startScrubMousePosition = currentMousePosition;
                dragType = (int)DragType.TimeScrub;
                #endregion
                #region frame scrub
            }
            else if(mouseOverElement == (int)ElementType.FrameScrub) {
                // set start scrub mouse position
                startScrubMousePosition = currentMousePosition;
                dragType = (int)DragType.FrameScrub;
                #endregion
                #region resize track
            }
            else if(mouseOverElement == (int)ElementType.ResizeTrack) {
                startScrubMousePosition = currentMousePosition;
                startResize_width_track = aData.width_track;
                dragType = (int)DragType.ResizeTrack;
                #endregion
                #region timeline scrub
            }
            else if(mouseOverElement == (int)ElementType.TimelineScrub) {
                dragType = (int)DragType.TimelineScrub;
                #endregion
                #region resize action
            }
            else if(mouseOverElement == (int)ElementType.ResizeAction) {
                if(TakeEditCurrent().selectedTrack != mouseOverTrack) timelineSelectTrack(mouseOverTrack);
                dragType = (int)DragType.ResizeAction;
                #endregion
                #region resize horizontal scrollbar left
            }
            else if(mouseOverElement == (int)ElementType.ResizeHScrollbarLeft) {
                dragType = (int)DragType.ResizeHScrollbarLeft;
                #endregion
                #region resize horizontal scrollbar right
            }
            else if(mouseOverElement == (int)ElementType.ResizeHScrollbarRight) {
                dragType = (int)DragType.ResizeHScrollbarRight;
                #endregion
                #region cursor zoom
            }
            else if(mouseOverElement == (int)ElementType.CursorZoom) {
                startZoomMousePosition = currentMousePosition;
                zoomDirectionMousePosition = currentMousePosition;
                startZoomValue = aData.zoom;
                startFrame = currentTake.startFrame;
                startScrollViewValue = scrollViewValue.y;
                dragType = (int)DragType.CursorZoom;
                didPeakZoom = false;
                #endregion
                #region cursor hand
            }
            else if(mouseOverElement == (int)ElementType.CursorHand) {
                //startScrubFrame = mouseXOverFrame;
                justStartedHandGrab = true;
                dragType = (int)DragType.CursorHand;
                #endregion
            }
            else {
                // if did not drag from a draggable element
                dragType = (int)DragType.None;
            }
            // reset drag
            justStartedDrag = false;
        #endregion
            #region just finished drag
            // if finished drag
        }
        else if(justFinishedDrag) {
            // if finished drag onto frame x, update end drag frame
            if(mouseXOverFrame != 0) {
                endDragFrame = mouseXOverFrame;
            }
            // if finished move selection
            if(dragType == (int)DragType.MoveSelection) {
                bool instantiated;
                AMTakeData take;
                if(instantiated = MetaInstantiate("Move Keys")) {
                    take = currentTake;
                }
                else {
                    take = currentTake;
                    Undo.RegisterCompleteObjectUndo(getKeysAndTracks(take), "Move Keys");
                }

                AMKey[] dKeys = TakeEdit(take).offsetContextSelectionFramesBy(take, aData, endDragFrame - startDragFrame);

                foreach(AMKey dkey in dKeys)
                    if(instantiated) { DestroyImmediate(dkey); } else { Undo.DestroyObjectImmediate(dkey); }

                dKeys = checkForOutOfBoundsFramesOnSelectedTrack();

                foreach(AMKey dkey in dKeys)
                    if(instantiated) { DestroyImmediate(dkey); } else { Undo.DestroyObjectImmediate(dkey); }

                // preview selected frame
                take.previewFrame(aData, take.selectedFrame);
                AMCodeView.refresh();
                // if finished context selection

                setDirtyTracks(take);
                foreach(AMTrack track in take.trackValues)
                    setDirtyKeys(track);

                TakeEdit(take).ghostSelection.Clear();
            }
            else if(dragType == (int)DragType.ContextSelection) {
                contextSelectFrameRange(startDragFrame, endDragFrame);
                // if finished timeline scrub
            }
            else if(dragType == (int)DragType.TimeScrub || dragType == (int)DragType.FrameScrub) {
                // do nothing
                // if finished dragging group element
            }
            else if(dragType == (int)DragType.GroupElement) {
                registerTakesUndo(aData, "Move Element", false);
                processDropGroupElement(draggingGroupElementType, draggingGroupElement, mouseOverElement, mouseOverGroupElement);
                setDirtyTakes(aData);
            }
            else if(dragType == (int)DragType.ResizeAction) {
                AMTrack _track = TakeEditCurrent().getSelectedTrack(currentTake);
                Undo.RegisterCompleteObjectUndo(_track, "Resize Action");
                AMKey[] dKeys = _track.removeDuplicateKeys();
                foreach(AMKey dkey in dKeys)
                    Undo.DestroyObjectImmediate(dkey);
                // preview selected frame
                currentTake.previewFrame(aData, currentTake.selectedFrame);
            }
            else if(dragType == (int)DragType.CursorZoom) {
                tex_cursor_zoom = null;
            }
            else if(dragType == (int)DragType.CursorHand) {
                endHandMousePosition = currentMousePosition;
                justFinishedHandDragTicker = 1;
                //startScrubFrame = 0;	
            }
            dragType = (int)DragType.None;
            // reset drag
            justFinishedDrag = false;

            #endregion
            #region is dragging
            // if is dragging
        }
        else if(isDragging) {
            #region move selection
            // if moving selection, offset selection
            if(dragType == (int)DragType.MoveSelection) {
                if(mouseXOverFrame != endDragFrame) {
                    endDragFrame = mouseXOverFrame;
                    TakeEditCurrent().offsetGhostSelectionBy(endDragFrame - ghostStartDragFrame);
                    ghostStartDragFrame = endDragFrame;
                }
            #endregion
                #region group element
            }
            else if(dragType == (int)DragType.GroupElement) {
                scrollViewValue.y += scrollAmountVertical / 6f;
                #endregion
                #region context selection
            }
            else if(dragType == (int)DragType.ContextSelection) {
                if(mouseXOverFrame != 0) {
                    endDragFrame = mouseXOverFrame;
                }
                contextSelectFrameRange(startDragFrame, endDragFrame);
                #endregion
                #region time scrub / frame scrub
                // if is dragging time or frame scrub, set scrub speed
            }
            else if(dragType == (int)DragType.TimeScrub || dragType == (int)DragType.FrameScrub) {
                setScrubSpeed();
                #endregion
                #region timeline scrub
            }
            else if(dragType == (int)DragType.TimelineScrub) {
                int frame = mouseXOverFrame;
                if(frame < (int)currentTake.startFrame) frame = (int)currentTake.startFrame;
                else if(frame > (int)currentTake.endFrame) frame = (int)currentTake.endFrame;
                selectFrame(frame);
                #endregion
                #region resize action
                // resize action
            }
            else if(dragType == (int)DragType.ResizeAction && mouseXOverFrame > 0) {
                AMTrack selTrack = TakeEditCurrent().getSelectedTrack(currentTake);
                if(selTrack.hasKeyOnFrame(resizeActionFrame)) {
                    AMKey selKey = selTrack.getKeyOnFrame(resizeActionFrame);
                    if((startResizeActionFrame == -1 || mouseXOverFrame > startResizeActionFrame) && (endResizeActionFrame == -1 || mouseXOverFrame < endResizeActionFrame)) {
                        if(selKey.frame != mouseXOverFrame) {

                            if(arrKeysLeft.Length > 0 && (mouseXOverFrame - startResizeActionFrame - 1) < arrKeysLeft.Length) {
                                // do nothing
                            }
                            else if(arrKeysRight.Length > 0 && (endResizeActionFrame - mouseXOverFrame - 1) < arrKeysRight.Length) {
                                // do nothing
                            }
                            else {
                                selKey.frame = mouseXOverFrame;
                                resizeActionFrame = mouseXOverFrame;
                                if(arrKeysLeft.Length > 0 && (mouseXOverFrame - startResizeActionFrame - 1) <= arrKeysLeft.Length) {
                                    for(int i = 0; i < arrKeysLeft.Length; i++) {
                                        arrKeysLeft[i].frame = startResizeActionFrame + i + 1;
                                    }
                                }
                                else if(arrKeysRight.Length > 0 && (endResizeActionFrame - mouseXOverFrame - 1) <= arrKeysRight.Length) {
                                    for(int i = 0; i < arrKeysRight.Length; i++) {
                                        arrKeysRight[i].frame = resizeActionFrame + i + 1;
                                    }
                                }
                                else {
                                    // update left
                                    int lastFrame = startResizeActionFrame;
                                    for(int i = 0; i < arrKeysLeft.Length; i++) {
                                        arrKeysLeft[i].frame = Mathf.FloorToInt((resizeActionFrame - startResizeActionFrame) * arrKeyRatiosLeft[i] + startResizeActionFrame);
                                        if(arrKeysLeft[i].frame <= lastFrame) {
                                            arrKeysLeft[i].frame = lastFrame + 1;

                                        }
                                        if(arrKeysLeft[i].frame >= resizeActionFrame) arrKeysLeft[i].frame = resizeActionFrame - 1;			// after last
                                        lastFrame = arrKeysLeft[i].frame;
                                    }

                                    // update right
                                    lastFrame = resizeActionFrame;
                                    for(int i = 0; i < arrKeysRight.Length; i++) {
                                        arrKeysRight[i].frame = Mathf.FloorToInt((endResizeActionFrame - resizeActionFrame) * arrKeyRatiosRight[i] + resizeActionFrame);
                                        if(arrKeysRight[i].frame <= lastFrame) {
                                            arrKeysRight[i].frame = lastFrame + 1;
                                        }
                                        if(arrKeysRight[i].frame >= endResizeActionFrame) arrKeysRight[i].frame = endResizeActionFrame - 1;	// after last
                                        lastFrame = arrKeysRight[i].frame;
                                    }
                                }
                                // update cache
                                selTrack.updateCache(aData);
                                AMCodeView.refresh();
                            }

                        }
                    }
                }
                #endregion
                #region resize horizontal scrollbar left
            }
            else if(dragType == (int)DragType.ResizeHScrollbarLeft) {
                if(mouseXOverHScrollbarFrame <= 0) currentTake.startFrame = 1;
                else if(mouseXOverHScrollbarFrame > currentTake.numFrames) currentTake.startFrame = currentTake.numFrames;
                else currentTake.startFrame = mouseXOverHScrollbarFrame;
                #endregion
                #region resize horizontal scrollbar right
            }
            else if(dragType == (int)DragType.ResizeHScrollbarRight) {
                if(mouseXOverHScrollbarFrame <= 0) currentTake.endFrame = 1;
                else if(mouseXOverHScrollbarFrame > currentTake.numFrames) currentTake.endFrame = currentTake.numFrames;
                else currentTake.endFrame = mouseXOverHScrollbarFrame;
                int min = Mathf.FloorToInt((position.width - width_track - 18f - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed)) / (height_track - height_action_min));
                if(currentTake.startFrame > currentTake.endFrame - min) currentTake.startFrame = currentTake.endFrame - min;
                #endregion
                #region cursor zoom
            }
            else if(dragType == (int)DragType.CursorZoom) {
                if(dragPan) {
                    if(didPeakZoom) {
                        if(wasZoomingIn && currentMousePosition.x <= cachedZoomMousePosition.x) {
                            startFrame = currentTake.startFrame;
                            zoomDirectionMousePosition.x = currentMousePosition.x;
                        }
                        else if(!wasZoomingIn && currentMousePosition.x >= cachedZoomMousePosition.x) {
                            startFrame = currentTake.startFrame;
                            zoomDirectionMousePosition.x = currentMousePosition.x;
                        }
                        didPeakZoom = false;
                    }
                    float frameDiff = currentTake.endFrame - currentTake.startFrame;
                    float framesInView = (currentTake.endFrame-currentTake.startFrame);
                    float areaWidth = position.width - width_track - (aData.isInspectorOpen ? width_inspector_open - 4f : width_inspector_closed) - 21f;
                    float currFrame = startFrame + (zoomDirectionMousePosition.x- currentMousePosition.x) * (framesInView/areaWidth);
                    if(currFrame <= 1) {
                        wasZoomingIn = false;
                        cachedZoomMousePosition = currentMousePosition;
                        currFrame = 1f;
                        didPeakZoom = true;
                    }
                    if(currFrame + frameDiff > currentTake.numFrames) {
                        wasZoomingIn = true;
                        cachedZoomMousePosition = currentMousePosition;
                        zoomDirectionMousePosition.x = currentMousePosition.x;
                        currFrame = currentTake.numFrames - frameDiff;
                        didPeakZoom = true;
                    }

                    currentTake.startFrame = currFrame;
                    currentTake.endFrame = currentTake.startFrame + frameDiff;
                    scrollViewValue.y = startScrollViewValue + (zoomDirectionMousePosition.y - currentMousePosition.y);
                }
                else {
                    if(didPeakZoom) {
                        if(wasZoomingIn && currentMousePosition.x <= cachedZoomMousePosition.x) {
                            // direction change	
                            startZoomValue = aData.zoom;
                            zoomDirectionMousePosition = currentMousePosition;
                        }
                        else if(!wasZoomingIn && currentMousePosition.x >= cachedZoomMousePosition.x) {
                            // direction change	
                            startZoomValue = aData.zoom;
                            zoomDirectionMousePosition = currentMousePosition;
                        }
                        didPeakZoom = false;
                    }

                    float zoomValue = startZoomValue + (zoomDirectionMousePosition.x - currentMousePosition.x)/300f;

                    if(zoomValue < 0f) {
                        zoomValue = 0f;
                        cachedZoomMousePosition = currentMousePosition;
                        wasZoomingIn = true;
                        didPeakZoom = true;
                    }
                    else if(zoomValue > 1f) {
                        zoomValue = 1f;
                        cachedZoomMousePosition = currentMousePosition;
                        wasZoomingIn = false;
                        didPeakZoom = true;
                    }

                    if(zoomValue < aData.zoom) tex_cursor_zoom = tex_cursor_zoomin;
                    else if(zoomValue > aData.zoom) tex_cursor_zoom = tex_cursor_zoomout;
                    aData.zoom = zoomValue;
                }
            }
                #endregion
        }
            #endregion
    }
    void processUpdateMethodInfoCache(bool now = false) {
        if(now) updateMethodInfoCacheBuffer = 0;
        // update methodinfo cache if necessary
        if(!aData || currentTake == null) return;
        if(currentTake.getTrackCount() <= 0) return;
        if(TakeEditCurrent().selectedTrack <= -1) return;
        if(updateMethodInfoCacheBuffer > 0) updateMethodInfoCacheBuffer--;
        if((indexMethodInfo == -1) && (updateMethodInfoCacheBuffer <= 0)) {

            // if track is event
            AMTrack selectedTrack = TakeEditCurrent().getSelectedTrack(currentTake);
            if(TakeEditCurrent().selectedTrack > -1 && selectedTrack is AMEventTrack) {
                // if event track has key on selected frame
                if(selectedTrack.hasKeyOnFrame(currentTake.selectedFrame)) {
                    // update methodinfo cache
                    updateCachedMethodInfo(selectedTrack.GetTarget(aData) as GameObject);
                    updateMethodInfoCacheBuffer = updateRateMethodInfoCache;
                    // set index to method info index

                    if(cachedMethodInfo.Count > 0) {
                        AMEventKey eKey = (selectedTrack.getKeyOnFrame(currentTake.selectedFrame) as AMEventKey);
                        MethodInfo m = eKey.getMethodInfo(selectedTrack.GetTarget(aData) as GameObject);
                        if(m != null) {
                            for(int i = 0; i < cachedMethodInfo.Count; i++) {
                                if(cachedMethodInfo[i] == m) {
                                    indexMethodInfo = i;
                                    return;
                                }
                            }
                        }
                        indexMethodInfo = 0;
                    }
                }
            }
        }
    }
    void processHandDragAcceleration() {
        float speed = (int)((Mathf.Clamp(Mathf.Abs(handDragAccelaration), 0, 200) / 12) * (aData.zoom + 0.2f));
        if(handDragAccelaration > 0) {

            if(currentTake.endFrame < currentTake.numFrames) {
                currentTake.startFrame += speed;
                currentTake.endFrame += speed;
                if(ticker % 2 == 0) handDragAccelaration--;
            }
            else {
                handDragAccelaration = 0;
            }
        }
        else if(handDragAccelaration < 0) {
            if(currentTake.startFrame > 1f) {
                currentTake.startFrame -= speed;
                currentTake.endFrame -= speed;
                if(ticker % 2 == 0) handDragAccelaration++;
            }
            else {
                handDragAccelaration = 0;
            }
        }
    }
    void processDropGroupElement(int sourceType, Vector2 sourceElement, int destType, Vector2 destElement) {
        // dropped inside group
        if(destType == (int)ElementType.Group) {
            if(sourceType == (int)ElementType.Track) {
                // drop track on group
                currentTake.moveToGroup((int)sourceElement.y, (int)destElement.x, true);
            }
            else if(sourceType == (int)ElementType.Group) {
                // drop group on group
                if((int)sourceElement.x != (int)destElement.x)
                    currentTake.moveToGroup((int)sourceElement.x, (int)destElement.x, true);
            }
            // dropped outside group
        }
        else if(destType == (int)ElementType.GroupOutside) {
            if(sourceType == (int)ElementType.Track) {
                // drop track on group

                currentTake.moveGroupElement((int)sourceElement.y, (int)destElement.x);
            }
            else if(sourceType == (int)ElementType.Group) {
                // drop group on group
                //int destGroup = aData.getCurrentTake().getElementGroup((int)destElement.x);
                //if((int)sourceElement.x != destGroup)
                //aData.getCurrentTake().moveToGroup((int)sourceElement.x,destGroup);
                if((int)sourceElement.x != (int)destElement.x)
                    currentTake.moveGroupElement((int)sourceElement.x, (int)destElement.x);

            }
            // dropped on track
        }
        else if(destType == (int)ElementType.Track) {
            if(sourceType == (int)ElementType.Track) {
                // drop track on track
                if((int)sourceElement.y != (int)destElement.y)
                    currentTake.moveToGroup((int)sourceElement.y, (int)destElement.x, false, (int)destElement.y);
            }
            else if(sourceType == (int)ElementType.Group) {
                // drop group on track
                if((int)destElement.x == 0 || (int)sourceElement.x != (int)destElement.x)
                    currentTake.moveToGroup((int)sourceElement.x, (int)destElement.x, false, (int)destElement.y);
            }
        }
        else {
            // drop on window, move to root group
            if(sourceType == (int)ElementType.Track) {
                currentTake.moveToGroup((int)sourceElement.y, 0, mouseAboveGroupElements);
            }
            else if(sourceType == (int)ElementType.Group) {
                // move group to last position
                currentTake.moveToGroup((int)sourceElement.x, 0, mouseAboveGroupElements);
            }
        }
        // re-select track to update selected group
        if(sourceType == (int)ElementType.Track) timelineSelectTrack((int)sourceElement.y);
        // scroll to the track
        float scrollTo = -1f;
        scrollTo = currentTake.getElementY((sourceType == (int)ElementType.Group ? (int)sourceElement.x : (int)sourceElement.y), height_track, height_track_foldin, height_group);
        setScrollViewValue(scrollTo);
    }
    public static void recalculateNumFramesToRender() {
        if(window) window.cachedZoom = -1f;
    }
    void calculateNumFramesToRender(bool clickedZoom, Event e) {
        if(currentTake == null) return;

        int min = Mathf.FloorToInt((position.width - width_track - 18f - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed)) / (oData.disableTimelineActions ? height_track / 2f : height_track - height_action_min));
        int _mouseXOverFrame = (int)currentTake.startFrame + Mathf.CeilToInt((e.mousePosition.x - width_track) / current_width_frame) - 1;
        // move frames with hand cursor
        if(dragType == (int)DragType.CursorHand && !justStartedHandGrab) {
            if(_mouseXOverFrame != startScrubFrame) {
                float numFrames = currentTake.endFrame - currentTake.startFrame;
                float dist_hand_drag = startScrubFrame - _mouseXOverFrame;
                currentTake.startFrame += dist_hand_drag;
                currentTake.endFrame += dist_hand_drag;
                if(currentTake.startFrame < 1f) {
                    currentTake.startFrame = 1f;
                    currentTake.endFrame += numFrames - (currentTake.endFrame - currentTake.startFrame);
                }
                else if(currentTake.endFrame > currentTake.numFrames) {
                    currentTake.endFrame = currentTake.numFrames;
                    currentTake.startFrame -= numFrames - (currentTake.endFrame - currentTake.startFrame);
                }
            }
            // calculate the number of frames to render based on zoom
        }
        else if(aData.zoom != cachedZoom && dragType != (int)DragType.ResizeHScrollbarLeft && dragType != (int)DragType.ResizeHScrollbarRight) {
            //numFramesToRender
            if(oData.scrubby_zoom_slider) numFramesToRender = Mathf.Lerp(0.0f, 1.0f, aData.zoom) * ((float)currentTake.numFrames - min) + min;
            else numFramesToRender = Expo.EaseIn(aData.zoom, 0.0f, 1.0f, 1.0f, 0.0f, 0.0f) * ((float)currentTake.numFrames - min) + min;
            // frame dimensions
            current_width_frame = Mathf.Clamp((position.width - width_track - 18f - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed)) / numFramesToRender, 0f, (oData.disableTimelineActions ? height_track / 2f : height_track - height_action_min));
            current_height_frame = Mathf.Clamp(current_width_frame * 2f, 20f, (oData.disableTimelineActions ? height_track : 40f));
            float half = 0f;
            // zoom out			
            if(currentTake.endFrame - currentTake.startFrame + 1 < Mathf.FloorToInt(numFramesToRender)) {
                if((oData.scrubby_zoom_cursor && dragType == (int)DragType.CursorZoom) /*|| (aData.scrubby_zoom_slider && dragType != (int)DragType.CursorZoom)*/) {
                    int newPosFrame = (int)currentTake.startFrame + Mathf.CeilToInt((startZoomMousePosition.x - width_track) / current_width_frame) - 1;
                    int _diff = startZoomXOverFrame - newPosFrame;
                    currentTake.startFrame += _diff;
                    currentTake.endFrame += _diff;
                }
                else {

                    half = (((int)numFramesToRender - (currentTake.endFrame - currentTake.startFrame + 1)) / 2f);
                    currentTake.startFrame -= Mathf.FloorToInt(half);
                    currentTake.endFrame += Mathf.CeilToInt(half);
                    // clicked zoom out
                    if(clickedZoom) {
                        int newPosFrame = (int)currentTake.startFrame + Mathf.CeilToInt((e.mousePosition.x - width_track) / current_width_frame) - 1;
                        int _diff = _mouseXOverFrame - newPosFrame;
                        currentTake.startFrame += _diff;
                        currentTake.endFrame += _diff;
                    }
                }
                // zoom in
            }
            else if(currentTake.endFrame - currentTake.startFrame + 1 > Mathf.FloorToInt(numFramesToRender)) {
                //targetPos = ((float)startZoomXOverFrame)/((float)aData.getCurrentTake().endFrame);
                half = (((currentTake.endFrame - currentTake.startFrame + 1) - numFramesToRender) / 2f);
                //float scrubby_startframe = (float)aData.getCurrentTake().startFrame+half;
                currentTake.startFrame += Mathf.FloorToInt(half);
                currentTake.endFrame -= Mathf.CeilToInt(half);
                int targetFrame = 0;
                // clicked zoom in
                if(clickedZoom) {
                    int newPosFrame = (int)currentTake.startFrame + Mathf.CeilToInt((e.mousePosition.x - width_track) / current_width_frame) - 1;
                    int _diff = _mouseXOverFrame - newPosFrame;
                    currentTake.startFrame += _diff;
                    currentTake.endFrame += _diff;
                    // scrubby zoom in
                }
                else if((oData.scrubby_zoom_cursor && dragType == (int)DragType.CursorZoom) || (oData.scrubby_zoom_slider && dragType != (int)DragType.CursorZoom)) {
                    if(dragType != (int)DragType.CursorZoom) {
                        // scrubby zoom slider to indicator
                        targetFrame = currentTake.selectedFrame;
                        float dist_scrubbyzoom = Mathf.Round(targetFrame - Mathf.FloorToInt(currentTake.startFrame + numFramesToRender / 2f));
                        int offset = Mathf.RoundToInt(dist_scrubbyzoom * (1f - Mathf.Lerp(0.0f, 1.0f, aData.zoom)));
                        currentTake.startFrame += offset;
                        currentTake.endFrame += offset;
                    }
                    else {
                        // scrubby zoom cursor to mouse position
                        int newPosFrame = (int)currentTake.startFrame + Mathf.CeilToInt((startZoomMousePosition.x - width_track) / current_width_frame) - 1;
                        int _diff = startZoomXOverFrame - newPosFrame;
                        currentTake.startFrame += _diff;
                        currentTake.endFrame += _diff;
                    }
                }

            }
            // if beyond boundaries, adjust
            int diff = 0;
            if(currentTake.endFrame > currentTake.numFrames) {
                diff = (int)currentTake.endFrame - currentTake.numFrames;
                currentTake.endFrame -= diff;
                currentTake.startFrame += diff;
            }
            else if(currentTake.startFrame < 1) {
                diff = 1 - (int)currentTake.startFrame;
                currentTake.startFrame -= diff;
                currentTake.endFrame += diff;
            }
            if(half * 2 < (int)numFramesToRender) currentTake.endFrame++;
            cachedZoom = aData.zoom;
            return;
        }
        // calculates the number of frames to render based on window width
        if(currentTake.startFrame < 1) currentTake.startFrame = 1;

        if(currentTake.endFrame < currentTake.startFrame + min) currentTake.endFrame = currentTake.startFrame + min;
        if(currentTake.endFrame > currentTake.numFrames) currentTake.endFrame = currentTake.numFrames;
        if(currentTake.startFrame > currentTake.endFrame - min) currentTake.startFrame = currentTake.endFrame - min;
        numFramesToRender = currentTake.endFrame - currentTake.startFrame + 1;
        current_width_frame = Mathf.Clamp((position.width - width_track - 18f - (aData.isInspectorOpen ? width_inspector_open : width_inspector_closed)) / numFramesToRender, 0f, (oData.disableTimelineActions ? height_track / 2f : height_track - height_action_min));
        current_height_frame = Mathf.Clamp(current_width_frame * 2f, 20f, (oData.disableTimelineActions ? height_track : 40f));
        if(dragType == (int)DragType.ResizeHScrollbarLeft || dragType == (int)DragType.ResizeHScrollbarRight) {
            if(oData.scrubby_zoom_slider) aData.zoom = Mathf.Lerp(0f, 1f, (numFramesToRender - min) / ((float)currentTake.numFrames - min));
            else aData.zoom = AMUtil.EaseInExpoReversed(0f, 1f, (numFramesToRender - min) / ((float)currentTake.numFrames - min));
            cachedZoom = aData.zoom;
        }
    }

    #endregion

    #region Timeline/Timeline Manipulation

    void timelineSelectTrack(int _track) {
        // select a track from the timeline
        cancelTextEditting();

        if(currentTake.getTrackCount() <= 0) return;

        // select track
        TakeEditCurrent().selectTrack(currentTake, _track, isShiftDown, isControlDown);

        AMTrack track = currentTake.getTrack(_track);

        // check if target's path has been changed, if so, clear cache
        aData.e_maintainTargetCache(track);

        // set active object
        if(track.GetTarget(aData) == null) {
            if(!string.IsNullOrEmpty(track.targetPath)) {
                Selection.activeObject = aData.gameObject;
                Debug.LogWarning("Missing target: "+track.GetTargetPath(aData));
            }
        }
        else
            timelineSelectObjectFor(track);
    }
    bool timelineSelectGroupOrTrackFromCurrent(int dir) {
        AMTakeData take = currentTake;
        AMTakeEdit takeEdit = TakeEditCurrent();

        int parentGrpId = 0;
        int id = 0;
        bool proceed = false;

        //AMTrack 
        if(takeEdit.selectedTrack != -1) {
            id = takeEdit.selectedTrack;
            parentGrpId = take.getElementGroup(takeEdit.selectedTrack);
            proceed = true;
        }
        else if(takeEdit.selectedGroup != 0) {
            id = takeEdit.selectedGroup;

            //if we are moving down, just select the first element
            if(dir > 0) {
                AMGroup grp = take.getGroup(id);
                if(grp.elements.Count > 0 && grp.foldout) {
                    int nextId = grp.elements[0];
                    if(nextId < 0) //select group
                        timelineSelectGroup(nextId);
                    else
                        timelineSelectTrack(nextId);
                    return true;
                }
            }

            parentGrpId = take.getElementGroup(takeEdit.selectedGroup);
            proceed = true;
        }

        if(proceed) {
            //Debug.Log("grp: "+parentGrpId);
            AMGroup grp = take.getGroup(parentGrpId);

            int curInd = grp.getItemIndex(id);
            int nextInd = curInd + dir;
            if(curInd != -1) {
                int nextId;
                if(nextInd < 0 || nextInd >= grp.elements.Count) {
                    if(parentGrpId == 0) {
                        if(id < 0) {
                            if(dir > 0) {
                                nextInd = -1;
                                while(id < 0) {
                                    grp = take.getGroup(id);
                                    if(grp.elements.Count > 0) {
                                        if(grp.foldout) {
                                            nextInd = 0;
                                            break;
                                        }
                                    }
                                    id = take.getElementGroup(id);
                                }
                                if(nextInd != -1) {
                                    nextId = grp.elements[nextInd];
                                }
                                else
                                    return false;
                            }
                            else
                                return false;
                        }
                        else
                            return false;
                    }
                    else {
                        if(dir < 0)
                            nextId = parentGrpId;
                        else { //moving down, check the next element up the hierarchy
                            //see if we can get the first element if id is group
                            if(id < 0 && (grp = take.getGroup(id)).elements.Count > 0 && grp.foldout) {
                                nextInd = 0;
                            }
                            else {
                                do {
                                    id = parentGrpId;
                                    parentGrpId = take.getElementGroup(id);
                                    grp = take.getGroup(parentGrpId);
                                    curInd = grp.getItemIndex(id);
                                    if(curInd == -1) break;
                                    nextInd = curInd + dir;
                                } while(parentGrpId < 0 && nextInd >= grp.elements.Count);
                                if(curInd == -1 || nextInd >= grp.elements.Count)
                                    return false;
                            }
                            nextId = grp.elements[nextInd];
                        }
                    }
                }
                else {
                    nextId = grp.elements[nextInd];
                    if(nextId < 0 && dir < 0) { //if next is a group, see if we can select its last element
                        grp = take.getGroup(nextId);
                        if(grp.elements.Count > 0 && grp.foldout) {
                            nextId = grp.elements[grp.elements.Count - 1];
                        }
                    }
                }

                if(nextId < 0) //select group
                    timelineSelectGroup(nextId);
                else
                    timelineSelectTrack(nextId);
            }
            else
                proceed = false;
        }

        return proceed;
    }
    void timelineSelectGroup(int group_id) {
        cancelTextEditting();
        TakeEditCurrent().selectGroup(currentTake, group_id, isShiftDown, isControlDown);
        TakeEditCurrent().selectedTrack = -1;
    }
    void timelineSelectFrame(int _track, int _frame, bool deselectKeyboardFocus = true) {
        // select a frame from the timeline
        cancelTextEditting();
        indexMethodInfo = -1;	// reset methodinfo index to update caches
        if(currentTake.getTrackCount() <= 0) return;
        // select frame
        TakeEditCurrent().selectFrame(currentTake, _track, _frame, numFramesToRender, isShiftDown, isControlDown);
        // preview frame
        currentTake.previewFrame(aData, _frame);
        // set active object
        if(_track > -1) timelineSelectObjectFor(currentTake.getTrack(_track));
        // deselect keyboard focus
        if(deselectKeyboardFocus)
            GUIUtility.keyboardControl = 0;
    }
    void timelineSelectObjectFor(AMTrack track) {
        // translation/rot/anim/property obj
        if(track.GetType() == typeof(AMTranslationTrack) || track.GetType() == typeof(AMRotationTrack) 
            || track.GetType() == typeof(AMAnimationTrack) || track.GetType() == typeof(AMPropertyTrack))
            Selection.activeObject = track.GetTarget(aData);
    }
    void timelineSelectNextKey() {
        // select next key
        if(currentTake.getTrackCount() <= 0) return;
        if(currentTake.getTrack(TakeEditCurrent().selectedTrack).keys.Count <= 0) return;
        int frame = currentTake.getTrack(TakeEditCurrent().selectedTrack).getKeyFrameAfterFrame(currentTake.selectedFrame);
        if(frame <= -1) return;
        timelineSelectFrame(TakeEditCurrent().selectedTrack, frame);

    }
    void timelineSelectPrevKey() {
        // select previous key
        if(currentTake.getTrackCount() <= 0) return;
        if(currentTake.getTrack(TakeEditCurrent().selectedTrack).keys.Count <= 0) return;
        int frame = currentTake.getTrack(TakeEditCurrent().selectedTrack).getKeyFrameBeforeFrame(currentTake.selectedFrame);
        if(frame <= -1) return;
        timelineSelectFrame(TakeEditCurrent().selectedTrack, frame);

    }
    public void selectFrame(int frame) {
        if(currentTake.selectedFrame != frame) {
            timelineSelectFrame(TakeEditCurrent().selectedTrack, frame, false);
        }
    }
    void playerTogglePlay() {
        GUIUtility.keyboardControl = 0;
        cancelTextEditting();
        // toggle player off if is playing
        if(isPlaying) {
            isPlaying = false;
            // select where stopped
            timelineSelectFrame(TakeEditCurrent().selectedTrack, currentTake.selectedFrame);
            currentTake.stopAudio(aData);
            return;
        }
        // set preview player variables
        playerStartTime = Time.realtimeSinceStartup;
        playerStartedFrame = playerStartFrame = currentTake.selectedFrame;
        // start playing
        isPlaying = true;
        playerCurLoop = 0;
        playerBackward = false;
        // sample audio from current frame
        currentTake.sampleAudio(aData, (float)currentTake.selectedFrame, playbackSpeedValue[currentTake.playbackSpeedIndex]);
    }

    #endregion

    #region Set/Get

    string getNewTargetName() {
        int count = 1;
        while(true) {
            if(GameObject.Find("Target" + count)) count++;
            else break;
        }
        return "Target" + count;
    }
    public static void recordUndoTrackAndKeys(AMTrack track, bool complete, string label) {
        if(complete) {
            Undo.RegisterCompleteObjectUndo(track, label);
            Undo.RegisterCompleteObjectUndo(track.keys.ToArray(), label);
        }
        else {
            Undo.RecordObject(track, label);
            Undo.RecordObjects(track.keys.ToArray(), label);
        }
    }
    public static void setDirtyKeys(AMTrack track) {
        foreach(AMKey key in track.keys) {
            EditorUtility.SetDirty(key);
        }
    }
    static void setDirtyTracks(AMTakeData take) {
        foreach(AMTrack track in take.trackValues) {
            EditorUtility.SetDirty(track);
        }
    }
    void setScrubSpeed() {
        scrubSpeed = ((currentMousePosition.x - startScrubMousePosition.x)) / 30;

    }
    void setScrollViewValue(float val) {
        val = Mathf.Clamp(val, 0f, maxScrollView());
        if(val < scrollViewValue.y || val > scrollViewValue.y + position.height - 66f)
            scrollViewValue.y = Mathf.Clamp(val, 0f, maxScrollView());
    }
    // timeline action info
    string getInfoTextForAction(AMTrack _track, AMKey _key, bool brief, int clamped) {
        int easeInd = _key.easeType;

        // get text for track type
        #region translation
        if(_key is AMTranslationKey) {
            if(easeInd == AMKey.EaseTypeNone) { return ""; }
            return easeTypeNames[easeInd];
        #endregion
            #region rotation
        }
        else if(_key is AMRotationKey) {
            if(easeInd == AMKey.EaseTypeNone) { return ""; }
            return easeTypeNames[easeInd];
            #endregion
            #region animation
        }
        else if(_key is AMAnimationKey) {
            if(!(_key as AMAnimationKey).amClip) return "Not Set";
            return (_key as AMAnimationKey).amClip.name + "\n" + ((WrapMode)(_key as AMAnimationKey).wrapMode).ToString();
            #endregion
            #region audio
        }
        else if(_key is AMAudioKey) {
            if(!(_key as AMAudioKey).audioClip) return "Not Set";
            return (_key as AMAudioKey).audioClip.name;
            #endregion
            #region property
        }
        else if(_key is AMPropertyKey) {
            AMPropertyTrack propTrack = _track as AMPropertyTrack;
            AMPropertyKey propkey = _key as AMPropertyKey;

            string info = propTrack.getTrackType() + "\n";
            if(propkey.targetsAreEqual(propTrack.valueType) || easeInd == AMKey.EaseTypeNone || !propTrack.canTween) brief = true;
            if(!brief && propkey.endFrame != -1 && easeInd != AMKey.EaseTypeNone) {
                info += easeTypeNames[easeInd] + ": ";
            }
            string detail = propkey.getValueString(propTrack.GetCachedInfoType(aData), propTrack.valueType, brief);	// extra details such as integer values ex. 1 -> 12
            if(detail != null) info += detail;
            return info;
            #endregion
            #region event
        }
        else if(_key is AMEventKey) {
            if(string.IsNullOrEmpty((_key as AMEventKey).methodName)) {
                return "Not Set";
            }
            string txtInfoEvent = (_key as AMEventKey).methodName;
            // include parameters
            if((_key as AMEventKey).parameters != null) {
                txtInfoEvent += "(";
                for(int i = 0; i < (_key as AMEventKey).parameters.Count; i++) {
                    if((_key as AMEventKey).parameters[i] == null) txtInfoEvent += "";
                    else txtInfoEvent += (_key as AMEventKey).parameters[i].getStringValue();
                    if(i < (_key as AMEventKey).parameters.Count - 1) txtInfoEvent += ", ";
                }

                txtInfoEvent += ")";
                return txtInfoEvent;
            }
            return (_key as AMEventKey).methodName;
            #endregion
            #region orientation
        }
        else if(_key is AMOrientationKey) {
            if(easeInd == AMKey.EaseTypeNone) { return ""; }

            if(!(_key as AMOrientationKey).GetTarget(aData)) return "No Target";
            string txtInfoOrientation = null;
            if((_key as AMOrientationKey).isLookFollow(aData)) {
                txtInfoOrientation = (_key as AMOrientationKey).GetTarget(aData).gameObject.name;
                return txtInfoOrientation;
            }
            txtInfoOrientation = (_key as AMOrientationKey).GetTarget(aData).gameObject.name +
				" -> " + ((_key as AMOrientationKey).GetTargetEnd(aData) ? (_key as AMOrientationKey).GetTargetEnd(aData).gameObject.name : "No Target");
            txtInfoOrientation += "\n" + easeTypeNames[easeInd];
            return txtInfoOrientation;
            #endregion
            #region camera switcher
        }
        else if(_key is AMCameraSwitcherKey) {
            if(!(_key as AMCameraSwitcherKey).hasStartTarget(aData)) return "None";
            string txtInfoCameraSwitcher = null;
            if((_key as AMCameraSwitcherKey).targetsAreEqual(aData) || clamped != 0) {
                txtInfoCameraSwitcher = (_key as AMCameraSwitcherKey).getStartTargetName(aData);
                return txtInfoCameraSwitcher;
            }
            txtInfoCameraSwitcher = (_key as AMCameraSwitcherKey).getStartTargetName(aData) +
			" -> " + (_key as AMCameraSwitcherKey).getEndTargetName(aData);
            txtInfoCameraSwitcher += "\n"+AMTransitionPicker.TransitionNamesDict[((_key as AMCameraSwitcherKey).cameraFadeType > AMTransitionPicker.TransitionNamesDict.Length ? 0 : (_key as AMCameraSwitcherKey).cameraFadeType)];
            if((_key as AMCameraSwitcherKey).cameraFadeType != (int)AMCameraSwitcherKey.Fade.None) txtInfoCameraSwitcher += ": "+easeTypeNames[(_key as AMCameraSwitcherKey).easeType];
            return txtInfoCameraSwitcher;
            #endregion
            #region goactive
        }
        else if(_key is AMGOSetActiveKey) {
            return "";
            #endregion
            #region trigger
        }
        else if(_key is AMTriggerKey) {
            AMTriggerKey tkey = _key as AMTriggerKey;
            return string.Format("\"{0}\", {1}, {2}", tkey.valueString, tkey.valueInt, tkey.valueFloat);
            #endregion
        }

        return "Unknown";
    }
    public string getMethodInfoSignature(MethodInfo methodInfo) {
        ParameterInfo[] parameters = methodInfo.GetParameters();
        // loop through parameters, add them to signature
        string methodString = methodInfo.Name + " (";
        for(int i = 0; i < parameters.Length; i++) {
            //unsupported param type
            if(AMEventParameter.GetValueType(parameters[i].ParameterType) == -1) return "";

            methodString += typeStringBrief(parameters[i].ParameterType);
            if(i < parameters.Length - 1) methodString += ", ";
        }
        methodString += ")";
        return methodString;
    }
    public string[] getMethodNames() {
        // get all method names from every comonent on GameObject, and update methodinfo cache
        return cachedMethodNames.ToArray();
    }
    public Texture getTrackIconTexture(AMTrack _track) {
        if(_track is AMAnimationTrack) return texIconAnimation;
        else if(_track is AMEventTrack) return texIconEvent;
        else if(_track is AMPropertyTrack) return texIconProperty;
        else if(_track is AMTranslationTrack) return texIconTranslation;
        else if(_track is AMAudioTrack) return texIconAudio;
        else if(_track is AMRotationTrack) return texIconRotation;
        else if(_track is AMOrientationTrack) return texIconOrientation;
        else if(_track is AMGOSetActiveTrack) return texIconProperty;
        else if(_track is AMCameraSwitcherTrack) return texIconCameraSwitcher;
        else if(_track is AMTriggerTrack) return texIconEvent;

        Debug.LogWarning("Animator: Icon texture not found for track " + _track.getTrackType());
        return null;
    }
    Vector2 getGlobalMousePosition(Event e) {
        Vector2 convertedGUIPos = GUIUtility.GUIToScreenPoint(e.mousePosition);
        convertedGUIPos.x -= position.x;
        convertedGUIPos.y -= position.y;
        return convertedGUIPos;
    }
    public static EditorWindow GetMainGameView() {
        System.Type T = System.Type.GetType("UnityEditor.GameView,UnityEditor");
        System.Reflection.MethodInfo GetMainGameView = T.GetMethod("GetMainGameView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        System.Object Res = GetMainGameView.Invoke(null, null);
        return (EditorWindow)Res;
    }
    public static Rect GetMainGameViewPosition() {
        return GetMainGameView().position;
    }

    #endregion

    #region Add/Delete

    void addTargetWithTranslationTrack(object key) {
        addTarget(key, true);
    }
    void addTargetWithoutTranslationTrack(object key) {
        addTarget(key, false);
    }
    void addTarget(object key, bool withTranslationTrack) {
        AMTrack sTrack;
        bool addCompUndo;
        if(MetaInstantiate("Add Target")) {
            sTrack = TakeEditCurrent().getSelectedTrack(currentTake);
            addCompUndo = false;
        }
        else {
            sTrack = TakeEditCurrent().getSelectedTrack(currentTake);
            recordUndoTrackAndKeys(sTrack, false, "Add Target");
            addCompUndo = true;
        }

        AMOrientationKey oKey = key as AMOrientationKey;
        // create target
        GameObject target = new GameObject(getNewTargetName());
        Transform t = sTrack.GetTarget(aData) as Transform;
        target.transform.parent = aData.transform;
        target.transform.position = t.position + t.forward * 5f;
        target.transform.rotation = Quaternion.identity;
        target.transform.localScale = Vector3.one;
        Undo.AddComponent<AMTarget>(target);
        // set target
        oKey.SetTarget(aData, target.transform);
        // update cache
        sTrack.updateCache(aData);
        AMCodeView.refresh();
        // preview new frame
        currentTake.previewFrame(aData, currentTake.selectedFrame);
        // save data
        EditorUtility.SetDirty(sTrack);
        setDirtyKeys(sTrack);

        // add to translation track
        if(withTranslationTrack) {
            objects_window = new List<GameObject>();
            objects_window.Add(target);
            addTrack((int)Track.Translation, addCompUndo);
        }
    }
    void addTrack(object trackType, bool addCompUndo) {
        AMTakeData take = currentTake;

        // add one null GameObject if no GameObject dragged onto timeline (when clicked on new track button)
        if(objects_window.Count <= 0) {
            objects_window.Add(null);
        }
        foreach(GameObject object_window in objects_window) {
            addTrackWithGameObject(trackType, object_window, addCompUndo);
        }
        objects_window = new List<GameObject>();
        timelineSelectTrack(take.track_count);
        // move scrollview to last created track
        setScrollViewValue(take.getElementY(TakeEdit(take).selectedTrack, height_track, height_track_foldin, height_group));

        setDirtyTakes(aData);
        AMCodeView.refresh();
    }

    void addTrackWithGameObject(object trackType, GameObject object_window, bool addCompUndo) {
        GameObject holder = aData.TargetGetDataHolder().gameObject;

        // add track based on index
        switch((int)trackType) {
            case (int)Track.Translation:
                AMTranslationTrack ttrack = addCompUndo ? Undo.AddComponent<AMTranslationTrack>(holder) : holder.AddComponent<AMTranslationTrack>();
                ttrack.pixelPerUnit = oData.pixelPerUnitDefault;
                currentTake.addTrack(TakeEditCurrent().selectedGroup, aData, object_window ? object_window.transform : null, ttrack);
                break;
            case (int)Track.Rotation:
                currentTake.addTrack(TakeEditCurrent().selectedGroup, aData, object_window ? object_window.transform : null,
                    addCompUndo ? Undo.AddComponent<AMRotationTrack>(holder) : holder.AddComponent<AMRotationTrack>());
                break;
            case (int)Track.Orientation:
                currentTake.addTrack(TakeEditCurrent().selectedGroup, aData, object_window ? object_window.transform : null,
                    addCompUndo ? Undo.AddComponent<AMOrientationTrack>(holder) : holder.AddComponent<AMOrientationTrack>());
                break;
            case (int)Track.Animation:
                currentTake.addTrack(TakeEditCurrent().selectedGroup, aData, object_window ? object_window.transform : null,
                    addCompUndo ? Undo.AddComponent<AMAnimationTrack>(holder) : holder.AddComponent<AMAnimationTrack>());
                break;
            case (int)Track.Audio:
                currentTake.addTrack(TakeEditCurrent().selectedGroup, aData, object_window ? object_window.transform : null,
                    addCompUndo ? Undo.AddComponent<AMAudioTrack>(holder) : holder.AddComponent<AMAudioTrack>());
                break;
            case (int)Track.Property:
                currentTake.addTrack(TakeEditCurrent().selectedGroup, aData, object_window ? object_window.transform : null,
                    addCompUndo ? Undo.AddComponent<AMPropertyTrack>(holder) : holder.AddComponent<AMPropertyTrack>());
                break;
            case (int)Track.Event:
                currentTake.addTrack(TakeEditCurrent().selectedGroup, aData, object_window ? object_window.transform : null,
                    addCompUndo ? Undo.AddComponent<AMEventTrack>(holder) : holder.AddComponent<AMEventTrack>());
                break;
            case (int)Track.CameraSwitcher:
                if(GameObject.FindObjectsOfType<Camera>().Length <= 1) {
                    EditorUtility.DisplayDialog("Cannot add Camera Switcher", "You need at least 2 cameras in your scene to start using the Camera Switcher track.", "Okay");
                }
                else if(currentTake.cameraSwitcher) {
                    // already exists
                    EditorUtility.DisplayDialog("Camera Switcher Already Exists", "You can only have one Camera Switcher track. Transition between cameras by adding keyframes to the track.", "Okay");
                }
                else {
                    currentTake.addTrack(TakeEditCurrent().selectedGroup, aData, object_window ? object_window.transform : null,
                        addCompUndo ? Undo.AddComponent<AMCameraSwitcherTrack>(holder) : holder.AddComponent<AMCameraSwitcherTrack>());
                    // preview selected frame
                    if(object_window != null) {
                        currentTake.previewFrame(aData, TakeEditCurrent().selectedFrame);
                    }
                }
                break;
            case (int)Track.GOSetActive:
                currentTake.addTrack(TakeEditCurrent().selectedGroup, aData, object_window ? object_window.transform : null,
                    addCompUndo ? Undo.AddComponent<AMGOSetActiveTrack>(holder) : holder.AddComponent<AMGOSetActiveTrack>());
                break;
            case (int)Track.Trigger:
                currentTake.addTrack(TakeEditCurrent().selectedGroup, aData, object_window ? object_window.transform : null,
                    addCompUndo ? Undo.AddComponent<AMTriggerTrack>(holder) : holder.AddComponent<AMTriggerTrack>());
                break;
            default:
                int combo_index = (int)trackType - 100;
                if(combo_index >= oData.quickAdd_Combos.Count) {
                    Debug.LogError("Animator: Track type '" + (int)trackType + "' not found.");
                }
                else {
                    foreach(int _etrack in oData.quickAdd_Combos[combo_index]) {
                        addTrackWithGameObject((int)_etrack, object_window, addCompUndo);
                    }
                }
                break;
        }
    }

    public bool addSpriteKeysToTrack(UnityEngine.Object[] objs, int trackId, int frame) {
        AMPropertyTrack track = currentTake.getTrack(trackId) as AMPropertyTrack;
        if(track && track.getTrackType() == "sprite") {
            List<Sprite> sprites = AMEditorUtil.GetSprites(objs);
            if(sprites.Count > 0) {
                //prepare data
                const string label = "Add Sprite Keys";
                AMTrack.OnAddKey addCall;
                if(registerTakesUndo(aData, label, false)) {
                    track = currentTake.getTrack(trackId) as AMPropertyTrack;
                    addCall = OnAddKeyComp;
                }
                else {
                    recordUndoTrackAndKeys(track, true, label);
                    addCall = OnAddKeyUndoComp;
                }

                //insert keys
                AMTakeData take = currentTake;

                if(sprites.Count > 1) {
                    float sprFPS = oData.spriteInsertFramePerSecond;

                    //expand num frames if needed
                    int spriteNumFrames = Mathf.RoundToInt(((float)sprites.Count/sprFPS)*(float)take.frameRate);
                    if(take.numFrames < frame + spriteNumFrames)
                        take.numFrames += (frame + spriteNumFrames) - take.numFrames;

                    track.offsetKeysFromBy(aData, frame, spriteNumFrames);

                    for(int i = 0; i < sprites.Count; i++) {
                        AMPropertyKey key = track.addKey(aData, addCall, frame);
                        key.setValue(sprites[i]);

                        int nframe = Mathf.RoundToInt((((float)frame/(float)take.frameRate) + (1.0f/sprFPS))*(float)take.frameRate);
                        frame = Mathf.Max(nframe, frame+1);

                        //add final key if it's the last in track
                        if(i == sprites.Count-1 && track.keys[track.keys.Count-1] == key) {
                            key = track.addKey(aData, addCall, frame);
                            key.setValue(sprites[i]);
                        }
                    }
                }
                else { //just set key
                    AMKey key = track.getKeyOnFrame(frame, false);
                    if(key == null)
                        key = track.addKey(aData, addCall, frame);

                    (key as AMPropertyKey).setValue(sprites[0]);
                }

                EditorUtility.SetDirty(track);
                setDirtyKeys(track);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true if track added successfully
    /// </summary>
    public bool addSpriteTrackWithKeyObjects(UnityEngine.Object[] objs, int startFrame) {
        List<Sprite> sprites = AMEditorUtil.GetSprites(objs);
        if(sprites.Count > 0) {
            sprites.Sort(delegate(Sprite obj1, Sprite obj2) { return obj1.name.CompareTo(obj2.name); });

            //create go
            const string label = "Add Sprite Track";
            GameObject newGO = new GameObject("sprite", typeof(SpriteRenderer));
            Undo.RegisterCreatedObjectUndo(newGO, label);
            Undo.SetTransformParent(newGO.transform, aData.TargetGetRoot(), label);
            newGO.transform.localPosition = Vector3.zero;

            //prepare animator data
            bool instantiated = registerTakesUndo(aData, "Add Sprite Track", false);
            AMTrack.OnAddKey addCall = instantiated ? (AMTrack.OnAddKey)OnAddKeyComp : (AMTrack.OnAddKey)OnAddKeyUndoComp;

            //add track
            addTrack((int)Track.Property, !instantiated);

            AMTakeData take = currentTake;
            timelineSelectTrack(currentTake.track_count);

            AMPropertyTrack newTrack = TakeEdit(take).getSelectedTrack(take) as AMPropertyTrack;
            newTrack.SetTarget(aData, newGO.transform);

            //insert keys
            float sprFPS = oData.spriteInsertFramePerSecond;

            //expand num frames if needed
            int spriteNumFrames = Mathf.RoundToInt(((float)sprites.Count/sprFPS)*(float)take.frameRate);
            if(take.numFrames < spriteNumFrames)
                take.numFrames = spriteNumFrames;

            SpriteRenderer comp = newGO.GetComponent<SpriteRenderer>();
            newTrack.setComponent(aData, comp);
            newTrack.setPropertyInfo(comp.GetType().GetProperty("sprite"));

            int frame = startFrame;
            for(int i = 0; i < sprites.Count; i++) {
                AMPropertyKey key = newTrack.addKey(aData, addCall, frame);
                key.setValue(sprites[i]);

                int nframe = Mathf.RoundToInt((((float)frame/(float)take.frameRate) + (1.0f/sprFPS))*(float)take.frameRate);
                frame = Mathf.Max(nframe, frame+1);
            }

            //add last frame
            AMPropertyKey lastKey = newTrack.addKey(aData, addCall, frame);
            lastKey.setValue(sprites[sprites.Count - 1]);

            comp.sprite = sprites[0];

            return true;
        }

        return false;
    }
    void addKeyToFrame(int frame) {
        // add key if there are tracks
        if(currentTake.getTrackCount() > 0) {
            // add a key
            addKey(TakeEditCurrent().selectedTrack, frame);
            // preview current frame
            //aData.getCurrentTake().previewFrame(aData, aData.getCurrentTake().selectedFrame);
        }
        timelineSelectFrame(TakeEditCurrent().selectedTrack, currentTake.selectedFrame, false);
    }
    void addKeyToSelectedFrame() {
        // add key if there are tracks
        if(currentTake.getTrackCount() > 0) {
            // add a key
            addKey(TakeEditCurrent().selectedTrack, currentTake.selectedFrame);
            // preview current frame
            currentTake.previewFrame(aData, currentTake.selectedFrame);
        }
        timelineSelectFrame(TakeEditCurrent().selectedTrack, currentTake.selectedFrame, false);
    }
    void addKey(int _track, int _frame) {
        // add a key to the track number and frame, used in OnGUI. Needs to be updated for every track type.
        AMTrack.OnAddKey addCall;
        AMTrack amTrack;
        if(MetaInstantiate("New Key")) {
            addCall = OnAddKeyComp;
            amTrack = currentTake.getTrack(_track);
        }
        else {
            addCall = OnAddKeyUndoComp;
            amTrack = currentTake.getTrack(_track);
            recordUndoTrackAndKeys(amTrack, true, "New Key");
        }

        // translation
        if(amTrack is AMTranslationTrack) {
            Transform t = amTrack.GetTarget(aData) as Transform;
            // if missing object, return
            if(!t) {
                showAlertMissingObjectType("Transform");
                return;
            }
            (amTrack as AMTranslationTrack).addKey(aData, addCall, _frame, t.localPosition);
        }
        else if(amTrack is AMRotationTrack) {
            // rotation
            Transform t = amTrack.GetTarget(aData) as Transform;
            // if missing object, return
            if(!t) {
                showAlertMissingObjectType("Transform");
                return;
            }
            // add key to rotation track
            (amTrack as AMRotationTrack).addKey(aData, addCall, _frame, t.localRotation);
        }
        else if(amTrack is AMOrientationTrack) {
            // orientation

            // if missing object, return
            if(!amTrack.GetTarget(aData)) {
                showAlertMissingObjectType("Transform");
                return;
            }
            // add key to orientation track
            Transform last_target = null;
            int last_key = (amTrack as AMOrientationTrack).getKeyFrameBeforeFrame(_frame, false);
            if(last_key == -1) last_key = (amTrack as AMOrientationTrack).getKeyFrameAfterFrame(_frame, false);
            if(last_key != -1) {
                AMOrientationKey _oKey = ((amTrack as AMOrientationTrack).getKeyOnFrame(last_key) as AMOrientationKey);
                last_target = _oKey.GetTarget(aData);
            }
            (amTrack as AMOrientationTrack).addKey(aData, addCall, _frame, last_target);
        }
        else if(amTrack is AMAnimationTrack) {
            // animation
            GameObject go = amTrack.GetTarget(aData) as GameObject;
            // if missing object, return
            if(!go) {
                showAlertMissingObjectType("GameObject");
                return;
            }
            // add key to animation track
            (amTrack as AMAnimationTrack).addKey(aData, addCall, _frame, go.animation.clip, WrapMode.Once);
        }
        else if(amTrack is AMAudioTrack) {
            // audio
            AudioSource a = amTrack.GetTarget(aData) as AudioSource;
            // if missing object, return
            if(!a) {
                showAlertMissingObjectType("AudioSource");
                return;
            }
            // add key to animation track
            (amTrack as AMAudioTrack).addKey(aData, addCall, _frame, null, false);

        }
        else if(amTrack is AMPropertyTrack) {
            // property
            GameObject go = amTrack.GetTarget(aData) as GameObject;
            // if missing object, return
            if(!go) {
                showAlertMissingObjectType("GameObject");
                return;
            }
            // if missing property, return
            if(!(amTrack as AMPropertyTrack).isPropertySet()) {
                EditorUtility.DisplayDialog("Property Not Set", "You must set the track property before you can add keys.", "Okay");
                return;
            }
            (amTrack as AMPropertyTrack).addKey(aData, addCall, _frame);
        }
        else if(amTrack is AMEventTrack) {
            // event
            GameObject go = amTrack.GetTarget(aData) as GameObject;
            // if missing object, return
            if(!go) {
                showAlertMissingObjectType("GameObject");
                return;
            }
            // add key to event track
            (amTrack as AMEventTrack).addKey(aData, addCall, _frame);
        }
        else if(amTrack is AMCameraSwitcherTrack) {
            // camera switcher
            AMCameraSwitcherKey _cKey = null;
            int last_key = (amTrack as AMCameraSwitcherTrack).getKeyFrameBeforeFrame(_frame, false);
            if(last_key == -1) last_key = (amTrack as AMCameraSwitcherTrack).getKeyFrameAfterFrame(_frame, false);
            if(last_key != -1) {
                _cKey = ((amTrack as AMCameraSwitcherTrack).getKeyOnFrame(last_key) as AMCameraSwitcherKey);
            }
            // add key to camera switcher
            (amTrack as AMCameraSwitcherTrack).addKey(aData, addCall, _frame, null, _cKey);
        }
        else if(amTrack is AMGOSetActiveTrack) {
            // go set active
            GameObject go = amTrack.GetTarget(aData) as GameObject;
            // if missing object, return
            if(!go) {
                showAlertMissingObjectType("GameObject");
                return;
            }
            // add key to go active track
            (amTrack as AMGOSetActiveTrack).addKey(aData, addCall, _frame);
        }
        else if(amTrack is AMTriggerTrack) {
            (amTrack as AMTriggerTrack).addKey(aData, addCall, _frame);
        }

        AMTrack selectedTrack = TakeEditCurrent().getSelectedTrack(currentTake);
        if(selectedTrack) {
            EditorUtility.SetDirty(selectedTrack);
            setDirtyKeys(selectedTrack);
        }
        AMCodeView.refresh();
    }
    void deleteKeyFromSelectedFrame() {
        bool instantiated = MetaInstantiate("Clear Frame");
        AMTrack amTrack = TakeEditCurrent().getSelectedTrack(currentTake);
        if(!instantiated)
            recordUndoTrackAndKeys(amTrack, true, "Clear Frame");

        AMKey[] dkeys = amTrack.removeKeyOnFrame(currentTake.selectedFrame);
        if(!instantiated) {
            foreach(AMKey dkey in dkeys)
                Undo.DestroyObjectImmediate(dkey);
        }

        amTrack.updateCache(aData);

        // save data
        if(!instantiated) {
            EditorUtility.SetDirty(amTrack);
            setDirtyKeys(amTrack);
        }

        // select current frame
        timelineSelectFrame(TakeEditCurrent().selectedTrack, TakeEditCurrent().selectedFrame);

        AMCodeView.refresh();
    }
    void deleteSelectedKeys(bool showWarning) {
        bool shouldClearFrames = true;
        if(showWarning) {
            if(TakeEditCurrent().contextSelectionTracks.Count > 1) {
                if(!EditorUtility.DisplayDialog("Clear From Multiple Tracks?", "Are you sure you want to clear the selected frames from all of the selected tracks?", "Clear Frames", "Cancel")) {
                    shouldClearFrames = false;
                }
            }
        }
        if(shouldClearFrames) {
            bool instantiated = MetaInstantiate("Clear Frame");

            foreach(int track_id in TakeEditCurrent().contextSelectionTracks) {
                int trackInd = currentTake.getTrackIndex(track_id);
                if(trackInd == -1) continue;

                AMTrack amTrack = currentTake.trackValues[trackInd];

                if(!instantiated)
                    recordUndoTrackAndKeys(amTrack, true, "Clear Frames");

                AMKey[] dkeys = TakeEditCurrent().removeSelectedKeysFromTrack(currentTake, aData, track_id);
                if(!instantiated) {
                    foreach(AMKey dkey in dkeys)
                        Undo.DestroyObjectImmediate(dkey);

                    EditorUtility.SetDirty(amTrack);
                    setDirtyKeys(amTrack);
                }
            }
        }

        // select current frame
        timelineSelectFrame(TakeEditCurrent().selectedTrack, TakeEditCurrent().selectedFrame, false);

        AMCodeView.refresh();
    }

    #endregion

    #region Menus/Context

    void addTrackFromMenu(object type) {
        bool addCompUndo = !aData.e_metaCanInstantiatePrefab;
        registerTakesUndo(aData, "New Track", false);
        addTrack((int)type, addCompUndo);
    }
    void buildAddTrackMenu() {
        menu.AddItem(new GUIContent("Translation"), false, addTrackFromMenu, (int)Track.Translation);
        menu.AddItem(new GUIContent("Rotation"), false, addTrackFromMenu, (int)Track.Rotation);
        menu.AddItem(new GUIContent("Orientation"), false, addTrackFromMenu, (int)Track.Orientation);
        menu.AddItem(new GUIContent("Animation"), false, addTrackFromMenu, (int)Track.Animation);
        menu.AddItem(new GUIContent("Audio"), false, addTrackFromMenu, (int)Track.Audio);
        menu.AddItem(new GUIContent("Property"), false, addTrackFromMenu, (int)Track.Property);
        menu.AddItem(new GUIContent("Event"), false, addTrackFromMenu, (int)Track.Event);
        menu.AddItem(new GUIContent("GO Active"), false, addTrackFromMenu, (int)Track.GOSetActive);
        menu.AddItem(new GUIContent("Camera Switcher"), false, addTrackFromMenu, (int)Track.CameraSwitcher);
        menu.AddItem(new GUIContent("Trigger"), false, addTrackFromMenu, (int)Track.Trigger);
    }
    void buildAddTrackMenu_Drag() {
        bool hasTransform = true;
        bool hasAnimation = true;
        bool hasAudioSource = true;
        bool hasCamera = true;

        foreach(GameObject g in objects_window) {
            // break loop when all variables are false
            if(!hasTransform && !hasAnimation && !hasAudioSource && !hasCamera) break;

            if(hasTransform && !g.GetComponent(typeof(Transform))) {
                hasTransform = false;
            }
            if(hasAnimation && !g.GetComponent(typeof(Animation))) {
                hasAnimation = false;
            }
            if(hasAudioSource && !g.GetComponent(typeof(AudioSource))) {
                hasAudioSource = false;
            }
            if(hasCamera && !g.GetComponent(typeof(Camera))) {
                hasCamera = false;
            }
        }
        // add track menu
        menu_drag = new GenericMenu();

        // Translation
        if(hasTransform) { menu_drag.AddItem(new GUIContent("Translation"), false, addTrackFromMenu, (int)Track.Translation); }
        else { menu_drag.AddDisabledItem(new GUIContent("Translation")); }
        // Rotation
        if(hasTransform) { menu_drag.AddItem(new GUIContent("Rotation"), false, addTrackFromMenu, (int)Track.Rotation); }
        else { menu_drag.AddDisabledItem(new GUIContent("Rotation")); }
        // Orientation
        if(hasTransform) menu_drag.AddItem(new GUIContent("Orientation"), false, addTrackFromMenu, (int)Track.Orientation);
        else menu_drag.AddDisabledItem(new GUIContent("Orientation"));
        // Animation
        if(hasAnimation) menu_drag.AddItem(new GUIContent("Animation"), false, addTrackFromMenu, (int)Track.Animation);
        else menu_drag.AddDisabledItem(new GUIContent("Animation"));
        // Audio
        if(hasAudioSource) menu_drag.AddItem(new GUIContent("Audio"), false, addTrackFromMenu, (int)Track.Audio);
        else menu_drag.AddDisabledItem(new GUIContent("Audio"));
        // Property
        menu_drag.AddItem(new GUIContent("Property"), false, addTrackFromMenu, (int)Track.Property);
        // Event
        menu_drag.AddItem(new GUIContent("Event"), false, addTrackFromMenu, (int)Track.Event);
        // GO Active
        menu_drag.AddItem(new GUIContent("GO Active"), false, addTrackFromMenu, (int)Track.GOSetActive);
        // Camera Switcher
        if(hasCamera && !currentTake.cameraSwitcher) menu_drag.AddItem(new GUIContent("Camera Switcher"), false, addTrackFromMenu, (int)Track.CameraSwitcher);
        else menu_drag.AddDisabledItem(new GUIContent("Camera Switcher"));

        if(oData.quickAdd_Combos.Count > 0) {
            // multiple tracks
            menu_drag.AddSeparator("");
            foreach(List<int> combo in oData.quickAdd_Combos) {
                string combo_name = "";
                for(int i = 0; i < combo.Count; i++) {
                    //combo_name += Enum.GetName(typeof(Track),combo[i])+" ";
                    combo_name += TrackNames[combo[i]] + " "+(combo[i] == (int)Track.CameraSwitcher && currentTake.cameraSwitcher ? "(Key) " : "");
                    if(i < combo.Count - 1) combo_name += "+ ";
                }
                if(canQuickAddCombo(combo, hasTransform, hasAnimation, hasAudioSource, hasCamera)) menu_drag.AddItem(new GUIContent(combo_name), false, addTrackFromMenu, 100 + oData.quickAdd_Combos.IndexOf(combo));
                else menu_drag.AddDisabledItem(new GUIContent(combo_name));
            }
        }

    }
    bool canQuickAddCombo(List<int> combo, bool hasTransform, bool hasAnimation, bool hasAudioSource, bool hasCamera) {
        foreach(int _track in combo) {
            if(!hasTransform && (_track == (int)Track.Translation || _track == (int)Track.Rotation || _track == (int)Track.Orientation))
                return false;
            else if(!hasAnimation && _track == (int)Track.Animation)
                return false;
            else if(!hasAudioSource && _track == (int)Track.Audio)
                return false;
            else if(!hasCamera && _track == (int)Track.CameraSwitcher)
                return false;
        }
        return true;
    }
    bool canPaste() {
        bool canPaste = false;
        bool singleTrack = contextSelectionKeysBuffer.Count == 1;
        AMTrack selectedTrack = TakeEditCurrent().getSelectedTrack(currentTake);
        if(contextSelectionKeysBuffer.Count > 0) {
            if(singleTrack) {
                // if origin is property track
                if(selectedTrack is AMPropertyTrack) {
                    // if pasting into property track
                    if(contextSelectionTracksBuffer[0] is AMPropertyTrack) {
                        // if property tracks have the same property
                        if((selectedTrack as AMPropertyTrack).hasSamePropertyAs(aData, (contextSelectionTracksBuffer[0] as AMPropertyTrack))) {
                            canPaste = true;
                        }
                    }
                    // if origin is event track
                }
                else if(selectedTrack is AMEventTrack) {
                    // if pasting into event track
                    if(contextSelectionTracksBuffer[0] is AMEventTrack) {
                        // if event tracks are compaitable
                        if((selectedTrack as AMEventTrack).hasSameEventsAs(aData, (contextSelectionTracksBuffer[0] as AMEventTrack))) {
                            canPaste = true;
                        }
                    }
                }
                else if(selectedTrack) {
                    if(selectedTrack.getTrackType() == contextSelectionTracksBuffer[0].getTrackType()) {
                        canPaste = true;
                    }
                }
            }
            else {
                // to do
                if(contextSelectionTracksBuffer.Contains(selectedTrack)) canPaste = true;
            }
        }
        return canPaste;
    }
    void buildContextMenu(int frame) {
        AMTakeEdit takeEdit = TakeEditCurrent();
        contextMenuFrame = frame;
        contextMenu = new GenericMenu();
        bool selectionHasKeys = takeEdit.contextSelectionTracks.Count > 1 || takeEdit.contextSelectionHasKeys(currentTake);
        bool _canPaste = canPaste();

        contextMenu.AddItem(new GUIContent("Insert Keyframe"), false, invokeContextMenuItem, 0);
        contextMenu.AddSeparator("");
        if(selectionHasKeys) {
            contextMenu.AddItem(new GUIContent("Cut Frames"), false, invokeContextMenuItem, 1);
            contextMenu.AddItem(new GUIContent("Copy Frames"), false, invokeContextMenuItem, 2);
            if(_canPaste) contextMenu.AddItem(new GUIContent("Paste Frames"), false, invokeContextMenuItem, 3);
            else contextMenu.AddDisabledItem(new GUIContent("Paste Frames"));
            contextMenu.AddItem(new GUIContent("Clear Frames"), false, invokeContextMenuItem, 4);
        }
        else {
            contextMenu.AddDisabledItem(new GUIContent("Cut Frames"));
            contextMenu.AddDisabledItem(new GUIContent("Copy Frames"));
            if(_canPaste) contextMenu.AddItem(new GUIContent("Paste Frames"), false, invokeContextMenuItem, 3);
            else contextMenu.AddDisabledItem(new GUIContent("Paste Frames"));
            contextMenu.AddDisabledItem(new GUIContent("Clear Frames"));
        }
        contextMenu.AddItem(new GUIContent("Select All Frames"), false, invokeContextMenuItem, 5);
        if(selectionHasKeys) {
            contextMenu.AddSeparator("");
            contextMenu.AddItem(new GUIContent("Reverse Key Order"), false, invokeContextMenuItem, 6);
        }
    }
    void invokeContextMenuItem(object _index) {
        int index = (int)_index;
        // insert keyframe
        if(index == 0) {
            addKeyToFrame(contextMenuFrame);
            selectFrame(contextMenuFrame);
        }
        else if(index == 1) contextCutKeys();
        else if(index == 2) contextCopyFrames();
        else if(index == 3) contextPasteKeys();
        else if(index == 4) deleteSelectedKeys(true);
        else if(index == 5) contextSelectAllFrames();
        else if(index == 6) contextReverseKeys();
    }
    void contextReverseKeys() {
        bool instantiated = MetaInstantiate("Reverse Key Order");
        AMTakeEdit takeEdit = TakeEditCurrent();
        AMTakeData take = currentTake;
        foreach(int track_id in takeEdit.contextSelectionTracks) {
            AMTrack track = take.getTrack(track_id);

            if(!instantiated) recordUndoTrackAndKeys(track, true, "Reverse Key Order");

            AMKey[] keys = takeEdit.getContextSelectionKeysForTrack(take.getTrack(track_id));
            for(int i = 0; i < keys.Length/2; i++) {
                AMKey key = keys[i];
                AMKey rkey = keys[keys.Length-1-i];

                int frame = key.frame; key.frame = rkey.frame; rkey.frame = frame;
                int easeType = key.easeType; key.easeType = rkey.easeType; rkey.easeType = easeType;
                AnimationCurve easeCurve = key.easeCurve; key.easeCurve = rkey.easeCurve; rkey.easeCurve = easeCurve;
                List<float> customEase = key.customEase; key.customEase = rkey.customEase; rkey.customEase = customEase;
                float amp = key.amplitude; key.amplitude = rkey.amplitude; rkey.amplitude = amp;
                float period = key.period; key.period = rkey.period; rkey.period = period;
            }

            track.updateCache(aData);

            EditorUtility.SetDirty(track);
            setDirtyKeys(track);
        }
    }
    void contextCutKeys() {
        contextCopyFrames();
        deleteSelectedKeys(false);
    }
    void contextPasteKeys() {
        if(contextSelectionKeysBuffer == null || contextSelectionKeysBuffer.Count == 0) return;

        bool singleTrack = contextSelectionKeysBuffer.Count == 1;
        int offset = (int)contextSelectionRange.y - (int)contextSelectionRange.x + 1;

        bool addUndo = !MetaInstantiate("Paste Frames");

        if(singleTrack) {
            AMTrack track = TakeEditCurrent().getSelectedTrack(currentTake);

            if(addUndo) recordUndoTrackAndKeys(track, true, "Paste Frames");

            List<AMKey> newKeys = new List<AMKey>(contextSelectionKeysBuffer[0].Count);
            foreach(AMKey a in contextSelectionKeysBuffer[0]) {
                if(a != null) {
                    AMKey newKey = (addUndo ? Undo.AddComponent(track.gameObject, a.GetType()) : track.gameObject.AddComponent(a.GetType())) as AMKey;
                    a.CopyTo(newKey);
                    newKey.frame += (contextMenuFrame - (int)contextSelectionRange.x);
                    newKey.enabled = false;
                    newKeys.Add(newKey);
                }
            }

            track.offsetKeysFromBy(aData, contextMenuFrame, offset);

            setDirtyKeys(track);

            track.keys.AddRange(newKeys);

            EditorUtility.SetDirty(track);
        }
        else {
            List<AMKey> newKeys = new List<AMKey>();

            for(int i = 0; i < contextSelectionTracksBuffer.Count; i++) {
                AMTrack track = contextSelectionTracksBuffer[i];
                if(addUndo) recordUndoTrackAndKeys(track, true, "Paste Frames");

                foreach(AMKey a in contextSelectionKeysBuffer[i]) {
                    if(a != null) {
                        AMKey newKey = (addUndo ? Undo.AddComponent(track.gameObject, a.GetType()) : track.gameObject.AddComponent(a.GetType())) as AMKey;
                        a.CopyTo(newKey);
                        newKey.frame += (contextMenuFrame - (int)contextSelectionRange.x);
                        newKey.enabled = false;
                        newKeys.Add(newKey);
                    }
                }

                // offset all keys beyond paste
                track.offsetKeysFromBy(aData, contextMenuFrame, offset);

                setDirtyKeys(track);

                track.keys.AddRange(newKeys);

                EditorUtility.SetDirty(track);

                newKeys.Clear();
            }
        }


        // show message if there are out of bounds keys
        checkForOutOfBoundsFramesOnSelectedTrack();
        // update cache
        if(singleTrack) {
            TakeEditCurrent().getSelectedTrack(currentTake).updateCache(aData);
        }
        else {
            for(int i = 0; i < contextSelectionTracksBuffer.Count; i++) {
                contextSelectionTracksBuffer[i].updateCache(aData);
            }
        }
        AMCodeView.refresh();
        // clear buffer
        ClearKeysBuffer();
        contextSelectionTracksBuffer.Clear();
        // update selection
        //   retrieve cached context selection 
        TakeEditCurrent().contextSelection = new List<int>();
        foreach(int frame in cachedContextSelection) {
            TakeEditCurrent().contextSelection.Add(frame);
        }
        // offset selection
        for(int i = 0; i < TakeEditCurrent().contextSelection.Count; i++) {
            TakeEditCurrent().contextSelection[i] += (contextMenuFrame - (int)contextSelectionRange.x);

        }
        // copy again for multiple pastes
        contextCopyFrames();
    }
    void contextSaveKeysToBuffer() {
        if(TakeEditCurrent().contextSelection.Count <= 0) return;
        // sort
        TakeEditCurrent().contextSelection.Sort();
        // set selection range
        contextSelectionRange.x = TakeEditCurrent().contextSelection[0];
        contextSelectionRange.y = TakeEditCurrent().contextSelection[TakeEditCurrent().contextSelection.Count - 1];
        // set selection track
        //contextSelectionTrack = aData.getCurrentTake().selectedTrack;

        ClearKeysBuffer();
        TakeEditCurrent().contextSelectionTracks.Sort();
        contextSelectionTracksBuffer.Clear();

        foreach(int track_id in TakeEditCurrent().contextSelectionTracks) {
            contextSelectionTracksBuffer.Add(currentTake.getTrack(track_id));
        }
        foreach(AMTrack track in contextSelectionTracksBuffer) {
            contextSelectionKeysBuffer.Add(new List<AMKey>());
            foreach(AMKey key in TakeEditCurrent().getContextSelectionKeysForTrack(track)) {
                AMKey nkey = mTempHolder.AddComponent(key.GetType()) as AMKey;
                key.CopyTo(nkey);
                key.enabled = false;
                contextSelectionKeysBuffer[contextSelectionKeysBuffer.Count - 1].Add(nkey);
            }
        }
    }

    void contextCopyFrames() {
        cachedContextSelection.Clear();
        // cache context selection
        foreach(int frame in TakeEditCurrent().contextSelection) {
            cachedContextSelection.Add(frame);
        }
        // save keys
        contextSaveKeysToBuffer();
    }
    void contextSelectAllFrames() {
        registerTakesUndo(aData, "Select All Frames", false);
        TakeEditCurrent().contextSelectAllFrames(currentTake.numFrames);
    }
    public void contextSelectFrame(int frame, int prevFrame) {
        // select range if shift down
        if(isShiftDown) {
            // if control is down, toggle
            TakeEditCurrent().contextSelectFrameRange(prevFrame, frame);
            return;
        }
        // clear context selection if control is not down
        if(!isControlDown) TakeEditCurrent().contextSelection.Clear();
        // select single, toggle if control is down
        TakeEditCurrent().contextSelectFrame(frame, isControlDown);
        //contextSelectFrameRange(frame,frame);
    }
    public void contextSelectFrameRange(int startFrame, int endFrame) {
        // clear context selection if control is not down
        if(isShiftDown) {
            TakeEditCurrent().contextSelectFrameRange(currentTake.selectedFrame, endFrame);
            return;
        }
        if(!isControlDown) TakeEditCurrent().contextSelection.Clear();

        TakeEditCurrent().contextSelectFrameRange(startFrame, endFrame);
    }
    #endregion

    #region Other Fns

    public int findWidthFontSize(float width, GUIStyle style, GUIContent content, int min = 8, int max = 15) {
        // finds the largest font size that can fit in the given style. max 15
        style.fontSize = max;
        while(style.CalcSize(content).x > width) {
            style.fontSize--;
            if(style.fontSize <= min) break;
        }
        return style.fontSize;
    }
    public int findHeightFontSize(float height, GUIStyle style, GUIContent content, int min = 8, int max = 15) {
        style.fontSize = max;
        while(style.CalcSize(content).y > height) {
            style.fontSize--;
            if(style.fontSize <= min) break;
        }
        return style.fontSize;
    }
    public bool rectContainsMouse(Rect rect, Vector2 mousePosition) {
        if(mousePosition.x < rect.x || mousePosition.x > rect.x + rect.width) return false;
        if(mousePosition.y < rect.y || mousePosition.y > rect.y + rect.height) return false;
        return true;
    }
    public bool didDoubleClick(string elementID) {
        if(doubleClickElementID != elementID) {
            doubleClickCachedTime = EditorApplication.timeSinceStartup;
            doubleClickElementID = elementID;
            return false;
        }
        if(EditorApplication.timeSinceStartup - doubleClickCachedTime <= doubleClickTime) {
            doubleClickElementID = null;
            return true;
        }
        else {
            doubleClickCachedTime = EditorApplication.timeSinceStartup;
            return false;
        }
    }
    public void updateCachedMethodInfo(GameObject go) {
        if(!go) return;
        cachedMethodInfo = new List<MethodInfo>();
        cachedMethodNames = new List<string>();
        cachedMethodInfoComponents = new List<Component>();
        Component[] arrComponents = go.GetComponents(typeof(Component));
        foreach(Component c in arrComponents) {
            if(c.GetType().BaseType == typeof(Component) || c.GetType().BaseType == typeof(Behaviour)) continue;
            MethodInfo[] methodInfos = c.GetType().GetMethods(methodFlags);
            foreach(MethodInfo methodInfo in methodInfos) {
                if((methodInfo.Name == "Start") || (methodInfo.Name == "Update") || (methodInfo.Name == "Main")) continue;
                string methodSig = getMethodInfoSignature(methodInfo);
                if(!string.IsNullOrEmpty(methodSig)) {
                    cachedMethodNames.Add(methodSig);
                    cachedMethodInfo.Add(methodInfo);
                    cachedMethodInfoComponents.Add(c);
                }
            }
        }
    }

    //returns deleted keys
    AMKey[] checkForOutOfBoundsFramesOnSelectedTrack() {
        AMTakeData take = currentTake;
        List<AMKey> dkeys = new List<AMKey>();
        List<AMTrack> selectedTracks = new List<AMTrack>();
        int shift = 1;
        AMTrack _track_shift = null;
        int increase = 0;
        AMTrack _track_increase = null;
        foreach(int track_id in TakeEdit(take).contextSelectionTracks) {
            AMTrack track = take.getTrack(track_id);
            selectedTracks.Add(track);
            if(track.keys.Count >= 1) {
                if(track.keys[0].frame < shift) {
                    shift = track.keys[0].frame;
                    _track_shift = track;
                }
                if(track.keys[track.keys.Count - 1].frame - take.numFrames > increase) {
                    increase = track.keys[track.keys.Count - 1].frame - take.numFrames;
                    _track_increase = track;
                }
            }
        }
        if(_track_shift != null) {
            if(EditorUtility.DisplayDialog("Shift Frames?", "Keyframes have been moved out of bounds before the first frame. Some data will be lost if frames are not shifted.", "Shift", "No")) {
                TakeEdit(take).shiftOutOfBoundsKeysOnTrack(take, aData, _track_shift);
            }
            else {
                // delete all keys beyond last frame
                dkeys.AddRange(take.removeKeysBefore(aData, 1));
            }
        }
        if(_track_increase != null) {
            if(EditorUtility.DisplayDialog("Increase Number of Frames?", "Keyframes have been pushed out of bounds beyond the last frame. Some data will be lost if the number of frames is not increased.", "Increase", "No")) {
                take.numFrames = _track_increase.keys[_track_increase.keys.Count - 1].frame;
            }
            else {
                // delete all keys beyond last frame
                foreach(AMTrack track in selectedTracks)
                    dkeys.AddRange(track.removeKeysAfter(take.numFrames));
            }
        }

        return dkeys.ToArray();
    }
    /*void checkForOutOfBoundsFramesOnTrack(int track_id) {
        AMTrack _track = currentTake.getTrack(track_id);
        if(_track.keys.Count <= 0) return;
        bool needsShift = false;
        bool needsIncrease = false;
        _track.sortKeys();
        // if first key less than 1
        if(_track.keys.Count >= 1 && _track.keys[0].frame < 1) {
            needsShift = true;
        }
        if(needsShift) {
            if(EditorUtility.DisplayDialog("Shift Frames?", "Keyframes have been moved out of bounds before the first frame. Some data will be lost if frames are not shifted.", "Shift", "No")) {
                currentTake.shiftOutOfBoundsKeysOnSelectedTrack();
            }
            else {
                // delete all keys beyond last frame
                currentTake.deleteKeysBefore(1);
            }
        }
        // if last key is beyond last frame
        if(_track.keys.Count >= 1 && _track.keys[_track.keys.Count - 1].frame > currentTake.numFrames) {
            needsIncrease = true;
        }
        if(needsIncrease) {
            if(EditorUtility.DisplayDialog("Increase Number of Frames?", "Keyframes have been pushed out of bounds beyond the last frame. Some data will be lost if the number of frames is not increased.", "Increase", "No")) {
                currentTake.numFrames = _track.keys[_track.keys.Count - 1].frame;
            }
            else {
                // delete all keys beyond last frame
                _track.deleteKeysAfter(currentTake.numFrames);
            }
        }
    }*/
    void resetPreview() {
        // reset all object transforms to frame 1
        currentTake.previewFrame(aData, 1f);
    }
    bool isTextEditting() {
        return isRenamingTrack != -1 || isRenamingTake || isRenamingGroup != 0;
    }
    void cancelTextEditting(bool toggleIsRenamingTake = false) {
        if(!isChangingTimeControl && !isChangingFrameControl) {
            try {
                if(GUIUtility.keyboardControl != 0) GUIUtility.keyboardControl = 0;
            }
            catch {

            }
        }
        if(isRenamingTrack != -1) {
            isRenamingTrack = -1;
        }
        if(isRenamingTake) {
            aData.e_makeTakeNameUnique(currentTake);
        }
        if(toggleIsRenamingTake) {
            isRenamingTake = !isRenamingTake;
        }
        else {
            if(isRenamingTake) isRenamingTake = false;
        }
        if(isRenamingGroup != 0) isRenamingGroup = 0;
    }
    WrapMode indexToWrapMode(int index) {
        switch(index) {
            case 0:
                return WrapMode.Once;
            case 1:
                return WrapMode.Loop;
            case 2:
                return WrapMode.ClampForever;
            case 3:
                return WrapMode.PingPong;
            default:
                Debug.LogError("Animator: No Wrap Mode found for index " + index);
                return WrapMode.Default;
        }
    }
    string trimString(string _str, int max_chars) {
        if(_str.Length <= max_chars) return _str;
        return _str.Substring(0, max_chars) + "...";
    }
    string typeStringBrief(Type t) {
        if(t.IsArray) return typeStringBrief(t.GetElementType()) + "[]";
        if(t == typeof(int)) return "int";
        if(t == typeof(long)) return "long";
        if(t == typeof(float)) return "float";
        if(t == typeof(double)) return "double";
        if(t == typeof(Vector2)) return "Vector2";
        if(t == typeof(Vector3)) return "Vector3";
        if(t == typeof(Vector4)) return "Vector4";
        if(t == typeof(Color)) return "Color";
        if(t == typeof(Rect)) return "Rect";
        if(t == typeof(string)) return "string";
        if(t == typeof(char)) return "char";
        return t.Name;
    }
    int wrapModeToIndex(WrapMode wrapMode) {
        switch(wrapMode) {
            case WrapMode.Once:
                return 0;
            case WrapMode.Loop:
                return 1;
            case WrapMode.ClampForever:
                return 2;
            case WrapMode.PingPong:
                return 3;
            default:
                Debug.LogError("Animator: No Index found for WrapMode " + wrapMode.ToString());
                return -1;
        }
    }
    float maxScrollView() {
        return height_all_tracks;
    }

    #endregion

    #endregion
}

