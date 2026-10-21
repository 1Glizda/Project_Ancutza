using UnityEngine;

namespace Han.CheckIn
{
    /// <summary>
    /// Generates a low-poly 3D open ledger / registry book mesh with leather cover,
    /// curved spine, and paper rims directly facing the first-person camera.
    /// Flat page bed ensures the WorldSpace Canvas is never occluded.
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

        private Mesh generatedMesh;

        private void OnEnable()
        {
            BuildMesh();
        }

        private void OnValidate()
        {
            BuildMesh();
        }

        public void BuildMesh()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter == null) return;

            generatedMesh = new Mesh();
            generatedMesh.name = "LowPoly_LedgerBacking";

            float halfH = pageHeight * 0.5f;
            float spineH = spineWidth * 0.5f;

            float leftOuterX = -(spineH + pageWidth);
            float rightOuterX = (spineH + pageWidth);

            float coverLeftX = leftOuterX - coverMargin;
            float coverRightX = rightOuterX + coverMargin;
            float coverTopY = halfH + coverMargin;
            float coverBottomY = -halfH - coverMargin;
            float coverBackZ = paperThickness * 1.5f;

            Vector3[] vertices = new Vector3[]
            {
                // --- 0-3: Left Page Base Bed (at Z = 0) ---
                new Vector3(leftOuterX, halfH, 0f),
                new Vector3(-spineH, halfH, 0f),
                new Vector3(-spineH, -halfH, 0f),
                new Vector3(leftOuterX, -halfH, 0f),

                // --- 4-7: Right Page Base Bed (at Z = 0) ---
                new Vector3(spineH, halfH, 0f),
                new Vector3(rightOuterX, halfH, 0f),
                new Vector3(rightOuterX, -halfH, 0f),
                new Vector3(spineH, -halfH, 0f),

                // --- 8-11: Leather Cover Back (at Z = coverBackZ) ---
                new Vector3(coverLeftX, coverTopY, coverBackZ),
                new Vector3(coverLeftX, coverBottomY, coverBackZ),
                new Vector3(0f, coverTopY, coverBackZ * 1.2f),
                new Vector3(0f, coverBottomY, coverBackZ * 1.2f),
                new Vector3(coverRightX, coverTopY, coverBackZ),
                new Vector3(coverRightX, coverBottomY, coverBackZ),

                // --- 14-17: Paper Stack Rims Left & Right ---
                new Vector3(leftOuterX, halfH, paperThickness),
                new Vector3(leftOuterX, -halfH, paperThickness),
                new Vector3(rightOuterX, halfH, paperThickness),
                new Vector3(rightOuterX, -halfH, paperThickness)
            };

            Vector2[] uvs = new Vector2[vertices.Length];
            // Page bed UVs (Parchment texture mapped across left and right pages)
            uvs[0] = new Vector2(0.02f, 0.98f);  // Left outer top
            uvs[1] = new Vector2(0.49f, 0.98f);  // Left inner top (near spine)
            uvs[2] = new Vector2(0.49f, 0.02f);  // Left inner bottom
            uvs[3] = new Vector2(0.02f, 0.02f);  // Left outer bottom

            uvs[4] = new Vector2(0.51f, 0.98f);  // Right inner top (near spine)
            uvs[5] = new Vector2(0.98f, 0.98f);  // Right outer top
            uvs[6] = new Vector2(0.98f, 0.02f);  // Right outer bottom
            uvs[7] = new Vector2(0.51f, 0.02f);  // Right inner bottom

            // Leather Cover Back and Outer Rims
            for (int i = 8; i < vertices.Length; i++)
            {
                uvs[i] = new Vector2(0.05f, 0.05f);
            }

            System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>
            {
                // Page Bed (Front facing camera - normal points along -Z)
                0, 1, 2,  0, 2, 3,
                4, 5, 6,  4, 6, 7,
                1, 4, 7,  1, 7, 2,

                // Outer Paper Rim Left
                0, 3, 15,  0, 15, 14,

                // Outer Paper Rim Right
                5, 16, 17,  5, 17, 6,

                // Leather Cover Back Left
                8, 10, 11,  8, 11, 9,

                // Leather Cover Back Right
                10, 12, 13,  10, 13, 11
            };

            generatedMesh.vertices = vertices;
            generatedMesh.uv = uvs;
            generatedMesh.triangles = triangles.ToArray();
            generatedMesh.RecalculateNormals();
            generatedMesh.RecalculateBounds();

            filter.sharedMesh = generatedMesh;
        }

        public float PageWidth => pageWidth;
        public float PageHeight => pageHeight;
    }
}
