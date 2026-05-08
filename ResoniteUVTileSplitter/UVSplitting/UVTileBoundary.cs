using Elements.Core;

namespace ResoniteUVTileSplitter.UVSplitting;
/// <summary>
/// Represents a line that cut through a triangle / a half-plane that can be intersected with a triangle.
/// </summary>
/// <param name="Origin">A point on the line/edge of the half-plane</param>
/// <param name="Normal">The normal pointing in the direction of the half-plane</param>
internal record struct UVTileBoundary(float2 Origin, float2 Normal) {
	
}
