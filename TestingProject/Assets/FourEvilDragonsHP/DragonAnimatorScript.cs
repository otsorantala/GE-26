using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DragonAnimatorScript : MonoBehaviour

{
    private Animator animator;
    public Animation dead;
    public GameObject dragon;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator = GetComponent<Animator>();
        dragon = GameObject.FindWithTag("dragon");
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.O))
        {

        }

    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            animator.SetTrigger("Scream");
        }
    }

    //Called after die animation
    public void dede()
    {
        Destroy(dragon);
        SceneManager.LoadSceneAsync("BloodScene", LoadSceneMode.Additive);
    }

}
