using Elements.Core;

namespace ResoniteUVTileSplitter.UVSplitting;

/// <summary>
/// UV coordinates enriched with information on how they were computed
/// </summary>
/// <param name="UV">UV coordinates</param>
/// <param name="Mixture">Mixture between the three vertices that formed the original triangle (sum should be 1.)</param>
internal record struct MixedUV(float2 UV, float3 Mixture) {
	internal readonly float U { get => UV.x; }
	internal readonly float V { get => UV.y; }

	/// <summary>
	/// Mixes two coordinates
	/// </summary>
	/// <param name="a">First coordinate <paramref name="lerp"/>=0</param>
	/// <param name="b">Second coordinate <paramref name="lerp"/>=1</param>
	/// <param name="lerp">Interpolation progress ([0..1])</param>
	/// <returns>A mixture of the two coordinates.</returns>
	static internal MixedUV Lerp(MixedUV a, MixedUV b, float lerp) {
		return new(MathX.Lerp(a.UV, b.UV, lerp), MathX.Lerp(a.Mixture, b.Mixture, lerp));
	}
}
