using RimWorld;
using RimWorld.Planet;
using Verse;

namespace TSA.MicroQuest
{
    public static class Util
    {
        public static bool TryFindQuestSiteTile(PlanetTile origin, int minDistance, int maxDistance, out PlanetTile tile)
        {
            // Prefer the dedicated site-tile finder (distance from a player tile).
            if (TileFinder.TryFindNewSiteTile(out tile, minDistance, maxDistance))
            {
                float dist = Find.WorldGrid.ApproxDistanceInTiles(origin, tile);
                if (dist >= minDistance && dist <= maxDistance)
                    return true;
            }

            // Fallback: random candidates in the distance band (never the origin tile).
            for (int i = 0; i < 2000; i++)
            {
                PlanetTile candidate = TileFinder.RandomStartingTile();
                if (candidate == origin)
                    continue;

                float dist = Find.WorldGrid.ApproxDistanceInTiles(origin, candidate);
                if (dist < minDistance || dist > maxDistance)
                    continue;
                if (!TileFinder.IsValidTileForNewSettlement(candidate))
                    continue;
                if (Find.WorldObjects.AnyWorldObjectAt(candidate))
                    continue;

                tile = candidate;
                return true;
            }

            tile = PlanetTile.Invalid;
            return false;
        }
    }
}
