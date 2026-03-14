using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

public enum SwipeDirection
{
    Up,
    Down,
    Left,
    Right
}

[Serializable]
public enum SwipeScreenSection
{
    Any,
    Left,
    Right
}

[Serializable]
public enum GesturePreset
{
    None,
    LandscapeSideCombos
}

[Serializable]
public class SwipeStep
{
    [Tooltip("Which section of the screen must receive this swipe.")]
    public SwipeScreenSection section = SwipeScreenSection.Any;

    [Tooltip("Required swipe direction for this step.")]
    public SwipeDirection direction = SwipeDirection.Up;
}

[Serializable]
public class SwipeGesture
{
    [Tooltip("Name shown in logs when the gesture is recognized.")]
    public string gestureName = "New Gesture";

    [Tooltip("Swipe sequence to match. Each step can require Left/Right screen section.")]
    public List<SwipeStep> sequence = new List<SwipeStep>();

    [Tooltip("Optional callback invoked when this gesture is recognized.")]
    public UnityEvent onRecognized;
}

public class SwipeInputManager : MonoBehaviour
{
    [Header("Swipe Detection")]
    [SerializeField] private float minSwipeDistance = 120f;
    [SerializeField] private float maxSwipeDuration = 0.75f;
    [SerializeField] private float maxGapBetweenSwipes = 0.8f;
    [SerializeField, Range(0.2f, 0.8f)] private float leftSectionWidthPercent = 0.5f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool enableMouseSimulation = true;
    [SerializeField] private bool debugLogs = true;
    [SerializeField, Min(1)] private int minimumGestureLength = 2;

    [Header("Preset")]
    [Tooltip("Set this to a preset to auto-fill gesture list. It resets back to None after applying.")]
    [SerializeField] private GesturePreset presetToApply = GesturePreset.None;

    [Header("Gesture List")]
    [SerializeField] private List<SwipeGesture> gestures = new List<SwipeGesture>();

    private readonly Dictionary<int, SwipeStart> activeTouchStarts = new Dictionary<int, SwipeStart>();
    private readonly List<SwipeSample> swipeBuffer = new List<SwipeSample>();

    private SwipeStart mouseSwipeStart;
    private bool isMouseTracking;
    private float lastSwipeTime = float.NegativeInfinity;
    private int maxGestureLength = 1;

    private struct SwipeStart
    {
        public Vector2 Position;
        public float Time;
        public bool Consumed;
    }

    private struct SwipeSample
    {
        public SwipeScreenSection Section;
        public SwipeDirection Direction;
    }

    private void Awake()
    {
        RecalculateMaxGestureLength();
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        EnhancedTouchSupport.Enable();
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        EnhancedTouchSupport.Disable();
#endif
    }

    private void Reset()
    {
        if (gestures == null || gestures.Count == 0)
        {
            ApplyPreset(GesturePreset.LandscapeSideCombos);
        }
    }

    private void OnValidate()
    {
        minSwipeDistance = Mathf.Max(1f, minSwipeDistance);
        maxSwipeDuration = Mathf.Max(0.01f, maxSwipeDuration);
        maxGapBetweenSwipes = Mathf.Max(0.01f, maxGapBetweenSwipes);
        leftSectionWidthPercent = Mathf.Clamp(leftSectionWidthPercent, 0.2f, 0.8f);
        minimumGestureLength = Mathf.Max(1, minimumGestureLength);

        if (presetToApply != GesturePreset.None)
        {
            ApplyPreset(presetToApply);
            presetToApply = GesturePreset.None;
        }

        RecalculateMaxGestureLength();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        HandleTouchInputInputSystem();
        HandleMouseInputInputSystem();
#elif ENABLE_LEGACY_INPUT_MANAGER
        HandleTouchInputLegacy();
        HandleMouseInputLegacy();
#endif
    }

    private void HandleTouchInputLegacy()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            UnityEngine.Touch touch = Input.GetTouch(i);

            switch (touch.phase)
            {
                case UnityEngine.TouchPhase.Began:
                    activeTouchStarts[touch.fingerId] = new SwipeStart
                    {
                        Position = touch.position,
                        Time = Now(),
                        Consumed = false
                    };
                    break;

                case UnityEngine.TouchPhase.Moved:
                case UnityEngine.TouchPhase.Stationary:
                    TryConsumeLiveSwipe(touch.fingerId, touch.position, Now());
                    break;

                case UnityEngine.TouchPhase.Ended:
                case UnityEngine.TouchPhase.Canceled:
                    if (activeTouchStarts.TryGetValue(touch.fingerId, out SwipeStart start))
                    {
                        if (!start.Consumed)
                        {
                            ProcessSwipe(start, touch.position, Now());
                        }

                        activeTouchStarts.Remove(touch.fingerId);
                    }
                    break;
            }
        }
    }

    private void HandleMouseInputLegacy()
    {
        if (!enableMouseSimulation) return;

        if (Input.GetMouseButtonDown(0))
        {
            isMouseTracking = true;
            mouseSwipeStart = new SwipeStart
            {
                Position = Input.mousePosition,
                Time = Now(),
                Consumed = false
            };
        }

        if (isMouseTracking && Input.GetMouseButton(0))
        {
            TryConsumeLiveMouseSwipe(Input.mousePosition, Now());
        }

        if (isMouseTracking && Input.GetMouseButtonUp(0))
        {
            isMouseTracking = false;

            if (!mouseSwipeStart.Consumed)
            {
                ProcessSwipe(mouseSwipeStart, Input.mousePosition, Now());
            }
        }
    }

#if ENABLE_INPUT_SYSTEM
    private void HandleTouchInputInputSystem()
    {
        var touches = EnhancedTouch.activeTouches;
        for (int i = 0; i < touches.Count; i++)
        {
            EnhancedTouch touch = touches[i];

            switch (touch.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    activeTouchStarts[touch.touchId] = new SwipeStart
                    {
                        Position = touch.screenPosition,
                        Time = Now(),
                        Consumed = false
                    };
                    break;

                case UnityEngine.InputSystem.TouchPhase.Moved:
                case UnityEngine.InputSystem.TouchPhase.Stationary:
                    TryConsumeLiveSwipe(touch.touchId, touch.screenPosition, Now());
                    break;

                case UnityEngine.InputSystem.TouchPhase.Ended:
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    if (activeTouchStarts.TryGetValue(touch.touchId, out SwipeStart start))
                    {
                        if (!start.Consumed)
                        {
                            ProcessSwipe(start, touch.screenPosition, Now());
                        }

                        activeTouchStarts.Remove(touch.touchId);
                    }
                    break;
            }
        }
    }

    private void HandleMouseInputInputSystem()
    {
        if (!enableMouseSimulation) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            isMouseTracking = true;
            mouseSwipeStart = new SwipeStart
            {
                Position = mouse.position.ReadValue(),
                Time = Now(),
                Consumed = false
            };
        }

        if (isMouseTracking && mouse.leftButton.isPressed)
        {
            TryConsumeLiveMouseSwipe(mouse.position.ReadValue(), Now());
        }

        if (isMouseTracking && mouse.leftButton.wasReleasedThisFrame)
        {
            isMouseTracking = false;

            if (!mouseSwipeStart.Consumed)
            {
                ProcessSwipe(mouseSwipeStart, mouse.position.ReadValue(), Now());
            }
        }
    }
#endif

    private void ProcessSwipe(SwipeStart start, Vector2 endPosition, float endTime)
    {
        float duration = endTime - start.Time;
        if (duration > maxSwipeDuration) return;

        Vector2 delta = endPosition - start.Position;
        if (delta.magnitude < minSwipeDistance) return;

        SwipeDirection direction = GetDirection(delta);
        SwipeScreenSection section = GetScreenSection(start.Position);
        RegisterSwipe(section, direction, endTime);
    }

    private void RegisterSwipe(SwipeScreenSection section, SwipeDirection direction, float timestamp)
    {
        if (swipeBuffer.Count > 0 && (timestamp - lastSwipeTime) > maxGapBetweenSwipes)
        {
            swipeBuffer.Clear();
        }

        swipeBuffer.Add(new SwipeSample
        {
            Section = section,
            Direction = direction
        });

        lastSwipeTime = timestamp;

        TrimBufferToMaxLength();

        TryRecognizeGesture();
    }

    private void TryRecognizeGesture()
    {
        SwipeGesture bestMatch = null;
        int bestMatchLength = -1;

        foreach (SwipeGesture gesture in gestures)
        {
            if (gesture == null || gesture.sequence == null || gesture.sequence.Count == 0) continue;
            if (gesture.sequence.Count < minimumGestureLength) continue;
            if (gesture.sequence.Count > swipeBuffer.Count) continue;
            if (!EndsWithSequence(swipeBuffer, gesture.sequence)) continue;
            if (gesture.sequence.Count <= bestMatchLength) continue;

            bestMatch = gesture;
            bestMatchLength = gesture.sequence.Count;
        }

        if (bestMatch == null) return;

        if (debugLogs)
        {
            Debug.Log($"[SwipeInputManager] Gesture recognized: {bestMatch.gestureName} ({SequenceToString(bestMatch.sequence)})");
        }

        bestMatch.onRecognized?.Invoke();
        swipeBuffer.Clear();
    }

    private static bool EndsWithSequence(List<SwipeSample> source, List<SwipeStep> sequence)
    {
        int offset = source.Count - sequence.Count;
        for (int i = 0; i < sequence.Count; i++)
        {
            SwipeSample sample = source[offset + i];
            SwipeStep step = sequence[i];

            if (sample.Direction != step.direction) return false;
            if (step.section != SwipeScreenSection.Any && sample.Section != step.section) return false;
        }

        return true;
    }

    private SwipeScreenSection GetScreenSection(Vector2 screenPosition)
    {
        float splitX = Screen.width * leftSectionWidthPercent;
        return screenPosition.x <= splitX ? SwipeScreenSection.Left : SwipeScreenSection.Right;
    }

    private void TryConsumeLiveSwipe(int pointerId, Vector2 currentPosition, float currentTime)
    {
        if (!activeTouchStarts.TryGetValue(pointerId, out SwipeStart start)) return;
        if (start.Consumed) return;

        if (!TryGetSwipe(start, currentPosition, currentTime, out SwipeScreenSection section, out SwipeDirection direction))
        {
            return;
        }

        start.Consumed = true;
        activeTouchStarts[pointerId] = start;
        RegisterSwipe(section, direction, currentTime);
    }

    private void TryConsumeLiveMouseSwipe(Vector2 currentPosition, float currentTime)
    {
        if (mouseSwipeStart.Consumed) return;

        if (!TryGetSwipe(mouseSwipeStart, currentPosition, currentTime, out SwipeScreenSection section, out SwipeDirection direction))
        {
            return;
        }

        mouseSwipeStart.Consumed = true;
        RegisterSwipe(section, direction, currentTime);
    }

    private static SwipeDirection GetDirection(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            return delta.x >= 0f ? SwipeDirection.Right : SwipeDirection.Left;
        }

        return delta.y >= 0f ? SwipeDirection.Up : SwipeDirection.Down;
    }

    private void TrimBufferToMaxLength()
    {
        while (swipeBuffer.Count > maxGestureLength)
        {
            swipeBuffer.RemoveAt(0);
        }
    }

    private void RecalculateMaxGestureLength()
    {
        maxGestureLength = 1;

        foreach (SwipeGesture gesture in gestures)
        {
            if (gesture == null || gesture.sequence == null) continue;
            maxGestureLength = Mathf.Max(maxGestureLength, gesture.sequence.Count);
        }
    }

    private float Now()
    {
        return useUnscaledTime ? Time.unscaledTime : Time.time;
    }

    private bool TryGetSwipe(SwipeStart start, Vector2 endPosition, float endTime, out SwipeScreenSection section, out SwipeDirection direction)
    {
        section = SwipeScreenSection.Any;
        direction = SwipeDirection.Up;

        float duration = endTime - start.Time;
        if (duration > maxSwipeDuration) return false;

        Vector2 delta = endPosition - start.Position;
        if (delta.magnitude < minSwipeDistance) return false;

        direction = GetDirection(delta);
        section = GetScreenSection(start.Position);
        return true;
    }

    private static string SequenceToString(List<SwipeStep> sequence)
    {
        if (sequence == null || sequence.Count == 0) return "(empty)";
        return string.Join(" + ", sequence.ConvertAll(step => $"{step.section} {step.direction}"));
    }

    private void ApplyPreset(GesturePreset preset)
    {
        if (preset == GesturePreset.None) return;

        if (preset == GesturePreset.LandscapeSideCombos)
        {
            gestures = new List<SwipeGesture>
            {
                CreateGesture(
                    "Right Up + Left Down",
                    new SwipeStep { section = SwipeScreenSection.Right, direction = SwipeDirection.Up },
                    new SwipeStep { section = SwipeScreenSection.Left, direction = SwipeDirection.Down }),
                CreateGesture(
                    "Right Up + Right Up",
                    new SwipeStep { section = SwipeScreenSection.Right, direction = SwipeDirection.Up },
                    new SwipeStep { section = SwipeScreenSection.Right, direction = SwipeDirection.Up }),
                CreateGesture(
                    "Left Up + Left Down",
                    new SwipeStep { section = SwipeScreenSection.Left, direction = SwipeDirection.Up },
                    new SwipeStep { section = SwipeScreenSection.Left, direction = SwipeDirection.Down }),
                CreateGesture(
                    "Right Down + Left Up",
                    new SwipeStep { section = SwipeScreenSection.Right, direction = SwipeDirection.Down },
                    new SwipeStep { section = SwipeScreenSection.Left, direction = SwipeDirection.Up })
            };
        }

        RecalculateMaxGestureLength();
    }

    private static SwipeGesture CreateGesture(string name, params SwipeStep[] steps)
    {
        return new SwipeGesture
        {
            gestureName = name,
            sequence = new List<SwipeStep>(steps)
        };
    }
}
