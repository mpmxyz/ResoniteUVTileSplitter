using Elements.Assets;
using Elements.Core;

namespace ResoniteUVTileSplitter.MeshXSplitting;

/// <summary>
/// This class contains methods that are used to mix three vertices together.
/// This is important to reconstruct the new mesh triangles after creating the tiles in UV space.
/// </summary>
internal static class Mixer {
	/// <summary>
	/// Mixes the vertices <paramref name="a"/>, <paramref name="b"/> and <paramref name="c"/> into the vertex <paramref name="outputVertex"/>.
	/// <paramref name="a"/>, <paramref name="b"/> and <paramref name="c"/> are expected to be part of the same mesh.
	/// The <paramref name="coefficients"/> are expected to have a sum of 1.
	/// The target mesh is expected to have the same blendshapes as the source mesh.
	/// </summary>
	/// <param name="coefficients">Mixing factors of the input vertex properties. (x->c, y->b, z->c) The sum of x, y and z has to be 1.</param>
	/// <param name="a">First input vertex</param>
	/// <param name="b">Second input vertex</param>
	/// <param name="c">Third input vertex</param>
	/// <param name="outputVertex">Output vertex -> The vertex this value represents is written to.</param>
	/// <returns>information about the success of the mixing operation</returns>
	internal static MixingResult MixVertices(float3 coefficients, Vertex a, Vertex b, Vertex c, Vertex outputVertex) {
		if (DebugHelpers.IsDebugEnabled) {
			DebugHelpers.Debug($"MixVertices {coefficients}, {a.Position}/{b.Position}/{c.Position}, {a.UV0}/{b.UV0}/{c.UV0}");
		}
		MixingResult result = MixingResult.Ok;
		var sourceMesh = a.Mesh;
		outputVertex.Position = Mix(coefficients, a.Position, b.Position, c.Position);
		if (sourceMesh.HasColors) {
			outputVertex.Color = Mix(coefficients, a.Color, b.Color, c.Color);
		}
		if (sourceMesh.HasNormals) {
			outputVertex.Normal = Mix(coefficients, a.Normal, b.Normal, c.Normal);
		}
		if (sourceMesh.HasTangents) {
			outputVertex.Tangent4 = Mix(coefficients, a.Tangent4, b.Tangent4, c.Tangent4);
		}
		for (int uvChannel = 0; uvChannel < 4; uvChannel++) {
			if (sourceMesh.HasUV_2D(uvChannel)) {
				outputVertex.SetUV(uvChannel, Mix(coefficients, a.GetUV(uvChannel), b.GetUV(uvChannel), c.GetUV(uvChannel)));
			} else if (sourceMesh.HasUV_3D(uvChannel)) {
				outputVertex.SetUV_3D(uvChannel, Mix(coefficients, a.GetUV_Auto(uvChannel), b.GetUV_Auto(uvChannel), c.GetUV_Auto(uvChannel)).xyz);
			} else if (sourceMesh.HasUV_4D(uvChannel)) {
				outputVertex.SetUV_4D(uvChannel, Mix(coefficients, a.GetUV_Auto(uvChannel), b.GetUV_Auto(uvChannel), c.GetUV_Auto(uvChannel)));
			}
		}

		if (outputVertex.Mesh.HasBoneBindings) {
			int mixedBoneCount = MixBoneBindings(coefficients, a.BoneBinding, b.BoneBinding, c.BoneBinding, out var mixedBoneBinding);
			if (mixedBoneCount > 4) {
				result |= MixingResult.TooManyBones;
			}
			outputVertex.BoneBinding = mixedBoneBinding;
		}

		MixBlendshapes(coefficients, a, b, c, outputVertex);
		if (DebugHelpers.IsDebugEnabled) {
			DebugHelpers.Debug($"MixVertices result {outputVertex.Position}, {outputVertex.UV0}");
		}
		return result;
	}

	/// <summary>
	/// This computes new bone bindings as a mix of the input bone bindings.
	/// The <paramref name="coefficients"/> are expected to have a sum of 1.
	/// </summary>
	/// <param name="coefficients">Mixing factors of the input values. (x->c, y->b, z->c) The sum of x, y and z has to be 1.</param>
	/// <param name="a">First input</param>
	/// <param name="b">Second input</param>
	/// <param name="c">Third input</param>
	/// <param name="result">Output of the resulting bone bindings</param>
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
	/// Mixes the blendshape deltas of vertices <paramref name="a"/>, <paramref name="b"/> and <paramref name="c"/> into the vertex <paramref name="outputVertex"/>.
	/// <paramref name="a"/>, <paramref name="b"/> and <paramref name="c"/> are expected to be part of the same mesh.
	/// The <paramref name="coefficients"/> are expected to have a sum of 1.
	/// The target mesh is expected to have the same blendshapes as the source mesh.
	/// </summary>
	/// <param name="coefficients">Mixing factors of the input values. (x->c, y->b, z->c) The sum of x, y and z has to be 1.</param>
	/// <param name="a">First input vertex</param>
	/// <param name="b">Second input vertex</param>
	/// <param name="c">Third input vertex</param>
	/// <param name="outputVertex">Output vertex -> The vertex this value represents is written to.</param>
	/// <returns>Number of mixed blend shapes</returns>
	private static int MixBlendshapes(float3 coefficients, Vertex a, Vertex b, Vertex c, Vertex outputVertex) {
		//assumes a.Mesh == b.Mesh == c.Mesh
		MeshX sourceMesh = a.Mesh;
		MeshX targetMesh = outputVertex.Mesh;
		foreach (BlendShape sourceBlendshape in sourceMesh.BlendShapes) {
			string name = sourceBlendshape.Name;
			BlendShape targetBlendshape = targetMesh.GetBlendShape(name);
			//assumes sourceBlendshape.FrameCount == targetBlendshape.FrameCount
			for (int frameIndex = 0; frameIndex < sourceBlendshape.FrameCount; frameIndex++) {
				BlendShapeFrame sourceFrame = sourceBlendshape[frameIndex];
				BlendShapeFrame targetFrame = targetBlendshape[frameIndex];
				
				float3 positionDeltaA = sourceFrame.RawPositions[a.Index];
				float3 positionDeltaB = sourceFrame.RawPositions[b.Index];
				float3 positionDeltaC = sourceFrame.RawPositions[c.Index];
				targetFrame.RawPositions[outputVertex.Index] = Mix(coefficients, positionDeltaA, positionDeltaB, positionDeltaC);
				if (sourceBlendshape.HasNormals) {
					float3 normalDeltaA = sourceFrame.RawNormals[a.Index];
					float3 normalDeltaB = sourceFrame.RawNormals[b.Index];
					float3 normalDeltaC = sourceFrame.RawNormals[c.Index];
					targetFrame.RawNormals[outputVertex.Index] = Mix(coefficients, normalDeltaA, normalDeltaB, normalDeltaC);
				}
				if (sourceBlendshape.HasTangents) {
					float3 tangentDeltaA = sourceFrame.RawTangents[a.Index];
					float3 tangentDeltaB = sourceFrame.RawTangents[b.Index];
					float3 tangentDeltaC = sourceFrame.RawTangents[c.Index];
					targetFrame.RawTangents[outputVertex.Index] = Mix(coefficients, tangentDeltaA, tangentDeltaB, tangentDeltaC);
				}
			}
		}

		return outputVertex.Mesh.BlendShapeCount;
	}

	/// <summary>
	/// This mixes the three values <paramref name="a"/>, <paramref name="b"/> and <paramref name="c"/> using the <paramref name="coefficients"/>.
	/// The <paramref name="coefficients"/> are expected to have a sum of 1.
	/// The implementation depends on Coder&lt;<typeparamref name="T"/>&gt;.Identity and Coder&lt;<typeparamref name="T"/>&gt;.Lerp.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="coefficients">Mixing factors of the input values. (x->c, y->b, z->c) The sum of x, y and z has to be 1.</param>
	/// <param name="a">First value</param>
	/// <param name="b">Second value</param>
	/// <param name="c">Third value</param>
	/// <returns>A mixture of all values weighted by the <paramref name="coefficients"/>.</returns>
	private static T Mix<T>(float3 coefficients, T a, T b, T c) {
		T accumulatedValue = Coder<T>.Default;
		float accumulatedCoefficients = 0;

		void Accumulate(float coefficient, T target) {
			if (coefficient != 0) {
				accumulatedCoefficients += coefficient;
				accumulatedValue = Coder<T>.Lerp(accumulatedValue, target, coefficient / accumulatedCoefficients);
			}
		}
		if (DebugHelpers.IsDebugEnabled) {
			DebugHelpers.Debug($"Mixing {a} {b} {c} with {coefficients}");
		}

		Accumulate(coefficients.x, a);
		Accumulate(coefficients.y, b);
		Accumulate(coefficients.z, c);

		if (DebugHelpers.IsDebugEnabled) {
			DebugHelpers.Debug($"Result {accumulatedValue} (sum(coefficients)={accumulatedCoefficients})");
		}

		return accumulatedValue;
	}
}
