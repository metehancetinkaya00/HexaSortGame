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

    public HexLevelLayout hexLevelLayout;

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

    private readonly Dictionary<Hex, HexCell> cells = new Dictionary<Hex, HexCell>();
    private readonly Queue<HexCell> resolveQueue = new Queue<HexCell>();
    private readonly HashSet<HexCell> queuedCells = new HashSet<HexCell>();
    private readonly HashSet<HexCell> busyCells = new HashSet<HexCell>();

    private Camera cam;
    private System.Random rng;
    private Transform activeAnchor;
    private int piecesLeftInPack;

    private HandPiece dragSourcePiece;
    private GameObject dragGhostObject;
    private GameObject hiddenSourceObject;

    private Vector2 lastPointerScreenPos;
    private bool hasPointerPos;

    private bool resolveRunning;
    private bool resolveRequested;
    private bool hasFailed;

    private GameObject dropIndicatorObject;

    private int HandSlotCount
    {
        get
        {
            if (randomPack != null)
                return Mathf.Max(1, randomPack.piecesPerPack);

            return 3;
        }
    }

    private void Awake()
    {
        cam = Camera.main;

        if (!ValidateSetup())
        {
            enabled = false;
            return;
        }

        rng = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);

        CreateDropIndicator();

        for (int i = 0; i < setAnchors.Length; i++)
        {
            EnsureHandSlots(setAnchors[i], HandSlotCount);
        }

        BuildBoardFromLayout();
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

        for (int i = 0; i < setAnchors.Length; i++)
        {
            if (setAnchors[i] == null)
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
            return;

        dropIndicatorObject = Instantiate(dropIndicatorPrefab);
        dropIndicatorObject.name = "DropIndicator";
        dropIndicatorObject.SetActive(false);

        Collider[] colliders = dropIndicatorObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private void BuildBoardFromLayout()
    {
        cells.Clear();

        List<Hex> coords = new List<Hex>();

        if (hexLevelLayout != null)
        {
            foreach (Hex hexCoord in hexLevelLayout.EnumerateHexes())
            {
                coords.Add(hexCoord);
            }
        }
        else
        {
            foreach (Hex hexCoord in Hex.Spiral(Hex.zero, 0, boardRadius))
            {
                coords.Add(hexCoord);
            }
        }

        for (int i = 0; i < coords.Count; i++)
        {
            Hex hexCoord = coords[i];

            HexCell cellInstance = Instantiate(cellPrefab, cellsRoot);
            cellInstance.Init(hexCoord);
            cellInstance.transform.position = hexCoord.ToWorld(yCell);
            cellInstance.Stack.SetTiles(System.Array.Empty<TileColor>());

            cells[hexCoord] = cellInstance;
        }
    }

    private void GenerateNextPack()
    {
        if (chooseRandomAnchorEachPack)
        {
            int anchorIndex = rng.Next(0, setAnchors.Length);
            activeAnchor = setAnchors[anchorIndex];
        }
        else if (activeAnchor == null)
        {
            activeAnchor = setAnchors[0];
        }

        int slotCount = HandSlotCount;
        piecesLeftInPack = slotCount;

        EnsureHandSlots(activeAnchor, slotCount);
        ClearAnchorHand(activeAnchor, slotCount);

        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            Transform slotTransform = activeAnchor.Find("HandSlot" + slotIndex);
            if (slotTransform == null)
                continue;

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
            if (slotTransform == null)
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
            if (slotTransform == null)
                continue;

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
        boxCollider.center = new Vector3(0f, handPieceColliderSize.y * 0.5f, 0f);

        HandPiece piece = pieceObject.AddComponent<HandPiece>();
        piece.SetTiles(tiles);

        BuildGhostFromTiles(piece.tiles, pieceObject.transform);
    }

    private void Update()
    {
        if (hasFailed)
            return;

        if (TryGetPointerScreenPos(out Vector2 pointerScreenPos))
        {
            lastPointerScreenPos = pointerScreenPos;
            hasPointerPos = true;
        }

        if (!hasPointerPos)
            return;

        if (WasPressedThisFrame())
            TryBeginDrag(lastPointerScreenPos);

        if (IsPressed())
            DragUpdate(lastPointerScreenPos);

        if (WasReleasedThisFrame())
            TryEndDrag(lastPointerScreenPos);
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
            return Touchscreen.current.primaryTouch.press.wasPressedThisFrame;

        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private bool WasReleasedThisFrame()
    {
        if (Touchscreen.current != null)
            return Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;

        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
    }

    private bool IsPressed()
    {
        if (Touchscreen.current != null)
            return Touchscreen.current.primaryTouch.press.isPressed;

        return Mouse.current != null && Mouse.current.leftButton.isPressed;
    }

    private void TryBeginDrag(Vector2 screenPos)
    {
        if (dragGhostObject != null)
            return;

        if (RaycastHandPiece(screenPos, out HandPiece hitPiece) &&
            hitPiece != null &&
            hitPiece.tiles != null &&
            hitPiece.tiles.Count > 0)
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
        if (dragGhostObject == null)
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

        targetCell.Stack.PushMany(tiles);
        SyncCellViews(targetCell);

        Destroy(dragSourcePiece.gameObject);
        dragSourcePiece = null;

        CleanupDrag();

        piecesLeftInPack--;
        if (piecesLeftInPack <= 0)
            GenerateNextPack();

        RequestResolveFromCell(targetCell);
        CheckFailNow();
    }

    private void CleanupDrag()
    {
        if (dragGhostObject != null)
            Destroy(dragGhostObject);

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
        if (pieceObject == null)
            return;

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

    private bool CanDropOnCell(HexCell cell)
    {
        if (cell == null || cell.Stack == null)
            return false;

        if (busyCells.Contains(cell))
            return false;

        return cell.Stack.IsEmpty;
    }

    private void UpdateDropPreview(Vector2 screenPos)
    {
        if (dropIndicatorObject == null)
            return;

        bool isDragging = dragGhostObject != null && dragSourcePiece != null;
        if (!isDragging)
        {
            dropIndicatorObject.SetActive(false);
            return;
        }

        if (RaycastCell(screenPos, out HexCell targetCell) &&
            targetCell != null &&
            CanDropOnCell(targetCell))
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
        if (dropIndicatorObject != null)
            dropIndicatorObject.SetActive(false);
    }

    private bool IsBoardFull()
    {
        foreach (KeyValuePair<Hex, HexCell> pair in cells)
        {
            HexCell cell = pair.Value;
            if (cell != null && cell.Stack != null && cell.Stack.IsEmpty)
                return false;
        }

        return true;
    }

    private void CheckFailNow()
    {
        if (hasFailed)
            return;

        if (IsBoardFull())
        {
            hasFailed = true;
            CleanupDrag();

            if (scoreManager != null)
                scoreManager.ShowFailed();
        }
    }

    private void RequestResolveFromCell(HexCell cell)
    {
        if (cell == null)
            return;

        resolveRequested = true;
        EnqueueResolve(cell);

        if (!resolveRunning)
            StartCoroutine(ResolveLoop());
    }

    private void EnqueueResolve(HexCell cell)
    {
        if (cell == null)
            return;

        if (queuedCells.Contains(cell))
            return;

        queuedCells.Add(cell);
        resolveQueue.Enqueue(cell);
    }

    private IEnumerator ResolveLoop()
    {
        resolveRunning = true;

        while (resolveQueue.Count > 0 || resolveRequested)
        {
            resolveRequested = false;

            HexCell cell = GetNextSeedCell();
            if (cell == null)
                break;

            yield return StartCoroutine(CheckForMerge(cell));
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

            if (queuedCell != null && queuedCell.Stack != null && !queuedCell.Stack.IsEmpty)
                seedCell = queuedCell;
        }

        if (seedCell != null)
            return seedCell;

        foreach (KeyValuePair<Hex, HexCell> pair in cells)
        {
            HexCell cell = pair.Value;
            if (cell != null && cell.Stack != null && !cell.Stack.IsEmpty)
                return cell;
        }

        return null;
    }

    private IEnumerator CheckForMerge(HexCell gridCell)
    {
        if (gridCell == null || gridCell.Stack == null || gridCell.Stack.IsEmpty)
            yield break;

        if (busyCells.Contains(gridCell))
            yield break;

        List<HexCell> neighborGridCells = GetOccupiedNeighborCells(gridCell);
        if (neighborGridCells.Count <= 0)
            yield break;

        TileColor? topColor = gridCell.Stack.TopColor;
        if (topColor == null)
            yield break;

        List<HexCell> similarNeighborGridCells = GetSimilarNeighborCells(topColor.Value, neighborGridCells);
        if (similarNeighborGridCells.Count <= 0)
            yield break;

        List<HexCell> updatedCells = new List<HexCell>();
        updatedCells.Add(gridCell);
        updatedCells.AddRange(similarNeighborGridCells);

        yield return StartCoroutine(MoveHexagonsToCell(gridCell, similarNeighborGridCells, topColor.Value));
        yield return StartCoroutine(CheckAndClearCell(gridCell));

        for (int i = 0; i < updatedCells.Count; i++)
        {
            if (updatedCells[i] != null)
                EnqueueResolve(updatedCells[i]);
        }
    }

    private List<HexCell> GetOccupiedNeighborCells(HexCell gridCell)
    {
        List<HexCell> result = new List<HexCell>();

        foreach (Hex neighborHex in gridCell.coord.Neighbours())
        {
            if (!cells.TryGetValue(neighborHex, out HexCell neighborCell))
                continue;

            if (neighborCell == null || neighborCell.Stack == null || neighborCell.Stack.IsEmpty)
                continue;

            result.Add(neighborCell);
        }

        return result;
    }

    private List<HexCell> GetSimilarNeighborCells(TileColor targetColor, List<HexCell> neighbors)
    {
        List<HexCell> result = new List<HexCell>();

        for (int i = 0; i < neighbors.Count; i++)
        {
            HexCell neighborCell = neighbors[i];
            if (neighborCell == null || neighborCell.Stack == null || neighborCell.Stack.IsEmpty)
                continue;

            TileColor? neighborTop = neighborCell.Stack.TopColor;
            if (neighborTop == null)
                continue;

            if (neighborTop.Value == targetColor)
                result.Add(neighborCell);
        }

        return result;
    }

    private IEnumerator MoveHexagonsToCell(HexCell targetCell, List<HexCell> sourceCells, TileColor targetColor)
    {
        if (targetCell == null || sourceCells == null || sourceCells.Count == 0)
            yield break;

        for (int i = 0; i < sourceCells.Count; i++)
        {
            HexCell sourceCell = sourceCells[i];

            if (sourceCell == null || sourceCell == targetCell)
                continue;

            if (busyCells.Contains(sourceCell) || busyCells.Contains(targetCell))
                continue;

            SyncCellViews(sourceCell);
            SyncCellViews(targetCell);

            List<TileColor> movingPack = sourceCell.Stack.PopTopRun();
            if (movingPack.Count == 0)
                continue;

            if (movingPack[0] != targetColor)
            {
                sourceCell.Stack.PushMany(movingPack);
                continue;
            }

            if (targetCell.Stack.Count + movingPack.Count > maxStackHeight)
            {
                sourceCell.Stack.PushMany(movingPack);
                continue;
            }

            int takeFromIndex = Mathf.Max(0, sourceCell.views.Count - movingPack.Count);
            List<GameObject> sourceViews = sourceCell.views.GetRange(takeFromIndex, movingPack.Count);
            sourceCell.views.RemoveRange(takeFromIndex, sourceViews.Count);

            targetCell.Stack.PushMany(movingPack);
            targetCell.views.AddRange(sourceViews);

            yield return StartCoroutine(AnimateTransferredViews(sourceCell, targetCell, sourceViews));
        }
    }

    private IEnumerator AnimateTransferredViews(HexCell fromCell, HexCell toCell, List<GameObject> movedViews)
    {
        if (fromCell == null || toCell == null || movedViews == null || movedViews.Count == 0)
            yield break;

        busyCells.Add(fromCell);
        busyCells.Add(toCell);

        float duration = mergeMoveDuration;
        float stepDelay = mergeStepDelay;

        int targetStartIndex = toCell.views.Count - movedViews.Count;

        for (int i = 0; i < movedViews.Count; i++)
        {
        
            int sourceIndex = movedViews.Count - 1 - i;
            GameObject tileObject = movedViews[sourceIndex];

  
            int targetIndex = targetStartIndex + i;

            Vector3 basePos = toCell.transform.position;
            Vector3 targetWorldPos = new Vector3(
                basePos.x,
                yCell + targetIndex * tileHeight,
                basePos.z
            );

            float delay = i * stepDelay;
            MoveTileToWorld(tileObject, targetWorldPos, delay, duration);
        }

        float total = (movedViews.Count - 1) * stepDelay + duration;
        yield return new WaitForSeconds(total);

        SyncCellViews(fromCell);
        SyncCellViews(toCell);

        busyCells.Remove(fromCell);
        busyCells.Remove(toCell);
    }

    private IEnumerator CheckAndClearCell(HexCell gridCell)
    {
        if (gridCell == null || gridCell.Stack == null || gridCell.Stack.IsEmpty)
            yield break;

        SyncCellViews(gridCell);

        int similarHexagonCount = gridCell.Stack.TopRunCount();
        if (similarHexagonCount < clearCount)
            yield break;

        List<TileColor> removedRun = gridCell.Stack.PopTopRun();
        if (removedRun.Count < clearCount)
        {
            gridCell.Stack.PushMany(removedRun);
            yield break;
        }

        int startIndex = Mathf.Max(0, gridCell.views.Count - removedRun.Count);
        List<GameObject> similarHexagons = gridCell.views.GetRange(startIndex, removedRun.Count);
        gridCell.views.RemoveRange(startIndex, similarHexagons.Count);

        if (scoreManager != null)
            scoreManager.AddScore(similarHexagons.Count);

        float delay = 0f;
        float step = 0.01f;
        float duration = 0.2f;

        for (int i = similarHexagons.Count - 1; i >= 0; i--)
        {
            GameObject tileObject = similarHexagons[i];
            VanishTile(tileObject, delay, duration);
            delay += step;
        }

        yield return new WaitForSeconds(duration + similarHexagons.Count * step);

        SyncCellViews(gridCell);
    }

    private void SyncAllCells()
    {
        foreach (KeyValuePair<Hex, HexCell> pair in cells)
        {
            SyncCellViews(pair.Value);
        }
    }

    private void SyncCellViews(HexCell cell)
    {
        if (cell == null)
            return;

        List<TileColor> snapshot = new List<TileColor>(cell.Stack.Snapshot());

        while (cell.views.Count > snapshot.Count)
        {
            GameObject viewObject = cell.views[cell.views.Count - 1];
            cell.views.RemoveAt(cell.views.Count - 1);

            if (viewObject != null)
                Destroy(viewObject);
        }

        while (cell.views.Count < snapshot.Count)
        {
            GameObject tileObject = Instantiate(tilePrefab, tileRoot);
            tileObject.name = "Tile_" + cell.coord + "_" + cell.views.Count;

            Collider tileCollider = tileObject.GetComponentInChildren<Collider>();
            if (tileCollider != null)
                tileCollider.enabled = false;

            cell.views.Add(tileObject);
        }

        for (int index = 0; index < snapshot.Count; index++)
        {
            TileColor tileColor = snapshot[index];
            GameObject tileObject = cell.views[index];

            Vector3 basePos = cell.transform.position;
            tileObject.transform.position = new Vector3(basePos.x, yCell + index * tileHeight, basePos.z);

            HexTileView view = tileObject.GetComponent<HexTileView>();
            if (view == null)
                view = tileObject.AddComponent<HexTileView>();

            Material mat = ((int)tileColor >= 0 && (int)tileColor < colorMaterials.Length)
                ? colorMaterials[(int)tileColor]
                : null;

            view.Init(tileColor, index, mat);
        }
    }

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
            tileObject.transform.localPosition = new Vector3(0f, i * tileHeight, 0f);

            HexTileView view = tileObject.GetComponent<HexTileView>();
            if (view == null)
                view = tileObject.AddComponent<HexTileView>();

            Material mat = colorMaterials[(int)tileColor];
            view.Init(tileColor, i, mat);

            Collider tileCollider = tileObject.GetComponentInChildren<Collider>();
            if (tileCollider != null)
                tileCollider.enabled = false;
        }
    }

    private void VanishTile(GameObject tileObject, float delay, float duration)
    {
        if (tileObject == null)
            return;

        Collider tileCollider = tileObject.GetComponentInChildren<Collider>();
        if (tileCollider != null)
            tileCollider.enabled = false;

        LeanTween.cancel(tileObject);

        LeanTween.scale(tileObject, Vector3.zero, duration)
            .setEase(LeanTweenType.easeInBack)
            .setDelay(delay)
            .setOnComplete(() => Destroy(tileObject));
    }

    private void MoveTileToWorld(GameObject tileObject, Vector3 targetWorldPos, float delay, float duration)
    {
        if (tileObject == null)
            return;

        LeanTween.cancel(tileObject);

        LeanTween.move(tileObject, targetWorldPos, duration)
            .setEase(LeanTweenType.easeInOutSine)
            .setDelay(delay);

        Vector3 flatFrom = tileObject.transform.position;
        flatFrom.y = 0f;

        Vector3 flatTo = targetWorldPos;
        flatTo.y = 0f;

        Vector3 direction = (flatTo - flatFrom).normalized;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.forward;

        Vector3 rotationAxis = Vector3.Cross(Vector3.up, direction);

        LeanTween.rotateAround(tileObject, rotationAxis, 180f, duration)
            .setEase(LeanTweenType.easeInOutSine)
            .setDelay(delay);
    }

    private bool RaycastCell(Vector2 screenPos, out HexCell hitCell)
    {
        hitCell = null;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hitInfo, 200f))
            return false;

        hitCell = hitInfo.collider.GetComponentInParent<HexCell>();
        return hitCell != null;
    }

    private bool RaycastHandPiece(Vector2 screenPos, out HandPiece hitPiece)
    {
        hitPiece = null;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hitInfo, 200f))
            return false;

        hitPiece = hitInfo.collider.GetComponentInParent<HandPiece>();
        return hitPiece != null;
    }

    public void RestartLevel()
    {
        StopAllCoroutines();

        hasFailed = false;
        dragSourcePiece = null;

        if (dragGhostObject != null)
        {
            Destroy(dragGhostObject);
            dragGhostObject = null;
        }

        if (hiddenSourceObject != null)
        {
            SetHandPieceVisible(hiddenSourceObject, true);
            hiddenSourceObject = null;
        }

        resolveRunning = false;
        resolveRequested = false;

        resolveQueue.Clear();
        queuedCells.Clear();
        busyCells.Clear();

        if (cellsRoot != null)
        {
            for (int i = cellsRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(cellsRoot.GetChild(i).gameObject);
            }
        }

        if (tileRoot != null)
        {
            for (int i = tileRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(tileRoot.GetChild(i).gameObject);
            }
        }

        ClearAllAnchorsHands();
        cells.Clear();

        rng = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);

        BuildBoardFromLayout();
        SyncAllCells();
        GenerateNextPack();
    }
    
    private void ClearAllAnchorsHands()
    {
        if (setAnchors == null)
            return;

        int slotCount = HandSlotCount;

        for (int i = 0; i < setAnchors.Length; i++)
        {
            Transform anchor = setAnchors[i];
            if (anchor == null)
                continue;

            EnsureHandSlots(anchor, slotCount);
            ClearAnchorHand(anchor, slotCount);
        }
    }
}