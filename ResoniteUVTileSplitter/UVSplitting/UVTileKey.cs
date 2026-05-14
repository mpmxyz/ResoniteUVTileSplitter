using Elements.Core;

namespace ResoniteUVTileSplitter.UVSplitting;

/// <summary>
/// This represents a UV tile within the UV plane.
/// Each tile has a size of 1x1. Tile (0,0) stretches across [0,1] x [0,1].
/// </summary>
/// <param name="UV">index of the tile = lower bounds of UV coordinates</param>
internal record struct UVTileKey(int2 UV) {
	internal readonly int U {
		get => UV.x;
	}
	internal readonly int V {
		get => UV.y;
	}

	internal UVTileKey(int u, int v) : this(new int2(u, v)) { }

	/// <summary>
	/// Determines a UV tile from a single point
	/// </summary>
	/// <param name="uv">Point to be within the tile</param>
	/// <returns>A tile that contains the given point</returns>
	internal static UVTileKey FromPoint(float2 uv) {
		//TODO: handle degenerate cases, limit value range to prevent excessive mesh creation
		return new UVTileKey((int2)MathX.Floor(uv));
	}
}
