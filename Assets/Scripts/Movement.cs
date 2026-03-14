using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FlyerMovement : MonoBehaviour
{
    [Header("Forward Movement")]
    [SerializeField] private float forwardForce = 30f;
    [SerializeField] private float maxForwardSpeed = 20f;

    [Header("General Physics")]
    [SerializeField] private float drag = 1f;
    [SerializeField] private float angularDrag = 2f;

    [Header("Optional Lift")]
    [SerializeField] private bool applyLift = false;
    [SerializeField] private float liftForce = 5f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearDamping = drag;
        rb.angularDamping = angularDrag;
    }

    private void FixedUpdate()
    {
        MoveForward();
        ApplyLift();
        LimitForwardSpeed();
    }

    private void MoveForward()
    {
        rb.AddForce(transform.forward * forwardForce, ForceMode.Force);
    }

    private void ApplyLift()
    {
        if (!applyLift) return;

        rb.AddForce(transform.up * liftForce, ForceMode.Force);
    }

    private void LimitForwardSpeed()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        if (localVelocity.z > maxForwardSpeed)
        {
            localVelocity.z = maxForwardSpeed;
            rb.linearVelocity = transform.TransformDirection(localVelocity);
        }
    }
}