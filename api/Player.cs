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
	public class Player
	{
		private Player(XElement elemPlayer)
		{
			Guid = elemPlayer.Attribute("guid").Value;
			Name = elemPlayer.Attribute("name").Value;
			Language = elemPlayer.Attribute("language").Value;
			School = elemPlayer.Attribute("school").Value;
			Limo =
				new Limo(
					new Point(Convert.ToInt32(elemPlayer.Attribute("limo-x").Value),
						Convert.ToInt32(elemPlayer.Attribute("limo-y").Value)), Convert.ToInt32(elemPlayer.Attribute("limo-angle").Value));
			PickUp = new List<Passenger>();
			PassengersDelivered = new List<Passenger>();
		}

		/// <summary>
		///     The unique identifier for this player. This will remain constant for the length of the game (while the Player
		///     objects passed will
		///     change on every call).
		/// </summary>
		public string Guid { get; private set; }

		/// <summary>
		///     The name of the player.
		/// </summary>
		public string Name { get; private set; }

		/// <summary>
		/// The computer language this player's AI is written in.
		/// </summary>
		public string Language { get; private set; }

		/// <summary>
		/// The school this player is from..
		/// </summary>
		public string School { get; private set; }

		/// <summary>
		///     Who to pick up at the next bus stop. Can be empty and can also only list people not there.
		///     This may be wrong after a pick-up occurs as all we get is a count. This is updated with the
		///     most recent list sent to the server.
		/// </summary>
		public List<Passenger> PickUp { get; private set; }

		/// <summary>
		///     The passengers delivered - this game.
		/// </summary>
		public List<Passenger> PassengersDelivered { get; private set; }

		/// <summary>
		///     The player's limo.
		/// </summary>
		public Limo Limo { get; private set; }

		/// <summary>
		///     The score for this player (this game, not across all games so far).
		/// </summary>
		public float Score { get; private set; }

		/// <summary>
		///     The score for this player across all games (so far).
		/// </summary>
		public float TotalScore { get; private set; }

		/// <summary>
		/// The maximum number of cards this player can have in their hand.
		/// </summary>
		public int MaxCardsInHand { get; private set; }

		/// <summary>
		/// The power up this player will play at the next bus stop.
		/// </summary>
		public PowerUp PowerUpNextBusStop { get; private set; }

		/// <summary>
		/// The power up in effect for the transit this player is presently executing.
		/// </summary>
		public PowerUp PowerUpTransit { get; private set; }

		/// <summary>
		///     Called on setup to create initial list of players.
		/// </summary>
		/// <param name="elemPlayers">The xml with all the players.</param>
		/// <returns>The created list of players.</returns>
		public static List<Player> FromXml(XElement elemPlayers)
		{
			List<Player> players = new List<Player>();
			foreach (XElement elemPlyrOn in elemPlayers.Elements("player"))
				players.Add(new Player(elemPlyrOn));
			return players;
		}

		public static void UpdateFromXml(List<Company> companies, List<Player> players, List<Passenger> passengers, XElement elemPlayers)
		{
			foreach (XElement elemPlyrOn in elemPlayers.Elements("player"))
			{
				Player plyrOn = players.Find(pl => pl.Guid == elemPlyrOn.Attribute("guid").Value);

				plyrOn.Score = Convert.ToSingle(elemPlyrOn.Attribute("score").Value);
				plyrOn.TotalScore = Convert.ToSingle(elemPlyrOn.Attribute("total-score").Value);
				plyrOn.MaxCardsInHand = Convert.ToInt32(elemPlyrOn.Attribute("cards-max").Value);
				plyrOn.Limo.CoffeeServings = Convert.ToInt32(elemPlyrOn.Attribute("coffee-servings").Value);

				// car location
				plyrOn.Limo.TilePosition = new Point(Convert.ToInt32(elemPlyrOn.Attribute("limo-x").Value),
					Convert.ToInt32(elemPlyrOn.Attribute("limo-y").Value));
				plyrOn.Limo.Angle = Convert.ToInt32(elemPlyrOn.Attribute("limo-angle").Value);

				// see if we now have a passenger.
				XAttribute attrPassenger = elemPlyrOn.Attribute("passenger");
				if (attrPassenger != null)
				{
					Passenger passenger = passengers.Find(ps => ps.Name == attrPassenger.Value);
					plyrOn.Limo.Passenger = passenger;
					passenger.Car = plyrOn.Limo;
				}
				else
					plyrOn.Limo.Passenger = null;

				// add most recent delivery if this is the first time we're told.
				attrPassenger = elemPlyrOn.Attribute("last-delivered");
				if (attrPassenger != null)
				{
					Passenger passenger = passengers.Find(ps => ps.Name == attrPassenger.Value);
					if (! plyrOn.PassengersDelivered.Contains(passenger))
						plyrOn.PassengersDelivered.Add(passenger);
				}

				// power-ups in action
				XElement elemCards = elemPlyrOn.Element("next-bus-stop");
				if (elemCards != null)
					plyrOn.PowerUpNextBusStop = PowerUp.GenerateFlyweight(elemCards, companies, passengers, players);
				elemCards = elemPlyrOn.Element("transit");
				if (elemCards != null)
					plyrOn.PowerUpTransit = PowerUp.GenerateFlyweight(elemCards, companies, passengers, players);
			}
		}

		public override string ToString()
		{
			return string.Format("{0}; NumDelivered:{1}", Name, PassengersDelivered.Count);
		}
	}
}