using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput), typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public event Action<string, float> OnDebugValueChanged;
    
    [Header("Player Settings")]
    public LayerMask pushObstacleLayers;

    [Header("Hand Settings")]
    public GameObject hands;
    public float handExtendSpeed = 10f;
    public float handRotationSpeed = 10f;
    [SerializeField] private float minReach = 0.9f;
    [SerializeField] private float maxReach = 2.0f;

    [Header("Collider Settings")]
    [SerializeField] private CapsuleCollider2D capsule;
    [SerializeField] private float ellipsePadding = 0.02f;
    [SerializeField] private float centerHeightOffset = 0.25f;
    
    private Rigidbody2D playerRb2D;
    private Rigidbody2D handsRb2D;
    private InputActionAsset inputActions;

    private float ellipseX; // world-space radius along local X
    private float ellipseY; // world-space radius along local Y
    private Vector2 centerWorld;

    #region Runtime Physics Variables
    private Vector2 forceVectorWorld;

    #endregion

    #region Debug Variables
    private LineRenderer collisionRender;
    private BoxCollider2D handsBoxCollider;

    #endregion

    void Awake()
    {
        inputActions = GetComponent<PlayerInput>().actions;
        inputActions.Disable();
        inputActions.FindActionMap("Player").Enable();

        playerRb2D = GetComponent<Rigidbody2D>();
        handsRb2D = hands.GetComponent<Rigidbody2D>();
        handsBoxCollider = hands.GetComponentInChildren<BoxCollider2D>();

        CreateHandsColliderLineRenderer();
        RecalculateEllipseFromCapsule();
    }

    #region Unity Lifecycle
    void Update()
    {
        return;
    }

    void FixedUpdate() 
    {
        MoveHands(Time.fixedDeltaTime);
        RotateHands(Time.fixedDeltaTime);
    }

    void LateUpdate()
    {
        DrawHandsCollider();
    }

    #endregion

    #region Physics Updates
    private void RecalculateEllipseFromCapsule()
    {
        Vector3 s = transform.lossyScale;
        Vector2 worldSize = new(
            capsule.size.x * Mathf.Abs(s.x),
            capsule.size.y * Mathf.Abs(s.y)
        );
        ellipseX = worldSize.x * 0.5f + ellipsePadding;
        ellipseY = worldSize.y * 0.5f + ellipsePadding;
        centerWorld = transform.TransformPoint(capsule.offset + new Vector2(0, centerHeightOffset));
    }

    #endregion

    #region Hand Movement
    private void MoveHands(float delta)
    {
        var mousePosition = inputActions.FindAction("HandPosition").ReadValue<Vector2>();
        var cameraMain = Camera.main;
        if (cameraMain == null || hands == null || capsule == null) return;

        Vector3 mouseWorld3 = cameraMain.ScreenToWorldPoint(mousePosition);
        Vector2 mouseWorld = new(mouseWorld3.x, mouseWorld3.y);

        // Keep ellipse data in sync with current transform/collider values.
        RecalculateEllipseFromCapsule();
        Vector2 center = centerWorld;
        PublishDebugValue("Center X", center.x);
        PublishDebugValue("Center Y", center.y);
        Vector2 toMouseWorld = mouseWorld - center;

        // Use current hand direction if cursor is exactly at center.
        Vector2 localDirection = Quaternion.Inverse(transform.rotation) * toMouseWorld;
        if (localDirection.sqrMagnitude < 0.0001f)
        {
            Vector2 currentHandDirWorld = (Vector2)hands.transform.position - center;
            localDirection = Quaternion.Inverse(transform.rotation) * currentHandDirWorld;
            if (localDirection.sqrMagnitude < 0.0001f)
            {
                localDirection = Vector2.up;
            }
        }
        localDirection.Normalize();

        float denominator = Mathf.Sqrt(
            (localDirection.x * localDirection.x) / (ellipseX * ellipseX) +
            (localDirection.y * localDirection.y) / (ellipseY * ellipseY)
        );
        float boundaryScale = 1f / Mathf.Max(denominator, 0.0001f);

        float mouseEllipseDistance = Mathf.Sqrt(
            (toMouseWorld.x * toMouseWorld.x) / (ellipseX * ellipseX) +
            (toMouseWorld.y * toMouseWorld.y) / (ellipseY * ellipseY)
        );
        float clampedReach = Mathf.Clamp(mouseEllipseDistance, minReach, maxReach);
        PublishDebugValue("Reach", clampedReach);

        Vector2 targetLocal = localDirection * (boundaryScale * clampedReach);
        Vector2 targetWorld = center + (Vector2)(transform.rotation * targetLocal);
        
        Vector2 current = handsRb2D.position;
        Vector2 next = Vector2.Lerp(current, targetWorld, handExtendSpeed * delta);
        handsRb2D.MovePosition(next); 
    }

    private void RotateHands(float delta)
    {
        Vector2 direction = hands.transform.position - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
        PublishDebugValue("Hands Angle", angle);

        float next = Mathf.LerpAngle(handsRb2D.rotation, angle, handRotationSpeed * delta);
        handsRb2D.MoveRotation(next);
    }

    #endregion

    #region Event Handlers
    private void PublishDebugValue(string name, float value)
    {
        OnDebugValueChanged?.Invoke(name, value);
    }

    #endregion

    #region Debug Methods
    private void CreateHandsColliderLineRenderer()
    {
        collisionRender = gameObject.AddComponent<LineRenderer>();
        collisionRender.useWorldSpace = true;
        collisionRender.loop = true;
        collisionRender.widthMultiplier = 1f;
        collisionRender.startWidth = 0.03f;
        collisionRender.endWidth = 0.03f;
        collisionRender.material = new Material(Shader.Find("Sprites/Default"));
        collisionRender.startColor = Color.cyan;
        collisionRender.endColor = Color.cyan;
        collisionRender.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        collisionRender.receiveShadows = false;
        collisionRender.sortingOrder = 100;
    }

    private void DrawHandsCollider()
    {
        if (collisionRender == null || handsBoxCollider == null || !handsBoxCollider.enabled)
        {
            if (collisionRender != null)
                collisionRender.positionCount = 0;
            return;
        }

        collisionRender.loop = true;
        Transform t = handsBoxCollider.transform;
        Vector2 o = handsBoxCollider.offset;
        Vector2 h = handsBoxCollider.size * 0.5f;

        collisionRender.positionCount = 4;
        collisionRender.SetPosition(0, t.TransformPoint(o + new Vector2(-h.x, -h.y)));
        collisionRender.SetPosition(1, t.TransformPoint(o + new Vector2(h.x, -h.y)));
        collisionRender.SetPosition(2, t.TransformPoint(o + new Vector2(h.x, h.y)));
        collisionRender.SetPosition(3, t.TransformPoint(o + new Vector2(-h.x, h.y)));
    }

    #endregion
}
