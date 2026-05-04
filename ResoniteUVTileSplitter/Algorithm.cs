using Elements.Core;

namespace ResoniteUVTileSplitter;
internal class Algorithm {
	/// <summary>
	/// This method will split a given triangle into triangles conforming to a UV tile grid.
	/// </summary>
	/// <param name="initial">initial triangle to be split</param>
	/// <param name="tiles">All combinations of UV tile and triangles will be added to this collection.</param>
	internal static void SplitIntoTiles(UVTriangle initial, ICollection<(UVTileKey, UVTriangle)> tiles) {
		SplitIntoTiles(initial, tiles, [], [], [], []);
	}

	/// <summary>
	/// This method will split a given triangle into triangles conforming to a UV tile grid.
	/// </summary>
	/// <param name="initial">initial triangle to be split</param>
	/// <param name="tiles">All combinations of UV tile and triangles will be added to this collection.</param>
	/// <param name="halfCutU">This parameter only exists to avoid memory allocations by object reuse. (internally used list of triangles, undefined state after call)</param>
	/// <param name="fullCutU">This parameter only exists to avoid memory allocations by object reuse. (internally used list of triangles, undefined state after call)</param>
	/// <param name="halfCutV">This parameter only exists to avoid memory allocations by object reuse. (internally used list of triangles, undefined state after call)</param>
	/// <param name="fullCutV">This parameter only exists to avoid memory allocations by object reuse. (internally used list of triangles, undefined state after call)</param>
	internal static void SplitIntoTiles(
		UVTriangle initial,
		ICollection<(UVTileKey, UVTriangle)> tiles,
		List<UVTriangle> halfCutU,
		List<UVTriangle> fullCutU,
		List<UVTriangle> halfCutV,
		List<UVTriangle> fullCutV) {
		// split by U, each result is split by V (only executes the splits that are required)
		//TODO: check for stack allocated alternative to CutUV lists
		if (DebugHelpers.PrintTrace) {
			DebugHelpers.Trace($"SplitIntoTiles {initial}, {tiles.Count}");
		}

		//processes a single tile of triangles
		void ProcessUVTile(int uIndex, int vIndex, List<UVTriangle> triangles) {
			if (DebugHelpers.PrintTrace) {
				DebugHelpers.Trace($"ProcessUVTile {uIndex}, {vIndex}, {triangles.Count}");
			}
			UVTileKey key = new(uIndex, vIndex);
			foreach(UVTriangle triangle in triangles) {
				tiles.Add((key, triangle));
			}
		}

		//processes a whole column of triangles within the same range of u coordinates
		//splits it into UV tiles
		void ProcessUColumn(int uIndex, List<UVTriangle> uSlice) {
			if (DebugHelpers.PrintTrace) {
				DebugHelpers.Trace($"ProcessUColumn {uIndex}, {uSlice.Count}");
			}
			foreach (UVTriangle sliceTriangle in uSlice) {
				SingleAxisRange vRange = sliceTriangle.VRange;
				foreach (Piece vPiece in vRange.Pieces) {
					halfCutV.Clear();
					fullCutV.Clear();
					Cut(sliceTriangle, vPiece.LowerVBoundary, halfCutV);
					foreach (UVTriangle triangle in halfCutV) {
						Cut(triangle, vPiece.UpperVBoundary, fullCutV);
					}
					ProcessUVTile(uIndex, vPiece.Index, fullCutV);
				}
			}
		}

		//splits the triangle into U columns
		SingleAxisRange uRange = initial.URange;
		foreach (Piece uPiece in uRange.Pieces) {
			if (DebugHelpers.PrintTrace) {
				DebugHelpers.Trace($"uPiece {uPiece.Index}");
			}
			halfCutU.Clear();
			fullCutU.Clear();
			Cut(initial, uPiece.LowerUBoundary, halfCutU);
			foreach (UVTriangle triangle in halfCutU) {
				Cut(triangle, uPiece.UpperUBoundary, fullCutU);
			}
			ProcessUColumn(uPiece.Index, fullCutU);
		}
		if (DebugHelpers.PrintTrace) {
			DebugHelpers.Trace($"Exit SplitIntoTiles {initial}, {tiles.Count}");
		}
	}

	/// <summary>
	/// Intersects a triangle with a given half plane. (positive side)
	/// </summary>
	/// <param name="triangle">to be intersected</param>
	/// <param name="boundary">half-plane the triangle is intersected with</param>
	/// <param name="positiveHalves">collection that is appended to with triangles that are both part of the original triangle and the half-plane</param>
	internal static void Cut(UVTriangle triangle, UVTileBoundary boundary, ICollection<UVTriangle> positiveHalves) {
		var initialCount = positiveHalves.Count;
		if (DebugHelpers.PrintTrace) {
			DebugHelpers.Trace($"Cut {triangle}, {boundary}, {positiveHalves.Count}");
		}
		Cut(triangle, boundary.Offset, boundary.Normal, positiveHalves);
		if (DebugHelpers.PrintTrace) {
			DebugHelpers.Trace($"Exit Cut {triangle}, {boundary}, {positiveHalves.Count}");
		}
		foreach (var outputTriangle in positiveHalves.Skip(initialCount)) {
			if (DebugHelpers.PrintTrace) {
				DebugHelpers.Trace($" {outputTriangle}");
			}
		}
	}

	/// <summary>
	/// Intersects a triangle with a given half plane. (positive side)
	/// </summary>
	/// <param name="triangle">to be intersected</param>
	/// <param name="offset">offset of the half-plane</param>
	/// <param name="normal">normal of the half-plane, points in the direction of the positive/"kept" side</param>
	/// <param name="positiveHalves">collection that is appended to with triangles that are both part of the original triangle and the half-plane</param>
	internal static void Cut(UVTriangle triangle, float2 offset, float2 normal, ICollection<UVTriangle> positiveHalves) {
		float Distance(TriangleUV uv) {
			return MathX.Dot(uv.UV - offset, normal);
		}

		TriangleUV Intersect(TriangleUV a, TriangleUV b) {
			//t=(o-a).n / (b-a).n
			//b * t + a * (1-t)
			float2 oa = offset - a.UV;
			float2 ba = b.UV - a.UV;
			float t = MathX.Dot(oa, normal) / MathX.Dot(ba, normal);
			return TriangleUV.Lerp(a, b, t);
		}

		//rotate triangle until Distance(UV0) <= 0 and Distance(UV1) > 0
		//In other words, (UV0, UV1) should be part of the transition from the negative to the positive side.
		//If no such transition exists there will also not be a transition in opposite direction.
		//There can also be only one such transition at maximum.
		//The comments assume d_ = Distance(UV_).
		bool IsNotNormalized() {
			return Distance(triangle.UV1) <= 0 || Distance(triangle.UV0) > 0;
		}

		if (IsNotNormalized()) {
			triangle.RotateLeft(); //-> d0 <= 0
			if (IsNotNormalized()) {
				triangle.RotateLeft(); //-> d0 <= 0
				//if (d1 <= 0) will confirm if d1 > 0
			} //else: d1 > 0
		} //else: d1 > 0
		float d1 = Distance(triangle.UV1);
		if (IsNotNormalized()) {
			//third try: either whole triangle is dX <= 0 or it is dX > 0 -> only exists in one half
			if (d1 > 0) { // dX > 0
				positiveHalves.Add(triangle);
			} else { // dX <= 0
				//negativeHalves.Add(triangle);
			}
			return;
		} //else: d1 > 0 (!)
		float d0 = Distance(triangle.UV0);
		float d2 = Distance(triangle.UV2);

		if (d0 < 0) { //&& d1 > 0
					  // cut (UV0, UV1)
			var intersection01 = Intersect(triangle.UV0, triangle.UV1);
			if (d2 < 0) {
				//cut (UV1, UV2)
				var intersection12 = Intersect(triangle.UV1, triangle.UV2);
				// create negative(UV0, X01, UV2), negative(X01, X12, UV2), positive(X01, UV1, X12)
				//negativeHalves.Add(new UVTriangle(triangle.UV0, intersection01, triangle.UV2));
				//negativeHalves.Add(new UVTriangle(intersection01, intersection12, triangle.UV2));
				positiveHalves.Add(new UVTriangle(intersection01, triangle.UV1, intersection12));
			} else if (d2 > 0) {
				//cut (UV2, UV0)
				var intersection20 = Intersect(triangle.UV2, triangle.UV0);
				// create negative(UV0, X01, X20), positive(X01, UV1, X20), positive(UV1, UV2, X20)
				//negativeHalves.Add(new UVTriangle(triangle.UV0, intersection01, intersection20));
				positiveHalves.Add(new UVTriangle(intersection01, triangle.UV1, intersection20));
				positiveHalves.Add(new UVTriangle(triangle.UV1, triangle.UV2, intersection20));
			} else { //d2 = 0
					 //UV2 is part of transition from positive to negative side.
					 // create negative(UV0, X01, UV2) and positive(X01, UV1, UV2)
				//negativeHalves.Add(new UVTriangle(triangle.UV0, intersection01, triangle.UV2));
				positiveHalves.Add(new UVTriangle(intersection01, triangle.UV1, triangle.UV2));
			}
		} else { // d0 >= 0 && d1 > 0
				 // UV0 is the exact point of transition from negative to positive side.
			if (d2 < 0) {
				// cut (UV1, UV2)
				// create positive(UV0, UV1, X12) and negative(X12, UV2, UV0)
				var intersection12 = Intersect(triangle.UV1, triangle.UV2);
				positiveHalves.Add(new UVTriangle(triangle.UV0, triangle.UV1, intersection12));
				//negativeHalves.Add(new UVTriangle(intersection12, triangle.UV2, triangle.UV0));
			} else { // d0 = 0 && d1 > 0 && d2 >= 0
					 // whole triangle has d>=0 -> only exists in positive half
				positiveHalves.Add(triangle);
			}
		}
	}
}
