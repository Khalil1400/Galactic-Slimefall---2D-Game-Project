using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Transitions")]
    public float fadeTime = 0.35f;
    public string mainMenuScene = "MainMenu";
    public CanvasGroup fadeOverlay;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void RestartLevel()
    {
        StartCoroutine(LoadScene(SceneManager.GetActiveScene().name));
    }

    public void LoadNextLevel()
    {
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextIndex < 0 || nextIndex >= SceneManager.sceneCountInBuildSettings)
        {
            LoadMainMenu();
            return;
        }

        string nextScenePath = SceneUtility.GetScenePathByBuildIndex(nextIndex);
        if (string.IsNullOrWhiteSpace(nextScenePath))
        {
            LoadMainMenu();
            return;
        }

        StartCoroutine(LoadScene(Path.GetFileNameWithoutExtension(nextScenePath)));
    }

    public void LoadMainMenu()
    {
        if (string.IsNullOrWhiteSpace(mainMenuScene))
        {
            RestartLevel();
            return;
        }

        StartCoroutine(LoadScene(mainMenuScene));
    }

    IEnumerator LoadScene(string sceneName)
    {
        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            float elapsed = 0f;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                fadeOverlay.alpha = Mathf.Clamp01(elapsed / fadeTime);
                yield return null;
            }
        }

        SceneManager.LoadScene(sceneName);
    }
}

