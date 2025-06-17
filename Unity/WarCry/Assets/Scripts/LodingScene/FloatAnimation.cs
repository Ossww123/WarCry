using UnityEngine;

public class FloatAnimation : MonoBehaviour
{
    public float amplitude = 15f;
    public float speed = 2f;
    private Vector3 startPos;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        transform.localPosition = startPos + Vector3.up * Mathf.Sin(Time.time * speed) * amplitude;
    }
}
