using System;
using System.Collections.Generic;
using HuesNCues.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuesNCues.Game
{
    /// <summary>
    /// Renders the 480-cell color board on a uGUI Canvas and reports clicks. It only
    /// READS from the Core assembly (ColorBoard / GridCoordinate) and holds no game
    /// rules - keeping the "view" separate from the "brain", as the architecture says.
    ///
    /// Runs before MatchView (execution order) so its Canvas and board exist when the
    /// match wires itself up.
    ///
    /// Performance choices (why this stays cheap with 480 cells):
    ///   - Cells have raycastTarget = false; one invisible panel behind them catches
    ///     clicks, so a click tests 1 raycast target instead of 480.
    ///   - Cells are positioned manually once (no layout group re-running layout).
    ///   - All cells share the default UI material, so uGUI batches them together.
    ///   - Window resizing only changes one localScale (see FitBoard).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class BoardView : MonoBehaviour
    {
        [Header("Cell layout (tweak to restyle the board)")]
        [Tooltip("Width/height of each cell in UI pixels.")]
        [SerializeField] private float cellSize = 28f;

        [Tooltip("Gap between cells in UI pixels.")]
        [SerializeField] private float spacing = 2f;

        [Tooltip("Optional sprite used for every cell (e.g. rounded/bordered). Empty = solid square.")]
        [SerializeField] private Sprite cellSprite;

        [Tooltip("Optional material for every cell (Shader Graph must use the Canvas target, " +
                 "and multiply by Vertex Color to keep each cell's own color).")]
        [SerializeField] private Material cellMaterial;

        [Tooltip("Optional. Board is drawn under this Canvas; if left empty, one is created.")]
        [SerializeField] private Canvas canvas;

        [Header("Board area margins (reference px) — leave room for the HUD")]
        [Tooltip("Space for the status line at the top.")]
        [SerializeField] private float marginTop = 100f;
        [Tooltip("Space for the scoreboard on the right.")]
        [SerializeField] private float marginRight = 420f;
        [Tooltip("Space for the clue/next controls at the bottom.")]
        [SerializeField] private float marginBottom = 160f;
        [SerializeField] private float marginLeft = 40f;

        private ColorBoard _board;              // loaded from Resources/BoardData.csv
        private Image[] _cells;                 // indexed by row * Columns + col
        private RectTransform _boardArea;       // fills the canvas; the board is fitted inside it
        private RectTransform _boardPanel;      // fixed design-size grid, scaled to fit _boardArea
        private readonly List<GameObject> _markers = new List<GameObject>();
        private GridCoordinate? _highlighted;   // cell popped during the reveal

        private float Step => cellSize + spacing;

        /// <summary>Raised when a valid board cell is clicked.</summary>
        public event Action<GridCoordinate> CellClicked;

        // Read access for other view/logic code.
        public ColorBoard Board => _board;
        public Canvas Canvas => canvas;
        public Color ColorOf(GridCoordinate c) => _board.GetColor(c);
        public string NameOf(GridCoordinate c) => _board.GetName(c);

        private void Awake()
        {
            _board = LoadBoard();
            var cv = EnsureCanvasAndEventSystem();
            BuildBoard(cv.transform);
        }

        // ----- Markers & highlight (used during a match) ----------------------------

        /// <summary>Spawns a marker prefab centered on a cell, tinted and labelled.</summary>
        public GameObject PlaceMarker(GameObject prefab, GridCoordinate coord, Color color, string label)
        {
            if (prefab == null || _boardPanel == null) return null;

            var go = Instantiate(prefab);
            var rt = (RectTransform)go.transform;
            rt.SetParent(_boardPanel, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cellSize * 0.8f, cellSize * 0.8f);
            rt.anchoredPosition = CellCenter(coord);
            rt.SetAsLastSibling(); // draw above the cells

            var img = go.GetComponent<Image>();
            if (img != null) img.color = color;
            var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = label;

            _markers.Add(go);
            return go;
        }

        /// <summary>Removes every marker (called when a new round starts).</summary>
        public void ClearMarkers()
        {
            foreach (var m in _markers)
                if (m != null) Destroy(m);
            _markers.Clear();
        }

        /// <summary>Pops the target cell so it stands out at the reveal.</summary>
        public void ShowTarget(GridCoordinate coord)
        {
            ClearTargetHighlight();
            if (_cells == null || !_board.Contains(coord)) return;

            var rt = _cells[Index(coord)].rectTransform;
            rt.localScale = Vector3.one * 1.4f;
            rt.SetAsLastSibling();
            // Keep the markers on top of the enlarged target.
            foreach (var m in _markers)
                if (m != null) ((RectTransform)m.transform).SetAsLastSibling();

            _highlighted = coord;
        }

        public void ClearTargetHighlight()
        {
            if (_highlighted.HasValue && _cells != null)
                _cells[Index(_highlighted.Value)].rectTransform.localScale = Vector3.one;
            _highlighted = null;
        }

        /// <summary>Shows/hides the whole board (hidden on the menu/lobby, shown in a match).</summary>
        public void SetBoardVisible(bool visible)
        {
            if (_boardArea == null) return;
            _boardArea.gameObject.SetActive(visible);
            if (visible) FitBoard(); // re-fit in case the window resized while hidden
        }

        // ----- Loading & construction -----------------------------------------------

        private ColorBoard LoadBoard()
        {
            var asset = Resources.Load<TextAsset>("BoardData");
            if (asset == null)
            {
                Debug.LogWarning("BoardData not found in a Resources folder; using the procedural board.");
                return ColorBoard.CreateProcedural();
            }

            try
            {
                return BoardCsvParser.Parse(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse BoardData.csv ({e.Message}); using the procedural board.");
                return ColorBoard.CreateProcedural();
            }
        }

        private Canvas EnsureCanvasAndEventSystem()
        {
            if (canvas == null)
            {
                var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f; // balance width/height at extreme aspect ratios
            }

            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            return canvas;
        }

        private void BuildBoard(Transform canvasTransform)
        {
            float totalW = ColorBoard.Columns * Step - spacing; // no trailing gap
            float totalH = ColorBoard.Rows * Step - spacing;

            // An area that follows the canvas size but leaves room for the HUD (status
            // on top, scoreboard on the right, clue/next at the bottom). The board is
            // fitted inside it, so it stays fully visible and never overlaps the HUD,
            // at any window size / aspect ratio.
            var areaGO = new GameObject("BoardArea", typeof(RectTransform));
            _boardArea = areaGO.GetComponent<RectTransform>();
            _boardArea.SetParent(canvasTransform, false);
            _boardArea.anchorMin = Vector2.zero;
            _boardArea.anchorMax = Vector2.one;
            _boardArea.offsetMin = new Vector2(marginLeft, marginBottom);
            _boardArea.offsetMax = new Vector2(-marginRight, -marginTop);
            areaGO.AddComponent<RectResizeReceiver>().Init(FitBoard);

            // The panel: fixed design size, centered, and the single click catcher.
            var panelGO = new GameObject("BoardPanel", typeof(RectTransform), typeof(Image), typeof(BoardClickReceiver));
            _boardPanel = panelGO.GetComponent<RectTransform>();
            _boardPanel.SetParent(_boardArea, false);
            _boardPanel.anchorMin = _boardPanel.anchorMax = new Vector2(0.5f, 0.5f);
            _boardPanel.pivot = new Vector2(0.5f, 0.5f);
            _boardPanel.sizeDelta = new Vector2(totalW, totalH);

            var panelImage = panelGO.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0f); // invisible, but still catches raycasts
            panelImage.raycastTarget = true;
            panelGO.GetComponent<BoardClickReceiver>().Init(this);

            // The 480 cells.
            _cells = new Image[_board.CellCount];
            foreach (var coord in _board.AllCoordinates())
            {
                var cellGO = new GameObject($"Cell_{coord.Label}", typeof(RectTransform), typeof(Image));
                var rt = cellGO.GetComponent<RectTransform>();
                rt.SetParent(_boardPanel, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(cellSize, cellSize);
                rt.anchoredPosition = new Vector2(coord.Column * Step, -coord.Row * Step);

                var img = cellGO.GetComponent<Image>();
                img.sprite = cellSprite;         // null = solid square; assign one to restyle all cells
                img.type = Image.Type.Sliced;
                img.color = _board.GetColor(coord);
                img.raycastTarget = false; // the key optimization
                // One shared material across all cells keeps uGUI batching intact.
                if (cellMaterial != null) img.material = cellMaterial;

                _cells[Index(coord)] = img;
            }

            FitBoard();
        }

        private void FitBoard()
        {
            if (_boardArea == null || _boardPanel == null) return;

            float availW = _boardArea.rect.width;
            float availH = _boardArea.rect.height;
            float panelW = _boardPanel.rect.width;
            float panelH = _boardPanel.rect.height;
            if (availW <= 0f || availH <= 0f || panelW <= 0f || panelH <= 0f) return;

            float scale = Mathf.Min(availW / panelW, availH / panelH);
            _boardPanel.localScale = new Vector3(scale, scale, 1f);
        }

        // ----- Click handling -------------------------------------------------------

        /// <summary>Called by BoardClickReceiver when the panel is clicked.</summary>
        internal void OnBoardClicked(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _boardPanel, eventData.position, eventData.pressEventCamera, out Vector2 local);

            float fromLeft = local.x + _boardPanel.rect.width * 0.5f;
            float fromTop = _boardPanel.rect.height * 0.5f - local.y;

            int col = Mathf.FloorToInt(fromLeft / Step);
            int row = Mathf.FloorToInt(fromTop / Step);
            var coord = new GridCoordinate(col, row);
            if (!_board.Contains(coord)) return; // clicked in a gap/outside

            if (CellClicked != null)
            {
                CellClicked.Invoke(coord);      // a match is driving the board
            }
            else
            {
                // Standalone (no match): log the cell for quick exploration/debugging.
                Color c = _board.GetColor(coord);
                Debug.Log($"{coord.Label}  \"{_board.GetName(coord)}\"  #{ColorUtility.ToHtmlStringRGB(c)}");
            }
        }

        private static int Index(GridCoordinate c) => c.Row * ColorBoard.Columns + c.Column;

        private Vector2 CellCenter(GridCoordinate c) =>
            new Vector2(c.Column * Step + cellSize * 0.5f, -(c.Row * Step + cellSize * 0.5f));
    }

    /// <summary>
    /// Tiny helper that lives on the board panel and forwards clicks to BoardView.
    /// Kept separate so BoardView does not need to be the raycast target itself.
    /// </summary>
    public class BoardClickReceiver : MonoBehaviour, IPointerClickHandler
    {
        private BoardView _owner;
        public void Init(BoardView owner) => _owner = owner;
        public void OnPointerClick(PointerEventData eventData) => _owner?.OnBoardClicked(eventData);
    }

    /// <summary>
    /// Fires a callback whenever its RectTransform changes size - Unity calls
    /// OnRectTransformDimensionsChange on layout/resize. Used to re-fit the board.
    /// </summary>
    public class RectResizeReceiver : MonoBehaviour
    {
        private System.Action _onChange;
        public void Init(System.Action onChange) => _onChange = onChange;
        private void OnRectTransformDimensionsChange() => _onChange?.Invoke();
    }
}
