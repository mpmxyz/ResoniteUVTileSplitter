using Elements.Core;

namespace ResoniteUVTileSplitter.UVSplitting;

/// <summary>
/// This value represents a single piece on a range of U values within a grid of size 1.
/// </summary>
/// <param name="Index">Lower value of the segment.</param>
internal record struct USegment(int Index) {
	//note: boundaries' normals point into the UV tile
	/// <summary>
	/// The lower boundary of this segment
	/// </summary>
	internal readonly UVTileBoundary LowerUBoundary => new(new float2(Index, 0), new float2(1, 0));

	/// <summary>
	/// The upper boundary of this segment
	/// </summary>
	internal readonly UVTileBoundary UpperUBoundary => new(new float2(Index + 1, 0), new float2(-1, 0));
}
