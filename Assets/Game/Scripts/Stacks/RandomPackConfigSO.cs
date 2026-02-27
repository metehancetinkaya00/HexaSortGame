using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "HexaSort/Random Pack Config", fileName = "RandomPackConfig")]
public class RandomPackConfigSO : ScriptableObject
{
    [Header("Pack")]
    [Min(1)] public int piecesPerPack = 3;

    [Header("Piece Size")]
    [Min(1)] public int minHexPerPiece = 3;
    [Min(1)] public int maxHexPerPiece = 8;

    [Header("Allowed Colors + Weights (0 = never)")]
    public List<ColorWeight> weights = new();

    [Serializable]
    public struct ColorWeight
    {
        public TileColor color;
        [Min(0f)] public float weight;
    }

    public List<TileColor> GeneratePiece(System.Random rng)
    {
        if (rng == null) rng = new System.Random();

        int min = Mathf.Max(1, minHexPerPiece);
        int max = Mathf.Max(min, maxHexPerPiece);
        int count = rng.Next(min, max + 1);

        var result = new List<TileColor>(count);
        for (int i = 0; i < count; i++)
            result.Add(PickWeightedColor(rng));

        return result;
    }

    private TileColor PickWeightedColor(System.Random rng)
    {
        
        if (weights == null || weights.Count == 0)
            return PickAnyColor(rng);

        float total = 0f;
        for (int i = 0; i < weights.Count; i++)
            total += Mathf.Max(0f, weights[i].weight);


        if (total <= 0.0001f)
            return PickAnyColor(rng);

        float roll = (float)(rng.NextDouble() * total);
        float acc = 0f;

        for (int i = 0; i < weights.Count; i++)
        {
            float w = Mathf.Max(0f, weights[i].weight);
            acc += w;
            if (roll <= acc)
                return weights[i].color;
        }

        return weights[weights.Count - 1].color;
    }

    private static TileColor PickAnyColor(System.Random rng)
    {
        var values = (TileColor[])Enum.GetValues(typeof(TileColor));
        return values[rng.Next(0, values.Length)];
    }
}