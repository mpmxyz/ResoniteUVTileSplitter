using Elements.Core;

namespace ResoniteUVTileSplitter;
/// <summary>
/// This class is used for debugging.
/// </summary>
internal static class DebugHelpers {
	/// <summary>
	/// enables debug log output
	/// </summary>
	internal static readonly bool PrintTrace = false;

	/// <summary>
	/// prints a line on the log if PrintTrace is enabled
	/// </summary>
	/// <param name="message">printed message</param>
	internal static void Trace(string message) {
		if (PrintTrace) {
			UniLog.Log(message);
		}
	}

	/// <summary>
	/// prints a line on the log
	/// </summary>
	/// <param name="message">printed message</param>
	internal static void Debug(string message) {
		UniLog.Log(message);
	}
}
