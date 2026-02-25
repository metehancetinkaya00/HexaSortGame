using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class HexBoard : MonoBehaviour
{
    [Header("Random Pack")]
    public RandomPackConfigSO randomPack;
    public int randomSeed = 0;
    public bool chooseRandomAnchorEachPack = true;

    [Header("Board Layout (Optional)")]
    public BoardLayoutSO boardLayout;

    [Header("Set Anchors")]
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

    [Header("Merge Animation")]
    public float mergeMoveDuration = 0.2f;
    public float mergeStepDelay = 0.01f;

    // internal
    Dictionary<Hex, HexCell> cells = new Dictionary<Hex, HexCell>();
    Camera cam;

    System.Random rng;
    Transform activeAnchor;
    int piecesLeftInPack;

    // drag
    HexCell dragFromCell;          
    HandPiece dragFromPiece;
    GameObject dragGhost;
    GameObject hiddenDragSource;

    // input helpers
    Vector2 lastPointerPos;
    bool hasPointerPos;

    // resolve loop
    bool resolveRunning;
    bool resolveRequested;

    Queue<HexCell> resolveQueue = new Queue<HexCell>();
    HashSet<HexCell> queued = new HashSet<HexCell>();
    HashSet<HexCell> busyCells = new HashSet<HexCell>();

    // drop indicator
    GameObject dropIndicator;

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

        // prepare slots on all anchors
        for (int i = 0; i < setAnchors.Length; i++)
            EnsureHandSlots(setAnchors[i]);

        BuildBoard();
        SyncAllCells();

        GenerateNextPack();
    }

    bool ValidateSetup()
    {
        if (Camera.main == null) { Debug.LogError("Main Camera missing / not tagged MainCamera."); return false; }
        if (randomPack == null) { Debug.LogError("randomPack not assigned."); return false; }
        if (setAnchors == null || setAnchors.Length == 0) { Debug.LogError("setAnchors missing."); return false; }
        for (int i = 0; i < setAnchors.Length; i++)
            if (setAnchors[i] == null) { Debug.LogError("setAnchors has null element."); return false; }

        if (cellsRoot == null) { Debug.LogError("cellsRoot not assigned."); return false; }
        if (tileRoot == null) { Debug.LogError("tileRoot not assigned."); return false; }
        if (cellPrefab == null) { Debug.LogError("cellPrefab not assigned."); return false; }
        if (cellPrefab.GetComponentInChildren<Collider>() == null) { Debug.LogError("cellPrefab needs Collider for raycast."); return false; }
        if (tilePrefab == null) { Debug.LogError("tilePrefab not assigned."); return false; }
        if (colorMaterials == null || colorMaterials.Length == 0) { Debug.LogError("colorMaterials missing/empty."); return false; }
        return true;
    }

    void CreateDropIndicator()
    {
        if (dropIndicatorPrefab == null) return;

        dropIndicator = Instantiate(dropIndicatorPrefab);
        dropIndicator.name = "DropIndicator";
        dropIndicator.SetActive(false);

        Collider[] cols = dropIndicator.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            cols[i].enabled = false;
    }

    // ---------------- Board ----------------
    void BuildBoard()
    {
        cells.Clear();

        List<Hex> coords = new List<Hex>();

        if (boardLayout != null && boardLayout.cells != null && boardLayout.cells.Count > 0)
        {
            // flatten: List<HexCoordList> -> items -> Hex
            for (int r = 0; r < boardLayout.cells.Count; r++)
            {
                var row = boardLayout.cells[r];
                if (row == null || row.items == null) continue;

                for (int j = 0; j < row.items.Count; j++)
                    coords.Add(row.items[j].ToHex());
            }
        }
        else
        {
            foreach (var h in Hex.Spiral(Hex.zero, 0, boardRadius))
                coords.Add(h);
        }

        for (int i = 0; i < coords.Count; i++)
        {
            var h = coords[i];
            var cell = Instantiate(cellPrefab, cellsRoot);
            cell.Init(h);
            cell.transform.position = h.ToWorld(yCell);
            cell.Stack.SetTiles(System.Array.Empty<TileColor>());
            cells[h] = cell;
        }
    }

    // ---------------- Pack Generation ----------------
    void GenerateNextPack()
    {
        if (chooseRandomAnchorEachPack)
        {
            int idx = rng.Next(0, setAnchors.Length);
            activeAnchor = setAnchors[idx];
        }
        else
        {
            if (activeAnchor == null) activeAnchor = setAnchors[0];
        }

        piecesLeftInPack = Mathf.Max(1, randomPack.piecesPerPack);

        ClearAnchorHand(activeAnchor);

        // 3 slot
        for (int i = 0; i < 3; i++)
        {
            Transform slot = activeAnchor.Find("HandSlot" + i);
            if (!slot) continue;

            List<TileColor> tiles = randomPack.GeneratePiece(rng);
            SpawnHandPiece(slot, tiles);
        }
    }

    void EnsureHandSlots(Transform anchor)
    {
        for (int i = 0; i < 3; i++)
        {
            Transform t = anchor.Find("HandSlot" + i);
            if (!t)
            {
                GameObject go = new GameObject("HandSlot" + i);
                t = go.transform;
                t.SetParent(anchor, false);
            }

            t.localPosition = new Vector3((i - 1) * handSlotSpacing, 0f, 0f);
            t.localRotation = Quaternion.identity;
        }
    }

    void ClearAnchorHand(Transform anchor)
    {
        for (int i = 0; i < 3; i++)
        {
            Transform slot = anchor.Find("HandSlot" + i);
            if (!slot) continue;

            for (int c = slot.childCount - 1; c >= 0; c--)
                Destroy(slot.GetChild(c).gameObject);
        }
    }

    void SpawnHandPiece(Transform slot, List<TileColor> tiles)
    {
        GameObject go = new GameObject("HandPiece");
        go.transform.SetParent(slot, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = handPieceColliderSize;
        box.center = new Vector3(0, handPieceColliderSize.y * 0.5f, 0);

        HandPiece piece = go.AddComponent<HandPiece>();
        piece.SetTiles(tiles);

        BuildGhostFromTiles(piece.tiles, go.transform);
    }

    // ---------------- Update / Input ----------------
    void Update()
    {
        if (hasFailed) return;

        Vector2 sp;
        if (TryGetPointerScreenPos(out sp))
        {
            lastPointerPos = sp;
            hasPointerPos = true;
        }

        if (!hasPointerPos) return;

        if (WasPressedThisFrame()) TryBeginDrag(lastPointerPos);
        if (IsPressed()) DragUpdate(lastPointerPos);
        if (WasReleasedThisFrame()) TryEndDrag(lastPointerPos);
    }

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
        if (Touchscreen.current != null) return Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    bool WasReleasedThisFrame()
    {
        if (Touchscreen.current != null) return Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
    }

    bool IsPressed()
    {
        if (Touchscreen.current != null) return Touchscreen.current.primaryTouch.press.isPressed;
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
    }

    // ---------------- Drag ----------------
    void TryBeginDrag(Vector2 screenPos)
    {
        HandPiece piece;
        if (RaycastHandPiece(screenPos, out piece) && piece != null && piece.tiles != null && piece.tiles.Count > 0)
        {
            dragFromPiece = piece;
            dragFromCell = null;

            hiddenDragSource = piece.gameObject;
            SetHandPieceVisible(hiddenDragSource, false);

            dragGhost = new GameObject("DragGhost_Piece");
            Vector3 p = piece.transform.position;
            dragGhost.transform.position = new Vector3(p.x, dragGhostY, p.z);

            BuildGhostFromTiles(piece.tiles, dragGhost.transform);
            UpdateDropPreview(screenPos);
        }
    }

    void DragUpdate(Vector2 screenPos)
    {
        if (!dragGhost)
        {
            HideDropPreview();
            return;
        }

        Plane plane = new Plane(Vector3.up, Vector3.up * yCell);
        Ray ray = cam.ScreenPointToRay(screenPos);
        float enter;
        if (plane.Raycast(ray, out enter))
        {
            Vector3 p = ray.GetPoint(enter);
            dragGhost.transform.position = new Vector3(p.x, dragGhostY, p.z);
        }

        UpdateDropPreview(screenPos);
    }

    void TryEndDrag(Vector2 screenPos)
    {
        HideDropPreview();

        if (dragFromPiece == null && dragFromCell == null)
        {
            CleanupDrag();
            return;
        }

        HexCell toCell;
        if (!RaycastCell(screenPos, out toCell) || toCell == null)
        {
            CleanupDrag();
            return;
        }

        if (!CanDropOnCell(toCell))
        {
            CleanupDrag();
            return;
        }

        // place from hand
        if (dragFromPiece != null)
        {
            List<TileColor> tiles = dragFromPiece.tiles;
            if (tiles == null || tiles.Count == 0) { CleanupDrag(); return; }
            if (toCell.Stack.Count + tiles.Count > maxStackHeight) { CleanupDrag(); return; }

            toCell.Stack.PushMany(tiles);
            SyncCellViews(toCell);

            hiddenDragSource = null;
            Destroy(dragFromPiece.gameObject);
            dragFromPiece = null;

            CleanupDrag();

            piecesLeftInPack--;
            if (piecesLeftInPack <= 0) GenerateNextPack();

            EnqueueResolveAround(toCell);
            RequestResolve();

            CheckFailNow();
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

        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++) rs[i].enabled = visible;

        Collider[] cs = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cs.Length; i++) cs[i].enabled = visible;
    }

    // ---------------- Drop Preview ----------------
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
        if (!dragging)
        {
            dropIndicator.SetActive(false);
            return;
        }

        HexCell cell;
        if (RaycastCell(screenPos, out cell) && cell != null && CanDropOnCell(cell))
        {
            dropIndicator.SetActive(true);
            Vector3 p = cell.transform.position;
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

    // ---------------- Fail ----------------
    bool IsBoardFull()
    {
        foreach (var kv in cells)
        {
            HexCell c = kv.Value;
            if (c != null && c.Stack != null && c.Stack.IsEmpty) return false;
        }
        return true;
    }

    void CheckFailNow()
    {
        if (hasFailed) return;

        if (IsBoardFull())
        {
            hasFailed = true;
            CleanupDrag();
            if (scoreManager) scoreManager.ShowFailed();
        }
    }

    // ---------------- Resolve Loop ----------------
    void RequestResolve()
    {
        resolveRequested = true;
        if (!resolveRunning)
            StartCoroutine(ResolveLoop());
    }

    void EnqueueResolve(HexCell c)
    {
        if (!c) return;
        if (queued.Contains(c)) return;

        queued.Add(c);
        resolveQueue.Enqueue(c);
    }

    void EnqueueResolveAround(HexCell c)
    {
        EnqueueResolve(c);

        foreach (var n in c.coord.Neighbours())
        {
            HexCell nb;
            if (cells.TryGetValue(n, out nb))
                EnqueueResolve(nb);
        }
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
                safety++;
                if (safety > 2000) break;

                HexCell seed = null;

                while (resolveQueue.Count > 0 && seed == null)
                {
                    var c = resolveQueue.Dequeue();
                    queued.Remove(c);
                    if (c != null && !c.Stack.IsEmpty) seed = c;
                }

                if (seed == null)
                {
                    foreach (var kv in cells)
                    {
                        if (kv.Value != null && !kv.Value.Stack.IsEmpty)
                        {
                            seed = kv.Value;
                            break;
                        }
                    }
                }

                if (seed == null) break;

                var top = seed.Stack.TopColor;
                if (top == null) continue;

                List<HexCell> group = CollectConnectedTopColorGroup(seed, top.Value);
                if (group.Count <= 1) continue;

                HexCell sink = ChooseSinkByDegree(group);

                // sources sorted -> "sırayla"
                group.Sort((a, b) =>
                {
                    int q = a.coord.q.CompareTo(b.coord.q);
                    if (q != 0) return q;
                    return a.coord.r.CompareTo(b.coord.r);
                });

                for (int i = 0; i < group.Count; i++)
                {
                    HexCell src = group[i];
                    if (src == null || src == sink) continue;
                    if (src.Stack.IsEmpty) continue;

                    var srcTop = src.Stack.TopColor;
                    if (srcTop == null || srcTop.Value != top.Value) continue;

                    if (busyCells.Contains(src) || busyCells.Contains(sink)) continue;

                    List<TileColor> pack = src.Stack.PopTopRun();
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
                    break; // ✅ tek hamle, sonra yeniden değerlendir
                }
            }

            bool clearedAny = false;
            foreach (var kv in cells)
            {
                HexCell cell = kv.Value;
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
                var vanishList = cell.views.GetRange(start, cell.views.Count - start);
                cell.views.RemoveRange(start, vanishList.Count);

                if (scoreManager) scoreManager.AddScore(vanishList.Count);

                float dur = 0.2f;
                float step = 0.01f;

                for (int i = 0; i < vanishList.Count; i++)
                {
                    var go = vanishList[i];
                    float delay = i * step;

                    LeanTween.cancel(go);
                    LeanTween.scale(go, Vector3.zero, dur)
                        .setEase(LeanTweenType.easeInBack)
                        .setDelay(delay)
                        .setOnComplete(() => Destroy(go));
                }

                float total = vanishList.Count > 0 ? ((vanishList.Count - 1) * step + dur) : dur;
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
        List<HexCell> result = new List<HexCell>();
        Queue<HexCell> q = new Queue<HexCell>();
        HashSet<HexCell> visited = new HashSet<HexCell>();

        visited.Add(start);
        q.Enqueue(start);

        while (q.Count > 0)
        {
            HexCell cur = q.Dequeue();
            result.Add(cur);

            foreach (var n in cur.coord.Neighbours())
            {
                HexCell nb;
                if (!cells.TryGetValue(n, out nb) || nb == null) continue;
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
        HashSet<HexCell> set = new HashSet<HexCell>(group);

        int Degree(HexCell c)
        {
            int d = 0;
            foreach (var n in c.coord.Neighbours())
            {
                HexCell nb;
                if (cells.TryGetValue(n, out nb) && nb != null && set.Contains(nb))
                    d++;
            }
            return d;
        }

        HexCell best = group[0];
        int bestDeg = Degree(best);
        int bestRun = best.Stack.TopRunCount();
        int bestCnt = best.Stack.Count;

        for (int i = 1; i < group.Count; i++)
        {
            HexCell c = group[i];

            int deg = Degree(c);
            int run = c.Stack.TopRunCount();
            int cnt = c.Stack.Count;

            bool better = false;
            if (deg > bestDeg) better = true;
            else if (deg == bestDeg && run > bestRun) better = true;
            else if (deg == bestDeg && run == bestRun && cnt > bestCnt) better = true;
            else if (deg == bestDeg && run == bestRun && cnt == bestCnt)
            {
                if (c.coord.q < best.coord.q) better = true;
                else if (c.coord.q == best.coord.q && c.coord.r < best.coord.r) better = true;
            }

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

        float dur = mergeMoveDuration;
        float stepDelay = mergeStepDelay;

        int takeFrom = Mathf.Max(0, from.views.Count - packCount);
        List<GameObject> moving = from.views.GetRange(takeFrom, from.views.Count - takeFrom);
        from.views.RemoveRange(takeFrom, moving.Count);

        int toStart = to.views.Count;
        to.views.AddRange(moving);

        for (int i = 0; i < moving.Count; i++)
        {
            GameObject go = moving[i];
            int targetIndex = toStart + i;

            Vector3 basePos = to.transform.position;
            Vector3 targetPos = new Vector3(basePos.x, yCell + targetIndex * tileHeight, basePos.z);

            float delay = i * stepDelay;

            LeanTween.cancel(go);
            LeanTween.move(go, targetPos, dur).setEase(LeanTweenType.easeInOutSine).setDelay(delay);

            Vector3 flatFrom = go.transform.position; flatFrom.y = 0f;
            Vector3 flatTo = targetPos; flatTo.y = 0f;
            Vector3 dir = (flatTo - flatFrom).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;

            Vector3 axis = Vector3.Cross(Vector3.up, dir);
            LeanTween.rotateAround(go, axis, 180f, dur).setEase(LeanTweenType.easeInOutSine).setDelay(delay);
        }

        float total = moving.Count > 0 ? ((moving.Count - 1) * stepDelay + dur) : dur;
        yield return new WaitForSeconds(total);

        SyncCellViews(from);
        SyncCellViews(to);

        busyCells.Remove(from);
        busyCells.Remove(to);
    }

    // ---------------- Views ----------------
    void SyncAllCells()
    {
        foreach (var kv in cells)
            SyncCellViews(kv.Value);
    }

    void SyncCellViews(HexCell cell)
    {
        if (cell == null) return;

        var data = new List<TileColor>(cell.Stack.Snapshot());

        while (cell.views.Count > data.Count)
        {
            var go = cell.views[cell.views.Count - 1];
            cell.views.RemoveAt(cell.views.Count - 1);
            if (go) Destroy(go);
        }

        while (cell.views.Count < data.Count)
        {
            var go = Instantiate(tilePrefab, tileRoot);
            go.name = "Tile_" + cell.coord + "_" + cell.views.Count;

            var col = go.GetComponentInChildren<Collider>();
            if (col) col.enabled = false;

            cell.views.Add(go);
        }

        for (int i = 0; i < data.Count; i++)
        {
            TileColor c = data[i];
            var go = cell.views[i];

            Vector3 basePos = cell.transform.position;
            go.transform.position = new Vector3(basePos.x, yCell + i * tileHeight, basePos.z);

            var view = go.GetComponent<HexTileView>();
            if (!view) view = go.AddComponent<HexTileView>();

            Material mat = ((int)c >= 0 && (int)c < colorMaterials.Length) ? colorMaterials[(int)c] : null;
            view.Init(c, i, mat);
        }
    }

    // ---------------- Ghost Build ----------------
    void BuildGhostFromTiles(List<TileColor> tiles, Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);

        for (int i = 0; i < tiles.Count; i++)
        {
            TileColor c = tiles[i];
            GameObject go = Instantiate(tilePrefab, parent);
            go.transform.localPosition = new Vector3(0, i * tileHeight, 0);

            var view = go.GetComponent<HexTileView>();
            if (!view) view = go.AddComponent<HexTileView>();

            view.Init(c, i, colorMaterials[(int)c]);

            var col = go.GetComponentInChildren<Collider>();
            if (col) col.enabled = false;
        }
    }

    // ---------------- Raycast ----------------
    bool RaycastCell(Vector2 screenPos, out HexCell cell)
    {
        cell = null;
        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit, 200f)) return false;
        cell = hit.collider.GetComponentInParent<HexCell>();
        return cell != null;
    }

    bool RaycastHandPiece(Vector2 screenPos, out HandPiece piece)
    {
        piece = null;
        Ray ray = cam.ScreenPointToRay(screenPos);
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit, 200f)) return false;
        piece = hit.collider.GetComponentInParent<HandPiece>();
        return piece != null;
    }
}