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

    private Dictionary<Hex, HexCell> cells = new Dictionary<Hex, HexCell>();
    private Camera cam;
    private System.Random rng;
    private Transform activeAnchor;
    private int piecesLeftInPack;

    // drag
    private HandPiece dragSourcePiece;
    private GameObject dragGhostObject;
    private GameObject hiddenSourceObject;

    // input
    private Vector2 lastPointerScreenPos;
    private bool hasPointerPos;

    // resolve
    private bool resolveRunning;
    private bool resolveRequested;
    private Queue<HexCell> resolveQueue = new Queue<HexCell>();
    private HashSet<HexCell> queuedCells = new HashSet<HexCell>();
    private HashSet<HexCell> busyCells = new HashSet<HexCell>();

    // preview
    private GameObject dropIndicatorObject;

    private bool hasFailed;

    private int HandSlotCount => (randomPack != null) ? Mathf.Max(1, randomPack.piecesPerPack) : 3;

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

        for (int anchorIndex = 0; anchorIndex < setAnchors.Length; anchorIndex++)
        {
            EnsureHandSlots(setAnchors[anchorIndex], HandSlotCount);
        }

        RefreshBoardFromScene();

        SyncAllCells();
        GenerateNextPack();
    }

    private bool ValidateSetup()
    {
        if (Camera.main == null)
        {
            Debug.LogError("Main Camera missing");
            return false;
        }

        if (randomPack == null)
        {
            Debug.LogError("randomPack not assigned.");
            return false;
        }

        if (setAnchors == null || setAnchors.Length == 0)
        {
            Debug.LogError("setAnchors missing.");
            return false;
        }

        for (int anchorIndex = 0; anchorIndex < setAnchors.Length; anchorIndex++)
        {
            if (setAnchors[anchorIndex] == null)
            {
                Debug.LogError("setAnchors has null element.");
                return false;
            }
        }

        if (cellsRoot == null)
        {
            Debug.LogError("cellsRoot not assigned.");
            return false;
        }

        if (tileRoot == null)
        {
            Debug.LogError("tileRoot not assigned.");
            return false;
        }

        if (cellPrefab == null)
        {
            Debug.LogError("cellPrefab not assigned.");
            return false;
        }

        if (cellPrefab.GetComponentInChildren<Collider>() == null)
        {
            Debug.LogError("cellPrefab needs Collider for raycast.");
            return false;
        }

        if (tilePrefab == null)
        {
            Debug.LogError("tilePrefab not assigned.");
            return false;
        }

        if (colorMaterials == null || colorMaterials.Length == 0)
        {
            Debug.LogError("colorMaterials missing/empty.");
            return false;
        }

        return true;
    }

    private void CreateDropIndicator()
    {
        if (dropIndicatorPrefab == null)
        {
            return;
        }

        dropIndicatorObject = Instantiate(dropIndicatorPrefab);
        dropIndicatorObject.name = "DropIndicator";
        dropIndicatorObject.SetActive(false);

        Collider[] colliders = dropIndicatorObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    // ---------------- Board ----------------
    public void RefreshBoardFromScene()
    {
        cells.Clear();

        HexCell[] sceneCells;

        if (cellsRoot != null)
            sceneCells = cellsRoot.GetComponentsInChildren<HexCell>(true);
        else
            sceneCells = FindObjectsOfType<HexCell>(true);

        for (int i = 0; i < sceneCells.Length; i++)
        {
            HexCell cell = sceneCells[i];
            if (cell == null) continue;

            // coord boş/yanlışsa pozisyondan hesapla
            if (cell.coord.col == 0 && cell.coord.row == 0)
            {
                Hex computed = cell.transform.position.ToHex();
                cell.coord = computed;
                cell.name = "Cell " + computed.ToString();
            }

            // stack null ise init et
            if (cell.Stack == null)
            {
                cell.Init(cell.coord);
                cell.Stack.SetTiles(System.Array.Empty<TileColor>());
            }

            cells[cell.coord] = cell;
        }

        Debug.Log("RefreshBoardFromScene: " + cells.Count + " cells cached.");
    }
    // ---------------- Pack ----------------

    private void GenerateNextPack()
    {
        if (chooseRandomAnchorEachPack)
        {
            int anchorIndex = rng.Next(0, setAnchors.Length);
            activeAnchor = setAnchors[anchorIndex];
        }
        else
        {
            if (activeAnchor == null)
            {
                activeAnchor = setAnchors[0];
            }
        }

        int slotCount = HandSlotCount;
        piecesLeftInPack = slotCount;

        EnsureHandSlots(activeAnchor, slotCount);
        ClearAnchorHand(activeAnchor, slotCount);

        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            Transform slotTransform = activeAnchor.Find("HandSlot" + slotIndex);
            if (!slotTransform)
            {
                continue;
            }

            List<TileColor> tiles = randomPack.GeneratePiece(rng);
            SpawnHandPiece(slotTransform, tiles);
        }
    }

    private void EnsureHandSlots(Transform anchor, int slotCount)
    {
        float center = (slotCount - 1) * 0.5f;

        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            Transform slotTransform = anchor.Find("HandSlot" + slotIndex);
            if (!slotTransform)
            {
                GameObject slotObject = new GameObject("HandSlot" + slotIndex);
                slotTransform = slotObject.transform;
                slotTransform.SetParent(anchor, false);
            }

            slotTransform.localPosition = new Vector3((slotIndex - center) * handSlotSpacing, 0f, 0f);
            slotTransform.localRotation = Quaternion.identity;
        }
    }

    private void ClearAnchorHand(Transform anchor, int slotCount)
    {
        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            Transform slotTransform = anchor.Find("HandSlot" + slotIndex);
            if (!slotTransform)
            {
                continue;
            }

            for (int childIndex = slotTransform.childCount - 1; childIndex >= 0; childIndex--)
            {
                Destroy(slotTransform.GetChild(childIndex).gameObject);
            }
        }
    }

    private void SpawnHandPiece(Transform slotTransform, List<TileColor> tiles)
    {
        GameObject pieceObject = new GameObject("HandPiece");
        pieceObject.transform.SetParent(slotTransform, false);
        pieceObject.transform.localPosition = Vector3.zero;
        pieceObject.transform.localRotation = Quaternion.identity;

        BoxCollider boxCollider = pieceObject.AddComponent<BoxCollider>();
        boxCollider.size = handPieceColliderSize;
        boxCollider.center = new Vector3(0, handPieceColliderSize.y * 0.5f, 0);

        HandPiece piece = pieceObject.AddComponent<HandPiece>();
        piece.SetTiles(tiles);

        BuildGhostFromTiles(piece.tiles, pieceObject.transform);
    }

    // ---------------- Update / Input ----------------

    void Update()
    {
        if (hasFailed)
        {
            return;
        }

        if (TryGetPointerScreenPos(out Vector2 pointerScreenPos))
        {
            lastPointerScreenPos = pointerScreenPos;
            hasPointerPos = true;
        }

        if (!hasPointerPos)
        {
            return;
        }

        if (WasPressedThisFrame())
        {
            TryBeginDrag(lastPointerScreenPos);
        }

        if (IsPressed())
        {
            DragUpdate(lastPointerScreenPos);
        }

        if (WasReleasedThisFrame())
        {
            TryEndDrag(lastPointerScreenPos);
        }
    }

    private bool TryGetPointerScreenPos(out Vector2 pointerScreenPos)
    {
        if (Touchscreen.current != null)
        {
            pointerScreenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        if (Mouse.current != null)
        {
            pointerScreenPos = Mouse.current.position.ReadValue();
            return true;
        }

        pointerScreenPos = default;
        return false;
    }

    private bool WasPressedThisFrame()
    {
        if (Touchscreen.current != null)
        {
            return Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        }

        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private bool WasReleasedThisFrame()
    {
        if (Touchscreen.current != null)
        {
            return Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
        }

        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
    }

    private bool IsPressed()
    {
        if (Touchscreen.current != null)
        {
            return Touchscreen.current.primaryTouch.press.isPressed;
        }

        return Mouse.current != null && Mouse.current.leftButton.isPressed;
    }

    // ---------------- Drag ----------------

    private void TryBeginDrag(Vector2 screenPos)
    {
        if (dragGhostObject != null)
        {
            return;
        }

        if (RaycastHandPiece(screenPos, out HandPiece hitPiece) && hitPiece != null && hitPiece.tiles != null && hitPiece.tiles.Count > 0)
        {
            dragSourcePiece = hitPiece;
            hiddenSourceObject = hitPiece.gameObject;
            SetHandPieceVisible(hiddenSourceObject, false);

            dragGhostObject = new GameObject("DragGhost_Piece");
            Vector3 sourceWorldPos = hitPiece.transform.position;
            dragGhostObject.transform.position = new Vector3(sourceWorldPos.x, dragGhostY, sourceWorldPos.z);

            BuildGhostFromTiles(hitPiece.tiles, dragGhostObject.transform);
            UpdateDropPreview(screenPos);
        }
    }

    private void DragUpdate(Vector2 screenPos)
    {
        if (!dragGhostObject)
        {
            HideDropPreview();
            return;
        }

        Plane plane = new Plane(Vector3.up, Vector3.up * yCell);
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            dragGhostObject.transform.position = new Vector3(hitPoint.x, dragGhostY, hitPoint.z);
        }

        UpdateDropPreview(screenPos);
    }

    private void TryEndDrag(Vector2 screenPos)
    {
        HideDropPreview();

        if (dragSourcePiece == null)
        {
            CleanupDrag();
            return;
        }

        if (!RaycastCell(screenPos, out HexCell targetCell) || targetCell == null)
        {
            CleanupDrag();
            return;
        }

        if (!CanDropOnCell(targetCell))
        {
            CleanupDrag();
            return;
        }

        List<TileColor> tiles = dragSourcePiece.tiles;
        if (tiles == null || tiles.Count == 0)
        {
            CleanupDrag();
            return;
        }

        if (targetCell.Stack.Count + tiles.Count > maxStackHeight)
        {
            CleanupDrag();
            return;
        }

        // drop
        targetCell.Stack.PushMany(tiles);
        SyncCellViews(targetCell);

        Destroy(dragSourcePiece.gameObject);
        dragSourcePiece = null;
        CleanupDrag();

        piecesLeftInPack--;
        if (piecesLeftInPack <= 0)
        {
            GenerateNextPack();
        }

        EnqueueResolveAround(targetCell);
        RequestResolve();
        CheckFailNow();
    }

    private void CleanupDrag()
    {
        if (dragGhostObject)
        {
            Destroy(dragGhostObject);
        }

        dragGhostObject = null;

        if (hiddenSourceObject != null)
        {
            SetHandPieceVisible(hiddenSourceObject, true);
            hiddenSourceObject = null;
        }

        dragSourcePiece = null;
        HideDropPreview();
    }

    private void SetHandPieceVisible(GameObject pieceObject, bool visible)
    {
        if (!pieceObject)
        {
            return;
        }

        Renderer[] renderers = pieceObject.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = visible;
        }

        Collider[] colliders = pieceObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = visible;
        }
    }

    // ---------------- Drop Preview ----------------

    private bool CanDropOnCell(HexCell cell)
    {
        if (cell == null || cell.Stack == null)
        {
            return false;
        }

        if (busyCells.Contains(cell))
        {
            return false;
        }

        if (!cell.Stack.IsEmpty)
        {
            return false;
        }

        return true;
    }

    private void UpdateDropPreview(Vector2 screenPos)
    {
        if (!dropIndicatorObject)
        {
            return;
        }

        bool isDragging = (dragGhostObject != null) && (dragSourcePiece != null);
        if (!isDragging)
        {
            dropIndicatorObject.SetActive(false);
            return;
        }

        if (RaycastCell(screenPos, out HexCell targetCell) && targetCell != null && CanDropOnCell(targetCell))
        {
            dropIndicatorObject.SetActive(true);
            Vector3 cellWorldPos = targetCell.transform.position;
            dropIndicatorObject.transform.position = new Vector3(cellWorldPos.x, yCell + dropIndicatorY, cellWorldPos.z);
            dropIndicatorObject.transform.rotation = Quaternion.identity;
        }
        else
        {
            dropIndicatorObject.SetActive(false);
        }
    }

    private void HideDropPreview()
    {
        if (dropIndicatorObject)
        {
            dropIndicatorObject.SetActive(false);
        }
    }

    // ---------------- Fail ----------------

    private bool IsBoardFull()
    {
        foreach (var kvp in cells)
        {
            HexCell cell = kvp.Value;
            if (cell != null && cell.Stack != null && cell.Stack.IsEmpty)
            {
                return false;
            }
        }

        return true;
    }

    private void CheckFailNow()
    {
        if (hasFailed)
        {
            return;
        }

        if (IsBoardFull())
        {
            hasFailed = true;
            CleanupDrag();

            if (scoreManager)
            {
                scoreManager.ShowFailed();
            }
        }
    }

    // ---------------- Resolve Loop ----------------

    private void RequestResolve()
    {
        resolveRequested = true;
        if (!resolveRunning)
        {
            StartCoroutine(ResolveLoop());
        }
    }

    private void EnqueueResolve(HexCell cell)
    {
        if (!cell)
        {
            return;
        }

        if (queuedCells.Contains(cell))
        {
            return;
        }

        queuedCells.Add(cell);
        resolveQueue.Enqueue(cell);
    }

    private void EnqueueResolveAround(HexCell centerCell)
    {
        EnqueueResolve(centerCell);

        foreach (var neighborHex in centerCell.coord.Neighbours())
        {
            if (cells.TryGetValue(neighborHex, out HexCell neighborCell))
            {
                EnqueueResolve(neighborCell);
            }
        }
    }

    private IEnumerator ResolveLoop()
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
                if (safety > 2000)
                {
                    break;
                }

                HexCell seedCell = GetNextSeedCell();
                if (seedCell == null)
                {
                    break;
                }

                TileColor? topColor = seedCell.Stack.TopColor;
                if (topColor == null)
                {
                    continue;
                }

                List<HexCell> group = CollectConnectedTopColorGroup(seedCell, topColor.Value);
                if (group.Count <= 1)
                {
                    continue;
                }

                HexCell sinkCell = ChooseSinkByDegree(group);

                group.Sort((a, b) =>
                {
                    int qCompare = a.coord.col.CompareTo(b.coord.col);
                    if (qCompare != 0)
                    {
                        return qCompare;
                    }

                    return a.coord.row.CompareTo(b.coord.row);
                });

                for (int groupIndex = 0; groupIndex < group.Count; groupIndex++)
                {
                    HexCell sourceCell = group[groupIndex];
                    if (sourceCell == null || sourceCell == sinkCell)
                    {
                        continue;
                    }

                    if (sourceCell.Stack.IsEmpty)
                    {
                        continue;
                    }

                    if (busyCells.Contains(sourceCell) || busyCells.Contains(sinkCell))
                    {
                        continue;
                    }

                    TileColor? sourceTop = sourceCell.Stack.TopColor;
                    if (sourceTop == null || sourceTop.Value != topColor.Value)
                    {
                        continue;
                    }

                    SyncCellViews(sourceCell);
                    SyncCellViews(sinkCell);

                    List<TileColor> movingPack = sourceCell.Stack.PopTopRun();
                    if (movingPack.Count == 0)
                    {
                        continue;
                    }

                    if (sinkCell.Stack.Count + movingPack.Count > maxStackHeight)
                    {
                        sourceCell.Stack.PushMany(movingPack);
                        continue;
                    }

                    sinkCell.Stack.PushMany(movingPack);

                    yield return StartCoroutine(AnimateMoveViews(sourceCell, sinkCell, movingPack.Count));

                    EnqueueResolveAround(sinkCell);
                    EnqueueResolveAround(sourceCell);
                    didMove = true;
                    break;
                }
            }

            bool clearedAny = false;

            foreach (var kvp in cells)
            {
                HexCell cell = kvp.Value;
                if (cell == null || cell.Stack.IsEmpty)
                {
                    continue;
                }

                int runCount = cell.Stack.TopRunCount();
                if (runCount < clearCount)
                {
                    continue;
                }

                SyncCellViews(cell);

                List<TileColor> removedRun = cell.Stack.PopTopRun();
                if (removedRun.Count < clearCount)
                {
                    cell.Stack.PushMany(removedRun);
                    continue;
                }

                int startIndex = Mathf.Max(0, cell.views.Count - removedRun.Count);
                List<GameObject> vanishObjects = cell.views.GetRange(startIndex, cell.views.Count - startIndex);
                cell.views.RemoveRange(startIndex, vanishObjects.Count);

                if (scoreManager)
                {
                    scoreManager.AddScore(vanishObjects.Count);
                }

                float duration = 0.2f;
                float step = 0.01f;

                for (int i = 0; i < vanishObjects.Count; i++)
                {
                    GameObject tileObject = vanishObjects[i];
                    GameObject tileObjectCopy = tileObject;
                    float delay = i * step;

                    LeanTween.cancel(tileObjectCopy);
                    LeanTween.scale(tileObjectCopy, Vector3.zero, duration)
                        .setEase(LeanTweenType.easeInBack)
                        .setDelay(delay)
                        .setOnComplete(() => Destroy(tileObjectCopy));
                }

                float total = vanishObjects.Count > 0 ? ((vanishObjects.Count - 1) * step + duration) : duration;
                yield return new WaitForSeconds(total);

                SyncCellViews(cell);
                EnqueueResolveAround(cell);
                clearedAny = true;
            }

            if (resolveRequested || clearedAny || resolveQueue.Count > 0)
            {
                continue;
            }

            break;
        }

        resolveRunning = false;
        CheckFailNow();
    }

    private HexCell GetNextSeedCell()
    {
        HexCell seedCell = null;

        while (resolveQueue.Count > 0 && seedCell == null)
        {
            HexCell queuedCell = resolveQueue.Dequeue();
            queuedCells.Remove(queuedCell);

            if (queuedCell != null && !queuedCell.Stack.IsEmpty)
            {
                seedCell = queuedCell;
            }
        }

        if (seedCell != null)
        {
            return seedCell;
        }

        foreach (var kvp in cells)
        {
            HexCell cell = kvp.Value;
            if (cell != null && !cell.Stack.IsEmpty)
            {
                return cell;
            }
        }

        return null;
    }

    private List<HexCell> CollectConnectedTopColorGroup(HexCell startCell, TileColor color)
    {
        List<HexCell> result = new List<HexCell>();
        Queue<HexCell> frontier = new Queue<HexCell>();
        HashSet<HexCell> visited = new HashSet<HexCell>();

        visited.Add(startCell);
        frontier.Enqueue(startCell);

        while (frontier.Count > 0)
        {
            HexCell currentCell = frontier.Dequeue();
            result.Add(currentCell);

            foreach (var neighborHex in currentCell.coord.Neighbours())
            {
                if (!cells.TryGetValue(neighborHex, out HexCell neighborCell) || neighborCell == null)
                {
                    continue;
                }

                if (visited.Contains(neighborCell))
                {
                    continue;
                }

                if (neighborCell.Stack.IsEmpty)
                {
                    continue;
                }

                TileColor? neighborTop = neighborCell.Stack.TopColor;
                if (neighborTop == null || neighborTop.Value != color)
                {
                    continue;
                }

                visited.Add(neighborCell);
                frontier.Enqueue(neighborCell);
            }
        }

        return result;
    }

    private HexCell ChooseSinkByDegree(List<HexCell> group)
    {
        HashSet<HexCell> groupSet = new HashSet<HexCell>(group);

        int Degree(HexCell cell)
        {
            int degree = 0;

            foreach (var neighborHex in cell.coord.Neighbours())
            {
                if (cells.TryGetValue(neighborHex, out HexCell neighborCell) && neighborCell != null && groupSet.Contains(neighborCell))
                {
                    degree++;
                }
            }

            return degree;
        }

        HexCell bestCell = group[0];
        int bestDegree = Degree(bestCell);
        int bestRun = bestCell.Stack.TopRunCount();
        int bestCount = bestCell.Stack.Count;

        for (int i = 1; i < group.Count; i++)
        {
            HexCell candidateCell = group[i];
            int candidateDegree = Degree(candidateCell);
            int candidateRun = candidateCell.Stack.TopRunCount();
            int candidateCount = candidateCell.Stack.Count;

            bool isBetter = false;

            if (candidateDegree > bestDegree)
            {
                isBetter = true;
            }
            else if (candidateDegree == bestDegree && candidateRun > bestRun)
            {
                isBetter = true;
            }
            else if (candidateDegree == bestDegree && candidateRun == bestRun && candidateCount > bestCount)
            {
                isBetter = true;
            }

            if (isBetter)
            {
                bestCell = candidateCell;
                bestDegree = candidateDegree;
                bestRun = candidateRun;
                bestCount = candidateCount;
            }
        }

        return bestCell;
    }

    private IEnumerator AnimateMoveViews(HexCell fromCell, HexCell toCell, int packCount)
    {
        busyCells.Add(fromCell);
        busyCells.Add(toCell);

        float duration = mergeMoveDuration;
        float stepDelay = mergeStepDelay;

        int takeFromIndex = Mathf.Max(0, fromCell.views.Count - packCount);
        List<GameObject> movingObjects = fromCell.views.GetRange(takeFromIndex, fromCell.views.Count - takeFromIndex);
        fromCell.views.RemoveRange(takeFromIndex, movingObjects.Count);

        int targetStartIndex = toCell.views.Count;
        toCell.views.AddRange(movingObjects);

        for (int i = 0; i < movingObjects.Count; i++)
        {
            GameObject tileObject = movingObjects[i];
            int targetIndex = targetStartIndex + i;

            Vector3 basePos = toCell.transform.position;
            Vector3 targetPos = new Vector3(basePos.x, yCell + targetIndex * tileHeight, basePos.z);

            float delay = i * stepDelay;

            LeanTween.cancel(tileObject);
            LeanTween.move(tileObject, targetPos, duration)
                .setEase(LeanTweenType.easeInOutSine)
                .setDelay(delay);

            Vector3 flatFrom = tileObject.transform.position;
            flatFrom.y = 0f;
            Vector3 flatTo = targetPos;
            flatTo.y = 0f;
            Vector3 dir = (flatTo - flatFrom).normalized;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector3.forward;
            }

            Vector3 axis = Vector3.Cross(Vector3.up, dir);
            LeanTween.rotateAround(tileObject, axis, 180f, duration)
                .setEase(LeanTweenType.easeInOutSine)
                .setDelay(delay);
        }

        float total = movingObjects.Count > 0 ? ((movingObjects.Count - 1) * stepDelay + duration) : duration;
        yield return new WaitForSeconds(total);

        SyncCellViews(fromCell);
        SyncCellViews(toCell);

        busyCells.Remove(fromCell);
        busyCells.Remove(toCell);
    }

    // ---------------- Views ----------------

    private void SyncAllCells()
    {
        foreach (var kvp in cells)
        {
            SyncCellViews(kvp.Value);
        }
    }

    private void SyncCellViews(HexCell cell)
    {
        if (cell == null)
        {
            return;
        }

        List<TileColor> snapshot = new List<TileColor>(cell.Stack.Snapshot());

        while (cell.views.Count > snapshot.Count)
        {
            GameObject viewObject = cell.views[cell.views.Count - 1];
            cell.views.RemoveAt(cell.views.Count - 1);
            if (viewObject)
            {
                Destroy(viewObject);
            }
        }

        while (cell.views.Count < snapshot.Count)
        {
            GameObject tileObject = Instantiate(tilePrefab, tileRoot);
            tileObject.name = $"Tile_{cell.coord}_{cell.views.Count}";

            Collider tileCollider = tileObject.GetComponentInChildren<Collider>();
            if (tileCollider)
            {
                tileCollider.enabled = false;
            }

            cell.views.Add(tileObject);
        }

        for (int index = 0; index < snapshot.Count; index++)
        {
            TileColor tileColor = snapshot[index];
            GameObject tileObject = cell.views[index];

            Vector3 basePos = cell.transform.position;
            tileObject.transform.position = new Vector3(basePos.x, yCell + index * tileHeight, basePos.z);

            HexTileView view = tileObject.GetComponent<HexTileView>();
            if (!view)
            {
                view = tileObject.AddComponent<HexTileView>();
            }

            Material mat = ((int)tileColor >= 0 && (int)tileColor < colorMaterials.Length) ? colorMaterials[(int)tileColor] : null;
            view.Init(tileColor, index, mat);
        }
    }

    // ---------------- Ghost Build ----------------

    private void BuildGhostFromTiles(List<TileColor> tiles, Transform parent)
    {
        for (int childIndex = parent.childCount - 1; childIndex >= 0; childIndex--)
        {
            Destroy(parent.GetChild(childIndex).gameObject);
        }

        for (int i = 0; i < tiles.Count; i++)
        {
            TileColor tileColor = tiles[i];
            GameObject tileObject = Instantiate(tilePrefab, parent);
            tileObject.transform.localPosition = new Vector3(0, i * tileHeight, 0);

            HexTileView view = tileObject.GetComponent<HexTileView>();
            if (!view)
            {
                view = tileObject.AddComponent<HexTileView>();
            }

            Material mat = colorMaterials[(int)tileColor];
            view.Init(tileColor, i, mat);

            Collider tileCollider = tileObject.GetComponentInChildren<Collider>();
            if (tileCollider)
            {
                tileCollider.enabled = false;
            }
        }
    }

    // ---------------- Raycast ----------------

    private bool RaycastCell(Vector2 screenPos, out HexCell hitCell)
    {
        hitCell = null;
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (!Physics.Raycast(ray, out RaycastHit hitInfo, 200f))
        {
            return false;
        }

        hitCell = hitInfo.collider.GetComponentInParent<HexCell>();
        return hitCell != null;
    }

    private bool RaycastHandPiece(Vector2 screenPos, out HandPiece hitPiece)
    {
        hitPiece = null;
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (!Physics.Raycast(ray, out RaycastHit hitInfo, 200f))
        {
            return false;
        }

        hitPiece = hitInfo.collider.GetComponentInParent<HandPiece>();
        return hitPiece != null;
    }
}