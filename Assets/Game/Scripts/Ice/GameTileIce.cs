using System;
using System.Collections;
using UnityEngine;

namespace Grid
{
    public class GameTileIce : MonoBehaviour
    {
        [Header("Ice Pieces")]
        public GameObject[] icePieces;

        [Header("Optional FX")]
        public Animator animator;
        public string hitTriggerName = "Melt";

    
        private const int MaxMeltCount = 3;

        private int meltCount = 0;

        public void ResetIce()
        {
            meltCount = 0;

            if (icePieces != null)
            {
                foreach (var piece in icePieces)
                {
                    if (piece != null)
                        piece.SetActive(true);
                }
            }

            gameObject.SetActive(true);
        }

        public void MeltTile(Action onComplete = null, Action onMelt = null)
        {
            if (meltCount >= MaxMeltCount)
                return;

            meltCount++;

            if (animator != null && !string.IsNullOrEmpty(hitTriggerName))
                animator.SetTrigger(hitTriggerName);

            HideOneIcePiece();

            StartCoroutine(MeltRoutine(onComplete, onMelt));
        }

        private void HideOneIcePiece()
        {
            if (icePieces == null || icePieces.Length == 0)
                return;

        
            int pieceIndex = meltCount - 1;

            if (pieceIndex >= 0 && pieceIndex < icePieces.Length && icePieces[pieceIndex] != null)
                icePieces[pieceIndex].SetActive(false);
        }

        private IEnumerator MeltRoutine(Action onComplete, Action onMelt)
        {
            yield return new WaitForSeconds(0.1f);

            onComplete?.Invoke();

            if (meltCount >= MaxMeltCount)
            {
                yield return new WaitForSeconds(0.15f);

                onMelt?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}
