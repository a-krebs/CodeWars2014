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
using System.Linq;
using System.Xml.Linq;

namespace PlayerCSharpAI2.api
{
	public class Passenger
	{
		private Passenger(XElement elemPassenger, List<Company> companies)
		{
			Name = elemPassenger.Attribute("name").Value;
			PointsDelivered = Convert.ToInt32(elemPassenger.Attribute("points-delivered").Value);
			XAttribute attr = elemPassenger.Attribute("lobby");
			if (attr != null)
				Lobby = companies.Find(cpy => cpy.Name == attr.Value);
			attr = elemPassenger.Attribute("destination");
			if (attr != null)
				Destination = companies.Find(cpy => cpy.Name == attr.Value);
			Route = new List<Company>();
			foreach (XElement elemRoute in elemPassenger.Elements("route"))
				Route.Add(companies.Find(cpy => cpy.Name == elemRoute.Value));
			Enemies = new List<Passenger>();
		}

		/// <summary>
		///     The name of this passenger.
		/// </summary>
		public string Name { get; private set; }

		/// <summary>
		///     The number of points a player gets for delivering this passenger.
		/// </summary>
		public int PointsDelivered { get; private set; }

		/// <summary>
		///     The limo the passenger is presently in. null if not in a limo.
		/// </summary>
		public Limo Car { get; set; }

		/// <summary>
		///     The bus stop the passenger is presently waiting in. null if in a limo or has arrived at final destination.
		/// </summary>
		public Company Lobby { get; private set; }

		/// <summary>
		///     The company the passenger wishes to go to. This is valid both at a bus stop and in a car. It is null if
		///     they have been delivered to their final destination.
		/// </summary>
		public Company Destination { get; private set; }

		/// <summary>
		///     The remaining companies the passenger wishes to go to after destination, in order. This does not include
		///     the Destination company.
		/// </summary>
		public IList<Company> Route { get; private set; }

		/// <summary>
		///     If any of these passengers are at a bus stop, this passenger will not exit the car at the bus stop.
		///     If a passenger at the bus stop has this passenger as an enemy, the passenger can still exit the car.
		/// </summary>
		public IList<Passenger> Enemies { get; private set; }

		public static List<Passenger> FromXml(XElement elemPassengers, List<Company> companies)
		{
			List<Passenger> passengers = new List<Passenger>();
			foreach (XElement elemPsngrOn in elemPassengers.Elements("passenger"))
				passengers.Add(new Passenger(elemPsngrOn, companies));

			// need to now assign enemies - needed all Passenger objects created first.
			foreach (XElement elemPsngrOn in elemPassengers.Elements("passenger"))
			{
				Passenger psngrOn = passengers.Find(psngr => psngr.Name == elemPsngrOn.Attribute("name").Value);
				foreach (XElement elemEnemyOn in elemPsngrOn.Elements("enemy"))
					psngrOn.Enemies.Add(passengers.Find(psngr => psngr.Name == elemEnemyOn.Value));
			}

			// set if they're in a lobby
			foreach (Passenger psngrOn in passengers)
			{
				if (psngrOn.Lobby == null)
					continue;
				Company cmpnyOn = companies.Find(cmpny => cmpny == psngrOn.Lobby);
				cmpnyOn.Passengers.Add(psngrOn);
			}

			return passengers;
		}

		public static void UpdateFromXml(List<Company> companies, List<Player> players, List<Passenger> passengers,
			XElement elemPassengers)
		{
			foreach (XElement elemPsngrOn in elemPassengers.Elements("passenger"))
			{
				Passenger psngrOn = passengers.Find(ps => ps.Name == elemPsngrOn.Attribute("name").Value);

				// update passenger settings
				XAttribute attr = elemPsngrOn.Attribute("destination");
				if (attr != null)
					psngrOn.Destination = companies.Find(cmpy => cmpy.Name == attr.Value);
				// rebuild the route
				psngrOn.Route.Clear();
				attr = elemPsngrOn.Attribute("route");
				if (attr != null)
				{
					string[] companyNames = attr.Value.Split(new char[] {';'}, StringSplitOptions.RemoveEmptyEntries);
					foreach (string nameOn in companyNames)
						psngrOn.Route.Add(companies.Find(c => c.Name == nameOn));
				}

				// set props based on waiting, travelling, done
				switch (elemPsngrOn.Attribute("status").Value)
				{
					case "lobby":
						Company cmpny = companies.Find(cmpy => cmpy.Name == elemPsngrOn.Attribute("lobby").Value);
						psngrOn.Lobby = cmpny;
						psngrOn.Car = null;
						if (! cmpny.Passengers.Contains(psngrOn))
							cmpny.Passengers.Add(psngrOn);

						foreach (var plyrOn in players)
							if (plyrOn.Limo.Passenger == psngrOn)
								plyrOn.Limo.Passenger = null;
						foreach (var cmpyOn in companies)
							if (cmpyOn != cmpny)
								cmpyOn.Passengers.Remove(psngrOn);
						break;

					case "travelling":
						Player plyr = players.Find(p => p.Name == elemPsngrOn.Attribute("limo-driver").Value);
						psngrOn.Car = plyr.Limo;
						psngrOn.Lobby = null;

						foreach (var plyrOn in players)
							if (plyrOn != plyr && plyrOn.Limo.Passenger == psngrOn)
								plyrOn.Limo.Passenger = null;
						foreach (var cmpyOn in companies)
							cmpyOn.Passengers.Remove(psngrOn);
						break;

					case "done":
						psngrOn.Destination = null;
						psngrOn.Lobby = null;
						psngrOn.Car = null;
						break;
				}
			}
		}

		public override string ToString()
		{
			return Name;
		}
	}
}