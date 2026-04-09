using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Hand Settings")]
    public GameObject hands;
    public Rigidbody2D handsRb2D;
    public float handExtendSpeed = 10f;
    public float handRotationSpeed = 10f;
    [SerializeField] private float minReach = 0.9f;
    [SerializeField] private float maxReach = 2.0f;

    [Header("Collider Settings")]
    [SerializeField] private CapsuleCollider2D capsule;
    [SerializeField] private float ellipsePadding = 0.02f;
    private InputActionAsset inputActions;

    private float ellipseX; // world-space radius along local X
    private float ellipseY; // world-space radius along local Y
    private Vector2 centerWorld;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        inputActions = GetComponent<PlayerInput>().actions;
        inputActions.Disable();
        inputActions.FindActionMap("Player").Enable();

        handsRb2D = hands.GetComponent<Rigidbody2D>();

        RecalculateEllipseFromCapsule();
    }

    #region Unity Lifecycle
    // Update is called once per frame
    void Update()
    {
        return;
    }

    void FixedUpdate() 
    {
        MoveHands(Time.fixedDeltaTime);
        RotateHands(Time.fixedDeltaTime);
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
        centerWorld = transform.TransformPoint(capsule.offset);
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

        Vector2 targetLocal = localDirection * (boundaryScale * clampedReach);
        Vector2 targetWorld = center + (Vector2)(transform.rotation * targetLocal);
        
        Vector2 current = handsRb2D.position;
        Vector2 next = Vector2.Lerp(current, targetWorld, handExtendSpeed * delta);
        handsRb2D.MovePosition(next); 
    }

    private void RotateHands(float delta)
    {
        var direction = hands.transform.position - transform.position;
        var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
        hands.transform.rotation = Quaternion.Lerp(hands.transform.rotation, Quaternion.Euler(0, 0, angle), handRotationSpeed * delta);
    }

    #endregion
}
