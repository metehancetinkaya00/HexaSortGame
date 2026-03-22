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

        private int meltCount = 0;

        public void ResetIce()
        {
            meltCount = 0;

            if (icePieces != null)
            {
                for (int i = 0; i < icePieces.Length; i++)
                {
                    if (icePieces[i] != null)
                    {
                        icePieces[i].SetActive(true);
                    }
                }
            }

            gameObject.SetActive(true);
        }

        public void MeltTile(Action onComplete = null, Action onMelt = null)
        {
            if (meltCount >= 3)
            {
                return;
            }

            meltCount++;

            if (animator != null && !string.IsNullOrEmpty(hitTriggerName))
            {
                animator.SetTrigger(hitTriggerName);
            }

            HideOneIcePiece();

            StartCoroutine(MeltRoutine(onComplete, onMelt));
        }

        private void HideOneIcePiece()
        {
            if (icePieces == null || icePieces.Length == 0)
            {
                return;
            }

            int pieceIndex = meltCount - 1;

            if (pieceIndex >= 0 && pieceIndex < icePieces.Length)
            {
                if (icePieces[pieceIndex] != null)
                {
                    icePieces[pieceIndex].SetActive(false);
                }
            }
        }

        private IEnumerator MeltRoutine(Action onComplete, Action onMelt)
        {
            yield return new WaitForSeconds(0.1f);

            if (onComplete != null)
            {
                onComplete.Invoke();
            }

            if (meltCount >= 3)
            {
                yield return new WaitForSeconds(0.15f);

                if (onMelt != null)
                {
                    onMelt.Invoke();
                }

                Destroy(gameObject);
            }
        }
    }
}