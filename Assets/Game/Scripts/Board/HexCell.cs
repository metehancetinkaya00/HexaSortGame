using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class HexCell : MonoBehaviour
{
    [Header("Coord (runtime set)")]
    public Hex coord;

    public HexStack Stack { get; private set; }

    public List<GameObject> views = new List<GameObject>();

    [Header("Lock State")]
    public HexCellKind cellKind = HexCellKind.Normal;
    public int requiredClearCount = 0;
    public bool isUnlocked = true;

    [Header("Optional Visuals")]
    public GameObject lockedVisual;
    public TMP_Text lockedCountText;

    public void Init(Hex newCoord)
    {
        coord = newCoord;
        Stack = new HexStack(this);
        name = $"Cell {coord}";
    }

    public void SetupCellState(HexCellKind newKind, int newRequiredClearCount)
    {
        cellKind = newKind;
        requiredClearCount = Mathf.Max(0, newRequiredClearCount);

        if (cellKind == HexCellKind.Locked)
        {
            isUnlocked = false;
        }
        else
        {
            isUnlocked = true;
        }

        RefreshCellVisual(0);
    }

    public bool IsAvailable()
    {
        if (cellKind == HexCellKind.Empty)
        {
            return false;
        }

        if (cellKind == HexCellKind.Locked && !isUnlocked)
        {
            return false;
        }

        return true;
    }

    public bool TryUnlock(int currentClearCount)
    {
        if (cellKind != HexCellKind.Locked)
        {
            return false;
        }

        if (isUnlocked)
        {
            return false;
        }

        if (currentClearCount < requiredClearCount)
        {
            RefreshCellVisual(currentClearCount);
            return false;
        }

        isUnlocked = true;
        RefreshCellVisual(currentClearCount);
        return true;
    }

    public void RefreshCellVisual(int currentClearCount)
    {
        bool showLocked = cellKind == HexCellKind.Locked && !isUnlocked;

        if (lockedVisual != null)
        {
            lockedVisual.SetActive(showLocked);
        }

        if (lockedCountText != null)
        {
            lockedCountText.gameObject.SetActive(showLocked);

            if (showLocked)
            {
                int remaining = requiredClearCount - currentClearCount;
                if (remaining < 0)
                {
                    remaining = 0;
                }

                lockedCountText.text = remaining.ToString();
            }
        }
    }
}