using Elements.Core;

namespace ResoniteUVTileSplitter;
internal record struct UVTileKey(int2 UV) {
	internal readonly int U {
		get => UV.x;
	}
	internal readonly int V {
		get => UV.y;
	}

	internal UVTileKey(int u, int v) : this(new int2(u, v)) {

	}


	internal static UVTileKey FromUV(float u, float v) {
		//TODO: handle degenerate cases
		return new UVTileKey((int)MathF.Floor(u), (int)MathF.Floor(v));
	}
	internal static UVTileKey FromUV(float2 uv) {
		return FromUV(uv.x, uv.y);
	}
	internal static UVTileKey Min(UVTileKey a, UVTileKey b, UVTileKey c) {
		return new UVTileKey(Math.Min(a.U, Math.Min(b.U, c.U)), Math.Min(a.V, Math.Min(b.V, c.V)));
	}
	internal static UVTileKey Max(UVTileKey a, UVTileKey b, UVTileKey c) {
		return new UVTileKey(Math.Max(a.U, Math.Max(b.U, c.U)), Math.Max(a.V, Math.Max(b.V, c.V)));
	}
}
