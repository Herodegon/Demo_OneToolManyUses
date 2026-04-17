using UnityEngine;

public enum LineRendererType
{
    Linear,
    Ellipse,
}

public static class GlobalHelper
{
    public static LineRenderer CreateLineRenderer(GameObject parent, LineRendererType type, Color color = default)
    {
        // Instantiate a new LineRenderer GameObject as child of parent
        GameObject lineRendererObject = new("LineRenderer");
        lineRendererObject.transform.SetParent(parent.transform);
        lineRendererObject.transform.localRotation = Quaternion.identity;
        lineRendererObject.transform.localScale = Vector3.one;
        lineRendererObject.transform.localPosition = Vector3.zero;
        LineRenderer lineRenderer = lineRendererObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        switch (type)
        {
            case LineRendererType.Linear:
                CreateLinearLineRenderer(lineRenderer);
                break;
            case LineRendererType.Ellipse:
                CreateEllipseLineRenderer(lineRenderer);
                break;
            default:
                throw new System.ArgumentException($"Invalid LineRendererType: {type}");
        }
        return lineRenderer;
    }

    private static void CreateLinearLineRenderer(LineRenderer lineRenderer)
    {
        lineRenderer.useWorldSpace = true;
        lineRenderer.widthMultiplier = 1f;
        lineRenderer.startWidth = 0.03f;
        lineRenderer.endWidth = 0.03f;
    }

    private static void CreateEllipseLineRenderer(LineRenderer lineRenderer)
    {
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = true;
        lineRenderer.widthMultiplier = 1f;
        lineRenderer.startWidth = 0.03f;
        lineRenderer.endWidth = 0.03f;
    }
}
