using Elements.Assets;
using Elements.Core;

namespace ResoniteUVTileSplitter;
internal class Mixer {
	internal static T Mix<T>(float3 coefficients, T a, T b, T c) {
		T value = Coder<T>.Identity;
		float sum = 0;

		void Accumulate(float coefficient, T target) {
			if (coefficient != 0) {
				sum += coefficient;
				value = Coder<T>.Lerp(value, target, coefficient / sum);
			}
		}

		Accumulate(coefficients.x, a);
		Accumulate(coefficients.y, b);
		Accumulate(coefficients.z, c);
		
		return value;
	}
	internal static void MixVertices(float3 coefficients, Vertex a, Vertex b, Vertex c, Vertex result) {
		//TODO: BoneBinding (with Exception max # of bones is exceeded)
		//TODO: BlendShapes
		var sourceMesh = a.Mesh;
		if (sourceMesh.HasColors) {
			result.Color = Mix(coefficients, a.Color, b.Color, c.Color);
		}
		if (sourceMesh.HasNormals) {
			result.Normal = Mix(coefficients, a.Normal, b.Normal, c.Normal);
		}
		result.Position = Mix(coefficients, a.Position, b.Position, c.Position);
		if (sourceMesh.HasTangents) {
			result.Tangent4 = Mix(coefficients, a.Tangent4, b.Tangent4, c.Tangent4);
		}
		for (int uvChannel = 0; uvChannel < 4; uvChannel++) {
			if (sourceMesh.HasUV_2D(uvChannel)) {
				result.SetUV(uvChannel, Mix(coefficients, a.GetUV(uvChannel), b.GetUV(uvChannel), c.GetUV(uvChannel)));
			} else if (sourceMesh.HasUV_3D(uvChannel)) {
				result.SetUV_3D(uvChannel, Mix(coefficients, a.GetUV_Auto(uvChannel), b.GetUV_Auto(uvChannel), c.GetUV_Auto(uvChannel)).xyz);
			} else if (sourceMesh.HasUV_4D(uvChannel)) {
				result.SetUV_4D(uvChannel, Mix(coefficients, a.GetUV_Auto(uvChannel), b.GetUV_Auto(uvChannel), c.GetUV_Auto(uvChannel)));
			}
		}
	}
}
