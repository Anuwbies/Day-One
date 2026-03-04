using UnityEngine;
using UnityEngine.Rendering;

public class GridVisualizer : MonoBehaviour
{
    [Header("Settings")]
    public Color gridColor = new Color(1, 1, 1, 0.2f);
    public string sortingLayerName = "Background";
    public int sortingOrder = 1000;
    public int range = 15; 

    private Grid grid;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh gridMesh;
    private Material gridMaterial;
    private bool isVisible = false;

    private void Awake()
    {
        grid = GetComponent<Grid>();
        SetupMeshComponents();
    }

    private void SetupMeshComponents()
    {
        // Create a child object to hold the mesh so we don't interfere with the Grid object itself
        GameObject visualizerObj = new GameObject("GridLinesVisualizer");
        visualizerObj.transform.SetParent(transform);
        visualizerObj.transform.localPosition = Vector3.zero;
        visualizerObj.transform.localRotation = Quaternion.identity;
        visualizerObj.transform.localScale = Vector3.one;

        meshFilter = visualizerObj.AddComponent<MeshFilter>();
        meshRenderer = visualizerObj.AddComponent<MeshRenderer>();

        // Use a simple built-in shader that supports transparency
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("UI/Default");
        
        gridMaterial = new Material(shader);
        gridMaterial.color = gridColor;
        meshRenderer.material = gridMaterial;
        
        // DRAW ON TOP of terrain (World layer) but behind Ghosts
        meshRenderer.sortingLayerName = sortingLayerName; 
        meshRenderer.sortingOrder = sortingOrder; 

        visualizerObj.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (meshRenderer != null && meshRenderer.gameObject != null)
        {
            meshRenderer.gameObject.SetActive(visible);
            if (visible) CreateGridMesh();
        }
    }

    private void Update()
    {
        if (!isVisible || grid == null) return;

        // Keep the grid centered on the camera
        Vector3 camPos = Camera.main.transform.position;
        Vector3Int centerCell = grid.WorldToCell(camPos);
        
        // Align to cell centers so our mesh (which uses edge offsets) lines up perfectly
        Vector3 snappedPos = grid.GetCellCenterWorld(centerCell);
        snappedPos.z = -0.01f; // Slight Z-offset to prevent flickering with ground
        meshRenderer.transform.position = snappedPos;
    }

    private void CreateGridMesh()
    {
        if (gridMesh != null) Destroy(gridMesh);

        gridMesh = new Mesh();
        gridMesh.name = "GridLines";

        // Number of lines: (range * 2 + 2) for both axes to cover the edges properly
        int linesPerAxis = (range * 2) + 2;
        int totalLines = linesPerAxis * 2;
        Vector3[] vertices = new Vector3[totalLines * 2];
        int[] indices = new int[totalLines * 2];

        int vIndex = 0;

        float cellW = grid.cellSize.x;
        float cellH = grid.cellSize.y;

        // Vertical lines (drawn at x-offsets from center)
        for (int i = -range; i <= range + 1; i++)
        {
            // Offset by 0.5 to land on the EDGE of the cell rather than the middle
            float xOffset = (i - 0.5f) * cellW;
            float yMin = -(range + 0.5f) * cellH;
            float yMax = (range + 0.5f) * cellH;

            vertices[vIndex] = new Vector3(xOffset, yMin, 0);
            vertices[vIndex + 1] = new Vector3(xOffset, yMax, 0);
            indices[vIndex] = vIndex;
            indices[vIndex + 1] = vIndex + 1;
            vIndex += 2;
        }

        // Horizontal lines
        for (int i = -range; i <= range + 1; i++)
        {
            float yOffset = (i - 0.5f) * cellH;
            float xMin = -(range + 0.5f) * cellW;
            float xMax = (range + 0.5f) * cellW;

            vertices[vIndex] = new Vector3(xMin, yOffset, 0);
            vertices[vIndex + 1] = new Vector3(xMax, yOffset, 0);
            indices[vIndex] = vIndex;
            indices[vIndex + 1] = vIndex + 1;
            vIndex += 2;
        }

        gridMesh.vertices = vertices;
        gridMesh.SetIndices(indices, MeshTopology.Lines, 0);
        meshFilter.mesh = gridMesh;
    }

    private void OnDestroy()
    {
        if (gridMesh != null) Destroy(gridMesh);
        if (gridMaterial != null) Destroy(gridMaterial);
    }
}
