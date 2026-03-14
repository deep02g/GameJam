using UnityEngine;

public class CameraFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Follow Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -7f);
    [SerializeField] private float positionSmoothTime = 0.15f;
    [SerializeField] private bool matchPlayerRotation = false;
    [SerializeField] private float rotationLerpSpeed = 8f;

    [Header("Physics Sync")]
    [SerializeField] private bool autoEnableRigidbodyInterpolation = true;

    private Rigidbody targetRigidbody;
    private Vector3 velocity;
    private Vector3 previousPhysicsPosition;
    private Vector3 currentPhysicsPosition;
    private Quaternion previousPhysicsRotation;
    private Quaternion currentPhysicsRotation;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        targetRigidbody = GetComponent<Rigidbody>();
        if (targetRigidbody != null && autoEnableRigidbodyInterpolation &&
            targetRigidbody.interpolation == RigidbodyInterpolation.None)
        {
            targetRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        Vector3 startPosition = targetRigidbody != null ? targetRigidbody.position : transform.position;
        Quaternion startRotation = targetRigidbody != null ? targetRigidbody.rotation : transform.rotation;
        previousPhysicsPosition = currentPhysicsPosition = startPosition;
        previousPhysicsRotation = currentPhysicsRotation = startRotation;
    }

    private void FixedUpdate()
    {
        previousPhysicsPosition = currentPhysicsPosition;
        previousPhysicsRotation = currentPhysicsRotation;

        if (targetRigidbody != null)
        {
            currentPhysicsPosition = targetRigidbody.position;
            currentPhysicsRotation = targetRigidbody.rotation;
            return;
        }

        currentPhysicsPosition = transform.position;
        currentPhysicsRotation = transform.rotation;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null) return;

        float interpolationFactor = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
        Vector3 targetCenter = Vector3.Lerp(previousPhysicsPosition, currentPhysicsPosition, interpolationFactor);
        Quaternion targetOrientation = Quaternion.Slerp(previousPhysicsRotation, currentPhysicsRotation, interpolationFactor);

        Vector3 targetPosition = targetCenter + (targetOrientation * offset);
        cameraTransform.position = Vector3.SmoothDamp(
            cameraTransform.position,
            targetPosition,
            ref velocity,
            positionSmoothTime);

        if (matchPlayerRotation)
        {
            Quaternion targetRotation = targetOrientation;
            cameraTransform.rotation = Quaternion.Slerp(
                cameraTransform.rotation,
                targetRotation,
                rotationLerpSpeed * Time.deltaTime);
            return;
        }

        cameraTransform.LookAt(targetCenter);
    }
}
