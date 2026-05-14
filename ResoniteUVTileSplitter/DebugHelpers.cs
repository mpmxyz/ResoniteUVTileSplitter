namespace ResoniteUVTileSplitter;
/// <summary>
/// This class is used for debugging. It keeps the main algorithms clean from mod loader types.
/// </summary>
internal static class DebugHelpers {
	/// <summary>
	/// enables debug log output
	/// </summary>
	internal static bool IsDebugEnabled { get => ResoniteUVTileSplitter.IsDebugEnabled(); }

	/// <summary>
	/// prints a line on the log if PrintTrace is enabled
	/// </summary>
	/// <param name="message">printed message</param>
	internal static void Debug(string message) {
		ResoniteUVTileSplitter.Debug(message);
	}

	/// <summary>
	/// prints a line on the log
	/// </summary>
	/// <param name="message">printed message</param>
	internal static void Msg(string message) {
		ResoniteUVTileSplitter.Msg(message);
	}
}
