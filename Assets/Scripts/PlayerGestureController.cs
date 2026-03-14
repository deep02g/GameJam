using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerGestureController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SwipeInputManager swipeInputManager;
    [SerializeField] private Transform visualTiltTarget;

    [Header("Heading Turn")]
    [SerializeField] private float turnSpeedDegreesPerSecond = 140f;
    [SerializeField, Range(0f, 180f)] private float maxYawAngle = 90f;

    [Header("Pitch Control")]
    [SerializeField] private float pitchSpeedDegreesPerSecond = 80f;
    [SerializeField, Range(0f, 80f)] private float maxPitchAngle = 0f;
    [SerializeField] private float pitchLiftAssist = 10f;

    [Header("Turn Assist")]
    [SerializeField] private float velocityAlignStrength = 6f;
    [SerializeField] private float sideSlipDamping = 8f;
    [SerializeField] private float minAssistSpeed = 1f;
    [SerializeField, Range(0f, 2f)] private float trajectoryTurnStrength = 1f;
    [SerializeField, Range(0f, 2f)] private float trajectoryPitchStrength = 1f;

    [Header("Visual Banking")]
    [SerializeField, Range(0f, 90f)] private float maxBankAngle = 40f;
    [SerializeField] private float bankInSpeed = 180f;
    [SerializeField] private float bankOutSpeed = 140f;

    [Header("Single Swipe Nudge")]
    [SerializeField] private bool enableSingleSwipeNudge = true;
    [SerializeField] private float swipeYawNudgeAngle = 10f;
    [SerializeField] private float swipePitchNudgeAngle = 8f;
    [SerializeField] private float swipeVerticalSpeedNudge = 1.5f;

    private Rigidbody rb;
    private Quaternion initialTiltLocalRotation;
    private float currentBankAngle;
    private float currentTurnInput;
    private float yawCenterAngle;
    private float currentPitchOffset;
    private float pendingYawNudge;
    private float pendingPitchNudge;
    private float pendingVerticalSpeedNudge;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        yawCenterAngle = rb.rotation.eulerAngles.y;

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

    private void OnEnable()
    {
        if (swipeInputManager != null)
        {
            swipeInputManager.SingleSwipeDetected += HandleSingleSwipeDetected;
        }
    }

    private void OnDisable()
    {
        if (swipeInputManager != null)
        {
            swipeInputManager.SingleSwipeDetected -= HandleSingleSwipeDetected;
        }
    }

    private void OnValidate()
    {
        turnSpeedDegreesPerSecond = Mathf.Max(0f, turnSpeedDegreesPerSecond);
        maxYawAngle = Mathf.Clamp(maxYawAngle, 0f, 180f);
        pitchSpeedDegreesPerSecond = Mathf.Max(0f, pitchSpeedDegreesPerSecond);
        maxPitchAngle = Mathf.Clamp(maxPitchAngle, 0f, 80f);
        pitchLiftAssist = Mathf.Max(0f, pitchLiftAssist);
        velocityAlignStrength = Mathf.Max(0f, velocityAlignStrength);
        sideSlipDamping = Mathf.Max(0f, sideSlipDamping);
        minAssistSpeed = Mathf.Max(0f, minAssistSpeed);
        trajectoryTurnStrength = Mathf.Clamp(trajectoryTurnStrength, 0f, 2f);
        trajectoryPitchStrength = Mathf.Clamp(trajectoryPitchStrength, 0f, 2f);
        maxBankAngle = Mathf.Clamp(maxBankAngle, 0f, 90f);
        bankInSpeed = Mathf.Max(0f, bankInSpeed);
        bankOutSpeed = Mathf.Max(0f, bankOutSpeed);
        swipeYawNudgeAngle = Mathf.Max(0f, swipeYawNudgeAngle);
        swipePitchNudgeAngle = Mathf.Max(0f, swipePitchNudgeAngle);
        swipeVerticalSpeedNudge = Mathf.Max(0f, swipeVerticalSpeedNudge);
    }

    private void FixedUpdate()
    {
        if (swipeInputManager == null) return;

        float turnInput = 0f;
        if (swipeInputManager.IsTurnLeftHeld) turnInput -= 1f;
        if (swipeInputManager.IsTurnRightHeld) turnInput += 1f;

        float pitchInput = 0f;
        if (swipeInputManager.IsPitchUpHeld) pitchInput += 1f;
        if (swipeInputManager.IsPitchDownHeld) pitchInput -= 1f;

        currentTurnInput = turnInput;

        float requestedYawDelta = turnInput * turnSpeedDegreesPerSecond * Time.fixedDeltaTime + pendingYawNudge;
        float requestedPitchDelta = -pitchInput * pitchSpeedDegreesPerSecond * Time.fixedDeltaTime + pendingPitchNudge;
        float verticalNudge = pendingVerticalSpeedNudge;
        pendingYawNudge = 0f;
        pendingPitchNudge = 0f;
        pendingVerticalSpeedNudge = 0f;

        float appliedYawDelta = requestedYawDelta;
        if (maxYawAngle > 0.01f)
        {
            float currentYawOffset = Mathf.DeltaAngle(yawCenterAngle, rb.rotation.eulerAngles.y);
            float nextYawOffset = Mathf.Clamp(currentYawOffset + requestedYawDelta, -maxYawAngle, maxYawAngle);
            appliedYawDelta = nextYawOffset - currentYawOffset;
        }

        float appliedPitchDelta = requestedPitchDelta;
        if (maxPitchAngle > 0.01f)
        {
            float nextPitchOffset = Mathf.Clamp(currentPitchOffset + requestedPitchDelta, -maxPitchAngle, maxPitchAngle);
            appliedPitchDelta = nextPitchOffset - currentPitchOffset;
            currentPitchOffset = nextPitchOffset;
        }
        else
        {
            currentPitchOffset += requestedPitchDelta;
        }

        if (!Mathf.Approximately(appliedYawDelta, 0f) || !Mathf.Approximately(appliedPitchDelta, 0f))
        {
            Quaternion nextRotation = rb.rotation;
            if (!Mathf.Approximately(appliedYawDelta, 0f))
            {
                nextRotation *= Quaternion.Euler(0f, appliedYawDelta, 0f);
            }

            if (!Mathf.Approximately(appliedPitchDelta, 0f))
            {
                nextRotation *= Quaternion.Euler(appliedPitchDelta, 0f, 0f);
            }

            rb.MoveRotation(nextRotation);
        }

        ApplyTurnAssist(appliedYawDelta, appliedPitchDelta, pitchInput, verticalNudge);
    }

    private void LateUpdate()
    {
        if (visualTiltTarget == null) return;

        float targetBank = -currentTurnInput * maxBankAngle;
        float speed = Mathf.Abs(targetBank) > Mathf.Abs(currentBankAngle) ? bankInSpeed : bankOutSpeed;
        currentBankAngle = Mathf.MoveTowards(currentBankAngle, targetBank, speed * Time.deltaTime);

        Quaternion bankRotation = Quaternion.Euler(currentBankAngle, 0f, 0f);
        visualTiltTarget.localRotation = initialTiltLocalRotation * bankRotation;
    }

    private void ApplyTurnAssist(float appliedYawDelta, float appliedPitchDelta, float pitchInput, float verticalSpeedNudge)
    {
        Vector3 velocity = rb.linearVelocity;

        if (!Mathf.Approximately(appliedYawDelta, 0f) && trajectoryTurnStrength > 0f)
        {
            float velocityYawDelta = appliedYawDelta * trajectoryTurnStrength;
            velocity = Quaternion.Euler(0f, velocityYawDelta, 0f) * velocity;
        }

        if (!Mathf.Approximately(appliedPitchDelta, 0f) && trajectoryPitchStrength > 0f)
        {
            float velocityPitchDelta = appliedPitchDelta * trajectoryPitchStrength;
            velocity = Quaternion.AngleAxis(velocityPitchDelta, transform.right) * velocity;
        }

        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        float speed = planarVelocity.magnitude;
        if (speed < minAssistSpeed)
        {
            rb.linearVelocity = velocity;
            return;
        }

        Vector3 targetDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (targetDirection.sqrMagnitude < 0.0001f) return;

        float alignStep = Mathf.Clamp01(velocityAlignStrength * Time.fixedDeltaTime);
        Vector3 alignedPlanarVelocity = Vector3.Slerp(planarVelocity, targetDirection * speed, alignStep);

        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        if (right.sqrMagnitude > 0.0001f)
        {
            float lateralSpeed = Vector3.Dot(alignedPlanarVelocity, right);
            float dampingStep = Mathf.Clamp01(sideSlipDamping * Time.fixedDeltaTime);
            alignedPlanarVelocity -= right * (lateralSpeed * dampingStep);
        }

        float verticalAssist = pitchInput * pitchLiftAssist * Time.fixedDeltaTime;
        float nextVerticalSpeed = velocity.y + verticalAssist + verticalSpeedNudge;

        rb.linearVelocity = alignedPlanarVelocity + Vector3.up * nextVerticalSpeed;
    }

    private void HandleSingleSwipeDetected(SwipeDirection direction, SwipeScreenSection section)
    {
        if (!enableSingleSwipeNudge) return;

        switch (direction)
        {
            case SwipeDirection.Left:
                pendingYawNudge -= swipeYawNudgeAngle;
                break;

            case SwipeDirection.Right:
                pendingYawNudge += swipeYawNudgeAngle;
                break;

            case SwipeDirection.Up:
                pendingPitchNudge -= swipePitchNudgeAngle;
                pendingVerticalSpeedNudge += swipeVerticalSpeedNudge;
                break;

            case SwipeDirection.Down:
                pendingPitchNudge += swipePitchNudgeAngle;
                pendingVerticalSpeedNudge -= swipeVerticalSpeedNudge;
                break;
        }
    }
}
