using UnityEngine;
using UnityEngine.SceneManagement;

public class FactorySetupLoader : MonoBehaviour
{
    public void LoadFactorySetup()
    {
        SceneManager.LoadScene("RunSetupScene");
    }
}
