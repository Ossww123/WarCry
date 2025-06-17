using UnityEngine;

namespace Army
{
    public class CubeMovement : MonoBehaviour
    {
        public float moveSpeed = 5f;

        private Rigidbody rb;

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            
            // 물리 효과는 유지하면서 회전은 고정
            if (rb != null)
            {
                rb.freezeRotation = true;
            }
        }

        private void Update()
        {
            MoveWithWASD();
        }

        private void MoveWithWASD()
        {
            float horizontalInput = Input.GetAxis("Horizontal");
            float verticalInput = Input.GetAxis("Vertical");

            Vector3 movement = new Vector3(horizontalInput, 0, verticalInput) * moveSpeed * Time.deltaTime;
            transform.Translate(movement, Space.World);
        }
    }
}
