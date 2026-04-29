using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerState
{
    Idle,
    Grabbing
}

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(TargetJoint2D))]
[RequireComponent(typeof(SpringJoint2D))]
public class PlayerController : MonoBehaviour
{
    public event Action<string, float> OnDebugValueChanged;
    
    [Header("Player Settings")]
    public LayerMask interactableLayers;
    private PlayerState currState = PlayerState.Idle;

    [Header("Hand Settings")]
    public GameObject hands;
    public float pushStrength = 10f;

    [SerializeField] private float minReach = 0.9f;
    [SerializeField] private float maxReach = 2.0f;

    [Header("Grab Settings")]
    [SerializeField] private float grabHoldForce = 10000f;
    [SerializeField] private float grabHoldMass = 100f;

    private float defaultHoldForce;
    private float defaultHoldMass;
    private bool isColliding = false;
    
    #region Rigidbody2D Components
    private Rigidbody2D playerRb2D;
    private Rigidbody2D handsRb2D;
    
    #endregion

    #region Joint2D Components
    private TargetJoint2D playerTargetJoint;
    private TargetJoint2D handsTargetJoint;
    private SpringJoint2D handsSpringJoint;
    private InputActionAsset inputActions;
    private Vector2 mouseWorld;

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
    }

    void Start()
    {
        handsRb2D = hands.GetComponent<Rigidbody2D>();
        handsBoxCollider = hands.GetComponentInChildren<BoxCollider2D>();

        handsTargetJoint = hands.GetComponent<TargetJoint2D>();
        playerTargetJoint = GetComponent<TargetJoint2D>();
        playerTargetJoint.enabled = false;

        handsSpringJoint = GetComponent<SpringJoint2D>();
        handsSpringJoint.distance = maxReach;
        handsSpringJoint.enabled = false;

        handsCollisionLineRender = GlobalHelper.CreateLineRenderer(hands, LineRendererType.Ellipse, Color.cyan);
        mouseToHandsLineRender = GlobalHelper.CreateLineRenderer(gameObject, LineRendererType.Linear, Color.red);

        defaultHoldForce = handsTargetJoint.maxForce;
        defaultHoldMass = handsRb2D.mass;
    }

    #region Unity Lifecycle
    void FixedUpdate() 
    {
        mouseWorld = Camera.main.ScreenToWorldPoint(inputActions["MousePosition"].ReadValue<Vector2>());

        // Clamp the target to within [minReach, maxReach] of the body so the
        // TargetJoint2D never has to fight the DistanceJoint2D. Without this,
        // pulling the mouse past maxReach makes the distance joint drag the
        // body toward the hand.
        Vector2 toMouse = mouseWorld - (Vector2)transform.position;
        float dist = toMouse.magnitude;
        Vector2 dir = dist > 0.0001f ? toMouse / dist : Vector2.up;
        float clampedDist = Mathf.Clamp(dist, minReach, maxReach);
        PublishDebugValue("Clamped Dist", clampedDist);

        isColliding = handsRb2D.IsTouchingLayers(interactableLayers);
        PublishDebugValue("Is Colliding", isColliding ? 1 : 0);

        // Activate spring joint if body is too far from hands
        if (Vector2.Distance(playerRb2D.position, handsRb2D.position) > maxReach)
        {
            handsSpringJoint.enabled = true;
            PublishDebugValue("Spring Joint Enabled", 1);
        }
        else if (handsSpringJoint.enabled)
        {
            handsSpringJoint.enabled = false;
            PublishDebugValue("Spring Joint Enabled", 0);
        }

        Vector2 reaction = isColliding ? -handsTargetJoint.reactionForce : Vector2.zero;

        switch (currState)
        {
            case PlayerState.Idle:
                handsTargetJoint.target = (Vector2)transform.position + dir * clampedDist;
                playerRb2D.AddForceAtPosition(reaction, handsRb2D.position);
                PublishDebugValue("Reaction", reaction.magnitude);
                break;
            case PlayerState.Grabbing:
                playerTargetJoint.target = (Vector2)hands.transform.position + -dir * clampedDist;
                break;
        }
    }

    void LateUpdate()
    {
        DrawHandsCollider();
        DrawMouseToHandsLine();
    }

    public void OnGrab(InputValue value)
    {
        if (!isColliding)
        {
            if (currState == PlayerState.Grabbing) EndGrab();
            return;
        }
        
        bool pressed = value.isPressed;
        if (pressed && currState == PlayerState.Idle) BeginGrab();
        else if (!pressed && currState == PlayerState.Grabbing) EndGrab();
    }

    private void BeginGrab()
    {
        currState = PlayerState.Grabbing;

        // Pin the hand to where it currently is in world space. The TargetJoint
        // becomes the pivot and its maxForce spikes so the swinging body can't
        // drag it off.
        handsTargetJoint.target = handsRb2D.position;
        handsTargetJoint.maxForce = grabHoldForce;
        handsRb2D.mass = grabHoldMass;

        playerTargetJoint.enabled = true;
    }

    private void EndGrab()
    {
        currState = PlayerState.Idle;

        handsTargetJoint.maxForce = defaultHoldForce;
        handsRb2D.mass = defaultHoldMass;
        
        playerTargetJoint.enabled = false;
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
        switch (currState)
        {
            case PlayerState.Idle:
                handsCollisionLineRender.startColor = Color.cyan;
                handsCollisionLineRender.endColor = Color.cyan;
                break;
            case PlayerState.Grabbing:
                handsCollisionLineRender.startColor = Color.purple;
                handsCollisionLineRender.endColor = Color.purple;
                break;
            default:
                handsCollisionLineRender.startColor = Color.white;
                handsCollisionLineRender.endColor = Color.white;
                break;
        }
        
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
