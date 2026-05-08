using Elements.Core;

namespace ResoniteUVTileSplitter.UVSplitting;

/// <summary>
/// This represents a horizontal range of UV tiles.
/// </summary>
/// <param name="Min">Minimum tile index</param>
/// <param name="Max">Maximum tile index</param>
internal record struct URange(int Min, int Max) {
	internal URange(int2 minMax) : this(minMax.x, minMax.y) { }

	/// <summary>
	/// True if this range stretches across more than one index.
	/// </summary>
	internal readonly bool HasBoundaries => Min < Max;

	/// <summary>
	/// A sequence of all segments within this range
	/// </summary>
	internal readonly IEnumerable<USegment> Segments {
		get {
			for (var i = Min; i <= Max; i++) {
				yield return new USegment(i);
			}
		}
	}
}
