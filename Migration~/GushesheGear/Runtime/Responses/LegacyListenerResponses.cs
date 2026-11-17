using RonJames.Foundation.Events;
using UnityEngine;
using UnityEngine.Events;

// Responses that carry over the TriggerUnityEvents logic of GushesheGear's old ActionListener
// subclasses, so the Listener Migrator can move those listeners onto Variable Event Listeners
// without losing behaviour. Each is a faithful port of the old method; field names match the old
// fields so the migrator can copy them across by name.

/// <summary>Ported from BoolActionListener: invokes onTrue or onFalse.</summary>
public class BoolSplitResponse : IResponse<bool>
{
    [SerializeField] private UnityEvent onTrue = new UnityEvent();
    [SerializeField] private UnityEvent onFalse = new UnityEvent();

    public void Respond(bool value) => (value ? onTrue : onFalse)?.Invoke();
}

/// <summary>Ported from IntListener: passes the value, as a string, to a StringEventProcessor.</summary>
public class IntToStringResponse : IResponse<int>
{
    [SerializeField] private StringEventProcessor onStringCasted = new StringEventProcessor();

    public void Respond(int value) => onStringCasted.ProcessStringEvent(value.ToString());
}

/// <summary>Ported from StringActionListener: translates the value with RuntimeTranslator, then invokes responseLocale.</summary>
public class TranslatedStringResponse : IResponse<string>
{
    [SerializeField] private UnityEvent<string> responseLocale = new UnityEvent<string>();

    public async void Respond(string value)
    {
        string translated = await RuntimeTranslator.TranslateAsync(value);
        responseLocale?.Invoke(translated);
    }
}

/// <summary>Ported from StuntStateListener: one UnityEvent per StuntState.</summary>
public class StuntStateResponse : IResponse<StuntState>
{
    [SerializeField] private UnityEvent onVehicleStunt = new UnityEvent();
    [SerializeField] private UnityEvent onCharacterStunt = new UnityEvent();
    [SerializeField] private UnityEvent onAutoStunt = new UnityEvent();
    [SerializeField] private UnityEvent onIdle = new UnityEvent();
    [SerializeField] private UnityEvent onPerformingStunt = new UnityEvent();
    [SerializeField] private UnityEvent onPerformingCharacterStunt = new UnityEvent();

    public void Respond(StuntState value)
    {
        switch (value)
        {
            case StuntState.Idle: onIdle?.Invoke(); break;
            case StuntState.AutoStunt: onAutoStunt?.Invoke(); break;
            case StuntState.VehicleStunt: onVehicleStunt?.Invoke(); break;
            case StuntState.CharacterStunt: onCharacterStunt?.Invoke(); break;
            case StuntState.PerformingStunt: onPerformingStunt?.Invoke(); break;
            case StuntState.PerformingCharacterStunt: onPerformingCharacterStunt?.Invoke(); break;
        }
    }
}

/// <summary>
/// Ported from FloatActionListener's TriggerUnityEvents: formats the value as mm:ss and passes it
/// through the rounding processor. Runs only for Raises that pass the listener's Filter, as before.
/// </summary>
public class FloatFormatResponse : IResponse<float>
{
    [SerializeField] private StringEventProcessor timeInMinutes = new StringEventProcessor();
    [SerializeField] private FloatEventProcessor roundedValueProcessor = new FloatEventProcessor();

    public void Respond(float value)
    {
        int minutes = Mathf.FloorToInt(value / 60f);
        int seconds = Mathf.FloorToInt(value % 60f);
        timeInMinutes.ProcessStringEvent(string.Format("{0:00}:{1:00}", minutes, seconds));
        roundedValueProcessor?.ProcessFloatEvent(value);
    }
}

/// <summary>
/// Ported from FloatActionListener's greater/less comparison. In the old listener these fired on
/// every Raise, outside the listen-value filter, so the migrator puts this Response on a second,
/// unfiltered listener. compareTo is the old listen value (when listen values were on) or compareValue.
/// </summary>
public class FloatCompareResponse : IResponse<float>
{
    [SerializeField] private float compareTo = 0f;
    [SerializeField] private UnityEvent isGreaterThanListenValue = new UnityEvent();
    [SerializeField] private UnityEvent isLessThanListenValue = new UnityEvent();

    public void Respond(float value)
    {
        if (value > compareTo)
        {
            isGreaterThanListenValue?.Invoke();
        }
        else if (value < compareTo)
        {
            isLessThanListenValue?.Invoke();
        }
    }
}
