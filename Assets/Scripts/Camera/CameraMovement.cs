using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    private CharacterController cc;
    [SerializeField]
    private float speed = 10f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cc = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 movement = Vector3.zero;
        movement += transform.right * Input.GetAxis("Horizontal");
        movement += transform.forward * Input.GetAxis("Vertical");
        cc.Move(movement*Time.deltaTime*speed);

    }
}
