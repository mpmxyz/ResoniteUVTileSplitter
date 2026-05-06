using Elements.Core;

namespace ResoniteUVTileSplitter;
internal record struct SingleAxisRange(int Min, int Max) {
	internal static SingleAxisRange FromElements(float a, float b, float c) {
		var uvMin = Math.Min(a, Math.Min(b, c));
		var uvMax = Math.Max(a, Math.Max(b, c));
		//TODO: degenerate edge cases, limit value range to prevent excessive mesh creation
		var uvFloor = MathX.FloorToInt(uvMin);
		var uvCeil = MathX.CeilToInt(uvMax);
		return new SingleAxisRange(uvFloor, Math.Max(uvCeil - 1, uvFloor));
	}

	internal readonly bool HasBoundaries => Min < Max;
	internal readonly IEnumerable<Piece> Pieces {
		get {
			for (var u = Min; u <= Max; u++) {
				yield return new Piece(u);
			}
		}
	}
}
