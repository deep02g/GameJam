using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerGestureController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SwipeInputManager swipeInputManager;
    [SerializeField] private Transform visualTiltTarget;

    [Header("Heading Turn")]
    [SerializeField] private float turnSpeedDegreesPerSecond = 140f;

    [Header("Visual Banking")]
    [SerializeField] private bool enableBanking = true;
    [SerializeField, Range(0f, 60f)] private float maxBankAngle = 22f;
    [SerializeField] private float bankInSpeed = 180f;
    [SerializeField] private float bankOutSpeed = 140f;
    [SerializeField] private bool invertBankDirection = false;

    private Rigidbody rb;
    private Quaternion initialTiltLocalRotation;
    private float currentBankAngle;
    private float currentTurnInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (swipeInputManager == null)
        {
            swipeInputManager = FindFirstObjectByType<SwipeInputManager>();
        }

        if (visualTiltTarget == null && transform.childCount > 0)
        {
            visualTiltTarget = transform.GetChild(0);
        }

        if (visualTiltTarget != null)
        {
            initialTiltLocalRotation = visualTiltTarget.localRotation;
        }
    }

    private void FixedUpdate()
    {
        if (swipeInputManager == null) return;

        float turnInput = 0f;
        if (swipeInputManager.IsTurnLeftHeld) turnInput -= 1f;
        if (swipeInputManager.IsTurnRightHeld) turnInput += 1f;
        currentTurnInput = turnInput;
        if (Mathf.Approximately(turnInput, 0f)) return;

        float yawDelta = turnInput * turnSpeedDegreesPerSecond * Time.fixedDeltaTime;
        Quaternion nextRotation = rb.rotation * Quaternion.Euler(0f, yawDelta, 0f);
        rb.MoveRotation(nextRotation);
    }

    private void LateUpdate()
    {
        if (!enableBanking || visualTiltTarget == null) return;

        float bankDirection = invertBankDirection ? 1f : -1f;
        float targetBank = currentTurnInput * maxBankAngle * bankDirection;
        float speed = Mathf.Abs(targetBank) > Mathf.Abs(currentBankAngle) ? bankInSpeed : bankOutSpeed;
        currentBankAngle = Mathf.MoveTowards(currentBankAngle, targetBank, speed * Time.deltaTime);

        Quaternion bankRotation = Quaternion.Euler(0f, 0f, currentBankAngle);
        visualTiltTarget.localRotation = initialTiltLocalRotation * bankRotation;
    }
}
