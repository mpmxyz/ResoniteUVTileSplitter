using System.Text.Unicode;

using Elements.Core;

namespace ResoniteUVTileSplitter;
internal record struct UVTriangle(TriangleUV UV0, TriangleUV UV1, TriangleUV UV2) {
	internal UVTriangle(
		float2 uv0,
		float2 uv1,
		float2 uv2) : this(
			new TriangleUV(uv0, new(1, 0, 0)),
			new TriangleUV(uv0, new(0, 1, 0)),
			new TriangleUV(uv0, new(0, 0, 1))) {

	}

	internal readonly SingleAxisRange URange {
		get {

			return SingleAxisRange.FromElements(UV0.U, UV1.U, UV2.U);
		}
	}
	internal readonly SingleAxisRange VRange {
		get {

			return SingleAxisRange.FromElements(UV0.V, UV1.V, UV2.V);
		}
	}

	internal void RotateLeft() {
		(UV0, UV1, UV2) = (UV1, UV2, UV0);
	}
	internal void RotateRight() {
		(UV0, UV1, UV2) = (UV2, UV0, UV1);
	}
}
