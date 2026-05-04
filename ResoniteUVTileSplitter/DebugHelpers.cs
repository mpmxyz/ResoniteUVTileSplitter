using Elements.Core;

namespace ResoniteUVTileSplitter;
internal static class DebugHelpers {
	internal static readonly bool PrintTrace = false;

	internal static void Trace(string message) {
		UniLog.Log(message);
	}
}
