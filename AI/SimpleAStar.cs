/*
 * ----------------------------------------------------------------------------
 * "THE BEER-WARE LICENSE" 
 * As long as you retain this notice you can do whatever you want with this 
 * stuff. If you meet an employee from Windward some day, and you think this
 * stuff is worth it, you can buy them a beer in return. Windward Studios
 * ----------------------------------------------------------------------------
 */

// Last year's pathfinder sucked (on purpose). The one this year is good (again on purpose). 
// While this can be improved some, it is unlikely to be worth the effort within the 8 hours you have.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using PlayerCSharpAI2.api;

namespace PlayerCSharpAI2.AI
{
	/// <summary>
	///     The Pathfinder (maybe I should name it Frémont).
	///     Good intro at http://www.policyalmanac.org/games/aStarTutorial.htm
	/// </summary>
	public class SimpleAStar
	{
		private static readonly Dictionary<long, List<Point>> cachePaths = new Dictionary<long, List<Point>>();

		private static readonly Point[] offsets = { new Point(-1, 0), new Point(1, 0), new Point(0, -1), new Point(0, 1) };

		private const int DEAD_END = 10000;

		private static readonly Point ptOffMap = new Point(-1, -1);

		public static void Flush()
		{
			cachePaths.Clear();
		}

		public static List<Point> CalculatePath(Map map, Point start, Point end)
		{
			// should never happen but just to be sure
			if (start == end)
				return new List<Point> { start };

			long startEnd = start.X | ((long)start.Y << 16) | ((long)end.X << 32) | ((long)end.Y << 48);
			List<Point> savedPath;
			if (cachePaths.TryGetValue(startEnd, out savedPath))
				return savedPath;

			// nodes are points we have walked to
			Dictionary<Point, TrailPoint> nodes = new Dictionary<Point, TrailPoint>();
			// points we have in a TrailPoint, but not yet evaluated. The int is the best guess as to the total cost.
			Dictionary<Point, TrailPoint> notEvaluated = new Dictionary<Point, TrailPoint>();

			TrailPoint tpOn = new TrailPoint(start, end, 0);
			while (true)
			{
				nodes.Add(tpOn.MapTile, tpOn);

				// get the neighbors
				TrailPoint tpClosest = null;
				foreach (Point ptOffset in offsets)
				{
					Point pt = new Point(tpOn.MapTile.X + ptOffset.X, tpOn.MapTile.Y + ptOffset.Y);
					MapSquare square = map.SquareOrDefault(pt);
					// off the map or not a road/bus stop
					if ((square == null) || (!square.IsDriveable))
						continue;

					// already evaluated - add it in
					if (nodes.ContainsKey(pt))
					{
						TrailPoint tpAlreadyEvaluated = nodes[pt];
						// may have a shorter or longer path back to the start
						TrailPoint tpRecalc;
						Point ptIgnore;
						if (tpAlreadyEvaluated.CostFromStart + 1 < tpOn.CostFromStart)
						{
							tpRecalc = tpOn;
							ptIgnore = tpAlreadyEvaluated.MapTile;
						}
						else if (tpOn.CostFromStart + 1 < tpAlreadyEvaluated.CostFromStart)
						{
							tpRecalc = tpAlreadyEvaluated;
							ptIgnore = tpOn.MapTile;
						}
						else
						{
							tpRecalc = null;
							ptIgnore = ptOffMap;
						}
						tpOn.CostFromStart = Math.Min(tpOn.CostFromStart, tpAlreadyEvaluated.CostFromStart + 1);
						tpAlreadyEvaluated.CostFromStart = Math.Min(tpAlreadyEvaluated.CostFromStart, tpOn.CostFromStart + 1);
						tpOn.Neighbors.Add(tpAlreadyEvaluated);
						if (tpRecalc != null)
							tpRecalc.RecalculateCostFromStart(ptIgnore, (map.Width * map.Height) / 4);
						continue;
					}

					// add this one in
					TrailPoint tpNeighbor = new TrailPoint(pt, end, tpOn.CostFromStart + 1);
					tpOn.Neighbors.Add(tpNeighbor);
					// may already be in notEvaluated. If so remove it as this is a more recent cost estimate.
					notEvaluated.Remove(tpNeighbor.MapTile);

					// we only assign to tpClosest if it is closer to the destination. If it's further away, then we
					// use notEvaluated below to find the one closest to the dest that we have not walked yet.
					if (tpClosest == null)
					{
						if (tpNeighbor.CostCompletePath <= tpOn.CostCompletePath)
							// new neighbor is closer - work from this next. 
							tpClosest = tpNeighbor;
						else
							// this is further away - put in the list to try if a better route is not found
							notEvaluated.Add(tpNeighbor.MapTile, tpNeighbor);
					}
					else if (tpClosest.CostCompletePath <= tpNeighbor.CostCompletePath)
						// this is further away - put in the list to try if a better route is not found
						notEvaluated.Add(tpNeighbor.MapTile, tpNeighbor);
					else
					{
						// this is closer than tpOn and another neighbor - use it next.
						notEvaluated.Add(tpClosest.MapTile, tpClosest);
						tpClosest = tpNeighbor;
					}
				}

				// re-calc based on neighbors
				tpOn.RecalculateDistance(ptOffMap, map.Width + map.Height);

				// if no closest, then get from notEvaluated. This is where it guarantees that we are getting the shortest
				// route - we go in here if the above did not move a step closer. This may not either as the best choice
				// may be the neighbor we didn't go with above - but we drop into this to find the closest based on what we know.
				if (tpClosest == null)
				{
					// We need the closest one as that's how we find the shortest path.
					tpClosest = notEvaluated.Values.OrderBy(tp => tp.CostCompletePath).FirstOrDefault();
					// if nothing left to check - should never happen.
					if (tpClosest == null)
						break;
					notEvaluated.Remove(tpClosest.MapTile);
				}

				// if we're at end - we're done!
				if (tpClosest.MapTile == end)
				{
					tpClosest.Neighbors.Add(tpOn);
					nodes.Add(tpClosest.MapTile, tpClosest);
					break;
				}

				// try this one
				tpOn = tpClosest;
			}


			List<Point> path = new List<Point>();
			if (!nodes.ContainsKey(end))
				return path;

			// Create the return path - from end back to beginning.
			tpOn = nodes[end];
			path.Add(tpOn.MapTile);
			while (tpOn.MapTile != start)
			{
				List<TrailPoint> neighbors = tpOn.Neighbors;
				int cost = tpOn.CostFromStart;

				tpOn = neighbors.OrderBy(t => t.CostFromStart).First();

				// we didn't get to the start.
				if (tpOn.CostFromStart >= cost)
					return path;
				path.Insert(0, tpOn.MapTile);
			}

			cachePaths.Add(startEnd, path);
			return path;
		}

		private class TrailPoint
		{
			/// <summary>
			///     The Map tile for this point in the trail.
			/// </summary>
			public Point MapTile { get; private set; }

			/// <summary>
			/// The neighboring tiles (up to 4). If 0 then this point has been added as a neighbor to another tile but
			/// is in the notEvaluated List because it has not yet been tried and therefore does not have its neighbors set.
			/// </summary>
			public List<TrailPoint> Neighbors { get; private set; }

			/// <summary>
			///     Estimate of the distance to the end. Direct line if have no neighbors. Best neighbor.Distance + 1
			///     if have neighbors. This value is bad if it's along a trail that failed.
			/// </summary>
			public int CostToEnd { get; private set; }

			/// <summary>
			///     The number of steps from the start to this tile.
			/// </summary>
			public int CostFromStart { get; set; }

			/// <summary>
			/// The cost from beginning to end if using this tile in the final path.
			/// </summary>
			public int CostCompletePath
			{
				get { return CostFromStart + CostToEnd; }
			}

			public TrailPoint(Point pt, Point end, int costFromStart)
			{
				MapTile = pt;
				Neighbors = new List<TrailPoint>();
				CostToEnd = Math.Abs(MapTile.X - end.X) + Math.Abs(MapTile.Y - end.Y);
				CostFromStart = costFromStart;
			}

			/// <summary>
			/// Recalculate the CostFromStart. We check our (new) neighbors as it may be faster through us.
			/// </summary>
			/// <param name="ptIgnore">Do not recalculate this map point (where we started this).</param>
			/// <param name="remainingSteps">Stop infinite recursion</param>
			public void RecalculateCostFromStart(Point ptIgnore, int remainingSteps)
			{

				// if we're at the start, we're done.
				if (CostFromStart == 0)
					return;
				// if gone too far, no more recalculate
				if (remainingSteps-- < 0)
					return;

				//  Need to update our neighbors - except the one that called us. They have a valid value, but
				// it may now be faster to get there from the start via the point we're on
				foreach (TrailPoint neighborOn in Neighbors.Where(neighborOn => neighborOn.MapTile != ptIgnore))
				{
					if (neighborOn.CostFromStart <= CostFromStart + 1)
						continue;
					neighborOn.CostFromStart = CostFromStart + 1;
					neighborOn.RecalculateCostFromStart(MapTile, remainingSteps);
				}
			}

			public void RecalculateDistance(Point mapTileCaller, int remainingSteps)
			{

				// if no neighbors then this is in notEvaluated and so can't recalculate.
				if (Neighbors.Count == 0)
					return;

				// if just 1 neighbor, then it's a dead end
				if (Neighbors.Count == 1)
				{
					CostToEnd = DEAD_END;
					return;
				}

				// it's 1+ lowest neighbor value (unless a dead end)
				int shortestDistance = Neighbors.Select(neighborOn => neighborOn.CostToEnd).Min();
				if (shortestDistance != DEAD_END)
					shortestDistance++;

				// no change, no need to recalc neighbors
				if (shortestDistance == CostToEnd || CostFromStart == 0)
					return;

				// new value (could be longer or shorter)
				CostToEnd = shortestDistance;

				// if gone too far, no more recalculate
				if (remainingSteps-- < 0)
					return;

				//  Need to tell our neighbors - except the one that called us
				foreach (TrailPoint neighborOn in Neighbors.Where(neighborOn => neighborOn.MapTile != mapTileCaller))
					neighborOn.RecalculateDistance(MapTile, remainingSteps);

				// and we re-calc again because that could have changed our neighbor's values
				shortestDistance = Neighbors.Select(neighborOn => neighborOn.CostToEnd).Min();
				// it's 1+ lowest neighbor value (unless a dead end)
				if (shortestDistance != DEAD_END)
					shortestDistance++;
				CostToEnd = shortestDistance;
			}

			public override string ToString()
			{
				return string.Format("Map:{0}, Cost:{1}+{2}={3}, Neighbors:{4}", MapTile, CostFromStart, CostToEnd, CostCompletePath,
					Neighbors.Count);
			}
		}
	}
}