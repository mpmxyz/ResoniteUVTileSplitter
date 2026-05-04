using Elements.Assets;

namespace ResoniteUVTileSplitter;
internal record struct MappedTriangle(Triangle Source, int UVChannel, UVTriangle TargetTriangle) {
	internal MappedTriangle(Triangle source, int uvChannel) : this(
		source,
		uvChannel,
		new UVTriangle(
			source.Vertex0.GetUV(uvChannel),
			source.Vertex1.GetUV(uvChannel),
			source.Vertex2.GetUV(uvChannel)
		)
	) { }

	
}
		
