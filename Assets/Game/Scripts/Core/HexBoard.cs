using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class HexBoard : MonoBehaviour
{
    public float duration;


    [Header("Random Pack")]
    public RandomPackConfigSO randomPack;
    public int randomSeed = 0; // 0 = her çalıştırmada farklı
    public bool chooseRandomAnchorEachPack = true;

    [Header("Board Layout (Optional)")]
    public BoardLayoutSO boardLayout;

    [Header("Set Anchors (Pack spawn point)")]
    public Transform[] setAnchors;
    public float handSlotSpacing = 1.8f;

    [Header("Board")]
    public int boardRadius = 2;
    public float yCell = 0f;
    public HexCell cellPrefab;
    public Transform cellsRoot;

    [Header("Tiles")]
    public Transform tileRoot;
    public GameObject tilePrefab;
    public float tileHeight = 0.2f;
    public int clearCount = 10;
    public Material[] colorMaterials;

    [Header("Hand Piece Collider")]
    public Vector3 handPieceColliderSize = new Vector3(1.2f, 2.0f, 1.2f);

    [Header("Limits")]
    public int maxStackHeight = 30;

    [Header("Drag Height")]
    public float dragGhostY = 0.8f;

    [Header("Score")]
    public ScoreManager scoreManager;

    [Header("Drop Preview")]
    public GameObject dropIndicatorPrefab;
    public float dropIndicatorY = 0.02f;
    GameObject dropIndicator;

    readonly Dictionary<Hex, HexCell> cells = new Dictionary<Hex, HexCell>();
    Camera cam;

    // current pack state
    System.Random rng;
    Transform activeAnchor;
    int piecesLeftInPack;

    // drag state
    HexCell dragFromCell;
    HandPiece dragFromPiece;
    GameObject dragGhost;
    GameObject hiddenDragSource;

    // touch release fix
    Vector2 lastPointerPos;
    bool hasPointerPos;

    // resolve queue system (place while merging)
    bool resolveRunning;
    bool resolveRequested;
    readonly Queue<HexCell> resolveQueue = new Queue<HexCell>();
    readonly HashSet<HexCell> queued = new HashSet<HexCell>();
    readonly HashSet<HexCell> busyCells = new HashSet<HexCell>();

    bool hasFailed;

    void Awake()
    {
        cam = Camera.main;

        if (!ValidateSetup())
        {
            enabled = false;
            return;
        }

        rng = (randomSeed == 0) ? new System.Random() : new System.Random(randomSeed);

        CreateDropIndicator();

        // hand slotları her anchor için hazırla
        for (int i = 0; i < setAnchors.Length; i++)
            EnsureHandSlots(setAnchors[i]);

        BuildBoardEmpty();
        SyncAllCells();

        GenerateNextPack();
    }

    bool ValidateSetup()
    {
        if (Camera.main == null) { Debug.LogError("Main Camera missing/tag not MainCamera."); return false; }
        if (!randomPack) { Debug.LogError("randomPack is not assigned."); return false; }
        if (setAnchors == null || setAnchors.Length == 0 || setAnchors.Any(a => a == null)) { Debug.LogError("setAnchors missing."); return false; }
        if (!cellsRoot) { Debug.LogError("cellsRoot not assigned."); return false; }
        if (!tileRoot) { Debug.LogError("tileRoot not assigned."); return false; }
        if (!cellPrefab) { Debug.LogError("cellPrefab not assigned."); return false; }
        if (!cellPrefab.GetComponentInChildren<Collider>()) { Debug.LogError("cellPrefab needs Collider for raycast."); return false; }
        if (!tilePrefab) { Debug.LogError("tilePrefab not assigned."); return false; }
        if (colorMaterials == null || colorMaterials.Length == 0) { Debug.LogError("colorMaterials missing/empty."); return false; }
        return true;
    }

    void CreateDropIndicator()
    {
        if (!dropIndicatorPrefab) return;

        dropIndicator = Instantiate(dropIndicatorPrefab);
        dropIndicator.name = "DropIndicator";
        dropIndicator.SetActive(false);

        foreach (var col in dropIndicator.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
    }

    // ---------------- BOARD BUILD ----------------
    void BuildBoardEmpty()
    {
        cells.Clear();

        IEnumerable<Hex> coords;
        if (boardLayout != null && boardLayout.cells != null && boardLayout.cells.Count > 0)
        {
            coords = boardLayout.cells
                .Where(row => row != null && row.items != null)
                .SelectMany(row => row.items)
                .Select(c => c.ToHex());
        }
        else
        {
            coords = Hex.Spiral(Hex.zero, 0, boardRadius);
        }

        foreach (var h in coords)
        {
            var cell = Instantiate(cellPrefab, cellsRoot);
            cell.Init(h);
            cell.transform.position = h.ToWorld(yCell);
            cell.Stack.SetTiles(System.Array.Empty<TileColor>());
            cells[h] = cell;
        }
    }

    // ---------------- PACK GENERATION ----------------
    void GenerateNextPack()
    {
        if (chooseRandomAnchorEachPack)
            activeAnchor = setAnchors[rng.Next(0, setAnchors.Length)];
        else if (activeAnchor == null)
            activeAnchor = setAnchors[0];

        piecesLeftInPack = Mathf.Max(1, randomPack.piecesPerPack);

        ClearAnchorHand(activeAnchor);

        // 3 slot varsayıyoruz (HandSlot0..2)
        for (int i = 0; i < 3; i++)
        {
            var slot = activeAnchor.Find($"HandSlot{i}");
            if (!slot) continue;

            var tiles = randomPack.GeneratePiece(rng);
            SpawnHandPiece(slot, tiles);
        }
    }

    void ClearAnchorHand(Transform anchor)
    {
        for (int i = 0; i < 3; i++)
        {
            var slot = anchor.Find($"HandSlot{i}");
            if (!slot) continue;
            for (int c = slot.childCount - 1; c >= 0; c--)
                Destroy(slot.GetChild(c).gameObject);
        }
    }

    void EnsureHandSlots(Transform anchor)
    {
        for (int i = 0; i < 3; i++)
        {
            var t = anchor.Find($"HandSlot{i}");
            if (!t)
            {
                var go = new GameObject($"HandSlot{i}");
                t = go.transform;
                t.SetParent(anchor, false);
            }
            t.localPosition = new Vector3((i - 1) * handSlotSpacing, 0f, 0f);
            t.localRotation = Quaternion.identity;
        }
    }

    void SpawnHandPiece(Transform slot, List<TileColor> tiles)
    {
        var go = new GameObject("HandPiece");
        go.transform.SetParent(slot, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var box = go.AddComponent<BoxCollider>();
        box.size = handPieceColliderSize;
        box.center = new Vector3(0, handPieceColliderSize.y * 0.5f, 0);

        var piece = go.AddComponent<HandPiece>();
        piece.SetTiles(tiles);

        BuildGhostFromTiles(piece.tiles, go.transform);
    }

    // ---------------- UPDATE ----------------
    void Update()
    {
        if (hasFailed) return;

        if (TryGetPointerScreenPos(out var sp))
        {
            lastPointerPos = sp;
            hasPointerPos = true;
        }

        if (!hasPointerPos) return;

        if (WasPressedThisFrame()) TryBeginDrag(lastPointerPos);
        if (IsPressed()) DragUpdate(lastPointerPos);
        if (WasReleasedThisFrame()) TryEndDrag(lastPointerPos);
    }

    // ---------------- DRAG ----------------
    void TryBeginDrag(Vector2 screenPos)
    {
        if (RaycastHandPiece(screenPos, out var piece) && piece != null && piece.tiles.Count > 0)
        {
            dragFromPiece = piece;
            dragFromCell = null;

            hiddenDragSource = piece.gameObject;
            SetHandPieceVisible(hiddenDragSource, false);

            dragGhost = new GameObject("DragGhost_Piece");
            var p = piece.transform.position;
            dragGhost.transform.position = new Vector3(p.x, dragGhostY, p.z);

            BuildGhostFromTiles(piece.tiles, dragGhost.transform);
            UpdateDropPreview(screenPos);
            return;
        }
        /*
        if (allowMoveOnBoard && RaycastCell(screenPos, out var cell) && cell != null && !cell.Stack.IsEmpty)
        {
            dragFromCell = cell;
            dragFromPiece = null;

            dragGhost = new GameObject("DragGhost_Cell");
            var p = cell.transform.position;
            dragGhost.transform.position = new Vector3(p.x, dragGhostY, p.z);

            int topRun = cell.Stack.TopRunCount();
            var tiles = cell.Stack.Snapshot().Skip(cell.Stack.Count - topRun).ToList();
            BuildGhostFromTiles(tiles, dragGhost.transform);

            UpdateDropPreview(screenPos);
        }*/
    }

    void DragUpdate(Vector2 screenPos)
    {
        if (!dragGhost) { HideDropPreview(); return; }

        var plane = new Plane(Vector3.up, Vector3.up * yCell);
        var ray = cam.ScreenPointToRay(screenPos);
        if (plane.Raycast(ray, out float enter))
        {
            var p = ray.GetPoint(enter);
            dragGhost.transform.position = new Vector3(p.x, dragGhostY, p.z);
        }

        UpdateDropPreview(screenPos);
    }

    void TryEndDrag(Vector2 screenPos)
    {
        HideDropPreview();

        if (dragFromPiece == null && dragFromCell == null) { CleanupDrag(); return; }
        if (!RaycastCell(screenPos, out var toCell) || toCell == null) { CleanupDrag(); return; }
        if (!CanDropOnCell(toCell)) { CleanupDrag(); return; }

        // HandPiece -> board
        if (dragFromPiece != null)
        {
            var tiles = dragFromPiece.tiles;
            if (tiles == null || tiles.Count == 0) { CleanupDrag(); return; }
            if (toCell.Stack.Count + tiles.Count > maxStackHeight) { CleanupDrag(); return; }

            toCell.Stack.PushMany(tiles);
            SyncCellViews(toCell);

            // remove handpiece
            hiddenDragSource = null;
            Destroy(dragFromPiece.gameObject);
            dragFromPiece = null;

            CleanupDrag();

            // pack counter (only on successful placement)
            piecesLeftInPack--;
            if (piecesLeftInPack <= 0)
                GenerateNextPack();

            // resolve request
            EnqueueResolveAround(toCell);
            RequestResolve();

            // fail check
            CheckFailNow();
            return;
        }

        // Board move (optional)
        if (dragFromCell != null)
        {
            if (dragFromCell.coord.DistanceTo(toCell.coord) != 1) { CleanupDrag(); return; }
            StartCoroutine(MoveTopRunCellToCell(dragFromCell, toCell));
            CleanupDrag();
        }
    }

    void CleanupDrag()
    {
        if (dragGhost) Destroy(dragGhost);
        dragGhost = null;

        if (hiddenDragSource != null)
        {
            SetHandPieceVisible(hiddenDragSource, true);
            hiddenDragSource = null;
        }

        dragFromCell = null;
        dragFromPiece = null;
        HideDropPreview();
    }

    void SetHandPieceVisible(GameObject go, bool visible)
    {
        if (!go) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = visible;
    }

    // ---------------- DROP PREVIEW ----------------
    bool CanDropOnCell(HexCell cell)
    {
        if (cell == null || cell.Stack == null) return false;
        if (busyCells.Contains(cell)) return false;
        if (!cell.Stack.IsEmpty) return false;
        return true;
    }

    void UpdateDropPreview(Vector2 screenPos)
    {
        if (!dropIndicator) return;

        bool dragging = (dragGhost != null) && (dragFromPiece != null || dragFromCell != null);
        if (!dragging) { dropIndicator.SetActive(false); return; }

        if (RaycastCell(screenPos, out var cell) && cell != null && CanDropOnCell(cell))
        {
            dropIndicator.SetActive(true);
            var p = cell.transform.position;
            dropIndicator.transform.position = new Vector3(p.x, yCell + dropIndicatorY, p.z);
            dropIndicator.transform.rotation = Quaternion.identity;
        }
        else
        {
            dropIndicator.SetActive(false);
        }
    }
    
    void HideDropPreview()
    {
        if (dropIndicator) dropIndicator.SetActive(false);
    }

    // ---------------- FAIL ----------------
    bool IsBoardFull()
    {
        foreach (var cell in cells.Values)
            if (cell != null && cell.Stack != null && cell.Stack.IsEmpty)
                return false;
        return true;
    }

    void CheckFailNow()
    {
        if (hasFailed) return;

        if (IsBoardFull())
        {
            hasFailed = true;
            CleanupDrag();
            scoreManager?.ShowFailed();
        }
    }

    // ---------------- RESOLVE (place while merging) ----------------
    void RequestResolve()
    {
        resolveRequested = true;
        if (!resolveRunning)
            StartCoroutine(ResolveLoop());
    }

    void EnqueueResolve(HexCell c)
    {
        if (c == null) return;
        if (queued.Add(c))
            resolveQueue.Enqueue(c);
    }

    void EnqueueResolveAround(HexCell c)
    {
        EnqueueResolve(c);
        foreach (var n in c.coord.Neighbours())
            if (cells.TryGetValue(n, out var nb)) EnqueueResolve(nb);
    }

    IEnumerator ResolveLoop()
    {
        resolveRunning = true;

        while (true)
        {
            resolveRequested = false;

            bool didMove = true;
            int safety = 0;

            while (didMove)
            {
                didMove = false;
                if (++safety > 2000) break;

                // seed
                HexCell seed = null;
                while (resolveQueue.Count > 0 && seed == null)
                {
                    var c = resolveQueue.Dequeue();
                    queued.Remove(c);
                    if (c != null && !c.Stack.IsEmpty) seed = c;
                }

                if (seed == null)
                    seed = cells.Values.FirstOrDefault(x => x != null && !x.Stack.IsEmpty);

                if (seed == null) break;

                var top = seed.Stack.TopColor;
                if (top == null) continue;

                var group = CollectConnectedTopColorGroup(seed, top.Value);
                if (group.Count <= 1) continue;

                var sink = ChooseSinkByDegree(group);
                var sources = group.Where(x => x != sink)
                                   .OrderBy(x => x.coord.q).ThenBy(x => x.coord.r)
                                   .ToList();

                foreach (var src in sources)
                {
                    if (src == null || src.Stack.IsEmpty) continue;
                    var srcTop = src.Stack.TopColor;
                    if (srcTop == null || srcTop.Value != top.Value) continue;

                    if (busyCells.Contains(src) || busyCells.Contains(sink)) continue;

                    var pack = src.Stack.PopTopRun();
                    if (pack.Count == 0) continue;

                    if (sink.Stack.Count + pack.Count > maxStackHeight)
                    {
                        src.Stack.PushMany(pack);
                        continue;
                    }

                    sink.Stack.PushMany(pack);
                    yield return StartCoroutine(AnimateMoveViews(src, sink, pack.Count));

                    EnqueueResolveAround(sink);
                    EnqueueResolveAround(src);

                    didMove = true;
                    break; // ✅ sırayla: her loop’ta tek transfer
                }
            }

            // CLEAR (tam paket)
            bool clearedAny = false;
            foreach (var cell in cells.Values)
            {
                if (cell == null || cell.Stack.IsEmpty) continue;

                int run = cell.Stack.TopRunCount();
                if (run < clearCount) continue;

                SyncCellViews(cell);

                var removed = cell.Stack.PopTopRun();
                if (removed.Count < clearCount)
                {
                    cell.Stack.PushMany(removed);
                    continue;
                }

                int start = Mathf.Max(0, cell.views.Count - removed.Count);
                var vanishList = cell.views.Skip(start).ToList();
                cell.views.RemoveRange(start, vanishList.Count);

                scoreManager?.AddScore(vanishList.Count);

                float dur = 0.2f, stepDelay = 0.01f;
                for (int i = 0; i < vanishList.Count; i++)
                {
                    var go = vanishList[i];
                    float delay = i * stepDelay;

                    LeanTween.cancel(go);
                    LeanTween.scale(go, Vector3.zero, dur)
                        .setEase(LeanTweenType.easeInBack)
                        .setDelay(delay)
                        .setOnComplete(() => Destroy(go));
                }

                float total = vanishList.Count > 0 ? ((vanishList.Count - 1) * stepDelay + dur) : dur;
                yield return new WaitForSeconds(total);

                SyncCellViews(cell);
                EnqueueResolveAround(cell);
                clearedAny = true;
            }

            if (resolveRequested || clearedAny || resolveQueue.Count > 0)
                continue;

            break;
        }

        resolveRunning = false;
        CheckFailNow();
    }

    List<HexCell> CollectConnectedTopColorGroup(HexCell start, TileColor color)
    {
        var result = new List<HexCell>();
        var visited = new HashSet<HexCell>();
        var q = new Queue<HexCell>();

        visited.Add(start);
        q.Enqueue(start);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            result.Add(cur);

            foreach (var n in cur.coord.Neighbours())
            {
                if (!cells.TryGetValue(n, out var nb) || nb == null) continue;
                if (visited.Contains(nb)) continue;
                if (nb.Stack.IsEmpty) continue;

                var nbTop = nb.Stack.TopColor;
                if (nbTop == null || nbTop.Value != color) continue;

                visited.Add(nb);
                q.Enqueue(nb);
            }
        }

        return result;
    }

    HexCell ChooseSinkByDegree(List<HexCell> group)
    {
        var set = new HashSet<HexCell>(group);

        int Degree(HexCell c)
        {
            int d = 0;
            foreach (var n in c.coord.Neighbours())
                if (cells.TryGetValue(n, out var nb) && nb != null && set.Contains(nb))
                    d++;
            return d;
        }

        HexCell best = group[0];
        int bestDeg = Degree(best);
        int bestRun = best.Stack.TopRunCount();
        int bestCnt = best.Stack.Count;

        for (int i = 1; i < group.Count; i++)
        {
            var c = group[i];
            int deg = Degree(c);
            int run = c.Stack.TopRunCount();
            int cnt = c.Stack.Count;

            bool better =
                (deg > bestDeg) ||
                (deg == bestDeg && run > bestRun) ||
                (deg == bestDeg && run == bestRun && cnt > bestCnt) ||
                (deg == bestDeg && run == bestRun && cnt == bestCnt &&
                 (c.coord.q < best.coord.q || (c.coord.q == best.coord.q && c.coord.r < best.coord.r)));

            if (better)
            {
                best = c;
                bestDeg = deg;
                bestRun = run;
                bestCnt = cnt;
            }
        }

        return best;
    }

    IEnumerator AnimateMoveViews(HexCell from, HexCell to, int packCount)
    {
        busyCells.Add(from);
        busyCells.Add(to);

        duration = 0.2f;
           float stepDelay = 0.01f;

        int takeFrom = Mathf.Max(0, from.views.Count - packCount);
        var moving = from.views.Skip(takeFrom).ToList();
        from.views.RemoveRange(takeFrom, moving.Count);

        int toStart = to.views.Count;
        to.views.AddRange(moving);

        for (int i = 0; i < moving.Count; i++)
        {
            var go = moving[i];
            int targetIndex = toStart + i;

            var basePos = to.transform.position;
            var targetPos = new Vector3(basePos.x, yCell + targetIndex * tileHeight, basePos.z);

            float delay = i * stepDelay;

            LeanTween.cancel(go);
            LeanTween.move(go, targetPos, duration).setEase(LeanTweenType.easeInOutSine).setDelay(delay);

            var flatFrom = go.transform.position; flatFrom.y = 0f;
            var flatTo = targetPos; flatTo.y = 0f;
            var dir = (flatTo - flatFrom).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            var axis = Vector3.Cross(Vector3.up, dir);

            LeanTween.rotateAround(go, axis, 180f, duration).setEase(LeanTweenType.easeInOutSine).setDelay(delay);
        }

        float total = moving.Count > 0 ? ((moving.Count - 1) * stepDelay + duration) : duration;
        yield return new WaitForSeconds(total);

        SyncCellViews(from);
        SyncCellViews(to);

        busyCells.Remove(from);
        busyCells.Remove(to);
    }

    IEnumerator MoveTopRunCellToCell(HexCell from, HexCell to)
    {
        if (from == null || to == null) yield break;
        if (from.Stack.IsEmpty) yield break;
        if (!to.Stack.IsEmpty) yield break;
        if (busyCells.Contains(from) || busyCells.Contains(to)) yield break;

        var pack = from.Stack.PopTopRun();
        if (pack.Count == 0) yield break;

        if (to.Stack.Count + pack.Count > maxStackHeight)
        {
            from.Stack.PushMany(pack);
            yield break;
        }

        to.Stack.PushMany(pack);
        yield return StartCoroutine(AnimateMoveViews(from, to, pack.Count));

        EnqueueResolveAround(to);
        EnqueueResolveAround(from);
        RequestResolve();
    }

    // ---------------- VIEWS ----------------
    void SyncAllCells()
    {
        foreach (var c in cells.Values) SyncCellViews(c);
    }

    void SyncCellViews(HexCell cell)
    {
        if (cell == null) return;

        var data = cell.Stack.Snapshot();

        while (cell.views.Count > data.Count)
        {
            var go = cell.views[cell.views.Count - 1];
            cell.views.RemoveAt(cell.views.Count - 1);
            if (go) Destroy(go);
        }

        while (cell.views.Count < data.Count)
        {
            var go = Instantiate(tilePrefab, tileRoot);
            go.name = $"Tile_{cell.coord}_{cell.views.Count}";
            var col = go.GetComponentInChildren<Collider>();
            if (col) col.enabled = false;
            cell.views.Add(go);
        }

        for (int i = 0; i < data.Count; i++)
        {
            var c = data[i];
            var go = cell.views[i];

            var basePos = cell.transform.position;
            go.transform.position = new Vector3(basePos.x, yCell + i * tileHeight, basePos.z);

            var view = go.GetComponent<HexTileView>() ?? go.AddComponent<HexTileView>();
            var mat = ((int)c >= 0 && (int)c < colorMaterials.Length) ? colorMaterials[(int)c] : null;
            view.Init(c, i, mat);
        }
    }

    // ---------------- Ghost build ----------------
    void BuildGhostFromTiles(List<TileColor> tiles, Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);

        for (int i = 0; i < tiles.Count; i++)
        {
            var c = tiles[i];
            var go = Instantiate(tilePrefab, parent);
            go.transform.localPosition = new Vector3(0, i * tileHeight, 0);

            var view = go.GetComponent<HexTileView>() ?? go.AddComponent<HexTileView>();
            view.Init(c, i, colorMaterials[(int)c]);

            var col = go.GetComponentInChildren<Collider>();
            if (col) col.enabled = false;
        }
    }

    // ---------------- RAYCAST ----------------
    bool RaycastCell(Vector2 screenPos, out HexCell cell)
    {
        cell = null;
        var ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out var hit, 200f)) return false;
        cell = hit.collider.GetComponentInParent<HexCell>();
        return cell != null;
    }

    bool RaycastHandPiece(Vector2 screenPos, out HandPiece piece)
    {
        piece = null;
        var ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out var hit, 200f)) return false;
        piece = hit.collider.GetComponentInParent<HandPiece>();
        return piece != null;
    }

    // ---------------- INPUT ----------------
    bool TryGetPointerScreenPos(out Vector2 pos)
    {
        if (Touchscreen.current != null)
        {
            pos = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        if (Mouse.current != null)
        {
            pos = Mouse.current.position.ReadValue();
            return true;
        }

        pos = default;
        return false;
    }

    bool WasPressedThisFrame()
    {
        if (Touchscreen.current != null)
            return Touchscreen.current.primaryTouch.press.wasPressedThisFrame;

        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    bool WasReleasedThisFrame()
    {
        if (Touchscreen.current != null)
            return Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;

        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
    }

    bool IsPressed()
    {
        if (Touchscreen.current != null)
            return Touchscreen.current.primaryTouch.press.isPressed;

        return Mouse.current != null && Mouse.current.leftButton.isPressed;
    }
}