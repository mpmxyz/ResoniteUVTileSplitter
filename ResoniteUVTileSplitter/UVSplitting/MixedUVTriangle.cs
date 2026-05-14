using Elements.Core;

namespace ResoniteUVTileSplitter.UVSplitting;

/// <summary>
/// This is a UV triangle which tracks the mixture ratios of the original's vertices that resulted in its vertices.
/// </summary>
/// <param name="UV0">First point of the triangle</param>
/// <param name="UV1">Second point of the triangle</param>
/// <param name="UV2">Third point of the triangle</param>
internal record struct MixedUVTriangle(MixedUV UV0, MixedUV UV1, MixedUV UV2) {
	/// <summary>
	/// This constructs a triangle with the original mixture ratios. (i.e. (1,0,0) for <paramref name="uv0"/>)
	/// </summary>
	/// <param name="uv0">First point of the triangle</param>
	/// <param name="uv1">Second point of the triangle</param>
	/// <param name="uv2">Third point of the triangle</param>
	internal MixedUVTriangle(
		float2 uv0,
		float2 uv1,
		float2 uv2) : this(
			new MixedUV(uv0, new(1, 0, 0)),
			new MixedUV(uv1, new(0, 1, 0)),
			new MixedUV(uv2, new(0, 0, 1))) {

	}

	/// <summary>
	/// The range of U indices
	/// </summary>
	internal readonly URange URange {
		get {
			return new URange(RangeFromElements(UV0.U, UV1.U, UV2.U));
		}
	}

	/// <summary>
	/// The range of V indices
	/// </summary>
	internal readonly VRange VRange {
		get {
			return new VRange(RangeFromElements(UV0.V, UV1.V, UV2.V));
		}
	}

	/// <summary>
	/// This 
	/// </summary>
	/// <param name="a">First value</param>
	/// <param name="b">Second value</param>
	/// <param name="c">Third value</param>
	/// <returns></returns>
	private static int2 RangeFromElements(float a, float b, float c) {
		var uvMin = Math.Min(a, Math.Min(b, c));
		var uvMax = Math.Max(a, Math.Max(b, c));
		//TODO: handle degenerate edge cases, limit value range to prevent excessive mesh creation
		var uvFloor = MathX.FloorToInt(uvMin);
		var uvCeil = MathX.CeilToInt(uvMax);
		return new int2(uvFloor, Math.Max(uvCeil - 1, uvFloor));
	}

	/// <summary>
	/// Shifts the composition of the triangle forward. This is still the same triangle but with a different initial vertex.
	/// </summary>
	internal void RotateLeft() {
		(UV0, UV1, UV2) = (UV1, UV2, UV0);
	}

	/// <summary>
	/// Shifts the composition of the triangle back. This is still the same triangle but with a different initial vertex.
	/// </summary>
	internal void RotateRight() {
		(UV0, UV1, UV2) = (UV2, UV0, UV1);
	}
}
