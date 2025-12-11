using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [SerializeField] Animator transitionAnimator;

    public static SceneLoader Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (transitionAnimator == null)
        {
            transitionAnimator = GetComponentInChildren<Animator>();
        }
    }

    public void LoadSceneByName(string sceneName)
    {
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        if (transitionAnimator != null)
        {
            transitionAnimator.SetTrigger("ZoomIn");
            yield return new WaitForSeconds(1f);
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone)
            yield return null;

        ResolveAnimatorForActiveScene();

        if (transitionAnimator != null)
        {
            transitionAnimator.SetTrigger("ZoomOut");
            yield return new WaitForSeconds(1f);
        }
    }

    private void ResolveAnimatorForActiveScene()
    {
        Scene active = SceneManager.GetActiveScene();

        if (transitionAnimator != null && transitionAnimator.gameObject.scene == active)
            return;

        // Try tag-based lookup (create/tag your transition canvas with "TransitionCanvas" in the scene)
        GameObject tagged = GameObject.FindWithTag("TransitionCanvas");
        if (tagged != null)
        {
            var anim = tagged.GetComponent<Animator>();
            if (anim != null)
            {
                transitionAnimator = anim;
                return;
            }
        }
        var all = Resources.FindObjectsOfTypeAll<Animator>();
        foreach (var a in all)
        {
            if (a.gameObject.scene == active)
            {
                transitionAnimator = a;
                return;
            }
        }
    }
}
