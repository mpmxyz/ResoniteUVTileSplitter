using Elements.Core;

namespace ResoniteUVTileSplitter;
internal record struct TriangleUV(float2 UV, float3 Mixture) {
	internal readonly float U { get => UV.x; }
	internal readonly float V { get => UV.y; }

	static internal TriangleUV Lerp(TriangleUV a, TriangleUV b, float lerp) {
		return new(MathX.Lerp(a.UV, b.UV, lerp), MathX.Lerp(a.Mixture, b.Mixture, lerp));
	}
}
