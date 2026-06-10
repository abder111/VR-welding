using UnityEngine;

/// <summary>
/// Simple physical UI button that can be clicked by the Meta XR Simulator mouse,
/// or triggered by colliding with the torch/hand.
/// </summary>
public class JointButton : MonoBehaviour
{
    public System.Action onClick;
    private float cooldown = 1f;
    private float lastClickTime = 0f;

    // For Simulator mouse click
    void OnMouseDown()
    {
        TriggerButton();
    }

    // For VR Physical touch
    void OnTriggerEnter(Collider other)
    {
        // Prevent random physics jitter from spamming the button
        if (Time.time - lastClickTime < cooldown) return;

        // If a hand or the torch touches it
        if (other.name.Contains("Hand") || other.name.Contains("Torch") || other.name.Contains("Controller"))
        {
            TriggerButton();
        }
    }

    private void TriggerButton()
    {
        if (Time.time - lastClickTime < cooldown) return;
        
        lastClickTime = Time.time;
        
        // Simple visual feedback (press down)
        StartCoroutine(ButtonPressAnim());

        if (onClick != null) onClick.Invoke();
    }

    private System.Collections.IEnumerator ButtonPressAnim()
    {
        Vector3 origPos = transform.localPosition;
        transform.localPosition = origPos - new Vector3(0f, 0.005f, 0f);
        
        yield return new WaitForSeconds(0.15f);
        
        transform.localPosition = origPos;
    }
}
