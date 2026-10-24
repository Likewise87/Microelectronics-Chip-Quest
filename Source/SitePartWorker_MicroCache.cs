using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace TSA.MicroQuest
{
    public class SitePartWorker_MicroCache : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);

            var comp = Current.Game?.GetComponent<GameComponent_MicroQuest>();
            if (comp != null)
                comp.CacheMapGenerated = true;

            var S = ModInit.Settings ?? new Settings();

            if (!TryFindCompoundRect(map, 8, 8, 2, out CellRect inner, out _))
            {
                Log.Warning("[TSA] No clean 8x8 area found; force-carving at map center.");
                inner = CellRect.CenteredOn(map.Center, 8, 8).ClipInsideMap(map);
            }

            // Prefer placing the house west; if that clips too hard, try east.
            CellRect houseRect = BuildHouseRect(map, inner, westSide: true);
            if (houseRect.Width < 5 || houseRect.Height < 5)
                houseRect = BuildHouseRect(map, inner, westSide: false);

            IntVec3 genCell = new IntVec3(houseRect.minX - 2, 0, houseRect.CenterCell.z);
            if (!genCell.InBounds(map))
                genCell = new IntVec3(houseRect.maxX + 2, 0, houseRect.CenterCell.z);
            if (!genCell.InBounds(map))
                genCell = new IntVec3(houseRect.CenterCell.x, 0, houseRect.minZ - 2);

            CellRect masterRect = CellRect.FromLimits(
                Math.Min(Math.Min(houseRect.minX, inner.minX), genCell.x),
                Math.Min(Math.Min(houseRect.minZ, inner.minZ), genCell.z),
                Math.Max(Math.Max(houseRect.maxX, inner.maxX), genCell.x),
                Math.Max(Math.Max(houseRect.maxZ, inner.maxZ), genCell.z)
            );

            WipeMountainAndRoofs(map, masterRect.ExpandedBy(3));

            var paved = DefDatabase<TerrainDef>.GetNamedSilentFail("PavedTile")
                        ?? DefDatabase<TerrainDef>.GetNamedSilentFail("Concrete")
                        ?? TerrainDefOf.Soil;
            foreach (var c in inner)
            {
                if (c.InBounds(map))
                    map.terrainGrid.SetTerrain(c, paved);
            }

            var cloth = ThingDefOf.Cloth;
            IntVec3 northGap = new IntVec3(inner.CenterCell.x, 0, inner.maxZ);
            IntVec3 southGap = new IntVec3(inner.CenterCell.x, 0, inner.minZ);

            foreach (var c in inner.EdgeCells)
            {
                if (!c.InBounds(map) || c == northGap || c == southGap) continue;
                var bag = ThingMaker.MakeThing(ThingDefOf.Sandbags, cloth);
                GenSpawn.Spawn(bag, c, map, WipeMode.Vanish);
            }

            var wood = ThingDefOf.WoodLog;
            var tableRect = CellRect.CenteredOn(inner.CenterCell, 2, 2);
            var table2x2 = DefDatabase<ThingDef>.GetNamedSilentFail("Table2x2c");

            if (table2x2 != null)
            {
                var table = ThingMaker.MakeThing(table2x2, wood);
                GenSpawn.Spawn(table, new IntVec3(tableRect.minX, 0, tableRect.minZ), map, Rot4.North, WipeMode.Vanish, false);
            }

            SpawnChairs(map, tableRect, wood);
            SpawnChipAndGoodies(map, tableRect);
            BuildHouse(map, houseRect, wood, genCell);
            SpawnDefenders(map, inner, S);
        }

        private static CellRect BuildHouseRect(Map map, CellRect inner, bool westSide)
        {
            const int houseWidth = 7;
            const int houseHeight = 9;
            int houseMinX;
            if (westSide)
            {
                int houseMaxX = inner.minX - 3;
                houseMinX = houseMaxX - (houseWidth - 1);
            }
            else
            {
                houseMinX = inner.maxX + 3;
            }

            int houseCenterZ = inner.CenterCell.z;
            int houseMinZ = houseCenterZ - (houseHeight / 2);
            return new CellRect(houseMinX, houseMinZ, houseWidth, houseHeight).ClipInsideMap(map);
        }

        private static void WipeMountainAndRoofs(Map map, CellRect rect)
        {
            foreach (var c in rect.ClipInsideMap(map))
            {
                map.fogGrid.Unfog(c);
                map.roofGrid.SetRoof(c, null);

                Building edifice = c.GetEdifice(map);
                if (edifice != null)
                    edifice.Destroy(DestroyMode.Vanish);

                List<Thing> things = c.GetThingList(map).ToList();
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    if (things[i] is Pawn) continue;
                    things[i].Destroy(DestroyMode.Vanish);
                }

                var ter = c.GetTerrain(map);
                if (ter.IsWater || ter.IsRock || ter.passability == Traversability.Impassable)
                    map.terrainGrid.SetTerrain(c, TerrainDefOf.Soil);
            }
        }

        private void BuildHouse(Map map, CellRect houseRect, ThingDef wood, IntVec3 genCell)
        {
            var woodFloor = DefDatabase<TerrainDef>.GetNamedSilentFail("WoodPlankFloor") ?? TerrainDefOf.MetalTile;
            foreach (var c in houseRect)
            {
                if (!c.InBounds(map)) continue;
                map.terrainGrid.SetTerrain(c, woodFloor);
                map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
            }

            IntVec3 doorCell = new IntVec3(houseRect.CenterCell.x, 0, houseRect.minZ);
            foreach (var c in houseRect.EdgeCells)
            {
                if (!c.InBounds(map)) continue;
                Thing wallOrDoor = (c == doorCell)
                    ? ThingMaker.MakeThing(ThingDefOf.Door, wood)
                    : ThingMaker.MakeThing(ThingDefOf.Wall, wood);
                GenSpawn.Spawn(wallOrDoor, c, map, Rot4.North, WipeMode.Vanish, false);
                GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.PowerConduit), c, map, Rot4.North, WipeMode.Vanish, false);
            }

            if (genCell.InBounds(map))
            {
                var genDef = DefDatabase<ThingDef>.GetNamedSilentFail("WoodFiredGenerator") ?? ThingDefOf.WoodFiredGenerator;
                var gen = GenSpawn.Spawn(ThingMaker.MakeThing(genDef), genCell, map, Rot4.North, WipeMode.Vanish, false);
                gen.TryGetComp<CompRefuelable>()?.Refuel(1000f);
                var flick = gen.TryGetComp<CompFlickable>();
                if (flick != null && !flick.SwitchIsOn)
                    flick.DoFlick();
            }

            SpawnInteriorFurniture(map, houseRect, wood);
        }

        private void SpawnInteriorFurniture(Map map, CellRect houseRect, ThingDef wood)
        {
            var bedDef = ThingDefOf.Bed;
            int westX = houseRect.minX + 1;
            int eastX = houseRect.maxX - 1;

            for (int z = houseRect.minZ + 1; z <= houseRect.maxZ - 1; z += 2)
            {
                IntVec3 wPos = new IntVec3(westX, 0, z);
                IntVec3 ePos = new IntVec3(eastX, 0, z);
                if (wPos.InBounds(map) && wPos.Standable(map))
                    GenSpawn.Spawn(ThingMaker.MakeThing(bedDef, wood), wPos, map, Rot4.East, WipeMode.Vanish);
                if (ePos.InBounds(map) && ePos.Standable(map))
                    GenSpawn.Spawn(ThingMaker.MakeThing(bedDef, wood), ePos, map, Rot4.West, WipeMode.Vanish);
            }
        }

        private void SpawnChairs(Map map, CellRect tableRect, ThingDef wood)
        {
            var chairDef = ThingDefOf.DiningChair;
            IntVec3 north = new IntVec3(tableRect.CenterCell.x, 0, tableRect.maxZ + 1);
            IntVec3 south = new IntVec3(tableRect.CenterCell.x, 0, tableRect.minZ - 1);
            if (north.InBounds(map)) GenSpawn.Spawn(ThingMaker.MakeThing(chairDef, wood), north, map, Rot4.South, WipeMode.Vanish);
            if (south.InBounds(map)) GenSpawn.Spawn(ThingMaker.MakeThing(chairDef, wood), south, map, Rot4.North, WipeMode.Vanish);
        }

        private void SpawnChipAndGoodies(Map map, CellRect tableRect)
        {
            var chipDef = DefDatabase<ThingDef>.GetNamedSilentFail(QuestHelper.ChipDefName);
            if (chipDef == null)
            {
                Log.Error("[TSA] Microelectronics_Chip def missing — research softlock risk.");
                return;
            }

            IntVec3 chipCell = new IntVec3(tableRect.maxX + 1, 0, tableRect.CenterCell.z);
            Thing chip = ThingMaker.MakeThing(chipDef);

            if (!TryPlaceOrForce(chip, chipCell, map))
            {
                // Last resort: map center
                if (!TryPlaceOrForce(chip, map.Center, map))
                    Log.Error("[TSA] Failed to place Microelectronics_Chip on cache map.");
            }

            var mealDef = DefDatabase<ThingDef>.GetNamedSilentFail("MealSurvivalPack");
            if (mealDef != null)
            {
                var pack = ThingMaker.MakeThing(mealDef);
                pack.stackCount = 30;
                GenPlace.TryPlaceThing(pack, chipCell + new IntVec3(0, 0, -1), map, ThingPlaceMode.Near);
            }
        }

        private static bool TryPlaceOrForce(Thing thing, IntVec3 preferred, Map map)
        {
            if (preferred.InBounds(map) && GenPlace.TryPlaceThing(thing, preferred, map, ThingPlaceMode.Near))
                return true;

            if (CellFinder.TryFindRandomCellNear(preferred.ClampInsideMap(map), map, 12, c => c.Standable(map), out IntVec3 alt)
                && GenPlace.TryPlaceThing(thing, alt, map, ThingPlaceMode.Direct))
                return true;

            IntVec3 forceCell = preferred.ClampInsideMap(map);
            if (!forceCell.InBounds(map))
                forceCell = map.Center;

            GenSpawn.Spawn(thing, forceCell, map, WipeMode.Vanish);
            return !thing.Destroyed && thing.Spawned;
        }

        private void SpawnDefenders(Map map, CellRect inner, Settings S)
        {
            var t1 = DefDatabase<PawnKindDef>.GetNamedSilentFail("TSA_Tribal_T1")
                     ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("Tribal_Warrior")
                     ?? PawnKindDefOf.Drifter;

            var facDef = DefDatabase<FactionDef>.GetNamedSilentFail("TSA_Tiered_Tribals");
            Faction? fac = null;
            if (facDef != null)
                fac = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.def == facDef);

            fac ??= Find.FactionManager.RandomEnemyFaction(allowHidden: false, allowDefeated: false, allowNonHumanlike: false)
                 ?? Find.FactionManager.RandomEnemyFaction();

            if (fac == null || t1 == null)
            {
                Log.Warning("[TSA] No enemy faction/pawnkind available; skipping cache defenders.");
                return;
            }

            var pawns = new List<Pawn>();
            for (int i = 0; i < S.PawnCount; i++)
            {
                var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(t1, fac));
                GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(inner.CenterCell, map, 3), map, WipeMode.Vanish);
                pawns.Add(p);
            }

            LordMaker.MakeNewLord(fac, new LordJob_ReactiveDefense(inner.CenterCell), map, pawns);
        }

        private static bool TryFindCompoundRect(Map map, int w, int h, int buffer, out CellRect inner, out CellRect expanded)
        {
            for (int radius = 0; radius <= 45; radius += 5)
            {
                if (CellFinder.TryFindRandomCellNear(map.Center, map, radius, c => c.Walkable(map), out var cand))
                {
                    inner = CellRect.CenteredOn(cand, w, h);
                    expanded = inner.ExpandedBy(buffer);
                    if (expanded.FullyContainedWithin(new CellRect(0, 0, map.Size.x, map.Size.z)))
                        return true;
                }
            }
            inner = expanded = default;
            return false;
        }
    }

    public class LordJob_ReactiveDefense : LordJob
    {
        private IntVec3 basePoint;
        public LordJob_ReactiveDefense() { }
        public LordJob_ReactiveDefense(IntVec3 basePoint) => this.basePoint = basePoint;

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();

            LordToil_DefendPoint defend = new LordToil_DefendPoint(basePoint, 12f);
            graph.AddToil(defend);

            LordToil_AssaultColony assault = new LordToil_AssaultColony();
            graph.AddToil(assault);

            Transition transition = new Transition(defend, assault);
            transition.AddTrigger(new Trigger_PawnDamagedCustom());
            transition.AddTrigger(new Trigger_PlayerNearbyOptimized(35f));
            transition.AddPreAction(new TransitionAction_Message("TSA_MicroQuest_DefendersAttacking".Translate(), MessageTypeDefOf.ThreatBig));
            graph.AddTransition(transition);

            return graph;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref basePoint, "basePoint");
        }
    }

    public class Trigger_PlayerNearbyOptimized : Trigger
    {
        private float sqrRadius;
        public Trigger_PlayerNearbyOptimized() { }
        public Trigger_PlayerNearbyOptimized(float radius) => this.sqrRadius = radius * radius;

        public override bool ActivateOn(Lord lord, TriggerSignal signal)
        {
            if (signal.type != TriggerSignalType.Tick || Find.TickManager.TicksGame % 120 != 0)
                return false;
            if (lord.ownedPawns.Count == 0)
                return false;

            Pawn leader = lord.ownedPawns[0];
            if (leader?.Map == null)
                return false;

            foreach (Pawn p in leader.Map.mapPawns.FreeColonistsSpawned)
            {
                if (!p.Dead && (leader.Position - p.Position).LengthHorizontalSquared < sqrRadius)
                    return true;
            }
            return false;
        }
    }

    public class Trigger_PawnDamagedCustom : Trigger
    {
        public override bool ActivateOn(Lord lord, TriggerSignal signal)
        {
            return signal.type == TriggerSignalType.PawnDamaged
                && signal.Pawn != null
                && signal.Pawn.Faction == lord.faction;
        }
    }
}
