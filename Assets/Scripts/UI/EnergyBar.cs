using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class EnergyBar : VisualElement
{
    // Custom style properties
    static CustomStyleProperty<Color> s_FillColor = new CustomStyleProperty<Color>("--fill-color");
    static CustomStyleProperty<Color> s_BackgroundColor = new CustomStyleProperty<Color>("--background-color");

    private Color m_FillColor = Color.blue;
    private Color m_BackgroundColor = Color.black;

    // Current and Maximum Energy
    [SerializeField, DontCreateProperty]
    private float m_CurrentEnergy;
    [SerializeField, DontCreateProperty]
    private float m_MaxEnergy = 100f;

    [UxmlAttribute, CreateProperty]
    public float currentEnergy
    {
        get => m_CurrentEnergy;
        set
        {
            m_CurrentEnergy = Mathf.Clamp(value, 0.0f, m_MaxEnergy);
            UpdateProgress();
        }
    }

    [UxmlAttribute, CreateProperty]
    public float maxEnergy
    {
        get => m_MaxEnergy;
        set
        {
            if (value > 0)
            {
                m_MaxEnergy = value;
                UpdateProgress();
            }
        }
    }

    // Progress (0 to 1) is now read-only and calculated based on energy
    public float progress => m_MaxEnergy > 0 ? m_CurrentEnergy / m_MaxEnergy : 0f;

    public EnergyBar()
    {
        // Register a callback after custom style resolution.
        RegisterCallback<CustomStyleResolvedEvent>(CustomStylesResolved);

        // Register a callback to generate the visual content of the control.
        generateVisualContent += OnGenerateVisualContent;
    }

    private void OnGenerateVisualContent(MeshGenerationContext context)
    {
        var painter = context.painter2D;

        // Get the dimensions of the current element
        Rect rect = contentRect;

        // Draw the background rectangle
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.xMin, rect.yMin)); // Top-left corner
        painter.LineTo(new Vector2(rect.xMax, rect.yMin)); // Top-right corner
        painter.LineTo(new Vector2(rect.xMax, rect.yMax)); // Bottom-right corner
        painter.LineTo(new Vector2(rect.xMin, rect.yMax)); // Bottom-left corner
        painter.ClosePath();
        painter.fillColor = m_BackgroundColor;
        painter.Fill();

        // Debug.Log("Energy progress : " + progress);

        // Draw the filled portion based on progress
        float fillWidth = rect.width * progress;
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.xMin, rect.yMin)); // Top-left corner
        painter.LineTo(new Vector2(rect.xMin + fillWidth, rect.yMin)); // Top-right corner (scaled by progress)
        painter.LineTo(new Vector2(rect.xMin + fillWidth, rect.yMax)); // Bottom-right corner (scaled by progress)
        painter.LineTo(new Vector2(rect.xMin, rect.yMax)); // Bottom-left corner
        painter.ClosePath();
        painter.fillColor = m_FillColor;
        painter.Fill();
    }

    private void CustomStylesResolved(CustomStyleResolvedEvent evt)
    {
        if (evt.currentTarget == this)
        {
            EnergyBar element = (EnergyBar)evt.currentTarget;
            element.UpdateCustomStyles();
        }
    }

    private void UpdateCustomStyles()
    {
        bool repaint = false;
        if (customStyle.TryGetValue(s_FillColor, out m_FillColor))
            repaint = true;
        if (customStyle.TryGetValue(s_BackgroundColor, out m_BackgroundColor))
            repaint = true;
        if (repaint)
            MarkDirtyRepaint();
    }

    private void UpdateProgress()
    {
        // Request a redraw whenever energy changes
        MarkDirtyRepaint();
    }
}
