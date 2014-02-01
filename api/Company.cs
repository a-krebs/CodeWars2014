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
	public class Company
	{
		private Company(XElement elemCompany)
		{
			Name = elemCompany.Attribute("name").Value;
			BusStop = new Point(Convert.ToInt32(elemCompany.Attribute("bus-stop-x").Value),
				Convert.ToInt32(elemCompany.Attribute("bus-stop-y").Value));
			Passengers = new List<Passenger>();
		}

		/// <summary>
		///     The name of the company.
		/// </summary>
		public string Name { get; private set; }

		/// <summary>
		///     The tile with the company's bus stop.
		/// </summary>
		public Point BusStop { get; private set; }

		/// <summary>
		///     The name of the passengers waiting at this company's bus stop for a ride.
		/// </summary>
		public IList<Passenger> Passengers { get; private set; }

		public static List<Company> FromXml(XElement elemCompanies)
		{
			List<Company> companies = new List<Company>();
			foreach (XElement elemCmpyOn in elemCompanies.Elements("company"))
				companies.Add(new Company(elemCmpyOn));
			return companies;
		}

		public override string ToString()
		{
			return string.Format("{0}; {1}", Name, BusStop);
		}
	}
}