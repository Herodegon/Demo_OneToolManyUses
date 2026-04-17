using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent
(
    typeof(PlayerInput), 
    typeof(Rigidbody2D)
)]
public class PlayerController : MonoBehaviour
{
    public event Action<string, float> OnDebugValueChanged;
    
    [Header("Player Settings")]
    public LayerMask interactableLayers;
    public AnimationCurve forceCurve;

    [Header("Hand Settings")]
    public GameObject hands;
    public float handExtendSpeed = 10f;
    public float handRotationSpeed = 10f;
    public float pushStrength = 10f;
    [SerializeField] private float minReach = 0.9f;
    [SerializeField] private float maxReach = 2.0f;

    [Header("Collider Settings")]
    [SerializeField] private CapsuleCollider2D capsule;
    [SerializeField] private float ellipsePadding = 0.02f;
    [SerializeField] private float centerHeightOffset = 0.25f;
    
    #region Physics Variables
    private Rigidbody2D playerRb2D;
    private Rigidbody2D handsRb2D;
    private InputActionAsset inputActions;

    private float ellipseX; // world-space radius along local X
    private float ellipseY; // world-space radius along local Y
    private Vector2 centerWorld;
    private Vector2 mouseWorld;
    private Vector2 handsTargetWorld;

    #endregion

    #region Debug Variables
    private LineRenderer handsCollisionLineRender;
    private LineRenderer mouseToHandsLineRender;
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

        handsCollisionLineRender = GlobalHelper.CreateLineRenderer(hands, LineRendererType.Ellipse, Color.cyan);
        mouseToHandsLineRender = GlobalHelper.CreateLineRenderer(gameObject, LineRendererType.Linear, Color.red);
        RecalculateEllipseFromCapsule();
    }

    #region Unity Lifecycle
    void FixedUpdate() 
    {
        MoveHands(Time.fixedDeltaTime);
        RotateHands(Time.fixedDeltaTime);
        ComputePushForce(Time.fixedDeltaTime);
    }

    void LateUpdate()
    {
        DrawHandsCollider();
        DrawMouseToHandsLine();
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

        mouseWorld = (Vector2)cameraMain.ScreenToWorldPoint(mousePosition);

        // Keep ellipse data in sync with current transform/collider values.
        RecalculateEllipseFromCapsule();
        Vector2 center = centerWorld;
        PublishDebugValue("Center X", center.x);
        PublishDebugValue("Center Y", center.y);
        Vector2 toMouseWorld = mouseWorld - centerWorld;

        // Use current hand direction if cursor is exactly at center.
        Vector2 localDirection = Quaternion.Inverse(transform.rotation) * toMouseWorld;
        if (localDirection.sqrMagnitude < 0.0001f)
        {
            Vector2 currentHandDirWorld = (Vector2)hands.transform.position - centerWorld;
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
        handsTargetWorld = centerWorld + (Vector2)(transform.rotation * targetLocal);

        Vector2 current = handsRb2D.position;
        Vector2 next = Vector2.Lerp(current, handsTargetWorld, handExtendSpeed * delta);
        handsRb2D.MovePosition(next); 
    } 

    private void RotateHands(float delta)
    {
        Vector2 direction = hands.transform.position - transform.position;
        float angle = Vector2.SignedAngle(Vector2.up, direction);
        PublishDebugValue("Hands Angle", angle);

        float next = Mathf.LerpAngle(handsRb2D.rotation, angle, handRotationSpeed * delta);
        handsRb2D.MoveRotation(next);
    }
    private void ComputePushForce(float delta)
    {
        if (!handsRb2D.IsTouchingLayers(interactableLayers)) return;

        // Constraint violation: where the hand wants to be vs where it is.
        Vector2 strain = handsTargetWorld - handsRb2D.position;
        float strainMag = strain.magnitude;
        if (strainMag < 0.0001f) return;
        Vector2 pushDir = -strain / strainMag;              // reaction on the body
        float magnitude = forceCurve.Evaluate(strainMag) * pushStrength;

        // Apply at the HAND, not the body center, so the body can rotate around the "pivot."
        playerRb2D.AddForceAtPosition(pushDir * magnitude, centerWorld);
    }

    #endregion

    #region Event Handlers
    private void PublishDebugValue(string name, float value)
    {
        OnDebugValueChanged?.Invoke(name, value);
    }

    #endregion

    #region Debug Methods
    private void DrawHandsCollider()
    {
        if (handsCollisionLineRender == null || handsBoxCollider == null || !handsBoxCollider.enabled)
        {
            if (handsCollisionLineRender != null)
                handsCollisionLineRender.positionCount = 0;
            return;
        }

        handsCollisionLineRender.loop = true;
        Transform t = handsBoxCollider.transform;
        Vector2 o = handsBoxCollider.offset;
        Vector2 h = handsBoxCollider.size * 0.5f;

        handsCollisionLineRender.positionCount = 4;
        handsCollisionLineRender.SetPosition(0, t.TransformPoint(o + new Vector2(-h.x, -h.y)));
        handsCollisionLineRender.SetPosition(1, t.TransformPoint(o + new Vector2(h.x, -h.y)));
        handsCollisionLineRender.SetPosition(2, t.TransformPoint(o + new Vector2(h.x, h.y)));
        handsCollisionLineRender.SetPosition(3, t.TransformPoint(o + new Vector2(-h.x, h.y)));
    }

    private void DrawMouseToHandsLine()
    {
        if (mouseToHandsLineRender == null) return;
        mouseToHandsLineRender.positionCount = 2;
        mouseToHandsLineRender.SetPosition(0, mouseWorld);
        mouseToHandsLineRender.SetPosition(1, handsRb2D.position);
    }

    #endregion
}
