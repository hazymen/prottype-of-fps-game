using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    private void Update()
    {
        // M キーを押すとメニューに戻る
        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            OnReturnToMenuPressed();
        }
    }

    public void OnReturnToMenuPressed()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SceneManager.LoadScene("MenuScene", LoadSceneMode.Single);
    }
}