using UnityEngine;

/// <summary>
/// World-space HUD displayed above the workbench.
/// Shows welding coverage percentage, workflow instructions, and joint type.
/// </summary>
public class WeldingHUD : MonoBehaviour
{
    private TextMesh coverageText;
    private TextMesh instructionText;
    private TextMesh jointTypeText;
    private WeldSeam weldSeam;
    private WeldingTorch torch;
    private WeldingTable weldingTable; // Optional - for workflow instructions

    public void Initialize(WeldSeam seam, WeldingTorch torchRef, Vector3 position)
    {
        weldSeam = seam;
        torch = torchRef;
        transform.position = position;

        // Only create text objects if they don't already exist (re-initialization safe)
        if (coverageText == null)
        {
            var coverageObj = new GameObject("CoverageText");
            coverageObj.transform.SetParent(transform);
            coverageObj.transform.localPosition = Vector3.zero;
            coverageText = coverageObj.AddComponent<TextMesh>();
            coverageText.characterSize = 0.04f;
            coverageText.fontSize = 60;
            coverageText.anchor = TextAnchor.MiddleCenter;
            coverageText.alignment = TextAlignment.Center;
            coverageText.color = Color.white;
        }

        if (instructionText == null)
        {
            var instrObj = new GameObject("InstructionText");
            instrObj.transform.SetParent(transform);
            instrObj.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            instructionText = instrObj.AddComponent<TextMesh>();
            instructionText.characterSize = 0.03f;
            instructionText.fontSize = 48;
            instructionText.anchor = TextAnchor.MiddleCenter;
            instructionText.alignment = TextAlignment.Center;
            instructionText.color = new Color(0.8f, 0.8f, 0.2f);
        }

        if (jointTypeText == null)
        {
            var jointObj = new GameObject("JointTypeText");
            jointObj.transform.SetParent(transform);
            jointObj.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            jointTypeText = jointObj.AddComponent<TextMesh>();
            jointTypeText.characterSize = 0.025f;
            jointTypeText.fontSize = 36;
            jointTypeText.anchor = TextAnchor.MiddleCenter;
            jointTypeText.alignment = TextAlignment.Center;
            jointTypeText.color = new Color(0.5f, 0.85f, 1f);
        }
    }

    public void SetWeldingTable(WeldingTable table)
    {
        weldingTable = table;
    }

    public void ClearSeam()
    {
        weldSeam = null;
    }

    void Update()
    {
        // Billboard: always face the active camera
        if (Camera.main != null)
        {
            transform.LookAt(Camera.main.transform);
            transform.Rotate(0f, 180f, 0f);
        }

        // --- Joint type label ---
        if (jointTypeText != null)
        {
            string jointName = weldingTable != null ? weldingTable.JointNameForHUD : "";
            jointTypeText.text = string.IsNullOrEmpty(jointName) ? "" : "Joint: " + jointName;
        }

        // --- No seam yet: show workflow instructions ---
        if (weldSeam == null)
        {
            if (coverageText != null)
            {
                coverageText.text = "Place pieces on the table!";
                coverageText.color = Color.white;
            }

            if (instructionText != null && weldingTable != null)
            {
                int count = weldingTable.PlacedPieceCount;
                if (count == 0)
                {
                    instructionText.text = "Step 1: Grab a piece from the shelf (E)";
                    instructionText.color = new Color(0.8f, 0.8f, 0.2f);
                }
                else if (count == 1)
                {
                    instructionText.text = "Step 2: Grab a second piece!";
                    instructionText.color = new Color(0.5f, 0.8f, 1f);
                }
            }
            return;
        }

        // --- Seam exists: show welding progress ---
        float coverage = weldSeam.CoveragePercent;

        if (coverage >= 99.9f)
        {
            if (!string.IsNullOrEmpty(weldSeam.FinalGrade))
            {
                if (coverageText != null)
                {
                    coverageText.text = string.Format("Complete! Grade: {0}", weldSeam.FinalGrade);
                    switch (weldSeam.FinalGrade)
                    {
                        case "S": coverageText.color = new Color(1f, 0.85f, 0.2f); break;
                        case "A": coverageText.color = Color.green; break;
                        case "B": coverageText.color = new Color(0.5f, 1f, 0f); break;
                        case "C": coverageText.color = Color.yellow; break;
                        case "D": coverageText.color = new Color(1f, 0.5f, 0f); break;
                        default:  coverageText.color = Color.red; break;
                    }
                }
            }
            if (instructionText != null)
            {
                instructionText.text = "Press Clear to try another joint!";
                instructionText.color = Color.green;
            }
        }
        else
        {
            if (coverageText != null)
            {
                coverageText.text = string.Format("Coverage: {0:F0}%", coverage);
                coverageText.color = Color.white;
            }

            if (instructionText != null)
            {
                if (torch != null && torch.IsGrabbed)
                {
                    if (torch.IsWelding)
                    {
                        instructionText.text = "Welding... move along the seam";
                        instructionText.color = new Color(1f, 0.6f, 0.2f);
                    }
                    else
                    {
                        instructionText.text = "Aim at the orange seam line, hold Space";
                        instructionText.color = new Color(0.5f, 0.8f, 1f);
                    }
                }
                else
                {
                    instructionText.text = "Step 3: Grab the torch (E) and weld the seam!";
                    instructionText.color = new Color(0.8f, 0.8f, 0.2f);
                }
            }
        }
    }
}
