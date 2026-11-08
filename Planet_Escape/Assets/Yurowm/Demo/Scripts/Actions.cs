using UnityEngine;
using System.Collections;

[RequireComponent (typeof (Animator))]
public class Actions : MonoBehaviour {

	private Animator animator;

	const int countOfDamageAnimations = 3;
	int lastDamageAnimation = -1;

	void Awake () {
		animator = GetComponent<Animator> ();
	}

	// Test

	void Update () {
		float horizontal = Input.GetAxisRaw ("Horizontal");
		float vertical = Input.GetAxisRaw ("Vertical");
		bool isMoving = horizontal != 0f || vertical != 0f;
		bool isJumping = animator.GetCurrentAnimatorStateInfo (0).IsName ("Jump") ||
			(animator.IsInTransition (0) && animator.GetNextAnimatorStateInfo (0).IsName ("Jump"));

		if (!isJumping && isMoving) {
			float targetSpeed = Input.GetKey (KeyCode.LeftShift) || Input.GetKey (KeyCode.RightShift) ? 1f : 0.5f;
			if (animator.GetFloat ("Speed") != targetSpeed || animator.GetBool ("Aiming")) {
				if (targetSpeed == 1f)
					Run ();
				else
					Walk ();
			}
		} else if (!isJumping && !isMoving && animator.GetFloat ("Speed") > 0f) {
			Stay ();
		}

		if (Input.GetKeyDown (KeyCode.Space))
			Jump ();
		if (Input.GetKeyDown (KeyCode.C))
			Sitting ();
		if (Input.GetKeyDown (KeyCode.Q)) {
			if (animator.GetBool ("Aiming"))
				Stay ();
			else
				Aiming ();
		}
		if (Input.GetKeyDown (KeyCode.E))
			Attack ();
		if (Input.GetKeyDown (KeyCode.F))
			Damage ();
		if (Input.GetKeyDown (KeyCode.R))
			Death ();
	}

	public void Stay () {
		animator.SetBool("Aiming", false);
		animator.SetFloat ("Speed", 0f);
		}

	public void Walk () {
		animator.SetBool("Aiming", false);
		animator.SetFloat ("Speed", 0.5f);
	}

	public void Run () {
		animator.SetBool("Aiming", false);
		animator.SetFloat ("Speed", 1f);
	}

	public void Attack () {
		Aiming ();
		animator.SetTrigger ("Attack");
	}

	public void Death () {
		if (animator.GetCurrentAnimatorStateInfo (0).IsName ("Death"))
			animator.Play("Idle", 0);
		else
			animator.SetTrigger ("Death");
	}

	public void Damage () {
		if (animator.GetCurrentAnimatorStateInfo (0).IsName ("Death")) return;
		int id = Random.Range(0, countOfDamageAnimations);
		if (countOfDamageAnimations > 1)
			while (id == lastDamageAnimation)
				id = Random.Range(0, countOfDamageAnimations);
		lastDamageAnimation = id;
		animator.SetInteger ("DamageID", id);
		animator.SetTrigger ("Damage");
	}

	public void Jump () {
		animator.SetBool ("Squat", false);
		animator.SetFloat ("Speed", 0f);
		animator.SetBool("Aiming", false);
		animator.Play ("Jump", 0, 0f);
	}

	public void Aiming () {
		animator.SetBool ("Squat", false);
		animator.SetFloat ("Speed", 0f);
		animator.SetBool("Aiming", true);
	}

	public void Sitting () {
		animator.SetBool ("Squat", !animator.GetBool("Squat"));
		animator.SetBool("Aiming", false);
	}
}
