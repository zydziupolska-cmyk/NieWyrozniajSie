using System.Collections.Generic;
using UnityEngine;

public enum SpotKind { Bench, Queue, Peek, Chat }

// Miejsce, w ktorym bot moze "cos robic": usiasc na lawce, stanac w kolejce,
// zajrzec do auta / na tablice, pogadac w grupce. Szpieg moze robic to samo (emotki).
public class ActivitySpot
{
    public static readonly List<ActivitySpot> All = new List<ActivitySpot>();

    public SpotKind kind;
    public Vector3 standPoint;          // gdzie stoi kontroler (na NavMeshu)
    public Vector3 facing;              // w ktora strone patrzy postac
    public Vector3 bodyOffset;          // przesuniecie ciala (np. na siedzisko lawki)
    public ActivitySpot nextInQueue;    // miejsce blizej okienka (null = pierwszy w kolejce)
    public CrowdMember occupant;

    public bool IsFree => occupant == null || occupant.IsDead;

    public Emote Emote
    {
        get
        {
            switch (kind)
            {
                case SpotKind.Bench: return Emote.Sit;
                case SpotKind.Peek: return Emote.Peek;
                case SpotKind.Chat: return Emote.Chat;
                default: return nextInQueue == null ? Emote.Chat : Emote.None; // pierwszy w kolejce "kupuje"
            }
        }
    }

    public static ActivitySpot Add(SpotKind kind, Vector3 standPoint, Vector3 facing, Vector3 bodyOffset = default)
    {
        facing.y = 0f;
        var s = new ActivitySpot
        {
            kind = kind,
            standPoint = standPoint,
            facing = facing.sqrMagnitude > 0.001f ? facing.normalized : Vector3.forward,
            bodyOffset = bodyOffset
        };
        All.Add(s);
        return s;
    }

    // Losowe wolne miejsce w zasiegu (blizsze sa bardziej prawdopodobne)
    public static ActivitySpot FindFree(Vector3 pos, float maxDistance)
    {
        ActivitySpot best = null;
        float bestScore = float.MaxValue;
        foreach (var s in All)
        {
            if (!s.IsFree) continue;
            // Do kolejki dolacza sie tylko na koncu (za kims albo na pierwszym wolnym miejscu)
            if (s.kind == SpotKind.Queue && s.nextInQueue != null && s.nextInQueue.IsFree) continue;
            float d = Vector3.Distance(pos, s.standPoint);
            if (d > maxDistance) continue;
            float score = d + Random.Range(0f, 15f);
            if (score < bestScore) { bestScore = score; best = s; }
        }
        return best;
    }

    // Najblizsza lawka w poblizu gracza (emotka "usiadz")
    public static ActivitySpot NearestFree(Vector3 pos, SpotKind kind, float maxDistance)
    {
        ActivitySpot best = null;
        float bestD = maxDistance;
        foreach (var s in All)
        {
            if (s.kind != kind || !s.IsFree) continue;
            float d = Vector3.Distance(pos, s.standPoint);
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }
}
