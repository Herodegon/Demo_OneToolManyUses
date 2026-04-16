using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private UiManager uiManager;

    private PlayerController playerController;

    void Awake()
    {
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (playerController != null)
        {
            playerController.OnDebugValueChanged += OnDebugValueChanged;
        }
    }

    // Update is called once per frame
    void Update()
    {
        return;
    }

    private void OnDebugValueChanged(string name, float value)
    {
        uiManager.UpdateDebugValue(name, value);
    }
}
