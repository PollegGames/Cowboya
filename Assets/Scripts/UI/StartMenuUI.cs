using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class StartMenuUI : MonoBehaviour
{
    [SerializeField] private string sceneToLoad = "GameScene"; // Replace with your actual scene name
    [SerializeField] private Sprite backgroundSprite;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        // Background
        var background = new VisualElement
        {
            style =
            {
                backgroundImage = new StyleBackground(backgroundSprite),
                position = Position.Absolute,
                top = 0,
                bottom = 0,
                left = 0,
                right = 0,
                backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center),
                backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center),
                backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat),
                backgroundSize = new BackgroundSize(BackgroundSizeType.Cover)
            }
        };

        root.Add(background);

        // Play Button
        var playButton = new Button(() => SceneManager.LoadScene(sceneToLoad))
        {
            text = "Play",
            style =
            {
                width = 200,
                height = 50,
                alignSelf = Align.Center,
                unityFontStyleAndWeight = FontStyle.Bold,
                fontSize = 24,
                marginTop = new Length(60, LengthUnit.Percent),
                backgroundColor = new Color(0f, 0.6f, 0.8f, 0.8f),
                color = Color.white,
                borderTopLeftRadius = 10,
                borderTopRightRadius = 10,
                borderBottomLeftRadius = 10,
                borderBottomRightRadius = 10,
            }
        };

        root.Add(playButton);
    }
}
