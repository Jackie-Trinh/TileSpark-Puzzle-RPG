using UnityEngine;
using UnityEngine.SceneManagement;

public class BootLoader : MonoBehaviour
{
    private void Start()
    {
        SceneManager.LoadScene(GameManager.SCENE_MAP);
    }
}
