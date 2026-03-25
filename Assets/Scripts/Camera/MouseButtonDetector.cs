using UnityEngine;

/// <summary>
/// TEMPORARY — attach to any GameObject, press your mouse buttons, check Console.
/// Delete this script after you find the button numbers.
/// </summary>
public class MouseButtonDetector : MonoBehaviour
{
    private void Update()
    {
        // Check mouse buttons 0 through 6
        for (int i = 0; i < 7; i++)
        {
            if (Input.GetMouseButtonDown(i))
            {
                Debug.Log($"[MOUSE] Button {i} pressed");
            }
        }

        // Also check scroll
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            Debug.Log($"[MOUSE] Scroll: {scroll:F2}");
        }
    }
}
