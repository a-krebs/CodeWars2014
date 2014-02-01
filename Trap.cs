/*
 * ----------------------------------------------------------------------------
 * "THE BEER-WARE LICENSE" 
 * As long as you retain this notice you can do whatever you want with this 
 * stuff. If you meet an employee from Windward some day, and you think this
 * stuff is worth it, you can buy them a beer in return. Windward Studios
 * ----------------------------------------------------------------------------
 */

using System.Diagnostics;

namespace PlayerCSharpAI2
{
	/// <summary>
	///     Used to set code coverage breakpoints in the code in DEBUG mode only.
	/// </summary>
	public static class Trap
	{
		// to turn breaks off in a debug session set this to false
		private static bool stopOnBreak = true;

		/// <summary>Will break in to the debugger (debug builds only).</summary>
		public static void trap()
		{
#if DEBUG
			if (stopOnBreak)
				Debugger.Break();
#endif
		}

		/// <summary>Will break in to the debugger if breakOn is true (debug builds only).</summary>
		/// <param name="breakOn">Will break if this boolean value is true.</param>
		public static void trap(bool breakOn)
		{
#if DEBUG
			if (stopOnBreak && breakOn)
				Debugger.Break();
#endif
		}
	}
}