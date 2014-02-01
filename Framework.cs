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
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using log4net;
using log4net.Config;
using PlayerCSharpAI2.AI;
using PlayerCSharpAI2.api;

namespace PlayerCSharpAI2
{
	public class Framework : IPlayerCallback
	{
		private TcpClient tcpClient;
		private readonly MyPlayerBrain brain;
		private readonly string ipAddress = "127.0.0.1";

		private string myGuid;

		// we play a card and remove it from our hand. But at the same time the server is sending us a status with our
		// hand as it sees it. So we ignore updates to our hand if it's been less than a second and we're not seeing the 
		// card we played in the incoming status.
		private PowerUp cardLastPlayed;
		private DateTime cardLastSendTime = DateTime.Now.AddSeconds(-2);

		// this is used to make sure we don't have multiple threads updating the Player/Passenger lists, sending
		// back multiple orders, etc. This is a lousy way to handle this - but it keeps the example simple and
		// leaves room for easy improvement.
		private int signal;

		private static readonly ILog log = LogManager.GetLogger(typeof (Framework));

		/// <summary>
		///     Run the A.I. player. All parameters are optional.
		/// </summary>
		/// <param name="args">I.P. address of server, name</param>
		public static void Main(string[] args)
		{
			XmlConfigurator.Configure();
			if (log.IsInfoEnabled)
				log.Info("***** Windwardopolis II starting *****");

			Framework framework = new Framework(args);
			framework.Run();
		}

		private Framework(IList<string> args)
		{
			brain = new MyPlayerBrain(args.Count >= 2 ? args[1] : null);
			if (args.Count >= 1)
				ipAddress = args[0];
			string msg = string.Format("Connecting to server {0} for user: {1}", ipAddress, brain.Name);
			if (log.IsInfoEnabled)
				log.Info(msg);
			Console.Out.WriteLine(msg);
		}

		private void Run()
		{
			while (true)
			{
				try
				{
					Console.Out.WriteLine();
					Console.Out.WriteLine("starting...");

					tcpClient = new TcpClient();
					tcpClient.Start(this, ipAddress);
					ConnectToServer();

					// It's all messages to us now.
					Console.Out.WriteLine("enter \"exit\" to exit program");
					while (true)
					{
						string line = Console.ReadLine();
						if (line == "exit")
							break;
					}
					Console.Out.WriteLine("exiting...");
					return;
				}
				catch (Exception ex)
				{
					Console.Out.WriteLine("ERROR restarting app (Exception: {0})", ex);
					log.Error("restarting Run()", ex);
				}
			}
		}

		public void StatusMessage(string message)
		{
			Console.Out.WriteLine(message);
		}

		public void IncomingMessage(string message)
		{
			try
			{
				DateTime startTime = DateTime.Now;
				// get the xml - we assume we always get a valid message from the server.
				XDocument xml = XDocument.Parse(message);

				switch (xml.Root.Name.LocalName)
				{
					case "setup":
						Console.Out.WriteLine("Received setup message");
						if (log.IsInfoEnabled)
							log.Info("Received setup message");

						List<Player> players = Player.FromXml(xml.Root.Element("players"));
						List<Company> companies = Company.FromXml(xml.Root.Element("companies"));
						List<CoffeeStore> stores = CoffeeStore.FromXml(xml.Root.Element("stores"));
						List<Passenger> passengers = Passenger.FromXml(xml.Root.Element("passengers"), companies);
						List<PowerUp> powerups = PowerUp.FromXml(xml.Root.Element("powerups"), companies, passengers);
						Map map = new Map(xml.Root.Element("map"), companies);
						myGuid = xml.Root.Attribute("my-guid").Value;
						Player me2 = players.Find(plyr => plyr.Guid == myGuid);

						brain.Setup(map, me2, players, companies, stores, passengers, powerups, PlayerOrdersEvent, PlayerPowerSend);
						break;

					case "powerup-status":
						// may be here because re-started and got this message before the re-send of setup.
						if (string.IsNullOrEmpty(myGuid))
							return;
						lock (this)
						{
							// bad news - we're throwing this message away.
							if (signal > 0)
								return;
							signal++;
						}

						try
						{
							// get what was played
							PlayerAIBase.STATUS puStatus = (PlayerAIBase.STATUS)Enum.Parse(typeof(PlayerAIBase.STATUS), xml.Root.Attribute("status").Value);
							string puGuid = xml.Root.Attribute("played-by") != null ? xml.Root.Attribute("played-by").Value : myGuid;
							Player plyrPowerUp = brain.Players.Find(plyr => plyr.Guid == puGuid);
							PowerUp cardPlayed = PowerUp.GenerateFlyweight(xml.Root.Element("card"), brain.Companies, brain.Passengers, brain.Players);
							
							if (log.IsInfoEnabled)
								log.Info(string.Format("{0} {1} on {2}", plyrPowerUp.Name, puStatus, cardPlayed));

							// do we update the card deck?
							if (Equals(cardPlayed, cardLastPlayed) || DateTime.Now.Subtract(cardLastSendTime).TotalSeconds > 1)
							{
								// move any not in deck to hand
								UpdateCards(xml.Root.Element("cards-deck").Elements("card"), brain.PowerUpDeck, brain.PowerUpHand);
								// delete any not in drawn
								UpdateCards(xml.Root.Element("cards-hand").Elements("card"), brain.PowerUpHand, null);
							}

							// pass in to play cards.
							brain.PowerupStatus(puStatus, plyrPowerUp, cardPlayed);
						}
						finally
						{
							lock (this) { signal--; }
						}
						break;

					case "status":
						// may be here because re-started and got this message before the re-send of setup.
						if (string.IsNullOrEmpty(myGuid))
							return;
						lock (this)
						{
							// bad news - we're throwing this message away.
							if (signal > 0)
								return;
							signal++;
						}
						try
						{

							PlayerAIBase.STATUS status =
								(PlayerAIBase.STATUS)Enum.Parse(typeof(PlayerAIBase.STATUS), xml.Root.Attribute("status").Value);
							XAttribute attr = xml.Root.Attribute("player-guid");
							string guid = attr != null ? attr.Value : myGuid;

							Player.UpdateFromXml(brain.Companies, brain.Players, brain.Passengers, xml.Root.Element("players"));
							Passenger.UpdateFromXml(brain.Companies, brain.Players, brain.Passengers, xml.Root.Element("passengers"));

							// update my path & pick-up.
							Player plyrStatus = brain.Players.Find(plyr => plyr.Guid == guid);
							XElement elem = xml.Root.Element("path");
							if (elem != null)
							{
								string[] path = elem.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
								plyrStatus.Limo.Path.Clear();
								foreach (string stepOn in path)
								{
									int pos = stepOn.IndexOf(',');
									plyrStatus.Limo.Path.Add(new Point(Convert.ToInt32(stepOn.Substring(0, pos)),
										Convert.ToInt32(stepOn.Substring(pos+1))));
								}
							}

							elem = xml.Root.Element("pick-up");
							if (elem != null)
							{
								string[] names = elem.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
								plyrStatus.PickUp.Clear();
								foreach (Passenger psngrOn in names.Select(pickupOn => brain.Passengers.Find(ps => ps.Name == pickupOn)))
									plyrStatus.PickUp.Add(psngrOn);
							}

							// pass in to generate new orders
							brain.GameStatus(status, plyrStatus);

						}
						finally
						{
							lock (this) { signal--; }
						}
						break;

					case "exit":
						Console.Out.WriteLine("Received exit message");
						if (log.IsInfoEnabled)
							log.Info("Received exit message");
						Environment.Exit(0);
						break;

					default:
						string msg = string.Format("ERROR: bad message (XML) from server - root node {0}", xml.Root.Name.LocalName);
						log.Warn(msg);
						Trace.WriteLine(msg);
						break;
				}

				TimeSpan turnTime = DateTime.Now.Subtract(startTime);
				if (turnTime.TotalMilliseconds > 800)
					Console.Out.WriteLine("WARNING - turn took {0} seconds", turnTime.TotalMilliseconds/1000);
			}
			catch (Exception ex)
			{
				Console.Out.WriteLine("Error on incoming message. Exception: {0}", ex);
				log.Error("Error on incoming message.", ex);
			}
		}

		private void UpdateCards(IEnumerable<XElement> elements, List<PowerUp> cardList, List<PowerUp> hand)
		{

			List<PowerUp> deck = new List<PowerUp>();
			foreach (XElement elemCardOn in elements)
				deck.Add(PowerUp.GenerateFlyweight(elemCardOn, brain.Companies, brain.Passengers, brain.Players));
			for (int ind = 0; ind < cardList.Count; )
			{
				PowerUp pu = cardList[ind];
				if (deck.Contains(pu))
				{
					pu.OkToPlay = deck.First(p => Equals(p, pu)).OkToPlay;
					deck.Remove(pu);
					ind++;
					continue;
				}
				// moving from deck to hand
				if (hand != null)
					hand.Add(pu);
				cardList.RemoveAt(ind);
			}
			// did some get added back to the deck/hand?
			cardList.AddRange(deck.Select(pu => new PowerUp(pu)));
		}

		private void PlayerOrdersEvent(string order, List<Point> path, List<Passenger> pickUp)
		{
			try
			{
				// update our info
				if (path.Count > 0)
				{
					brain.Me.Limo.Path.Clear();
					brain.Me.Limo.Path.AddRange(path);
				}
				if (pickUp.Count > 0)
				{
					brain.Me.PickUp.Clear();
					brain.Me.PickUp.AddRange(pickUp);
				}

				XDocument xml = new XDocument();
				XElement elem = new XElement(order);
				xml.Add(elem);
				if (path.Count > 0)
				{
					StringBuilder buf = new StringBuilder();
					foreach (Point ptOn in path)
						buf.Append(Convert.ToString(ptOn.X) + ',' + Convert.ToString(ptOn.Y) + ';');
					elem.Add(new XElement("path", buf));
				}
				if (pickUp.Count > 0)
				{
					StringBuilder buf = new StringBuilder();
					foreach (Passenger psngrOn in pickUp)
						buf.Append(psngrOn.Name + ';');
					elem.Add(new XElement("pick-up", buf));
				}
				tcpClient.SendMessage(xml.ToString());
			}
			catch (Exception ex)
			{
				log.Error(string.Format("PlayerOrdersEvent({0}, ...)", order));
				throw;
			}
		}

		private void PlayerPowerSend(PlayerAIBase.CARD_ACTION action, PowerUp powerup)
		{

			if (log.IsInfoEnabled)
				log.Info(string.Format("Request {0} {1}", action, powerup));

			cardLastPlayed = powerup;
			cardLastSendTime = DateTime.Now;

			XDocument xml = new XDocument();
			XElement elem = new XElement("order", new XAttribute("action", action));
			XElement elemCard = new XElement("powerup", new XAttribute("card", powerup.Card));
			if (powerup.Company != null)
				elemCard.Add(new XAttribute("company", powerup.Company.Name));
			if (powerup.Passenger != null)
				elemCard.Add(new XAttribute("passenger", powerup.Passenger.Name));
			if (powerup.Player != null)
				elemCard.Add(new XAttribute("player", powerup.Player.Name));
			elem.Add(elemCard);
			xml.Add(elem);
			tcpClient.SendMessage(xml.ToString());
		}

		public void ConnectionLost(Exception ex)
		{
			Console.Out.WriteLine("Lost our connection! Exception: " + ex.Message);
			log.Warn("Lost our connection!", ex);

			int delay = 500;
			while (true)
				try
				{
					if (tcpClient != null)
						tcpClient.Close();
					tcpClient = new TcpClient();
					tcpClient.Start(this, ipAddress);

					ConnectToServer();
					Console.Out.WriteLine("Re-connected");
					log.Warn("Re-connected");
					return;
				}
				catch (Exception e)
				{
					log.Warn("Re-connection fails!", e);
					Console.Out.WriteLine("Re-connection fails! Exception: " + e.Message);
					Thread.Sleep(delay);
					delay += 500;
				}
		}

		private void ConnectToServer()
		{
			try
			{
				XDocument doc = new XDocument();
				XElement root = new XElement("join", new XAttribute("name", brain.Name),
					new XAttribute("school", MyPlayerBrain.SCHOOL), new XAttribute("language", "C#"));
				byte[] data = brain.Avatar;
				if (data != null)
					root.Add(new XElement("avatar", Convert.ToBase64String(data)));
				doc.Add(root);
				tcpClient.SendMessage(doc.ToString());
			}
			catch (Exception ex)
			{
				log.Warn("ConnectToServer()", ex);
				throw;
			}
		}
	}
}