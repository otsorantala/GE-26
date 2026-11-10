using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MimicSpace
{
    /// <summary>
    /// This is a very basic movement script, if you want to replace it
    /// Just don't forget to update the Mimic's velocity vector with a Vector3(x, 0, z)
    /// </summary>
    public class Movement : MonoBehaviour
    {
        [Header("Controls")]
        [Tooltip("Body Height from ground")]
        [Range(0.5f, 5f)]
        public float height = 0.8f;
        public float speed = 5f;
        Vector3 velocity = Vector3.zero;
        public float velocityLerpCoef = 4f;
        Mimic myMimic;
        GameObject myPlayer;

        private void Start()
        {
            myMimic = GetComponent<Mimic>();
            myPlayer = GameObject.Find("MainPlayer");
        }

        void Update()
        {
            Vector3 toPlayer = myPlayer.transform.position - transform.position;
            toPlayer.y = 0f;
            Vector3 targetVelocity = toPlayer.sqrMagnitude > 0.0025f
                ? toPlayer.normalized * speed
                : Vector3.zero;
            velocity = Vector3.Lerp(velocity, targetVelocity, velocityLerpCoef * Time.deltaTime);

            // Assigning velocity to the mimic to assure great leg placement
            float distance = toPlayer.magnitude;
            float step = Mathf.Min(velocity.magnitude * Time.deltaTime, distance);
            Vector3 movement = distance > 0f ? toPlayer.normalized * step : Vector3.zero;
            myMimic.velocity = Time.deltaTime > 0f ? movement / Time.deltaTime : Vector3.zero;

            transform.position += movement;
            RaycastHit hit;
            Vector3 destHeight = transform.position;
            if (Mimic.RaycastIgnoringMimicColliders(transform.position + Vector3.up * 5f, -Vector3.up, out hit))
                destHeight = new Vector3(transform.position.x, hit.point.y + height, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, destHeight, velocityLerpCoef * Time.deltaTime);
        }
    }

}