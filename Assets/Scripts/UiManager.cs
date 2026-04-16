using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UiManager : MonoBehaviour
{
    [SerializeField] private GameObject debugValuePrefab;
    [SerializeField] private Transform debugValueContainer;

    private readonly Dictionary<string, TextMeshProUGUI> debugValueTexts = new();

    public void UpdateDebugValue(string name, float value)
    {
        if (!debugValueTexts.TryGetValue(name, out TextMeshProUGUI debugText))
        {
            GameObject debugItem = Instantiate(debugValuePrefab, debugValueContainer);

            debugText = debugItem.GetComponent<TextMeshProUGUI>();
            if (debugText == null)
            {
                debugText = debugItem.GetComponentInChildren<TextMeshProUGUI>();
            }

            if (debugText == null)
            {
                Debug.LogError($"No TextMeshProUGUI found on debug item prefab for key '{name}'.");
                return;
            }

            debugValueTexts.Add(name, debugText);
        }

        debugText.text = $"{name}: {value:F2}";
    }
}
