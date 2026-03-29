using UnityEngine;

public class CrosshairGUI : MonoBehaviour
{
    [Header("クロスヘア基本設定")]
    [SerializeField] private bool enableCrosshair = true;     // クロスヘア表示の有無

    [Header("クロスヘア線の設定")]
    [SerializeField] private float verticalLineLength = 30f;  // 上下の線の長さ
    [SerializeField] private float horizontalLineLength = 30f; // 左右の線の長さ
    [SerializeField] private float lineThickness = 1f;        // 線の太さ
    [SerializeField] private Color lineColor = Color.white;   // 線の色

    [Header("クロスヘアギャップ設定")]
    [SerializeField] private float verticalGap = 10f;         // 上下線の中央からのギャップ
    [SerializeField] private float horizontalGap = 10f;       // 左右線の中央からのギャップ

    [Header("中央点設定")]
    [SerializeField] private bool enableCenterPoint = true;   // 中央点の表示
    [SerializeField] private float centerPointSize = 4f;      // 中央点のサイズ
    [SerializeField] private Color centerPointColor = Color.white; // 中央点の色

    private void OnGUI()
    {
        if (enableCrosshair)
            DrawCrosshair();
    }

    private void DrawCrosshair()
    {
        // 画面中央座標
        float centerX = Screen.width / 2f;
        float centerY = Screen.height / 2f;

        // 縦線（上）
        GUI.color = lineColor;
        GUI.Box(new Rect(centerX - lineThickness / 2f, centerY - verticalLineLength - verticalGap, lineThickness, verticalLineLength), "");

        // 縦線（下）
        GUI.Box(new Rect(centerX - lineThickness / 2f, centerY + verticalGap, lineThickness, verticalLineLength), "");

        // 横線（左）
        GUI.Box(new Rect(centerX - horizontalLineLength - horizontalGap, centerY - lineThickness / 2f, horizontalLineLength, lineThickness), "");

        // 横線（右）
        GUI.Box(new Rect(centerX + horizontalGap, centerY - lineThickness / 2f, horizontalLineLength, lineThickness), "");

        // 中央点
        if (enableCenterPoint)
        {
            GUI.color = centerPointColor;
            GUI.Box(new Rect(centerX - centerPointSize / 2f, centerY - centerPointSize / 2f, centerPointSize, centerPointSize), "");
        }

        // GUIの色をリセット
        GUI.color = Color.white;
    }
}

