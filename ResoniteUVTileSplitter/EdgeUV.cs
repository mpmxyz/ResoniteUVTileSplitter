using Elements.Core;

namespace ResoniteUVTileSplitter;
internal record struct EdgeUV(float2 UV, float2 Mixture, int2 VertexIndices) {
	internal readonly float U { get => UV.x; }
	internal readonly float V { get => UV.y; }
}
