using HuesNCues.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuesNCues.Game
{
    /// <summary>
    /// Renders the 480-cell color board on a uGUI Canvas and reports the cell you
    /// click. It only READS from the Core assembly (ColorBoard / GridCoordinate) -
    /// it holds no game rules itself. That keeps the "view" and the "brain" separate,
    /// exactly as the architecture in the proposal describes.
    ///
    /// Setup: create an empty GameObject in a scene, add this component, press Play.
    /// It builds (or finds) a Canvas and an EventSystem for you.
    ///
    /// Performance choices (why this stays cheap even with 480 cells):
    ///   - Cells have raycastTarget = false. A single invisible panel behind them
    ///     catches clicks, so every click tests 1 raycast target instead of 480.
    ///   - Cells are positioned manually once, so there is no layout group re-running
    ///     a layout pass every time something changes.
    ///   - All cells share the default UI material, so uGUI batches them together.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [Header("Cell layout (tweak to restyle the board)")]
        [Tooltip("Width/height of each cell in UI pixels.")]
        [SerializeField] private float cellSize = 28f;

        [Tooltip("Gap between cells in UI pixels.")]
        [SerializeField] private float spacing = 2f;

        [Tooltip("Optional. Board is drawn under this Canvas; if left empty, one is created.")]
        [SerializeField] private Canvas canvas;

        [Tooltip("Empty space (in reference pixels) kept around the board when fitting it to the window.")]
        [SerializeField] private float screenPadding = 40f;

        private ColorBoard _board;              // loaded from Resources/BoardData.csv
        private Image[] _cells;                 // indexed by row * Columns + col
        private RectTransform _boardArea;       // fills the canvas; the board is fitted inside it
        private RectTransform _boardPanel;      // fixed design-size grid, scaled to fit _boardArea
        private TextMeshProUGUI _readout;

        private GridCoordinate? _selected;      // null until the first click

        private float Step => cellSize + spacing;

        private void Start()
        {
            _board = LoadBoard();
            var canvas = EnsureCanvasAndEventSystem();
            BuildBoard(canvas.transform);
            BuildReadout(canvas.transform);
        }

        /// <summary>
        /// Loads the authored board from Assets/Resources/BoardData.csv. If the file
        /// is missing or malformed we fall back to the procedural board so the game
        /// still runs (and we log why).
        /// </summary>
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
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to parse BoardData.csv ({e.Message}); using the procedural board.");
                return ColorBoard.CreateProcedural();
            }
        }

        // ----- One-time construction ------------------------------------------------

        private Canvas EnsureCanvasAndEventSystem()
        {
            // Use the Canvas assigned in the inspector; if none was set, create one.
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

            // Clicks need an active EventSystem. EventSystem.current avoids a scene search.
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            return canvas;
        }

        private void BuildBoard(Transform canvasTransform)
        {
            float totalW = ColorBoard.Columns * Step - spacing; // no trailing gap
            float totalH = ColorBoard.Rows * Step - spacing;

            // A full-screen area that follows the canvas size. The board is fitted
            // inside it, so it stays fully visible at any window size / aspect ratio.
            var areaGO = new GameObject("BoardArea", typeof(RectTransform));
            _boardArea = areaGO.GetComponent<RectTransform>();
            _boardArea.SetParent(canvasTransform, false);
            _boardArea.anchorMin = Vector2.zero;
            _boardArea.anchorMax = Vector2.one;
            _boardArea.offsetMin = new Vector2(screenPadding, screenPadding);
            _boardArea.offsetMax = new Vector2(-screenPadding, -screenPadding);
            // Re-fit the board whenever this area's size changes (i.e. the window resizes).
            areaGO.AddComponent<RectResizeReceiver>().Init(FitBoard);

            // The panel: fixed design size, centered in the area, and the single click
            // catcher for the whole grid.
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

                // Anchor every cell to the panel's TOP-LEFT corner and offset from there.
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(cellSize, cellSize);
                rt.anchoredPosition = new Vector2(coord.Column * Step, -coord.Row * Step);

                var img = cellGO.GetComponent<Image>();
                img.color = _board.GetColor(coord);
                img.raycastTarget = false; // <-- the key optimization

                _cells[Index(coord)] = img;
            }

            FitBoard(); // size the board to the current window
        }

        /// <summary>
        /// Scales the whole board (one transform) so the fixed-size grid fits inside
        /// the available area, keeping its aspect ratio and staying centered. Called
        /// once after building and again on every window resize.
        /// </summary>
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

        private void BuildReadout(Transform canvasTransform)
        {
            var go = new GameObject("Readout", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvasTransform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -40f);
            rt.sizeDelta = new Vector2(600f, 50f);

            // TextMeshPro uses its default font asset (imported via TMP Essentials).
            _readout = go.AddComponent<TextMeshProUGUI>();
            _readout.fontSize = 28;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = Color.white;
            _readout.text = "Click a cell";
        }

        // ----- Click handling -------------------------------------------------------

        /// <summary>Called by BoardClickReceiver when the panel is clicked.</summary>
        internal void OnBoardClicked(PointerEventData eventData)
        {
            // Convert the screen click into a local point inside the panel rect.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _boardPanel, eventData.position, eventData.pressEventCamera, out Vector2 local);

            // The panel pivot is its center, so shift to top-left origin, then divide by Step.
            float fromLeft = local.x + _boardPanel.rect.width * 0.5f;
            float fromTop = _boardPanel.rect.height * 0.5f - local.y;

            int col = Mathf.FloorToInt(fromLeft / Step);
            int row = Mathf.FloorToInt(fromTop / Step);
            var coord = new GridCoordinate(col, row);

            if (!_board.Contains(coord)) return; // clicked in the gap/outside
            Select(coord);
        }

        private void Select(GridCoordinate coord)
        {
            // Restore the previously selected cell to its normal size.
            if (_selected.HasValue)
                _cells[Index(_selected.Value)].rectTransform.localScale = Vector3.one;

            _selected = coord;

            // Pop the selected cell: scale it up and draw it above its neighbors.
            var img = _cells[Index(coord)];
            img.rectTransform.localScale = Vector3.one * 1.25f;
            img.rectTransform.SetAsLastSibling();

            Color c = _board.GetColor(coord);
            string name = _board.GetName(coord);
            string msg = $"{coord.Label}   \"{name}\"   #{ColorUtility.ToHtmlStringRGB(c)}";
            if (_readout != null) _readout.text = msg;
            Debug.Log(msg);
        }

        // ----- Helpers --------------------------------------------------------------

        private static int Index(GridCoordinate c) => c.Row * ColorBoard.Columns + c.Column;
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
    /// OnRectTransformDimensionsChange on layout/resize. Used to re-fit the board
    /// when the window (and therefore the full-screen area) changes size.
    /// </summary>
    public class RectResizeReceiver : MonoBehaviour
    {
        private System.Action _onChange;
        public void Init(System.Action onChange) => _onChange = onChange;
        private void OnRectTransformDimensionsChange() => _onChange?.Invoke();
    }
}
