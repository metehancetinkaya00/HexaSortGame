using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Grid;

public class HexCell : MonoBehaviour
{
    [Header("Coord (runtime set)")]
    public Hex coord;

    public HexStack Stack { get; private set; }

    public List<GameObject> views = new List<GameObject>();

    [Header("Lock State")]
    public HexCellKind cellKind = HexCellKind.Normal;
    public bool isUnlocked = true;

    [Header("Ice Lock")]
    public TMP_Text lockedCountText;
    public GameTileIce iceTile;


    private const int RequiredHitCount = 3;
    private int currentHitCount = 0;

    public void Init(Hex newCoord)
    {
        coord = newCoord;
        Stack = new HexStack(this);
        name = $"Cell {coord}";
    }

  
    public void SetupCellState(HexCellKind newKind, int requiredClearCount)
    {
        cellKind = newKind;

        if (cellKind == HexCellKind.Locked)
        {
            isUnlocked = false;
            currentHitCount = 0;

            if (iceTile != null)
                iceTile.ResetIce();
        }
        else
        {
            isUnlocked = true;
            currentHitCount = 0;
        }

        RefreshCellVisual();
    }

    public bool IsAvailable()
    {
        if (cellKind == HexCellKind.Empty)
            return false;

        if (cellKind == HexCellKind.Locked && !isUnlocked)
            return false;

        return true;
    }


    public bool DamageLock(int amount)
    {
        if (cellKind != HexCellKind.Locked)
            return false;

        if (isUnlocked)
            return false;

        if (amount <= 0)
            return false;

        for (int i = 0; i < amount; i++)
        {
            if (isUnlocked)
                break;

            currentHitCount++;

            if (iceTile != null)
            {
                iceTile.MeltTile(
                    onComplete: null,
                    onMelt: OnIceFullyMelted
                );
            }

            if (currentHitCount >= RequiredHitCount)
            {
                currentHitCount = RequiredHitCount;

           
                if (iceTile == null)
                    isUnlocked = true;

                break;
            }
        }

        RefreshCellVisual();
        return true;
    }

    private void OnIceFullyMelted()
    {
        isUnlocked = true;
        RefreshCellVisual();
    }

    public int GetRemainingHitCount()
    {
        if (isUnlocked)
            return 0;

        int remaining = RequiredHitCount - currentHitCount;
        return Mathf.Max(0, remaining);
    }

    public void RefreshCellVisual()
    {
        bool showLocked = cellKind == HexCellKind.Locked && !isUnlocked;

        if (lockedCountText != null)
        {
            lockedCountText.gameObject.SetActive(showLocked);

            if (showLocked)
                lockedCountText.text = GetRemainingHitCount().ToString();
        }

        if (iceTile != null && iceTile.gameObject != null)
        {
          
            bool shouldShow = showLocked;
            if (iceTile.gameObject.activeSelf != shouldShow)
                iceTile.gameObject.SetActive(shouldShow);
        }
    }
}