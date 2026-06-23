using System.Collections;
using UnityEngine;

public class DigController : MonoBehaviour
{
    Animator anim;
    public bool isDigging = false;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            if (!isDigging)
            {
                isDigging = true;
                Debug.Log("Started digging!");
                anim.SetBool("Dig", isDigging);

            }
            else
            {
                Debug.Log("Stopped digging!");
                anim.SetBool("Dig", false);
                StartCoroutine(StopDigging());
            }
        }
    }

    IEnumerator StopDigging()
    {
        
            yield return new WaitForSeconds(3f); // Simulate digging time
                isDigging = false;

    }
}