using System;
using System.Collections.Generic;
using UnityEngine;


public static class HexVectorExtensions
{
    public static Vector2 WorldToPlanar(this Vector3 world)
    {
        return new Vector2(world.x, world.z);
    }

    public static Vector3 PlanarToWorld(this Vector2 planar, float y)
    {
        return new Vector3(planar.x, y, planar.y);
    }

    public static Vector3 PlanarToWorld(this Vector2 planar)
    {
        return new Vector3(planar.x, 0f, planar.y);
    }

    public static Hex ToHex(this Vector3 world)
    {
        return Hex.FromWorld(world);
    }

    public static Hex ToHex(this Vector2 planar)
    {
        return Hex.FromPlanar(planar);
    }
}


[Serializable]
public struct Hex : IEquatable<Hex>
{
    public static float radius = 0.5f;

    
    public static Vector2 qbasis = new Vector2(2f, 0f) * radius;
    public static Vector2 rbasis = new Vector2(1f, Mathf.Sqrt(3f)) * radius;

    public static Vector2 qaxisinverse = new Vector2(0.5f, -Mathf.Sqrt(3f) / 6f);
    public static Vector2 raxisinverse = new Vector2(0f, Mathf.Sqrt(3f) / 3f);

    public static Hex[] axialdirection = new Hex[]
    {
        new Hex(1, 0),
        new Hex(0, 1),
        new Hex(-1, 1),
        new Hex(-1, 0),
        new Hex(0, -1),
        new Hex(1, -1),
    };

    public static Hex zero = new Hex(0, 0);

    public int col;
    public int row;

    public Hex(float col, float row) : this(Mathf.RoundToInt(col), Mathf.RoundToInt(row)) { }

    public Hex(int col, int row)
    {
        this.col = col;
        this.row = row;
    }

    public static Hex FromPlanar(Vector2 planar)
    {
        float col = Vector2.Dot(planar, qaxisinverse) / radius;
        float row = Vector2.Dot(planar, raxisinverse) / radius;
        return new Hex(col, row);
    }

    public static Hex FromWorld(Vector3 world)
    {
        return FromPlanar(world.WorldToPlanar());
    }

    public Vector2 ToPlanar()
    {
        return qbasis * col + rbasis * row;
    }

    public Vector3 ToWorld(float y)
    {
        return ToPlanar().PlanarToWorld(y);
    }

    public Vector3 ToWorld()
    {
        return ToPlanar().PlanarToWorld(0f);
    }

    public IEnumerable<Hex> Neighbours()
    {
        for (int i = 0; i < axialdirection.Length; i++)
            yield return this + axialdirection[i];
    }

    public Hex GetNeighbour(int dir)
    {
        int idx = dir % axialdirection.Length;
        if (idx < 0) idx += axialdirection.Length;
        return this + axialdirection[idx];
    }

    public int DistanceTo(Hex other)
    {
        return (Mathf.Abs(col - other.col)
              + Mathf.Abs(col + row - other.col - other.row)
              + Mathf.Abs(row - other.row)) / 2;
    }

    public static Hex operator +(Hex a, Hex b) => new Hex(a.col + b.col, a.row + b.row);
    public static Hex operator -(Hex a, Hex b) => new Hex(a.col - b.col, a.row - b.row);

 
    public static IEnumerable<Hex> Ring(Hex center, int radius)
    {
        if (radius <= 0) yield break;

        Hex current = center + new Hex(0, -radius);

        for (int directionIndex = 0; directionIndex < axialdirection.Length; directionIndex++)
        {
            Hex dir = axialdirection[directionIndex];
            for (int i = 0; i < radius; i++)
            {
                yield return current;
                current = current + dir;
            }
        }
    }

   
    public static IEnumerable<Hex> Spiral(Hex center, int minRadius, int maxRadius)
    {
        if (minRadius <= 0)
        {
            yield return center;
            minRadius = 1;
        }

        for (int radius = minRadius; radius <= maxRadius; radius++)
            foreach (Hex hex in Ring(center, radius))
                yield return hex;
    }

  
    public static IEnumerable<Hex> FloodFill(IEnumerable<Hex> startFrom)
    {
        if (startFrom == null) yield break;

        HashSet<Hex> visited = new HashSet<Hex>();
        Queue<Hex> frontier = new Queue<Hex>();

        foreach (var startCell in startFrom)
        {
            if (visited.Add(startCell))
                frontier.Enqueue(startCell);
        }

        while (frontier.Count > 0)
        {
            Hex current = frontier.Dequeue();
            yield return current;

            foreach (Hex next in current.Neighbours())
            {
                if (visited.Add(next))
                    frontier.Enqueue(next);
            }
        }
    }

    public bool Equals(Hex other) => col == other.col && row == other.row;
    public override bool Equals(object obj) => obj is Hex other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(col, row);

    public override string ToString() => $"({col};{row})";
}