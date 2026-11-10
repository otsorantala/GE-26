using UnityEngine;
using System.Collections;

public class ShootableBox : MonoBehaviour {

    //The box's current health point total
    public int currentHealth = 3;
    public float shakeDuration = 0.15f;
    public float shakeMagnitude = 0.1f;
    private Transform shakeTarget;
    private Vector3 shakeStartPosition;
    private Coroutine shakeCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"{gameObject.name} contacted by {other.gameObject.name}");
        Damage(1, other);
    }

    public void Damage(int damageAmount)
    {
        currentHealth -= damageAmount;
        StartShake();

        if (currentHealth <= 0)
        {
            StopShake();
            gameObject.SetActive(false);
        }
    }

    public void Damage(int damageAmount, Collider other)
    {
        Damage(damageAmount);
    }

    public void Damage(int damageAmount, Vector3 knockbackDirection)
    {
        Damage(damageAmount);
    }

    private void StartShake()
    {
        StopShake();
        shakeTarget = transform.parent != null ? transform.parent : transform;
        shakeStartPosition = shakeTarget.localPosition;
        shakeCoroutine = StartCoroutine(Shake());
    }

    private IEnumerator Shake()
    {
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            shakeTarget.localPosition = shakeStartPosition + Random.insideUnitSphere * shakeMagnitude;
            elapsed += Time.deltaTime;
            yield return null;
        }

        shakeTarget.localPosition = shakeStartPosition;
        shakeCoroutine = null;
        shakeTarget = null;
    }

    private void StopShake()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }

        if (shakeTarget != null)
            shakeTarget.localPosition = shakeStartPosition;

        shakeTarget = null;
    }
}