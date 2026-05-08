using Elements.Core;

namespace ResoniteUVTileSplitter.UVSplitting;

/// <summary>
/// This value represents a single piece on a range of V values within a grid of size 1.
/// </summary>
/// <param name="Index">Lower value of the segment.</param>
internal record struct VSegment(int Index) {
	//note: boundaries' normals point into the UV tile
	/// <summary>
	/// The lower boundary of this segment
	/// </summary>
	internal readonly UVTileBoundary LowerVBoundary => new(new float2(0, Index), new float2(0, 1));

	/// <summary>
	/// The upper boundary of this segment
	/// </summary>
	internal readonly UVTileBoundary UpperVBoundary => new(new float2(0, Index + 1), new float2(0, -1));
}
