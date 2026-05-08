using Elements.Assets;
using Elements.Core;

namespace ResoniteUVTileSplitter.MeshXSplitting;
/// <summary>
/// This class contains methods that are used to mix three vertices together.
/// This is important to reconstruct the new mesh triangles after creating the tiles in UV space.
/// </summary>
internal static class Mixer {
	/// <summary>
	/// Mixes the vertices <paramref name="a"/>, <paramref name="b"/> and <paramref name="c"/> into the vertex <paramref name="result"/>.
	/// <paramref name="a"/>, <paramref name="b"/> and <paramref name="c"/> are expected to be part of the same mesh.
	/// The sum of all <paramref name="coefficients"/> is expected to have the sum of 1.
	/// The uv channel <paramref name="splitUVChannel"/> is offset so that all UVs of UV tile <paramref name="splitOrigin"/> are within the range [0,1].
	/// The target mesh is expected to have the same blendshapes as the source mesh.
	/// </summary>
	/// <param name="coefficients">Mixing factors of the input vertex properties. (x->c, y->b, z->c) The sum of x, y and z has to be 1.</param>
	/// <param name="a">First input vertex</param>
	/// <param name="b">Second input vertex</param>
	/// <param name="c">Third input vertex</param>
	/// <param name="splitUVChannel">This UV channel will be offset.</param>
	/// <param name="splitOrigin">This is the offset that is subtracted from the affected UV channel.</param>
	/// <param name="result">Output vertex -> The vertex this value represents is written to.</param>
	internal static void MixVertices(float3 coefficients, Vertex a, Vertex b, Vertex c, int splitUVChannel, float2 splitOrigin, Vertex result) {
		if (DebugHelpers.PrintTrace) {
			DebugHelpers.Trace($"MixVertices {coefficients}, {a.Position}/{b.Position}/{c.Position}, {a.UV0}/{b.UV0}/{c.UV0}");
		}
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
				float2 offset;
				if (uvChannel == splitUVChannel) {
					//make sure to move into the 0-1 UV range, 
					offset = splitOrigin;
				} else {
					offset = new(0,0);
				}
				result.SetUV(uvChannel, Mix(coefficients, a.GetUV(uvChannel), b.GetUV(uvChannel), c.GetUV(uvChannel)) - offset);
			} else if (sourceMesh.HasUV_3D(uvChannel)) {
				result.SetUV_3D(uvChannel, Mix(coefficients, a.GetUV_Auto(uvChannel), b.GetUV_Auto(uvChannel), c.GetUV_Auto(uvChannel)).xyz);
			} else if (sourceMesh.HasUV_4D(uvChannel)) {
				result.SetUV_4D(uvChannel, Mix(coefficients, a.GetUV_Auto(uvChannel), b.GetUV_Auto(uvChannel), c.GetUV_Auto(uvChannel)));
			}
		}

		if (result.Mesh.HasBoneBindings) {
			int mixedBoneCount = MixBoneBindings(coefficients, a.BoneBinding, b.BoneBinding, c.BoneBinding, out var mixedBoneBinding);
			if (mixedBoneCount > 4) {
				//TODO: accumulate warnings into user information
				UniLog.Warning($"Mixing vertices {a.Index}, {b.Index} and {c.Index} resulted in {mixedBoneCount} bones which is more than 4.");
			}
			result.BoneBinding = mixedBoneBinding;
		}

		MixBlendshapes(coefficients, a, b, c, result);
		if (DebugHelpers.PrintTrace) {
			DebugHelpers.Trace($"MixVertices result {result.Position}, {result.UV0}");
		}
	}

	/// <summary>
	/// This computes new bone bindings as a mix of the input bone bindings.
	/// The sum of all <paramref name="coefficients"/> is expected to have the sum of 1.
	/// </summary>
	/// <param name="coefficients">Mixing factors of the input values. (x->c, y->b, z->c) The sum of x, y and z has to be 1.</param>
	/// <param name="a">First input</param>
	/// <param name="b">Second input</param>
	/// <param name="c">Third input</param>
	/// <param name="result"></param>
	/// <returns>Number of accumulated bone weights > 0; FrooxEngine at the moment only supports up to 4 bones per vertex. Any value greater than that indicates a silent loss of data.</returns>
	private static int MixBoneBindings(float3 coefficients, BoneBinding a, BoneBinding b, BoneBinding c, out BoneBinding result) {
		Dictionary<int, float> boneWeights = [];

		void Accumulate(BoneBinding source, float coefficient) {
			for (int j = 0; j < 4; j++) {
				source.GetBinding(j, out var boneIndex, out var boneWeight);
				if (boneWeight > 0) {
					boneWeights[boneIndex] = boneWeights.GetValueOrDefault(boneIndex, 0) + boneWeight * coefficient;
				}
			}
		}

		Accumulate(a, coefficients.x);
		Accumulate(b, coefficients.y);
		Accumulate(c, coefficients.z);

		result = new();
		foreach ((int bone, float weight) in boneWeights) {
			if (weight > 0) {
				result.AddBone(bone, weight);
			}
		}
		return boneWeights.Count;
	}

	/// <summary>
	/// Mixes the blendshape deltas of vertices <paramref name="a"/>, <paramref name="b"/> and <paramref name="c"/> into the vertex <paramref name="result"/>.
	/// <paramref name="a"/>, <paramref name="b"/> and <paramref name="c"/> are expected to be part of the same mesh.
	/// The sum of all <paramref name="coefficients"/> is expected to have the sum of 1.
	/// The target mesh is expected to have the same blendshapes as the source mesh.
	/// </summary>
	/// <param name="coefficients">Mixing factors of the input values. (x->c, y->b, z->c) The sum of x, y and z has to be 1.</param>
	/// <param name="a">First input vertex</param>
	/// <param name="b">Second input vertex</param>
	/// <param name="c">Third input vertex</param>
	/// <param name="result">Output vertex -> The vertex this value represents is written to.</param>
	/// <returns>Number of mixed blend shapes</returns>
	private static int MixBlendshapes(float3 coefficients, Vertex a, Vertex b, Vertex c, Vertex result) {
		//assumes a.Mesh == b.Mesh == c.Mesh
		MeshX sourceMesh = a.Mesh;
		MeshX targetMesh = result.Mesh;
		foreach (BlendShape sourceBlendshape in sourceMesh.BlendShapes) {
			string name = sourceBlendshape.Name;
			BlendShape targetBlendshape = targetMesh.GetBlendShape(name);
			for (int frameIndex = 0; frameIndex < sourceBlendshape.FrameCount; frameIndex++) {
				BlendShapeFrame sourceFrame = sourceBlendshape[frameIndex];
				BlendShapeFrame targetFrame = targetBlendshape[frameIndex];
				float3 positionDeltaA = sourceFrame.GetPositionDelta(a.Index);
				float3 positionDeltaB = sourceFrame.GetPositionDelta(b.Index);
				float3 positionDeltaC = sourceFrame.GetPositionDelta(c.Index);
				targetFrame.SetPositionDelta(result.Index, Mix(coefficients, positionDeltaA, positionDeltaB, positionDeltaC));
				if (sourceBlendshape.HasNormals) {
					float3 normalDeltaA = sourceFrame.GetNormalDelta(a.Index);
					float3 normalDeltaB = sourceFrame.GetNormalDelta(b.Index);
					float3 normalDeltaC = sourceFrame.GetNormalDelta(c.Index);
					targetFrame.SetNormalDelta(result.Index, Mix(coefficients, normalDeltaA, normalDeltaB, normalDeltaC));
				}
				if (sourceBlendshape.HasTangents) {
					float3 tangentDeltaA = sourceFrame.GetTangentDelta(a.Index);
					float3 tangentDeltaB = sourceFrame.GetTangentDelta(b.Index);
					float3 tangentDeltaC = sourceFrame.GetTangentDelta(c.Index);
					targetFrame.SetTangentDelta(result.Index, Mix(coefficients, tangentDeltaA, tangentDeltaB, tangentDeltaC));
				}
			}
		}

		return result.Mesh.BlendShapeCount;
	}

	/// <summary>
	/// This mixes the three values <paramref name="a"/>, <paramref name="b"/> and <paramref name="c"/> using the <paramref name="coefficients"/>.
	/// The sum of all <paramref name="coefficients"/> is expected to have the sum of 1.
	/// The implementation depends on Coder&lt;<typeparamref name="T"/>&gt;.Identity and Coder&lt;<typeparamref name="T"/>&gt;.Lerp.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="coefficients">Mixing factors of the input values. (x->c, y->b, z->c) The sum of x, y and z has to be 1.</param>
	/// <param name="a">First value</param>
	/// <param name="b">Second value</param>
	/// <param name="c">Third value</param>
	/// <returns>A mixture of all values weighted by the <paramref name="coefficients"/>.</returns>
	private static T Mix<T>(float3 coefficients, T a, T b, T c) {
		T accumulatedValue = Coder<T>.Identity;
		float accumulatedCoefficients = 0;

		void Accumulate(float coefficient, T target) {
			if (coefficient != 0) {
				accumulatedCoefficients += coefficient;
				accumulatedValue = Coder<T>.Lerp(accumulatedValue, target, coefficient / accumulatedCoefficients);
			}
		}

		Accumulate(coefficients.x, a);
		Accumulate(coefficients.y, b);
		Accumulate(coefficients.z, c);

		return accumulatedValue;
	}
}
