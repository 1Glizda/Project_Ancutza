using System.Collections.Generic;
using UnityEngine;

namespace Han.CheckIn
{
    /// <summary>
    /// Generates a tactile 3D open ledger / registry book mesh with leather cover,
    /// recessed spine valley, and layered paper rims directly facing the first-person camera.
    /// Page surface is offset to +pageSurfaceZ (+0.004m) to guarantee zero Z-fighting
    /// with TextMeshPro, portrait photos, and stamps placed at negative Z offsets.
    /// Also configures micro-normal parchment material properties and tangents for URP.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class OpenBookMeshGenerator : MonoBehaviour
    {
        [Header("Book Dimensions")]
        [SerializeField] private float pageWidth = 0.22f;
        [SerializeField] private float pageHeight = 0.30f;
        [SerializeField] private float paperThickness = 0.016f;
        [SerializeField] private float spineWidth = 0.024f;
        [SerializeField] private float coverMargin = 0.015f;

        [Header("Depth Separation (Anti Z-Fighting)")]
        [Tooltip("Recesses the page bed along +Z (away from camera) so text/photos at Z <= -0.002m never Z-fight.")]
        [SerializeField] private float pageSurfaceZ = 0.004f;
        [SerializeField] private float spineCreaseZ = 0.0065f;

        [Header("Micro-Normal Parchment Styling")]
        [SerializeField] private bool applyMicroNormalStyling = true;
        [Range(0f, 1.5f)]
        [SerializeField] private float microNormalStrength = 0.14f;
        [Range(0f, 0.3f)]
        [SerializeField] private float parchmentSmoothness = 0.06f;

        private Mesh generatedMesh;
        private static Texture2D cachedMicroNormalMap;

        private void OnEnable()
        {
            BuildMesh();
            if (applyMicroNormalStyling)
            {
                ApplyParchmentMaterialStyling();
            }
        }

        private void OnValidate()
        {
            BuildMesh();
            if (applyMicroNormalStyling)
            {
                ApplyParchmentMaterialStyling();
            }
        }

        public void BuildMesh()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter == null) return;

            generatedMesh = new Mesh();
            generatedMesh.name = "Archival_Ledger_3DMesh";

            float halfH = pageHeight * 0.5f;
            float spineH = spineWidth * 0.5f;

            float leftOuterX = -(spineH + pageWidth);
            float rightOuterX = (spineH + pageWidth);

            float coverLeftX = leftOuterX - coverMargin;
            float coverRightX = rightOuterX + coverMargin;
            float coverTopY = halfH + coverMargin;
            float coverBottomY = -halfH - coverMargin;
            float rimBackZ = pageSurfaceZ + paperThickness;
            float coverBackZ = pageSurfaceZ + paperThickness * 1.55f;

            Vector3[] vertices = new Vector3[]
            {
                // --- 0-3: Left Page Base Bed (at Z = pageSurfaceZ) ---
                new Vector3(leftOuterX, halfH, pageSurfaceZ),
                new Vector3(-spineH, halfH, pageSurfaceZ),
                new Vector3(-spineH, -halfH, pageSurfaceZ),
                new Vector3(leftOuterX, -halfH, pageSurfaceZ),

                // --- 4-7: Right Page Base Bed (at Z = pageSurfaceZ) ---
                new Vector3(spineH, halfH, pageSurfaceZ),
                new Vector3(rightOuterX, halfH, pageSurfaceZ),
                new Vector3(rightOuterX, -halfH, pageSurfaceZ),
                new Vector3(spineH, -halfH, pageSurfaceZ),

                // --- 8-13: Leather Cover Back (at Z = coverBackZ) ---
                new Vector3(coverLeftX, coverTopY, coverBackZ),
                new Vector3(coverLeftX, coverBottomY, coverBackZ),
                new Vector3(0f, coverTopY, coverBackZ * 1.15f),
                new Vector3(0f, coverBottomY, coverBackZ * 1.15f),
                new Vector3(coverRightX, coverTopY, coverBackZ),
                new Vector3(coverRightX, coverBottomY, coverBackZ),

                // --- 14-17: Paper Stack Rims Left & Right ---
                new Vector3(leftOuterX, halfH, rimBackZ),
                new Vector3(leftOuterX, -halfH, rimBackZ),
                new Vector3(rightOuterX, halfH, rimBackZ),
                new Vector3(rightOuterX, -halfH, rimBackZ),

                // --- 18-19: Center Spine Valley Crease ---
                new Vector3(0f, halfH, spineCreaseZ),
                new Vector3(0f, -halfH, spineCreaseZ),

                // --- 20-23: Bottom Paper Block Edge (Visible when book is tilted) ---
                new Vector3(leftOuterX, -halfH, pageSurfaceZ),
                new Vector3(-spineH, -halfH, pageSurfaceZ),
                new Vector3(-spineH, -halfH, rimBackZ),
                new Vector3(leftOuterX, -halfH, rimBackZ),

                new Vector3(spineH, -halfH, pageSurfaceZ),
                new Vector3(rightOuterX, -halfH, pageSurfaceZ),
                new Vector3(rightOuterX, -halfH, rimBackZ),
                new Vector3(spineH, -halfH, rimBackZ)
            };

            Vector2[] uvs = new Vector2[vertices.Length];
            // Left Page UVs
            uvs[0] = new Vector2(0.02f, 0.98f);
            uvs[1] = new Vector2(0.49f, 0.98f);
            uvs[2] = new Vector2(0.49f, 0.02f);
            uvs[3] = new Vector2(0.02f, 0.02f);

            // Right Page UVs
            uvs[4] = new Vector2(0.51f, 0.98f);
            uvs[5] = new Vector2(0.98f, 0.98f);
            uvs[6] = new Vector2(0.98f, 0.02f);
            uvs[7] = new Vector2(0.51f, 0.02f);

            // Leather Cover Back & Rims (sample dark spine/leather gutter at U = 0.50)
            for (int i = 8; i <= 13; i++)
            {
                uvs[i] = new Vector2(0.50f, 0.50f);
            }

            // Paper Stack Rims (sample aged parchment margin at U = 0.01)
            for (int i = 14; i <= 17; i++)
            {
                uvs[i] = new Vector2(0.01f, (i % 2 == 0) ? 0.95f : 0.05f);
            }

            // Spine Valley Crease (sample dark spine center at U = 0.50)
            uvs[18] = new Vector2(0.50f, 0.98f);
            uvs[19] = new Vector2(0.50f, 0.02f);

            // Bottom Paper Block Rims
            for (int i = 20; i < vertices.Length; i++)
            {
                uvs[i] = new Vector2(0.01f, 0.02f);
            }

            List<int> triangles = new List<int>
            {
                // Left Page Bed (Front facing camera - normal points along -Z)
                0, 1, 2,   0, 2, 3,

                // Right Page Bed
                4, 5, 6,   4, 6, 7,

                // Center Spine Valley (Left & Right slopes into spine crease)
                1, 18, 19,  1, 19, 2,
                18, 4, 7,   18, 7, 19,

                // Outer Paper Rim Left
                0, 3, 15,   0, 15, 14,

                // Outer Paper Rim Right
                5, 16, 17,  5, 17, 6,

                // Bottom Paper Stack Rim Left & Right
                20, 21, 22, 20, 22, 23,
                24, 25, 26, 24, 26, 27,

                // Leather Cover Back Left
                8, 10, 11,  8, 11, 9,

                // Leather Cover Back Right
                10, 12, 13, 10, 13, 11
            };

            generatedMesh.vertices = vertices;
            generatedMesh.uv = uvs;
            generatedMesh.triangles = triangles.ToArray();
            generatedMesh.RecalculateNormals();
            generatedMesh.RecalculateTangents();
            generatedMesh.RecalculateBounds();

            filter.sharedMesh = generatedMesh;
        }

        public void ApplyParchmentMaterialStyling()
        {
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr == null) return;

            // Disable world shadow-map cascade acne on the close-up 3D viewmodel
            mr.receiveShadows = false;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (mr.sharedMaterial == null) return;

            Material mat = mr.sharedMaterial;
            mat.SetFloat("_Smoothness", parchmentSmoothness);
            mat.SetFloat("_Glossiness", parchmentSmoothness);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.SetFloat("_ReceiveShadows", 0f);
            mat.SetColor("_BaseColor", new Color(0.98f, 0.95f, 0.90f, 1f));
            mat.SetColor("_Color", new Color(0.98f, 0.95f, 0.90f, 1f));

            Texture2D normalTex = GetOrCreateMicroNormalMap();
            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", microNormalStrength);
                mat.EnableKeyword("_NORMALMAP");
            }
        }

        private static Texture2D GetOrCreateMicroNormalMap()
        {
            const int size = 128;
            if (cachedMicroNormalMap == null)
            {
                cachedMicroNormalMap = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
                cachedMicroNormalMap.name = "Procedural_Parchment_MicroNormal";
                cachedMicroNormalMap.wrapMode = TextureWrapMode.Repeat;
                cachedMicroNormalMap.filterMode = FilterMode.Bilinear;
            }

            float[,] h = new float[size, size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    // Fine isotropic rag-paper micro-fiber grain (no horizontal banding)
                    float n1 = Mathf.PerlinNoise(u * 48f + 1.7f, v * 48f + 4.3f);
                    float n2 = Mathf.PerlinNoise(u * 96f + 9.2f, v * 96f + 2.8f) * 0.5f;
                    h[x, y] = (n1 + n2) * 0.35f;
                }
            }

            Color32[] cols = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                int ym1 = (y - 1 + size) % size;
                int yp1 = (y + 1) % size;
                for (int x = 0; x < size; x++)
                {
                    int xm1 = (x - 1 + size) % size;
                    int xp1 = (x + 1) % size;

                    float dx = (h[xp1, y] - h[xm1, y]) * 0.35f;
                    float dy = (h[x, yp1] - h[x, ym1]) * 0.35f;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;

                    byte r = (byte)Mathf.Clamp(Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), 0, 255);
                    byte g = (byte)Mathf.Clamp(Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), 0, 255);
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 128, 255);
                    cols[y * size + x] = new Color32(r, g, b, 255);
                }
            }

            cachedMicroNormalMap.SetPixels32(cols);
            cachedMicroNormalMap.Apply(true, false);
            return cachedMicroNormalMap;
        }

        public float PageWidth => pageWidth;
        public float PageHeight => pageHeight;
        public float PageSurfaceZ => pageSurfaceZ;
    }
}
