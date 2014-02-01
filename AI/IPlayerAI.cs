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
using PlayerCSharpAI2.api;

namespace PlayerCSharpAI2.AI
{
	public interface IPlayerAI
	{
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
		void Setup(Map map, Player me, List<Player> players, List<Company> companies, List<CoffeeStore> stores, List<Passenger> passengers,
			List<PowerUp> powerUps, PlayerAIBase.PlayerOrdersEvent ordersEvent, PlayerAIBase.PlayerCardEvent playerPowerSend);

		/// <summary>
		///     Called to send an update message to this A.I. We do NOT have to reply to it.
		/// </summary>
		/// <param name="status">The status message.</param>
		/// <param name="plyrStatus">The status of my player.</param>
		void GameStatus(PlayerAIBase.STATUS status, Player plyrStatus);
	}

	public class PlayerAIBase
	{
		public delegate void PlayerOrdersEvent(string order, List<Point> path, List<Passenger> pickUp);
		public delegate void PlayerCardEvent(PlayerAIBase.CARD_ACTION action, PowerUp powerup);

		// playing a card
		public enum CARD_ACTION
		{
			DRAW,
			DISCARD,
			PLAY
		}

		public enum STATUS
		{
			/// <summary>
			///     Called ever N ticks to update the AI with the game status.
			/// </summary>
			UPDATE,

			/// <summary>
			///     The car has no path.
			/// </summary>
			NO_PATH,

			/// <summary>
			///     The passenger was abandoned, no passenger was picked up.
			/// </summary>
			PASSENGER_ABANDONED,

			/// <summary>
			///     The passenger was delivered, no passenger was picked up.
			/// </summary>
			PASSENGER_DELIVERED,

			/// <summary>
			///     The passenger was delivered or abandoned, a new passenger was picked up.
			/// </summary>
			PASSENGER_DELIVERED_AND_PICKED_UP,

			/// <summary>
			///     The passenger refused to exit at the bus stop because an enemy was there.
			/// </summary>
			PASSENGER_REFUSED_ENEMY,

			/// <summary>
			///     A passenger was picked up. There was no passenger to deliver.
			/// </summary>
			PASSENGER_PICKED_UP,

			/// <summary>
			///     At a bus stop, nothing happened (no drop off, no pick up).
			/// </summary>
			PASSENGER_NO_ACTION,

			/// <summary>
			/// Coffee stop did not stock up car. You cannot stock up when you have a passenger.
			/// </summary>
			COFFEE_STORE_NO_STOCK_UP,

			/// <summary>
			/// Coffee stop stocked up car.
			/// </summary>
			COFFEE_STORE_CAR_RESTOCKED,

			/// <summary>
			/// The passenger refused to board due to lack of coffee.
			/// </summary>
			PASSENGER_REFUSED_NO_COFFEE,

			/// <summary>
			/// The passenger was delivered or abandoned, the new passenger refused to board due to lack of coffee.
			/// </summary>
			PASSENGER_DELIVERED_AND_PICK_UP_REFUSED,

			/// <summary>
			/// A draw request was refused as too many powerups are already in hand.
			/// </summary>
			POWER_UP_DRAW_TOO_MANY,

			/// <summary>
			/// A play request for a card not in hand.
			/// </summary>
			POWER_UP_PLAY_NOT_EXIST,

			/// <summary>
			/// A play request for a card drawn but haven't visited a stop yet.
			/// </summary>
			POWER_UP_PLAY_NOT_READY,

			/// <summary>
			/// It's illegal to play this card at this time.
			/// </summary>
			POWER_UP_ILLEGAL_TO_PLAY,

			/// <summary>
			/// The power up was played. For one that impacts a transit, the passenger is delivered.
			/// </summary>
			POWER_UP_PLAYED,

			/// <summary>
			/// The number of power-ups in the hand were too many. Randome one(s) discarded to reduce to the correct amount.
			/// </summary>
			POWER_UP_HAND_TOO_MANY,
		}
	}
}