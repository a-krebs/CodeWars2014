/*
 * ----------------------------------------------------------------------------
 * "THE BEER-WARE LICENSE" 
 * As long as you retain this notice you can do whatever you want with this 
 * stuff. If you meet an employee from Windward some day, and you think this
 * stuff is worth it, you can buy them a beer in return. Windward Studios
 * ----------------------------------------------------------------------------
 */

using System;
using System.Xml.Linq;

namespace PlayerCSharpAI2.api
{
	public class MapSquare
	{
		/// <summary>
		///     The direction of the road. Do not change these numbers, they are used as an index into an array.
		/// </summary>
		public enum DIRECTION
		{
			/// <summary>
			///     Road running north/south.
			/// </summary>
			NORTH_SOUTH = 0,

			/// <summary>
			///     Road running east/west.
			/// </summary>
			EAST_WEST = 1,

			/// <summary>
			///     A 4-way intersection.
			/// </summary>
			INTERSECTION = 2,

			/// <summary>
			///     A north/south road ended on the north side.
			/// </summary>
			NORTH_UTURN = 3,

			/// <summary>
			///     An east/west road ended on the east side.
			/// </summary>
			EAST_UTURN = 4,

			/// <summary>
			///     A north/south road ended on the south side.
			/// </summary>
			SOUTH_UTURN = 5,

			/// <summary>
			///     An east/west road ended on the west side.
			/// </summary>
			WEST_UTURN = 6,

			/// <summary>
			///     A T junction where the | of the T is entering from the north.
			/// </summary>
			T_NORTH = 7,

			/// <summary>
			///     A T junction where the | of the T is entering from the east.
			/// </summary>
			T_EAST = 8,

			/// <summary>
			///     A T junction where the | of the T is entering from the south.
			/// </summary>
			T_SOUTH = 9,

			/// <summary>
			///     A T junction where the | of the T is entering from the west.
			/// </summary>
			T_WEST = 10,

			/// <summary>
			///     A curve entered northward and exited eastward (or vice-versa).
			/// </summary>
			CURVE_NE = 11,

			/// <summary>
			///     A curve entered northward and exited westward (or vice-versa).
			/// </summary>
			CURVE_NW = 12,

			/// <summary>
			///     A curve entered southward and exited eastward (or vice-versa).
			/// </summary>
			CURVE_SE = 13,

			/// <summary>
			///     A curve entered southward and exited westward (or vice-versa).
			/// </summary>
			CURVE_SW = 14,
		};

		/// <summary>
		///     What type of square it is.
		/// </summary>
		public enum TYPE
		{
			/// <summary>
			///     Park. Nothing on this, does nothing, cannot be driven on.
			/// </summary>
			PARK,

			/// <summary>
			///     A road. The road DIRECTION determines which way cars can travel on the road.
			/// </summary>
			ROAD,

			/// <summary>
			///     A company's bus stop. This is where passengers are loaded and unloaded.
			/// </summary>
			BUS_STOP,

			/// <summary>
			///     Company building. Nothing on this, does nothing, cannot be driven on.
			/// </summary>
			COMPANY,

			/// <summary>
			/// A coffee store drive up window. This is where coffee is loaded into the car.
			/// </summary>
			COFFEE_STOP,

			/// <summary>
			/// A coffee store building. Nothing on this, does nothing, cannot be driven on.
			/// </summary>
			COFFEE_BUILDING,
		}

		/// <summary>
		///     Stop signs and signals for an intersection square.
		/// </summary>
		[Flags]
		public enum STOP_SIGNS
		{
			/// <summary>
			///     No stop signs or signals.
			/// </summary>
			NONE = 0,

			/// <summary>
			///     A stop entering from the North side.
			/// </summary>
			STOP_NORTH = 0x01,

			/// <summary>
			///     A stop entering from the East side.
			/// </summary>
			STOP_EAST = 0x02,

			/// <summary>
			///     A stop entering from the South side.
			/// </summary>
			STOP_SOUTH = 0x04,

			/// <summary>
			///     A stop entering from the West side.
			/// </summary>
			STOP_WEST = 0x08
		}

		public MapSquare(XElement elemTile)
		{
			Type = (TYPE) Enum.Parse(typeof (TYPE), elemTile.Attribute("type").Value);
			if (IsDriveable)
			{
				Direction = (DIRECTION) Enum.Parse(typeof (DIRECTION), elemTile.Attribute("direction").Value);
				XAttribute attr = elemTile.Attribute("stop-sign");
				StopSigns = attr == null ? STOP_SIGNS.NONE : (STOP_SIGNS) Enum.Parse(typeof (STOP_SIGNS), attr.Value);
				attr = elemTile.Attribute("signal");
				Signal = attr != null && attr.Value.ToLower() == "true";
			}
		}

		public void ctor(Company company)
		{
			Company = company;
		}

		/// <summary>
		///     Settings for stop signs in this square. NONE for none.
		/// </summary>
		public STOP_SIGNS StopSigns { get; private set; }

		/// <summary>
		///     The type of square.
		/// </summary>
		public bool Signal { get; private set; }

		/// <summary>
		///     The type of square.
		/// </summary>
		public TYPE Type { get; private set; }

		/// <summary>
		///     The company for this tile. Only set if a BUS_STOP.
		/// </summary>
		public Company Company { get; private set; }

		/// <summary>
		///     True if the square can be driven on (ROAD or BUS_STOP).
		/// </summary>
		public bool IsDriveable
		{
			get { return Type == TYPE.ROAD || Type == TYPE.BUS_STOP || Type == TYPE.COFFEE_STOP; }
		}

		/// <summary>
		///     The direction of the road. This is only used for ROAD and BUS_STOP tiles.
		/// </summary>
		public DIRECTION Direction { get; private set; }
	}
}