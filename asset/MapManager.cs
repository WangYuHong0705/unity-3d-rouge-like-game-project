using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MapManager : MonoBehaviour
{
    [Header("Internal References")]
    public RectTransform mapContent;

    [Header("Asset References")]
    public GameObject nodePrefab;

    [Header("Path Visuals")]
    public Color pathColor = Color.grey;
    public Sprite pathSprite;
    public float pathThickness = 6f;
    public float dotSpacing = 15f;

    private readonly List<List<MapNode>> runtimeNodes = new();
    private readonly List<GameObject> pathLines = new();
    private int viewCurrentRow;
    private int viewCurrentNode;

    // We hold a reference to the controller ONLY while rendering
    // (This ensures we don't store stale data)

    public void RenderMap(MapData data, UIControllerMap controller)
    {
        Debug.Log("MapManager: Rendering map...");
        if (data == null) return;

        // Auto-find content
        if (mapContent == null) mapContent = GetComponentInChildren<ScrollRect>()?.content;

        // Cleanup
        foreach (Transform t in mapContent) Destroy(t.gameObject);
        foreach (var line in pathLines) Destroy(line);
        pathLines.Clear();
        runtimeNodes.Clear();

        // Resize Content
        float maxY = 0;
        float Yoffset = 0;
        foreach (var row in data.rows) foreach (var n in row.nodes) if (n.anchoredY > maxY) maxY = n.anchoredY;
        mapContent.sizeDelta = new Vector2(mapContent.sizeDelta.x, maxY + Yoffset);

        // Render Nodes
        for (int r = 0; r < data.rows.Count; r++)
        {
            var rowData = data.rows[r];
            List<MapNode> uiRow = new List<MapNode>();

            foreach (var nodeData in rowData.nodes)
            {
                GameObject nodeObj = Instantiate(nodePrefab, mapContent);
                var mapNode = nodeObj.GetComponent<MapNode>();

                // Visual Setup
                nodeObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(nodeData.anchoredX, nodeData.anchoredY);
                mapNode.nodeName = nodeData.nodeName;
                mapNode.visited = nodeData.visited;
                mapNode.SetNodeType(nodeData.nodeType);

                // Interaction Setup
                // We use a lambda to capture the specific data indices
                int capturedR = nodeData.rowIndex;
                int capturedN = nodeData.nodeIndex;
                mapNode.button.onClick.AddListener(() => {
                    // Cheat/Debug: Teleport player when clicking map
                    controller.OnPlayerMovedToNode(capturedR, capturedN);
                });

                uiRow.Add(mapNode);
            }
            runtimeNodes.Add(uiRow);
        }

        // Visual States (Current, Reachable, etc)
        UpdateVisualStates(data.currentRow, data.currentNode);
        UpdatePath();
    }

    private void UpdateVisualStates(int currentRow, int currentNode)
    {
        Debug.Log("MapManager: Updating visual states...");
        viewCurrentRow = currentRow;
        viewCurrentNode = currentNode;

        for (int r = 0; r < runtimeNodes.Count; r++)
        {
            for (int n = 0; n < runtimeNodes[r].Count; n++)
            {
                var mn = runtimeNodes[r][n];

                if (r == currentRow && n == currentNode)
                    mn.SetAvailability(MapNode.Availability.Emphasized);
                else if (r == currentRow + 1)
                    mn.SetAvailability(MapNode.Availability.Default); // Reachable
                else if (mn.visited)
                    mn.SetAvailability(MapNode.Availability.Visited);
                else
                    mn.SetAvailability(MapNode.Availability.Hidden);
            }
        }
    }

    private void UpdatePath()
    {
        Debug.Log("MapManager: Updating path visualization...");
        // Simple visualization: connect visited nodes + current node
        // (Same drawing logic as previous versions)
        List<MapNode> activeNodes = new List<MapNode>();

        foreach (var row in runtimeNodes)
            foreach (var node in row)
                if (node.visited || (node.rowIndex == viewCurrentRow && node.nodeIndex == viewCurrentNode))
                    activeNodes.Add(node);

        // Sort by row
        activeNodes.Sort((a, b) => a.transform.localPosition.y.CompareTo(b.transform.localPosition.y));

        Sprite sprite = pathSprite;

        for (int i = 0; i < activeNodes.Count - 1; i++)
        {
            // Only draw line if they are sequential rows
            // (Assumes linear progression for visual simplicity)
            // You can add stricter logic here if your map branches
            DrawPathSegment(
                activeNodes[i].GetComponent<RectTransform>().anchoredPosition,
                activeNodes[i + 1].GetComponent<RectTransform>().anchoredPosition,
                sprite
            );
        }
    }

    private void DrawPathSegment(Vector2 start, Vector2 end, Sprite sprite)
    {
        Debug.Log("MapManager: Drawing path segment...");
        Vector2 dir = end - start;
        float dist = dir.magnitude;
        int count = Mathf.FloorToInt(dist / dotSpacing);
        Vector2 step = dir / (count + 1);

        for (int i = 1; i <= count; i++)
        {
            GameObject go = new GameObject("PathDot", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(mapContent, false);
            go.transform.SetAsFirstSibling();
            go.GetComponent<Image>().sprite = sprite;
            go.GetComponent<Image>().color = pathColor;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(pathThickness, pathThickness);
            go.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0);
            go.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0);
            go.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            go.GetComponent<RectTransform>().anchoredPosition = start + step * i;
            pathLines.Add(go);
        }
    }
}