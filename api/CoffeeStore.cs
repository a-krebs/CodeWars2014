using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml.Linq;

namespace PlayerCSharpAI2.api
{
	public class CoffeeStore
	{
		private CoffeeStore(XElement elemCompany)
		{
			Name = elemCompany.Attribute("name").Value;
			BusStop = new Point(Convert.ToInt32(elemCompany.Attribute("bus-stop-x").Value),
				Convert.ToInt32(elemCompany.Attribute("bus-stop-y").Value));
		}

		/// <summary>
		/// The name of the coffee store.
		/// </summary>
		public string Name { get; private set; }

		/// <summary>
		/// The tile with the store's bus stop.
		/// </summary>
		public Point BusStop { get; private set; }

		public static List<CoffeeStore> FromXml(XElement elemStores)
		{
			List<CoffeeStore> stores = new List<CoffeeStore>();
			foreach (XElement elemStoreOn in elemStores.Elements("store"))
				stores.Add(new CoffeeStore(elemStoreOn));
			return stores;
		}

		public override string ToString()
		{
			return string.Format("{0}; {1}", Name, BusStop);
		}
	}
}