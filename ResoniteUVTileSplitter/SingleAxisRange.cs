using Elements.Core;

namespace ResoniteUVTileSplitter;
internal record struct SingleAxisRange(int Min, int Max) {
	internal static SingleAxisRange FromElements(float a, float b, float c) {
		var uvMin = Math.Min(a, Math.Min(b, c));
		var uvMax = Math.Max(a, Math.Max(b, c));
		//TODO: degenerate edge cases
		var uvFloor = MathX.FloorToInt(uvMin);
		var uvCeil = MathX.CeilToInt(uvMax);
		return new SingleAxisRange(uvFloor, uvCeil - 1);
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
