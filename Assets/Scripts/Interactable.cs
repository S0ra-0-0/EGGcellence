using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class Interactable : MonoBehaviour
{
    [Tooltip("Short description shown in UI or for debugging.")]
    public string description;

    [Tooltip("Event invoked when this object is interacted with.")]
    public UnityEvent onInteract;

    [Tooltip("Event invoked when this object becomes focused (player aims at it).")]
    public UnityEvent onFocus;

    [Tooltip("Event invoked when this object loses focus).")]
    public UnityEvent onDefocus;

    [Tooltip("If true, this object will be disabled after interaction.")]
    public bool disableAfterInteract = false;

    [Tooltip("Optional: GameObject to enable/disable when interacted.")]
    public GameObject toggleObject;

    public void Interact(Player source)
    {
        Debug.Log($"Interacted with '{name}' - {description}");
        onInteract?.Invoke();

        if (toggleObject != null)
            toggleObject.SetActive(!toggleObject.activeSelf);

        if (disableAfterInteract)
            gameObject.SetActive(false);
    }

    public void OnFocus()
    {
        onFocus?.Invoke();
    }

    public void OnDefocus()
    {
        onDefocus?.Invoke();
    }
}