using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NBAManager
{
    // ── Scene names ───────────────────────────────────────────────────────────
    // Match these exactly to your scene file names in Assets/Scenes/

    public static class SceneNames
    {
        public const string Main = "Main";
        public const string Menu = "Menu";
        public const string Hub  = "Hub";
        public const string Game = "Game";
    }

    // ── Scene loader ──────────────────────────────────────────────────────────
    // Manages additive scene loading/unloading.
    // Lives on a GameObject in the Main scene — never destroyed.

    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        // Currently loaded content scene (Menu, Hub, or Game)
        private string _activeContentScene;

        // Loading curtain — assign a simple black fullscreen Image in the Inspector
        // to fade between scene transitions. Optional but recommended.
        [Header("Transition")]
        public CanvasGroup fadeCurtain;
        public float       fadeDuration = 0.25f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Main scene is already loaded — boot into Menu
            LoadMenuScene();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void LoadMenuScene() =>
            StartCoroutine(TransitionTo(SceneNames.Menu));

        public void LoadHubScene() =>
            StartCoroutine(TransitionTo(SceneNames.Hub));

        public void LoadGameScene() =>
            StartCoroutine(TransitionTo(SceneNames.Game));

        // ── Transition coroutine ──────────────────────────────────────────────

        private IEnumerator TransitionTo(string sceneName)
        {
            // Fade out
            yield return StartCoroutine(Fade(1f));

            // Unload current content scene
            if (!string.IsNullOrEmpty(_activeContentScene))
            {
                if (SceneManager.GetSceneByName(_activeContentScene).isLoaded)
                    yield return SceneManager.UnloadSceneAsync(_activeContentScene);
            }

            // Load new content scene additively
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            // Set it as active so new GameObjects instantiate into it
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
            _activeContentScene = sceneName;

            // Fade in
            yield return StartCoroutine(Fade(0f));
        }

        // ── Fade helper ───────────────────────────────────────────────────────

        private IEnumerator Fade(float targetAlpha)
        {
            if (fadeCurtain == null) yield break;

            float startAlpha = fadeCurtain.alpha;
            float elapsed    = 0f;

            fadeCurtain.gameObject.SetActive(true);
            fadeCurtain.blocksRaycasts = true;

            while (elapsed < fadeDuration)
            {
                elapsed              += Time.deltaTime;
                fadeCurtain.alpha     = Mathf.Lerp(startAlpha, targetAlpha,
                                            elapsed / fadeDuration);
                yield return null;
            }

            fadeCurtain.alpha = targetAlpha;

            if (targetAlpha == 0f)
            {
                fadeCurtain.blocksRaycasts = false;
                fadeCurtain.gameObject.SetActive(false);
            }
        }
    }
}