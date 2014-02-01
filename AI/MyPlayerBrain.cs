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
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;
using PlayerCSharpAI2.api;

namespace PlayerCSharpAI2.AI
{
	/// <summary>
	///     The sample C# AI. Start with this project but write your own code as this is a very simplistic implementation of
	///     the AI.
	/// </summary>
	public class MyPlayerBrain : IPlayerAI
	{
		// bugbug - put your team name here.
		private const string NAME = "The Optimal Elephants";

		// bugbug - put your school name here. Must be 11 letters or less (ie use MIT, not Massachussets Institute of Technology).
		public const string SCHOOL = "U of A";

		// bugbug - the collections below are not thread safe. If you create worker threads, while the objects will stay in the collections
		// (except for powerups being moved from PowerUpsDeck -> PowerUpsDrawn -> discarded) the properties of those objects are changed
		// each time a message comes in from the server. Therefore reading the objects from a worker thread can get inconsistent data.

		/// <summary>
		///     The name of the player.
		/// </summary>
		public string Name { get; private set; }

		/// <summary>
		/// The game map.
		/// </summary>
		public Map GameMap { get; private set; }

		/// <summary>
		/// All of the players, including myself.
		/// </summary>
		public List<Player> Players { get; private set; }

		/// <summary>
		/// All of the companies.
		/// </summary>
		public List<Company> Companies { get; private set; }

		/// <summary>
		/// All the coffee stores.
		/// </summary>
		public List<CoffeeStore> Stores { get; private set; }

		/// <summary>
		/// All of the passengers.
		/// </summary>
		public List<Passenger> Passengers { get; private set; }

		/// <summary>
		/// The powerups this player can draw.
		/// </summary>
		public List<PowerUp> PowerUpDeck { get; private set; }

		/// <summary>
		/// The powerups this player has in their hand (may have to wait before playing it).
		/// </summary>
		public List<PowerUp> PowerUpHand { get; private set; }

		/// <summary>
		///     Me (my player object).
		/// </summary>
		public Player Me { get; private set; }

		/// <summary>
		/// The passenger I am presently carrying.
		/// </summary>
		public Passenger MyPassenger { get; private set; }

		private PlayerAIBase.PlayerOrdersEvent sendOrders;

		private PlayerAIBase.PlayerCardEvent playCards;

		/// <summary>
		/// The maximum number of trips allowed before a refill is required.
		/// </summary>
		private const int MAX_TRIPS_BEFORE_REFILL = 3;

		private static readonly Random rand = new Random();
		private static readonly ILog log = LogManager.GetLogger(typeof (MyPlayerBrain));

		public MyPlayerBrain(string name)
		{
			Name = !string.IsNullOrEmpty(name) ? name : NAME;
			PowerUpHand = new List<PowerUp>();
		}

		/// <summary>
		///     The avatar of the player. Must be 32 x 32.
		/// </summary>
		public byte[] Avatar
		{
			get
			{
				// bugbug replace the file MyAvatar.png in the solution and this will load it. If you add it with a different
				// filename, make sure the Build Action for the file is Embedded Resource.
				using (Stream avatar = Assembly.GetExecutingAssembly().GetManifestResourceStream("PlayerCSharpAI2.AI.MyAvatar.png"))
				{
					if (avatar == null)
						return null;
					byte[] data = new byte[avatar.Length];
					avatar.Read(data, 0, data.Length);
					return data;
				}
			}
		}

		/// <summary>
		/// Called at the start of the game.
		/// </summary>
		/// <param name="map">The game map.</param>
		/// <param name="me">You. This is also in the players list.</param>
		/// <param name="players">All players (including you).</param>
		/// <param name="companies">The companies on the map.</param>
		/// <param name="stores">All coffee stores</param>
		/// <param name="passengers">The passengers that need a lift.</param>
		/// <param name="powerUps">The powerup cards in your deck.</param>
		/// <param name="ordersEvent">Method to call to send orders to the server.</param>
		/// <param name="playerPowerSend">Method to send card plays</param>
		public void Setup(Map map, Player me, List<Player> players, List<Company> companies, List<CoffeeStore> stores, List<Passenger> passengers,
			List<PowerUp> powerUps, PlayerAIBase.PlayerOrdersEvent ordersEvent, PlayerAIBase.PlayerCardEvent playerPowerSend)
		{
			try
			{
				GameMap = map;
				Players = players;
				Me = me;
				Companies = companies;
				Stores = stores;
				Passengers = passengers;
				PowerUpDeck = powerUps;
				sendOrders = ordersEvent;
				playCards = playerPowerSend;

				List<Passenger> pickup = AllPickups(me, passengers);

				// get the path from where we are to the dest.
				List<Point> path = CalculatePathPlus1(me, pickup[0].Lobby.BusStop);
				sendOrders("ready", path, pickup);
			}
			catch (Exception ex)
			{
				log.Fatal(string.Format("Setup({0}, ...", me == null ? "{null}" : me.Name), ex);
			}
		}

		/// <summary>
		/// Called to send an update message to this A.I. We do NOT have to send orders in response.
		/// </summary>
		/// <param name="status">The status message.</param>
		/// <param name="plyrStatus">The player this status is about. THIS MAY NOT BE YOU.</param>
		public void GameStatus(PlayerAIBase.STATUS status, Player plyrStatus)
		{
			// bugbug - Framework.cs updates the object's in this object's Players, Passengers, and Companies lists. This works fine as long
			// as this app is single threaded. However, if you create worker thread(s) or respond to multiple status messages simultaneously
			// then you need to split these out and synchronize access to the saved list objects.

			try
			{
				// bugbug - we return if not us because the below code is only for when we need a new path or our limo hit a bus stop.
				// if you want to act on other players arriving at bus stops, you need to remove this. But make sure you use Me, not
				// plyrStatus for the Player you are updatiing (particularly to determine what tile to start your path from).
                if (plyrStatus != Me)
                {
                    //If someone else picked up a passenger & it was out target passenger &
                    //we were not going to get coffee, abandon the destination
                    if (plyrStatus.Limo.Passenger == Me.PickUp.First() &&
                        (Stores.Where(st => st.BusStop == Me.Limo.Path.Last()).Count() == 0))
                    {
                        Me.Limo.Path.Clear();
                        Me.PickUp.Clear();
                    }
                    return;
                }
                //Activate cards, if necessary
                IEnumerable<PowerUp> inactivePowerUpList = PowerUpHand.Where(card => card.OkToPlay == false);
                if (inactivePowerUpList.Count() > 0 && 
                    (Stores.Select(store => store.BusStop).Contains(Me.Limo.TilePosition) ||
                    Companies.Select(comp => comp.BusStop).Contains(Me.Limo.TilePosition))){
                        foreach (PowerUp p in inactivePowerUpList)
                        {
                            p.OkToPlay = true;
                        }
                }
				if (status == PlayerAIBase.STATUS.UPDATE)
				{
					MaybePlayPowerUp();
					return;
				}

				DisplayStatus(status, plyrStatus);

				if (log.IsDebugEnabled)
					log.Info(string.Format("GameStatus({0}, ...)", status));

				Point ptDest = Point.Empty;
				List<Passenger> pickup = new List<Passenger>();
				// handle the passenger
				switch (status)
				{
					case PlayerAIBase.STATUS.NO_PATH:
					case PlayerAIBase.STATUS.PASSENGER_NO_ACTION:
						if (Me.Limo.Passenger == null)
						{
							pickup = AllPickups(Me, Passengers);
							ptDest = pickup[0].Lobby.BusStop;
						}
						else
							ptDest = Me.Limo.Passenger.Destination.BusStop;
						break;
					case PlayerAIBase.STATUS.PASSENGER_DELIVERED:
					case PlayerAIBase.STATUS.PASSENGER_ABANDONED:
						pickup = AllPickups(Me, Passengers);
						ptDest = pickup[0].Lobby.BusStop;
						break;
					case PlayerAIBase.STATUS.PASSENGER_REFUSED_ENEMY:
                        //If we have a Move Passenger card, use it to move the enemy
                        PowerUp pu2 = PowerUpHand.Where(card => card.Card == PowerUp.CARD.MOVE_PASSENGER)
                            .FirstOrDefault(p => p.OkToPlay);
                        //We have a card to play!
                        if (pu2 != null)
                        {
                            pu2.Passenger = Me.Limo.Passenger.Enemies.Where(en => Me.Limo.Passenger.Destination.Passengers.Contains(en)).First();
                            if (pu2.Passenger != null)
                            {
                                if (log.IsInfoEnabled)
                                    log.Info(string.Format("Moving enemy away from BusStop", pu2));
                                playCards(PlayerAIBase.CARD_ACTION.PLAY, pu2);
                                PowerUpHand.Remove(pu2);
                                ptDest = Me.Limo.Passenger.Destination.BusStop;
                                break;
                            }
                        }
                        //Fallthrough
                        ptDest =
                            Companies.Where(cpy => cpy != Me.Limo.Passenger.Destination && cpy.Passengers.Intersect(Me.Limo.Passenger.Enemies).Count() == 0).OrderBy(cpy =>  CalculatePathPlus1(Me, cpy.BusStop).Count()).First().BusStop;
						break;
					case PlayerAIBase.STATUS.PASSENGER_DELIVERED_AND_PICKED_UP:
					case PlayerAIBase.STATUS.PASSENGER_PICKED_UP:
						pickup = AllPickups(Me, Passengers);
						ptDest = Me.Limo.Passenger.Destination.BusStop;
						break;
				}

				// coffee store override
				switch (status)
				{
					case PlayerAIBase.STATUS.PASSENGER_DELIVERED_AND_PICKED_UP:
					case PlayerAIBase.STATUS.PASSENGER_DELIVERED:
                    case PlayerAIBase.STATUS.PASSENGER_ABANDONED:
						if (Me.Limo.CoffeeServings <= 0)
                            ptDest = Stores.OrderBy(st => CalculatePathPlus1(Me, st.BusStop).Count()).First().BusStop;
						break;
					case PlayerAIBase.STATUS.PASSENGER_REFUSED_NO_COFFEE:
					case PlayerAIBase.STATUS.PASSENGER_DELIVERED_AND_PICK_UP_REFUSED:
                        ptDest = Stores.OrderBy(st => CalculatePathPlus1(Me, st.BusStop).Count()).First().BusStop;
						break;
					case PlayerAIBase.STATUS.COFFEE_STORE_CAR_RESTOCKED:
						pickup = AllPickups(Me, Passengers);
						if (pickup.Count == 0)
							break;
						ptDest = pickup[0].Lobby.BusStop;
						break;
				}

				// may be another status
				if (ptDest == Point.Empty)
					return;

				DisplayOrders(ptDest);

				// get the path from where we are to the dest.
				List<Point> path = CalculatePathPlus1(Me, ptDest);

				if (log.IsDebugEnabled)
					log.Debug(string.Format("{0}; Path:{1}-{2}, {3} steps; Pickup:{4}, {5} total",
						status,
						path.Count > 0 ? path[0].ToString() : "{n/a}",
						path.Count > 0 ? path[path.Count - 1].ToString() : "{n/a}",
						path.Count,
						pickup.Count == 0 ? "{none}" : pickup[0].Name,
						pickup.Count));

				// update our saved Player to match new settings
				if (path.Count > 0)
				{
					Me.Limo.Path.Clear();
					Me.Limo.Path.AddRange(path);
				}
				if (pickup.Count > 0)
				{
					Me.PickUp.Clear();
					Me.PickUp.AddRange(pickup);
				}

				sendOrders("move", path, pickup);
			}
			catch (Exception ex)
			{
				log.Error(string.Format("GameStatus({0}, {1}, ...", status, Me == null ? "{null}" : Me.Name), ex);
			}
		}

		private void MaybePlayPowerUp()
		{
			if ((PowerUpHand.Count != 0) && (rand.Next(50) < 30))
				return;
			// not enough, draw
			if (PowerUpHand.Count < Me.MaxCardsInHand && PowerUpDeck.Count > 0)
			{
				for (int index = 0; index < Me.MaxCardsInHand - PowerUpHand.Count && PowerUpDeck.Count > 0; index++)
				{
					// select a card
					PowerUp pu = PowerUpDeck.First();
					PowerUpDeck.Remove(pu);
					PowerUpHand.Add(pu);
					playCards(PlayerAIBase.CARD_ACTION.DRAW, pu);
				}
				return;
			}

			// can we play one? Don't play Passenger Move Card
			PowerUp pu2 = PowerUpHand.Where(card => card.Card != PowerUp.CARD.MOVE_PASSENGER)
                .FirstOrDefault(p => p.OkToPlay);
			if (pu2 == null)
				return;
			// 100% play
			if (pu2.Card == PowerUp.CARD.MOVE_PASSENGER)
				pu2.Passenger = Passengers.OrderBy(c => rand.Next()).First(p => p.Car == null);
			if (pu2.Card == PowerUp.CARD.CHANGE_DESTINATION || pu2.Card == PowerUp.CARD.STOP_CAR)
			{
				IList<Player> plyrsWithPsngrs = Players.Where(pl => pl.Guid != Me.Guid && pl.Limo.Passenger != null).ToList();
				if (plyrsWithPsngrs.Count == 0)
					return;
				pu2.Player = plyrsWithPsngrs.OrderBy(c => rand.Next()).First();
			}
			if (log.IsInfoEnabled)
				log.Info(string.Format("Request play card {0} ", pu2));
			playCards(PlayerAIBase.CARD_ACTION.PLAY, pu2);
			PowerUpHand.Remove(pu2);
		}

		/// <summary>
		/// A power-up was played. It may be an error message, or success.
		/// </summary>
		/// <param name="puStatus">The status of the played card.</param>
		/// <param name="plyrPowerUp">The player who played the card.</param>
		/// <param name="cardPlayed">The card played.</param>
		public void PowerupStatus(PlayerAIBase.STATUS puStatus, Player plyrPowerUp, PowerUp cardPlayed)
		{
			// redo the path if we got relocated
			if ((puStatus == PlayerAIBase.STATUS.POWER_UP_PLAYED) && ((cardPlayed.Card == PowerUp.CARD.RELOCATE_ALL_CARS) || 
									((cardPlayed.Card == PowerUp.CARD.CHANGE_DESTINATION) && cardPlayed.Player.Guid == Me.Guid)))
				GameStatus(PlayerAIBase.STATUS.NO_PATH, Me);
		}

		private void DisplayStatus(PlayerAIBase.STATUS status, Player plyrStatus)
		{
			string msg = null;
			switch (status)
			{
				case PlayerAIBase.STATUS.PASSENGER_DELIVERED:
					msg = string.Format("{0} delivered to {1}", MyPassenger.Name, MyPassenger.Lobby.Name);
					MyPassenger = null;
					break;
				case PlayerAIBase.STATUS.PASSENGER_ABANDONED:
					msg = string.Format("{0} abandoned at {1}", MyPassenger.Name, MyPassenger.Lobby.Name);
					MyPassenger = null;
					break;
				case PlayerAIBase.STATUS.PASSENGER_REFUSED_ENEMY:
					msg = string.Format("{0} refused to exit at {1} - enemy there", plyrStatus.Limo.Passenger.Name,
						plyrStatus.Limo.Passenger.Destination.Name);
					break;
				case PlayerAIBase.STATUS.PASSENGER_DELIVERED_AND_PICKED_UP:
					msg = string.Format("{0} delivered at {1} and {2} picked up", MyPassenger.Name, MyPassenger.Lobby.Name,
						plyrStatus.Limo.Passenger.Name);
					MyPassenger = plyrStatus.Limo.Passenger;
					break;
				case PlayerAIBase.STATUS.PASSENGER_PICKED_UP:
					msg = string.Format("{0} picked up", plyrStatus.Limo.Passenger.Name);
					MyPassenger = plyrStatus.Limo.Passenger;
					break;
				case PlayerAIBase.STATUS.PASSENGER_REFUSED_NO_COFFEE:
					msg = "Passenger refused to board limo, no coffee";
					break;
				case PlayerAIBase.STATUS.PASSENGER_DELIVERED_AND_PICK_UP_REFUSED:
					msg = string.Format("{0} delivered at {1}, new passenger refused to board limo, no coffee", MyPassenger.Name,
						MyPassenger.Lobby.Name);
					break;
				case PlayerAIBase.STATUS.COFFEE_STORE_CAR_RESTOCKED:
					msg = "Coffee restocked!";
					break;
			}
			if (! string.IsNullOrEmpty(msg))
			{
				Console.Out.WriteLine(msg);
				if (log.IsInfoEnabled)
					log.Info(msg);
			}
		}

		private void DisplayOrders(Point ptDest)
		{
			string msg = null;
			CoffeeStore store = Stores.FirstOrDefault(s => s.BusStop == ptDest);
			if (store != null)
				msg = string.Format("Heading toward {0} at {1}", store.Name, ptDest);
			else
			{
				Company company = Companies.FirstOrDefault(c => c.BusStop == ptDest);
				if (company != null)
					msg = string.Format("Heading toward {0} at {1}", company.Name, ptDest);
			}
			if (!string.IsNullOrEmpty(msg))
			{
				Console.Out.WriteLine(msg);
				if (log.IsInfoEnabled)
					log.Info(msg);
			}
		}

		private List<Point> CalculatePathPlus1(Player me, Point ptDest)
		{
            return CalculatePathPlus1(me.Limo.TilePosition, ptDest);
		}

        private List<Point> CalculatePathPlus1(Point ptStart, Point ptDest)
        {
            List<Point> path = SimpleAStar.CalculatePath(GameMap, ptStart, ptDest);
            // add in leaving the bus stop so it has orders while we get the message saying it got there and are deciding what to do next.
            if (path.Count > 1)
                path.Add(path[path.Count - 2]);
            return path;
        }

		private List<Passenger> AllPickups(Player me, IEnumerable<Passenger> passengers)
		{
			List<Passenger> pickup = new List<Passenger>();
			pickup.AddRange(passengers.Where(
				psngr =>
					(!me.PassengersDelivered.Contains(psngr)) && (psngr != me.Limo.Passenger) && (psngr.Car == null) &&
					(psngr.Lobby != null) && (psngr.Destination != null)).OrderBy(psngr => (CalculatePathPlus1(me, psngr.Lobby.BusStop).Count() + 
                        CalculatePathPlus1(psngr.Lobby.BusStop, psngr.Destination.BusStop ).Count()) / psngr.PointsDelivered ));
			return pickup;
		}

	}
}