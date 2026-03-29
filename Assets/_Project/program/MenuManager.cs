using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public void OnPlayButtonPressed()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
    }
    
    public void OnExitButtonPressed()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
