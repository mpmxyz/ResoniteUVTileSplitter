using Elements.Core;

namespace ResoniteUVTileSplitter;

internal record struct Piece(int Index) {
	//note: boundaries' normals point into the UV tile
	internal readonly UVTileBoundary LowerUBoundary => new(new float2(Index, 0), new float2(1, 0));
	internal readonly UVTileBoundary LowerVBoundary => new(new float2(0, Index), new float2(0, 1));
	internal readonly UVTileBoundary UpperUBoundary => new(new float2(Index + 1, 0), new float2(-1, 0));
	internal readonly UVTileBoundary UpperVBoundary => new(new float2(0, Index + 1), new float2(0, -1));
}
