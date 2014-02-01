/*
 * ----------------------------------------------------------------------------
 * "THE BEER-WARE LICENSE" 
 * As long as you retain this notice you can do whatever you want with this 
 * stuff. If you meet an employee from Windward some day, and you think this
 * stuff is worth it, you can buy them a beer in return. Windward Studios
 * ----------------------------------------------------------------------------
 */

using System.Collections.Generic;
using System.Drawing;

namespace PlayerCSharpAI2.api
{
	public class Limo
	{
		/// <summary>
		///     Create the object.
		/// </summary>
		/// <param name="tilePosition">The location in tile units of the center of the vehicle.</param>
		/// <param name="angle">The Angle this unit is facing.</param>
		public Limo(Point tilePosition, int angle)
		{
			TilePosition = tilePosition;
			Angle = angle;
			Path = new List<Point>();
		}

		/// <summary>
		///     The location in tile units of the center of the vehicle.
		/// </summary>
		public Point TilePosition { get; set; }

		/// <summary>
		///     0 .. 359 The Angle this unit is facing. 0 is North and 90 is East.
		/// </summary>
		public int Angle { get; set; }

		/// <summary>
		///     The passenger in this limo. null if no passenger.
		/// </summary>
		public Passenger Passenger { get; set; }

		/// <summary>
		/// The coffee servings in this car. The coffee is reduced when the passenger exits the limo. It is
		/// reduced by 2 if the trip used a power-up multiplier for the points.
		/// </summary>
		public int CoffeeServings { get; set; }

		/// <summary>
		///     Only set for the AI's own Limo - the number of tiles remaining in the Limo's path.
		///     This may be wrong after movement as all we get is a count. This is updated with the
		///     most recent list sent to the server.
		/// </summary>
		public List<Point> Path { get; private set; }

		public override string ToString()
		{
			if (Passenger != null)
				return string.Format("{0}:{1}; Passenger:{2}; Dest:{3}; PathLength:{4}", TilePosition, Angle,
					Passenger == null ? "{none}" : Passenger.Name, Passenger.Destination, Path.Count);
			return string.Format("{0}:{1}; Passenger:{2}", TilePosition, Angle, Passenger == null ? "{none}" : Passenger.Name);
		}
	}
}