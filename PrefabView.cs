using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class PrefabView : VisualElement
{
    private PreviewRenderUtility previewUtility;
    private GameObject prefab;
    private GameObject previewInstance;
    private Vector2 previewDir = new(-180f, 0f);
    private Rect currentContentRect;
    private float zoom = 30f;
    private Bounds bounds;

    public new class UxmlFactory : UxmlFactory<PrefabView, UxmlTraits>
    {
    }

    public PrefabView()
    {
        RegisterCallback<WheelEvent>(OnWheelEvent, TrickleDown.TrickleDown);
        RegisterCallback<PointerDownEvent>(OnPointerDownEvent, TrickleDown.TrickleDown);
        RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        generateVisualContent += OnGenerateVisualContent;
        
        style.flexGrow = 1;
        style.backgroundColor = new StyleColor( resolvedStyle.backgroundColor != Color.clear ? resolvedStyle.backgroundColor :
            EditorGUIUtility.isProSkin ? new Color32(56, 56, 56, 255) : new Color32(194, 194, 194, 255));
    }

    public void SelectPrefab(GameObject selectedPrefab)
    {
        prefab = selectedPrefab;

        var meshes = prefab.GetComponentsInChildren<MeshRenderer>().ToList();
        if (prefab.TryGetComponent<MeshRenderer>(out var meshRenderer))
        {
            meshes.Add(meshRenderer);
        }

        if (meshes.Count == 0)
        {
            Debug.LogWarning("Chosen Prefab has no meshRenderers");
            prefab = null;
            style.backgroundImage = null;
            return;
        }

        var max = meshes.OrderBy(mesh => mesh.bounds.max.magnitude).First().bounds.max;
        var min = meshes.OrderBy(mesh => mesh.bounds.min.magnitude).Last().bounds.min;
        bounds.SetMinMax(min, max);
        zoom = 30f;
        RenderNewFrame();
        MarkDirtyRepaint();
    }

    private void RenderNewFrame()
    {
        if (!prefab) return;
        if (float.IsNaN(contentRect.height) || float.IsNaN(contentRect.width))
        {
            MarkDirtyRepaint();
            return;
        }
        if (contentRect.height <= 0 || contentRect.width <= 0)
        {
            Debug.LogWarning("Size of view is too small to create the preview: " + contentRect);
            return;
        }

        currentContentRect = contentRect;
        previewUtility?.Cleanup();
        previewUtility = new PreviewRenderUtility();
        previewUtility.BeginStaticPreview(contentRect);
        RenderPrefabPreview();
        var image = previewUtility.EndStaticPreview();
        style.backgroundImage = Background.FromTexture2D(image);
    }

    private void RenderPrefabPreview()
    {
        previewUtility.cameraFieldOfView = zoom;
        previewUtility.camera.clearFlags = CameraClearFlags.Nothing;
        previewUtility.camera.cameraType = CameraType.Game;
        previewUtility.camera.backgroundColor = resolvedStyle.backgroundColor;
        previewUtility.camera.transform.SetPositionAndRotation(
            -Vector3.forward * (bounds.size.magnitude * zoom * 0.1f),
            Quaternion.identity);
        previewUtility.camera.nearClipPlane = 0.1f;
        previewUtility.camera.farClipPlane = bounds.size.magnitude * zoom;

        var quaternion = Quaternion.Euler(-previewDir.y, previewDir.x, 0);
        var pos = quaternion * -(bounds.center - prefab.transform.position);

        if (previewInstance == null)
            previewInstance = previewUtility.InstantiatePrefabInScene(prefab);

        previewInstance.transform.position = pos;
        previewInstance.transform.rotation = quaternion;
        previewInstance.transform.Rotate(prefab.transform.rotation.eulerAngles);
        previewUtility.Render(true);
    }

    private void OnGenerateVisualContent(MeshGenerationContext context)
    {
        if (currentContentRect == contentRect) return;
        schedule.Execute(RenderNewFrame);
    }

    private void OnDetachFromPanel(DetachFromPanelEvent evt)
    {
        previewUtility?.Cleanup();
        generateVisualContent -= OnGenerateVisualContent;
    }

    private void OnWheelEvent(WheelEvent evt)
    {
        if (evt.delta.y == 0) return;
        zoom = Mathf.Clamp(zoom + evt.delta.y, 20, 75);
        RenderNewFrame();
        MarkDirtyRepaint();
    }

    private void OnPointerDownEvent(EventBase evt)
    {
        if (!prefab) return;

        EditorGUIUtility.SetWantsMouseJumping(1);
        RegisterCallback<MouseUpEvent>(OnMouseUpEvent, TrickleDown.TrickleDown);
        RegisterCallback<MouseMoveEvent>(OnMouseMoveEvent, TrickleDown.TrickleDown);
        this.CaptureMouse();
    }

    private void OnMouseUpEvent(MouseUpEvent evt)
    {
        EditorGUIUtility.SetWantsMouseJumping(0);
        UnregisterCallback<MouseMoveEvent>(OnMouseMoveEvent, TrickleDown.TrickleDown);
        UnregisterCallback<MouseUpEvent>(OnMouseUpEvent, TrickleDown.TrickleDown);
        this.ReleaseMouse();
    }

    private void OnMouseMoveEvent(MouseMoveEvent evt)
    {
        if (evt.mouseDelta == Vector2.zero) return;

        previewDir = RotatePreview(previewDir, contentRect, evt);
        RenderNewFrame();
        MarkDirtyRepaint();
    }

    private static Vector2 RotatePreview(Vector2 scrollPosition, Rect position, MouseMoveEvent mouseMoveEvent)
    {
        scrollPosition -= mouseMoveEvent.mouseDelta / Mathf.Min(position.width, position.height) * 140f;
        scrollPosition.y = Mathf.Clamp(scrollPosition.y, -90f, 90f);

        return scrollPosition;
    }
}
