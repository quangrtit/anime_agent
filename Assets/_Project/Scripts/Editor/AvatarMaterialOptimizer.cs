using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimeAssistant.Editor
{
    /// <summary>
    /// Builds atlas-backed meshes for imported VRoid renderers. The untouched
    /// source VRM remains under ThirdParty; only generated project assets are
    /// replaced. Blend-shape names and frames are retained for facial motion.
    /// </summary>
    internal static class AvatarMaterialOptimizer
    {
        private const string OutputFolder = "Assets/_Project/Art/Characters/Michan/Processed/MaterialAtlas";
        private const int AtlasSize = 4096;

        public static void Optimize(GameObject root)
        {
            EnsureFolders(OutputFolder);
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null || renderer.sharedMaterials.Length <= 1)
                {
                    continue;
                }

                OptimizeRenderer(renderer);
            }

            AssetDatabase.SaveAssets();
        }

        private static void OptimizeRenderer(SkinnedMeshRenderer renderer)
        {
            var sourceMesh = renderer.sharedMesh;
            var sourceMaterials = renderer.sharedMaterials;
            if (sourceMesh.subMeshCount != sourceMaterials.Length)
            {
                throw new InvalidOperationException(
                    $"Cannot atlas {renderer.name}: {sourceMesh.subMeshCount} submeshes but {sourceMaterials.Length} materials.");
            }

            var groups = Enumerable.Range(0, sourceMaterials.Length)
                .GroupBy(index => MaterialSignature(sourceMaterials[index]))
                .Select(group => group.ToArray())
                .ToArray();
            if (groups.Length > 8)
            {
                throw new InvalidOperationException($"Atlas grouping still produces {groups.Length} materials for {renderer.name}.");
            }

            var safeName = Sanitize(renderer.name);
            var rectBySubmesh = new Rect[sourceMesh.subMeshCount];
            var outputMaterials = new Material[groups.Length];
            for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                var submeshes = groups[groupIndex];
                var readableTextures = submeshes
                    .Select(index => ReadTintedTexture(sourceMaterials[index], false))
                    .ToArray();
                var readableShadeTextures = submeshes
                    .Select(index => ReadTintedTexture(sourceMaterials[index], true))
                    .ToArray();
                var atlas = new Texture2D(4, 4, TextureFormat.RGBA32, true, false)
                {
                    name = $"{safeName}_Atlas_{groupIndex}"
                };
                var rects = atlas.PackTextures(readableTextures, 8, AtlasSize, false);
                var shadeAtlas = new Texture2D(4, 4, TextureFormat.RGBA32, true, false)
                {
                    name = $"{safeName}_ShadeAtlas_{groupIndex}"
                };
                var shadeRects = shadeAtlas.PackTextures(readableShadeTextures, 8, AtlasSize, false);
                for (var i = 0; i < submeshes.Length; i++)
                {
                    if (!Approximately(rects[i], shadeRects[i]))
                    {
                        throw new InvalidOperationException("Base and shade atlas layouts diverged.");
                    }

                    rectBySubmesh[submeshes[i]] = rects[i];
                    UnityEngine.Object.DestroyImmediate(readableTextures[i]);
                    UnityEngine.Object.DestroyImmediate(readableShadeTextures[i]);
                }

                var texturePath = $"{OutputFolder}/{safeName}_Atlas_{groupIndex}.png";
                File.WriteAllBytes(Path.GetFullPath(texturePath), atlas.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(atlas);
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureAtlasImporter(texturePath);
                var shadeTexturePath = $"{OutputFolder}/{safeName}_ShadeAtlas_{groupIndex}.png";
                File.WriteAllBytes(Path.GetFullPath(shadeTexturePath), shadeAtlas.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(shadeAtlas);
                AssetDatabase.ImportAsset(shadeTexturePath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureAtlasImporter(shadeTexturePath);

                var materialPath = $"{OutputFolder}/{safeName}_Atlas_{groupIndex}.mat";
                AssetDatabase.DeleteAsset(materialPath);
                var material = new Material(sourceMaterials[submeshes[0]])
                {
                    name = $"{safeName}_Atlas_{groupIndex}",
                    mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath),
                    color = Color.white
                };
                material.SetTextureScale("_MainTex", Vector2.one);
                material.SetTextureOffset("_MainTex", Vector2.zero);
                if (material.HasProperty("_ShadeTex"))
                {
                    material.SetTexture("_ShadeTex", AssetDatabase.LoadAssetAtPath<Texture2D>(shadeTexturePath));
                    material.SetColor("_ShadeColor", Color.white);
                }

                if (material.HasProperty("_OutlineColor"))
                {
                    material.SetColor("_OutlineColor", new Color(0.035f, 0.045f, 0.09f, 1f));
                    material.SetFloat("_OutlineLightingMix", 0.25f);
                }

                AssetDatabase.CreateAsset(material, materialPath);
                outputMaterials[groupIndex] = material;
            }

            var optimizedMesh = BuildAtlasedMesh(sourceMesh, groups, rectBySubmesh);
            optimizedMesh.name = $"{safeName}_Atlased";
            var meshPath = $"{OutputFolder}/{safeName}_Atlased.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(optimizedMesh, meshPath);
            renderer.sharedMesh = optimizedMesh;
            renderer.sharedMaterials = outputMaterials;
            Debug.Log($"Atlased {renderer.name}: {sourceMaterials.Length} materials -> {outputMaterials.Length}; " +
                      $"vertices {sourceMesh.vertexCount} -> {optimizedMesh.vertexCount}; blend shapes {optimizedMesh.blendShapeCount}.");
        }

        private static Mesh BuildAtlasedMesh(Mesh source, IReadOnlyList<int[]> groups, IReadOnlyList<Rect> rectBySubmesh)
        {
            var sourceVertices = source.vertices;
            var sourceNormals = source.normals;
            var sourceTangents = source.tangents;
            var sourceColors = source.colors;
            var sourceUv = source.uv;
            var sourceUv2 = source.uv2;
            var sourceBoneWeights = source.boneWeights;

            var vertices = new List<Vector3>(source.vertexCount);
            var normals = new List<Vector3>(source.vertexCount);
            var tangents = new List<Vector4>(source.vertexCount);
            var colors = new List<Color>(source.vertexCount);
            var uv = new List<Vector2>(source.vertexCount);
            var uv2 = new List<Vector2>(source.vertexCount);
            var boneWeights = new List<BoneWeight>(source.vertexCount);
            var oldIndexForNew = new List<int>(source.vertexCount);
            var outputTriangles = new List<int>[groups.Count];

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var triangles = new List<int>();
                outputTriangles[groupIndex] = triangles;
                foreach (var submesh in groups[groupIndex])
                {
                    var remap = new Dictionary<int, int>();
                    foreach (var oldIndex in source.GetIndices(submesh))
                    {
                        if (!remap.TryGetValue(oldIndex, out var newIndex))
                        {
                            newIndex = vertices.Count;
                            remap[oldIndex] = newIndex;
                            oldIndexForNew.Add(oldIndex);
                            vertices.Add(sourceVertices[oldIndex]);
                            if (sourceNormals.Length == source.vertexCount) normals.Add(sourceNormals[oldIndex]);
                            if (sourceTangents.Length == source.vertexCount) tangents.Add(sourceTangents[oldIndex]);
                            if (sourceColors.Length == source.vertexCount) colors.Add(sourceColors[oldIndex]);
                            if (sourceUv2.Length == source.vertexCount) uv2.Add(sourceUv2[oldIndex]);
                            if (sourceBoneWeights.Length == source.vertexCount) boneWeights.Add(sourceBoneWeights[oldIndex]);

                            var originalUv = sourceUv.Length == source.vertexCount ? sourceUv[oldIndex] : Vector2.zero;
                            // Preserve UV tiling while keeping positive integer edges at
                            // 1.0; plain Repeat turns them into 0.0 and tears atlas seams.
                            var repeatedUv = new Vector2(WrapUv(originalUv.x), WrapUv(originalUv.y));
                            var rect = rectBySubmesh[submesh];
                            uv.Add(new Vector2(rect.x + repeatedUv.x * rect.width, rect.y + repeatedUv.y * rect.height));
                        }

                        triangles.Add(newIndex);
                    }
                }
            }

            var result = new Mesh
            {
                indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = vertices.ToArray(),
                bindposes = source.bindposes,
                subMeshCount = groups.Count
            };
            if (normals.Count == vertices.Count) result.normals = normals.ToArray();
            if (tangents.Count == vertices.Count) result.tangents = tangents.ToArray();
            if (colors.Count == vertices.Count) result.colors = colors.ToArray();
            if (uv2.Count == vertices.Count) result.uv2 = uv2.ToArray();
            if (boneWeights.Count == vertices.Count) result.boneWeights = boneWeights.ToArray();
            result.uv = uv.ToArray();
            for (var i = 0; i < outputTriangles.Length; i++)
            {
                result.SetTriangles(outputTriangles[i], i, false);
            }

            CopyBlendShapes(source, result, oldIndexForNew);
            result.RecalculateBounds();
            return result;
        }

        private static void CopyBlendShapes(Mesh source, Mesh destination, IReadOnlyList<int> oldIndexForNew)
        {
            var sourceDeltaVertices = new Vector3[source.vertexCount];
            var sourceDeltaNormals = new Vector3[source.vertexCount];
            var sourceDeltaTangents = new Vector3[source.vertexCount];
            for (var shape = 0; shape < source.blendShapeCount; shape++)
            {
                for (var frame = 0; frame < source.GetBlendShapeFrameCount(shape); frame++)
                {
                    source.GetBlendShapeFrameVertices(shape, frame, sourceDeltaVertices, sourceDeltaNormals,
                        sourceDeltaTangents);
                    var deltaVertices = new Vector3[oldIndexForNew.Count];
                    var deltaNormals = new Vector3[oldIndexForNew.Count];
                    var deltaTangents = new Vector3[oldIndexForNew.Count];
                    for (var i = 0; i < oldIndexForNew.Count; i++)
                    {
                        var oldIndex = oldIndexForNew[i];
                        deltaVertices[i] = sourceDeltaVertices[oldIndex];
                        deltaNormals[i] = sourceDeltaNormals[oldIndex];
                        deltaTangents[i] = sourceDeltaTangents[oldIndex];
                    }

                    destination.AddBlendShapeFrame(source.GetBlendShapeName(shape),
                        source.GetBlendShapeFrameWeight(shape, frame), deltaVertices, deltaNormals, deltaTangents);
                }
            }
        }

        private static Texture2D ReadTintedTexture(Material material, bool shade)
        {
            var baseTexture = material.mainTexture ?? Texture2D.whiteTexture;
            var source = shade && material.HasProperty("_ShadeTex")
                ? material.GetTexture("_ShadeTex") ?? Texture2D.whiteTexture
                : baseTexture;
            var width = Mathf.Clamp(baseTexture.width, 4, 2048);
            var height = Mathf.Clamp(baseTexture.height, 4, 2048);
            var temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            var readable = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            var tintProperty = shade ? "_ShadeColor" : "_Color";
            var tint = material.HasProperty(tintProperty) ? material.GetColor(tintProperty) : Color.white;
            if (tint != Color.white)
            {
                var pixels = readable.GetPixels();
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] *= tint;
                }

                readable.SetPixels(pixels);
                readable.Apply();
            }

            return readable;
        }

        private static bool Approximately(Rect left, Rect right)
        {
            return Mathf.Abs(left.x - right.x) < 0.0001f && Mathf.Abs(left.y - right.y) < 0.0001f &&
                   Mathf.Abs(left.width - right.width) < 0.0001f && Mathf.Abs(left.height - right.height) < 0.0001f;
        }

        private static float WrapUv(float value)
        {
            var wrapped = Mathf.Repeat(value, 1f);
            return value > 0f && Mathf.Abs(wrapped) < 0.00001f ? 1f : wrapped;
        }

        private static string MaterialSignature(Material material)
        {
            return string.Join("|", material.shader.name, material.renderQueue,
                FloatProperty(material, "_AlphaMode"), FloatProperty(material, "_CullMode"),
                FloatProperty(material, "_ZWrite"));
        }

        private static float FloatProperty(Material material, string property)
        {
            return material.HasProperty(property) ? material.GetFloat(property) : 0f;
        }

        private static void ConfigureAtlasImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = AtlasSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void EnsureFolders(string folder)
        {
            var current = "Assets";
            foreach (var part in folder.Substring("Assets/".Length).Split('/'))
            {
                var next = $"{current}/{part}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, part);
                }

                current = next;
            }
        }

        private static string Sanitize(string value)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Replace(' ', '_');
        }
    }
}
