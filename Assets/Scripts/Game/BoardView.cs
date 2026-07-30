using System;
using System.Collections.Generic;
using ColorGuesser.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ColorGuesser.Game
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

        [Tooltip("Name of the board CSV inside a Resources folder (no extension). " +
                 "\"BoardGenerated\" is the palette produced by Tools > Generate Board Palette.")]
        [SerializeField] private string boardDataResource = "BoardData";

        [Tooltip("Prefab used for each of the 480 cells. Leave empty for a plain Image. " +
                 "Keep it light — it is instantiated 480 times.")]
        [SerializeField] private BoardCellView cellPrefab;

        [Tooltip("Prefab used for the row/column labels. Leave empty for plain text.")]
        [SerializeField] private TextMeshProUGUI labelPrefab;

        [Tooltip("Sprite applied to cells when no cell prefab is set. Empty = solid square.")]
        [SerializeField] private Sprite cellSprite;

        [Tooltip("Optional material for every cell (Shader Graph must use the Canvas target, " +
                 "and multiply by Vertex Color to keep each cell's own color).")]
        [SerializeField] private Material cellMaterial;

        [Header("Row / column labels (1-30 across, A-P down)")]
        [SerializeField] private bool showLabels = true;

        [Tooltip("Space reserved around the grid for the labels, in UI pixels.")]
        [SerializeField] private float labelGutter = 26f;

        [SerializeField] private Color labelColor = Color.white;

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
        private BoardCellView[] _cells;         // indexed by row * Columns + col
        private RectTransform _boardArea;       // fills the canvas; the board is fitted inside it
        private RectTransform _boardPanel;      // fixed design-size grid, scaled to fit _boardArea
        private readonly List<GameObject> _markers = new List<GameObject>();
        private GridCoordinate? _highlighted;   // cell popped during the reveal

        private float Step => cellSize + spacing;

        /// <summary>Space reserved around the grid for labels (0 when labels are off).</summary>
        private float Gutter => showLabels ? labelGutter : 0f;

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
            var go = SpawnMarker(prefab, coord, color, label);
            if (go != null) _markers.Add(go);
            return go;
        }

        private GameObject SpawnMarker(GameObject prefab, GridCoordinate coord, Color color, string label)
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

            var cell = _cells[Index(coord)];
            cell.SetHighlighted(true);
            var rt = (RectTransform)cell.transform;
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
            {
                var cell = _cells[Index(_highlighted.Value)];
                cell.SetHighlighted(false);
                ((RectTransform)cell.transform).localScale = Vector3.one;
            }
            _highlighted = null;
        }

        /// <summary>
        /// Moves the board into a container supplied by the UI (e.g. a RectTransform on
        /// the MatchHud prefab), so the layout is authored in the prefab instead of by
        /// the margins below. The board stretches to fill it and re-fits on resize.
        /// </summary>
        public void SetBoardContainer(RectTransform container)
        {
            if (container == null || _boardArea == null) return;

            _boardArea.SetParent(container, false);
            _boardArea.anchorMin = Vector2.zero;
            _boardArea.anchorMax = Vector2.one;
            _boardArea.offsetMin = Vector2.zero;
            _boardArea.offsetMax = Vector2.zero;
            FitBoard();
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
            string resource = string.IsNullOrWhiteSpace(boardDataResource) ? "BoardData" : boardDataResource;
            var asset = Resources.Load<TextAsset>(resource);
            if (asset == null)
            {
                Debug.LogWarning($"'{resource}' not found in a Resources folder; using the procedural board.");
                return ColorBoard.CreateProcedural();
            }

            try
            {
                return BoardCsvParser.Parse(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse {resource}.csv ({e.Message}); using the procedural board.");
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
            // The panel includes a gutter on every side for the row/column labels.
            _boardPanel.sizeDelta = new Vector2(totalW + 2f * Gutter, totalH + 2f * Gutter);

            var panelImage = panelGO.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0f); // invisible, but still catches raycasts
            panelImage.raycastTarget = true;
            panelGO.GetComponent<BoardClickReceiver>().Init(this);

            // The 480 cells.
            _cells = new BoardCellView[_board.CellCount];
            foreach (var coord in _board.AllCoordinates())
            {
                var cell = CreateCell(coord);
                var rt = (RectTransform)cell.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                // Centre pivot, positioned by the cell's centre: scaling the target cell
                // at the reveal then grows it outwards instead of down and to the right.
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cellSize, cellSize);
                rt.anchoredPosition = CellCenter(coord);

                cell.SetColor(_board.GetColor(coord));

                var img = cell.ColorImage;
                if (img != null)
                {
                    img.raycastTarget = false; // the key optimization
                    // One shared material across all cells keeps uGUI batching intact.
                    if (cellMaterial != null) img.material = cellMaterial;
                }

                _cells[Index(coord)] = cell;
            }

            if (showLabels) BuildLabels(totalW, totalH);
            FitBoard();
        }

        /// <summary>
        /// Column numbers (1-30) above and below the grid, row letters (A-P) to the left
        /// and right - matching the printed board and the authored CSV. They live inside
        /// the board panel, so they scale with it and never drift out of alignment.
        /// </summary>
        private void BuildLabels(float totalW, float totalH)
        {
            float fontSize = Mathf.Max(8f, cellSize * 0.55f);
            float half = Gutter * 0.5f;

            for (int col = 0; col < ColorBoard.Columns; col++)
            {
                float x = Gutter + col * Step + cellSize * 0.5f;
                string text = (col + 1).ToString();
                MakeLabel(text, new Vector2(x, -half), fontSize);                        // top
                MakeLabel(text, new Vector2(x, -(Gutter + totalH + half)), fontSize);    // bottom
            }

            for (int row = 0; row < ColorBoard.Rows; row++)
            {
                float y = -(Gutter + row * Step + cellSize * 0.5f);
                string text = ((char)('A' + row)).ToString();
                MakeLabel(text, new Vector2(half, y), fontSize);                          // left
                MakeLabel(text, new Vector2(Gutter + totalW + half, y), fontSize);        // right
            }
        }

        /// <summary>Creates a cell from the prefab, or a plain Image if none is set.</summary>
        private BoardCellView CreateCell(GridCoordinate coord)
        {
            if (cellPrefab != null)
            {
                var instance = Instantiate(cellPrefab, _boardPanel);
                instance.name = $"Cell_{coord.Label}";
                return instance;
            }

            var go = new GameObject($"Cell_{coord.Label}", typeof(RectTransform), typeof(Image), typeof(BoardCellView));
            go.transform.SetParent(_boardPanel, false);
            var img = go.GetComponent<Image>();
            img.sprite = cellSprite;   // null = solid square
            img.type = Image.Type.Sliced;
            return go.GetComponent<BoardCellView>();
        }

        private void MakeLabel(string text, Vector2 anchoredPosition, float fontSize)
        {
            TextMeshProUGUI label;
            if (labelPrefab != null)
            {
                label = Instantiate(labelPrefab, _boardPanel);
            }
            else
            {
                var go = new GameObject("Label", typeof(RectTransform));
                go.transform.SetParent(_boardPanel, false);
                label = go.AddComponent<TextMeshProUGUI>();
                label.fontSize = fontSize;
                label.alignment = TextAlignmentOptions.Center;
                label.color = labelColor;
            }

            label.name = $"Label_{text}";
            label.text = text;
            label.raycastTarget = false;

            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Gutter * 1.6f, Gutter);
            rt.anchoredPosition = anchoredPosition;
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

            // Shift to the panel's top-left, then past the label gutter to the grid.
            float fromLeft = local.x + _boardPanel.rect.width * 0.5f - Gutter;
            float fromTop = _boardPanel.rect.height * 0.5f - local.y - Gutter;

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
            new Vector2(Gutter + c.Column * Step + cellSize * 0.5f,
                        -(Gutter + c.Row * Step + cellSize * 0.5f));
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
