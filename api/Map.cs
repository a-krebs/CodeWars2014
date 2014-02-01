/*
 * ----------------------------------------------------------------------------
 * "THE BEER-WARE LICENSE" 
 * As long as you retain this notice you can do whatever you want with this 
 * stuff. If you meet an employee from Windward some day, and you think this
 * stuff is worth it, you can buy them a beer in return. Windward Studios
 * ----------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml.Linq;

namespace PlayerCSharpAI2.api
{
	public class Map
	{
		public Map(XElement elemMap, List<Company> companies)
		{
			int width = Convert.ToInt32(elemMap.Attribute("width").Value);
			int height = Convert.ToInt32(elemMap.Attribute("height").Value);
			UnitsPerTile = Convert.ToInt32(elemMap.Attribute("units-tile").Value);

			Squares = new MapSquare[width][];
			for (int x = 0; x < width; x++)
				Squares[x] = new MapSquare[height];

			foreach (XElement elemTile in elemMap.Elements("tile"))
			{
				int x = Convert.ToInt32(elemTile.Attribute("x").Value);
				int y = Convert.ToInt32(elemTile.Attribute("y").Value);
				Squares[x][y] = new MapSquare(elemTile);
			}

			foreach (var cmpyOn in companies)
				Squares[cmpyOn.BusStop.X][cmpyOn.BusStop.Y].ctor(cmpyOn);
		}

		/// <summary>
		///     The map squares. This is in the format [x][y].
		/// </summary>
		public MapSquare[][] Squares { get; protected set; }

		/// <summary>
		///     The number of map units in a tile. Some points are in map units and
		///     some are in tile units.
		/// </summary>
		public int UnitsPerTile { get; private set; }

		/// <summary>
		///     The width of the map. Units are squares.
		/// </summary>
		public int Width
		{
			get { return Squares.Length; }
		}

		/// <summary>
		///     The height of the map. Units are squares.
		/// </summary>
		public int Height
		{
			get { return Squares[0].Length; }
		}

		/// <summary>
		///     Returns the requested point or null if off the map.
		/// </summary>
		/// <param name="pt"></param>
		/// <returns></returns>
		public MapSquare SquareOrDefault(Point pt)
		{
			if ((pt.X < 0) || (pt.Y < 0) || (pt.X >= Width) || (pt.Y >= Height))
				return null;
			return Squares[pt.X][pt.Y];
		}
	}
}