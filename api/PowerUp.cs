/*
 * ----------------------------------------------------------------------------
 * "THE BEER-WARE LICENSE" 
 * As long as you retain this notice you can do whatever you want with this 
 * stuff. If you meet an employee from Windward meet some day, and you think
 * this stuff is worth it, you can buy them a beer in return. Windward Studios
 * ----------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace PlayerCSharpAI2.api
{
	/// <summary>
	/// The power-ups available to cars.
	/// </summary>
	public class PowerUp
	{
		private Passenger passenger;
		private Player player;

		static readonly Dictionary<string, PowerUp> statusPowerUps = new Dictionary<string, PowerUp>();

		/// <summary>
		/// The specific power of this powerUp.
		/// </summary>
		public enum CARD
		{
			/// <summary>Will move all passengers (not in a car) to a random bus stop (can play anytime).</summary>
			MOVE_PASSENGER,

			/// <summary>Change destination for a passenger in an opponent’s car to a random company (can play anytime).</summary>
			CHANGE_DESTINATION,

			/// <summary>Delivery is 1.5X points, but your car travels at 1/4 speed.</summary>
			MULT_DELIVERY_QUARTER_SPEED,

			/// <summary>Drop all other cars to 1/4 speed for 30 seconds (can play anytime).</summary>
			ALL_OTHER_CARS_QUARTER_SPEED,

			/// <summary>Can make a specific car stop for 30 seconds (tacks on road) - (can play anytime).</summary>
			STOP_CAR,

			/// <summary>Relocate all cars (including yours) to random locations (can play anytime).</summary>
			RELOCATE_ALL_CARS,

			/// <summary>Relocate all passengers at bus stops to random locations (can play anytime).</summary>
			RELOCATE_ALL_PASSENGERS,

			/// <summary>1.2X multiplier for delivering a specific person (we have one card for each passenger).</summary>
			MULT_DELIVERING_PASSENGER,

			/// <summary>1.2X multiplier for delivering at a specific company (we have one card for each company).</summary>
			MULT_DELIVER_AT_COMPANY
		}

		private PowerUp(XElement elemCompany)
		{
			Name = elemCompany.Attribute("name").Value;
			Card = (CARD) Enum.Parse(typeof (CARD), elemCompany.Attribute("card").Value);
		}

		private PowerUp(CARD card, Company company, Passenger passenger, Player player)
		{
			Card = card;
			Name = card.ToString();
			if (company != null)
			{
				Company = company;
				Name += string.Format(" - {0}", company.Name);
			}
			if (passenger != null)
			{
				Passenger = passenger;
				Name += string.Format(" - {0}", passenger.Name);
			}
			if (player != null)
			{
				Player = player;
				Name += string.Format(" - {0}", player.Name);
			}
		}

		public PowerUp(PowerUp src)
		{
			passenger = src.passenger;
			player = src.player;
			Name = src.Name;
			Card = src.Card;
			Company = src.Company;
			OkToPlay = src.OkToPlay;
		}

		/// <summary>
		/// The name of the power-up.
		/// </summary>
		public string Name { get; private set; }

		/// <summary>
		/// The power-up card.
		/// </summary>
		public CARD Card { get; private set; }

		/// <summary>
		/// The passenger affected for MOVE_PASSENGER, MULT_DELIVERING_PASSENGER.
		/// </summary>
		public Passenger Passenger
		{
			get { return passenger; }
			set
			{
				if (Card == CARD.CHANGE_DESTINATION)
					throw new ApplicationException("set the Player for CHANGE_DESTINATION");
				Name = string.Format(Name, value.Name);
				passenger = value;
			}
		}

		/// <summary>
		/// The player affected for CHANGE_DESTINATION, STOP_CAR
		/// </summary>
		public Player Player
		{
			get { return player; }
			set
			{
				Name = string.Format(Name, value.Name);
				player = value;
			}
		}

		/// <summary>
		/// The company affected for MULT_DELIVER_AT_COMPANY.
		/// </summary>
		public Company Company { get; set; }

		/// <summary>
		/// It's ok to play this card. This is false until a card is drawn and the limo then visits a stop.
		/// </summary>
		public bool OkToPlay { get; set; }

		/// <summary>
		/// Returns a string that represents the current object.
		/// </summary>
		/// <returns>
		/// A string that represents the current object.
		/// </returns>
		public override string ToString()
		{
			return Name;
		}

		protected bool Equals(PowerUp other)
		{
			// we do NOT compare Name or OkToPlay - update from server does an equals ignoring that.
			if (Card != other.Card)
				return false;
			if ((Company == null) != (other.Company == null))
				return false;
			if ((Company != null) && (Company.Name != other.Company.Name))
				return false;
			if ((Passenger == null) != (other.Passenger == null))
				return false;
			if ((Passenger != null) && (Passenger.Name != other.Passenger.Name))
				return false;
			if ((Player == null) != (other.Player == null))
				return false;
			if ((Player != null) && (Player.Name != other.Player.Name))
				return false;
			return true;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((PowerUp) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = (Passenger != null ? Passenger.Name.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Player != null ? Player.Name.GetHashCode() : 0);
				hashCode = (hashCode*397) ^ (int) Card;
				hashCode = (hashCode * 397) ^ (Company != null ? Company.Name.GetHashCode() : 0);
				return hashCode;
			}
		}

		public static List<PowerUp> FromXml(XElement elemPowerups, List<Company> companies, List<Passenger> passengers)
		{
			List<PowerUp> powerups = new List<PowerUp>();
			foreach (XElement elemPuOn in elemPowerups.Elements("powerup"))
			{
				PowerUp pu = new PowerUp(elemPuOn);
				switch (pu.Card)
				{
					case CARD.MULT_DELIVERING_PASSENGER:
						pu.Passenger = passengers.Find(p => p.Name == elemPuOn.Attribute("passenger").Value);
						break;
					case CARD.MULT_DELIVER_AT_COMPANY:
						pu.Company = companies.Find(p => p.Name == elemPuOn.Attribute("company").Value);
						break;
				}
				powerups.Add(pu);
			}

			return powerups;
		}

		/// <summary>
		/// We only create one of each type to avoid memory allocations every time we get an update.
		/// </summary>
		/// <returns></returns>
		public static PowerUp GenerateFlyweight(XElement element, List<Company> companies , List<Passenger> passengers, List<Player> players)
		{
			CARD card = (CARD)Enum.Parse(typeof(CARD), element.Attribute("card").Value);
			string companyName = element.Attribute("company") == null ? null : element.Attribute("company").Value;
			string passengerName = element.Attribute("passenger") == null ? null : element.Attribute("passenger").Value;
			string playerName = element.Attribute("player") == null ? null : element.Attribute("player").Value;
			string key = string.Format("{0}:{1}:{2}:{3}", card, companyName, passengerName, playerName);
			bool okToPlay = Convert.ToBoolean(element.Attribute("ok-to-play").Value);

			if (statusPowerUps.ContainsKey(key))
			{
				statusPowerUps[key].OkToPlay = okToPlay;
				return statusPowerUps[key];
			}

			Company company = companies.FirstOrDefault(c => c.Name == companyName);
			Passenger passenger = passengers.FirstOrDefault(c => c.Name == passengerName);
			Player player = players.FirstOrDefault(c => c.Name == playerName);
			PowerUp pu = new PowerUp(card, company, passenger, player);
			statusPowerUps.Add(key, pu);

			pu.OkToPlay = okToPlay;
			return pu;
		}
	}
}
